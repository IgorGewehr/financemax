using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.Auth;

/// <summary>
/// Recusa de senha fraca (§7 do escopo F2: "mínimo 8, não-trivial"). Checagem BARATA e
/// determinística — não é o mesmo objetivo de um dicionário de milhões de senhas vazadas
/// (fora de escopo do MVP, um dono + um colega); é o piso que evita o óbvio ("12345678",
/// "senha123", a própria senha repetindo um único caractere).
/// </summary>
public static class PoliticaDeSenha
{
    private const int TamanhoMinimo = 8;

    private static readonly HashSet<string> SenhasTriviais = new(StringComparer.OrdinalIgnoreCase)
    {
        "12345678", "123456789", "1234567890", "password", "password1", "senha1234",
        "senha123", "qwertyui", "qwerty123", "admin1234", "abcd1234", "letmein11",
        "iloveyou1", "monkey123", "football1",
    };

    /// <summary><paramref name="contextoUsuario"/> (nome/e-mail do usuário dono da senha) — a
    /// senha não pode ser igual (ou conter) o e-mail/nome, o erro mais comum de "não-trivial"
    /// que uma blocklist estática nunca cobriria.</summary>
    public static Result Validar(string? senha, params IReadOnlyCollection<string?> contextoUsuario)
    {
        if (string.IsNullOrWhiteSpace(senha))
            return Result.Falhar(new Error("identidade.senha.obrigatoria", "Senha é obrigatória."));

        if (senha.Length < TamanhoMinimo)
            return Result.Falhar(new Error("identidade.senha.muito_curta", $"Senha deve ter ao menos {TamanhoMinimo} caracteres."));

        if (SenhasTriviais.Contains(senha))
            return Result.Falhar(new Error("identidade.senha.trivial", "Senha trivial demais — escolha outra."));

        if (TodosOsCaracteresIguais(senha))
            return Result.Falhar(new Error("identidade.senha.trivial", "Senha trivial demais — escolha outra."));

        if (EhSequenciaMonotonica(senha))
            return Result.Falhar(new Error("identidade.senha.trivial", "Senha trivial demais — escolha outra."));

        foreach (var pedaco in contextoUsuario)
        {
            if (string.IsNullOrWhiteSpace(pedaco)) continue;

            // Checa a string inteira (ex.: e-mail) E cada palavra isolada (ex.: "Maria" dentro de
            // "Maria Silva") — um nome composto raramente aparece POR INTEIRO numa senha, mas o
            // primeiro nome sozinho é o vazamento mais comum na prática.
            var candidatos = pedaco.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Append(pedaco.Trim());
            foreach (var candidato in candidatos)
            {
                if (candidato.Length >= 4 && senha.Contains(candidato, StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Falhar(new Error("identidade.senha.contem_dado_pessoal", "Senha não pode conter seu nome/e-mail."));
                }
            }
        }

        return Result.Ok();
    }

    private static bool TodosOsCaracteresIguais(string senha) => senha.Distinct().Count() == 1;

    /// <summary>"12345678"/"87654321"/"abcdefgh" — cada caractere é o anterior ±1 na tabela ASCII.</summary>
    private static bool EhSequenciaMonotonica(string senha)
    {
        if (senha.Length < 4) return false;

        var crescente = true;
        var decrescente = true;
        for (var i = 1; i < senha.Length; i++)
        {
            var delta = senha[i] - senha[i - 1];
            if (delta != 1) crescente = false;
            if (delta != -1) decrescente = false;
        }

        return crescente || decrescente;
    }
}
