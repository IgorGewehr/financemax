using System.Collections.Concurrent;

namespace SistemaX.Modules.Abstractions.Consultor.Llm;

/// <summary>Implementação in-memory de <see cref="IConsultorNarracaoLlmCache"/> — mesmo molde de
/// <see cref="InMemoryConsultorInsightCache"/> (singleton, dicionário concorrente, hit exige hash
/// EXATO).</summary>
public sealed class InMemoryConsultorNarracaoLlmCache : IConsultorNarracaoLlmCache
{
    private sealed record Entrada(string FactsHash, ConsultorInsightNarrado Insight);

    private readonly ConcurrentDictionary<string, Entrada> _porChave = new();

    public ConsultorInsightNarrado? ObterSeAtual(string modulo, string ruleId, string factsHash)
    {
        if (_porChave.TryGetValue(Chave(modulo, ruleId), out var entrada) && entrada.FactsHash == factsHash)
        {
            return entrada.Insight;
        }

        return null;
    }

    public void Gravar(string modulo, string ruleId, string factsHash, ConsultorInsightNarrado insight)
    {
        _porChave[Chave(modulo, ruleId)] = new Entrada(factsHash, insight);
    }

    private static string Chave(string modulo, string ruleId) => $"{modulo}:{ruleId}";
}
