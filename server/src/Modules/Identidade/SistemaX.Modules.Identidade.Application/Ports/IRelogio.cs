namespace SistemaX.Modules.Identidade.Application.Ports;

/// <summary>Abstração de tempo — mesmo racional de <c>Financeiro.Application.Ports.IRelogio</c>
/// (não duplicado via referência de projeto de propósito: Identidade não depende de Financeiro,
/// módulos-irmãos não se enxergam — só o Host enxerga os dois).</summary>
public interface IRelogio
{
    DateTimeOffset Agora();
}
