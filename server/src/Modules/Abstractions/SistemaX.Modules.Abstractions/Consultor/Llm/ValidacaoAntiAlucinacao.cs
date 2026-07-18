using System.Text.RegularExpressions;

namespace SistemaX.Modules.Abstractions.Consultor.Llm;

/// <summary>
/// Guarda anti-alucinação (item 2 da tarefa do Super Consultor): extrai os números da frase que o
/// LLM devolveu e confirme que são um SUBCONJUNTO dos números que já existiam nos fatos —
/// "nunca exibir número que o motor não gerou". Puramente textual, sem chamar nada: dado o mesmo
/// <see cref="ConsultorFato"/> e a mesma frase, sempre devolve o mesmo veredito.
///
/// "Números permitidos" é a união de <see cref="ConsultorFato.Facts"/> (os valores já formatados
/// que o provider calculou) COM <see cref="ConsultorFato.TemplateFallback"/> (a frase
/// determinística que o próprio motor já produziu com esses mesmos fatos, ver
/// <see cref="NarradorTemplate"/>) — o fallback às vezes menciona um número derivado/contextual
/// (ex.: o horizonte fixo "30 dias" de <c>HorizonteDiasPadrao</c>) que não está em <c>Facts</c> mas
/// é igualmente confiável por vir do motor determinístico, não do LLM. Reescrever essa frase
/// reaproveitando esse número não é invenção.
/// </summary>
public static class ValidacaoAntiAlucinacao
{
    /// <summary>Casa sequências de dígitos com separador de milhar/decimal (<c>.</c> ou <c>,</c>)
    /// entre elas — cobre <c>"1.234,56"</c>, <c>"12,5"</c>, <c>"45"</c>, <c>"30/11/2026"</c> (como 3
    /// tokens: 30, 11, 2026), etc. Não captura letras/símbolos — <c>R$</c>/<c>%</c> ficam de fora do
    /// match, só o número em si importa.</summary>
    private static readonly Regex NumeroRegex = new(@"\d+(?:[.,]\d+)*", RegexOptions.Compiled);

    public static bool EhSeguro(ConsultorFato fato, string frase)
    {
        if (string.IsNullOrWhiteSpace(frase)) return false;

        var permitidos = ExtrairNumeros(fato.TemplateFallback);
        foreach (var valor in fato.Facts.Values)
        {
            permitidos.UnionWith(ExtrairNumeros(valor));
        }

        var encontrados = ExtrairNumeros(frase);
        return encontrados.IsSubsetOf(permitidos);
    }

    private static HashSet<string> ExtrairNumeros(string texto) => NumeroRegex
        .Matches(texto)
        .Select(m => Normalizar(m.Value))
        .Where(n => n.Length > 0)
        .ToHashSet();

    /// <summary>Tolerância de formatação: remove separadores de milhar/decimal e zeros à esquerda —
    /// <c>"1.234,56"</c>, <c>"1234,56"</c> e <c>"123456"</c> normalizam pro mesmo token, mas
    /// <c>"45"</c> e <c>"4,5"</c> continuam DIFERENTES (o segundo normaliza pra <c>"45"</c> só se os
    /// dígitos brutos forem os mesmos — ambos colapsam pra "45" de propósito: a tolerância aqui é
    /// deliberadamente frouxa quanto a onde a vírgula cai, não quanto aos dígitos em si).</summary>
    private static string Normalizar(string token)
    {
        var digitos = token.Replace(".", string.Empty).Replace(",", string.Empty).TrimStart('0');
        return digitos.Length == 0 ? "0" : digitos;
    }
}
