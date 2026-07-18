namespace SistemaX.Financemax.Api.Tests;

/// <summary>Uma única instância de <see cref="FinancemaxApiFactory"/> (um boot, um banco, um
/// admin semeado) compartilhada por TODAS as classes de teste desta coleção — xUnit NUNCA
/// paraleliza classes da mesma coleção entre si, então os testes rodam sequencialmente contra o
/// mesmo servidor sem precisar desabilitar paralelismo no assembly inteiro.</summary>
[CollectionDefinition("financemax-api")]
public sealed class AuthCollection : ICollectionFixture<FinancemaxApiFactory>;
