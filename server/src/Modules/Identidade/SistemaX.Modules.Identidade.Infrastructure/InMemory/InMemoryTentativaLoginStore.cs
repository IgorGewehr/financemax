using System.Collections.Concurrent;
using SistemaX.Modules.Identidade.Application.Ports;

namespace SistemaX.Modules.Identidade.Infrastructure.InMemory;

/// <summary>
/// Lockout PROGRESSIVO (§7 do escopo F2) — implementação padrão e ÚNICA (não tem par SQLite de
/// propósito, ver comentário de <see cref="ITentativaLoginStore"/>). A partir da
/// <see cref="LimiteAntesDoBloqueio"/>-ésima falha CONSECUTIVA, cada falha adicional DOBRA a
/// duração do bloqueio (30s, 1min, 2min, 4min, ... até o teto de <see cref="BloqueioMaximo"/>) —
/// torna um ataque de força bruta cada vez mais caro sem nunca travar uma conta pra sempre (nunca
/// exige intervenção manual pra destravar).
/// </summary>
public sealed class InMemoryTentativaLoginStore : ITentativaLoginStore
{
    private const int LimiteAntesDoBloqueio = 5;
    private static readonly TimeSpan BloqueioBase = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BloqueioMaximo = TimeSpan.FromMinutes(15);

    private sealed record Estado(int Falhas, DateTimeOffset? BloqueadoAte);

    private readonly ConcurrentDictionary<string, Estado> _estados = new();

    public StatusBloqueio Verificar(string chave, DateTimeOffset agora)
    {
        if (!_estados.TryGetValue(chave, out var estado) || estado.BloqueadoAte is not { } ate)
        {
            return new StatusBloqueio(false, null);
        }

        return agora < ate ? new StatusBloqueio(true, ate) : new StatusBloqueio(false, null);
    }

    public StatusBloqueio RegistrarFalha(string chave, DateTimeOffset agora)
    {
        var novoEstado = _estados.AddOrUpdate(
            chave,
            _ => ProximoEstado(new Estado(0, null), agora),
            (_, atual) => ProximoEstado(atual, agora));

        return novoEstado.BloqueadoAte is { } ate && agora < ate
            ? new StatusBloqueio(true, ate)
            : new StatusBloqueio(false, null);
    }

    public void RegistrarSucesso(string chave) => _estados.TryRemove(chave, out _);

    private static Estado ProximoEstado(Estado atual, DateTimeOffset agora)
    {
        var falhas = atual.Falhas + 1;
        if (falhas < LimiteAntesDoBloqueio)
        {
            return new Estado(falhas, null);
        }

        var nivel = falhas - LimiteAntesDoBloqueio;
        var duracao = Multiplicar(BloqueioBase, nivel);
        if (duracao > BloqueioMaximo) duracao = BloqueioMaximo;

        return new Estado(falhas, agora + duracao);
    }

    private static TimeSpan Multiplicar(TimeSpan baseTempo, int nivel)
    {
        // 2^nivel, saturando em BloqueioMaximo antes de estourar overflow de double em níveis altos.
        var fator = Math.Min(Math.Pow(2, nivel), BloqueioMaximo.TotalSeconds / baseTempo.TotalSeconds + 1);
        return TimeSpan.FromSeconds(baseTempo.TotalSeconds * fator);
    }
}
