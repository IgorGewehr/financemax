using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

/// <summary>Cada campo <c>null</c> ⇒ não mexe. <paramref name="ResetarSenhaPara"/> não-nulo ⇒
/// reseta a senha e marca <c>mustChangePassword=true</c> (§4/§6 do escopo: reset administrativo —
/// o usuário troca no próximo login).</summary>
public sealed record AtualizarUsuarioComando(
    string BusinessId, string UsuarioId, Papel? NovoPapel = null, bool? Ativo = null, string? ResetarSenhaPara = null);

/// <summary><c>PATCH /api/usuarios/{id}</c> (§4) — papel/ativo/reset-senha, com o invariante do
/// último founder (§4: "não rebaixar/desativar o último founder").</summary>
public sealed class AtualizarUsuarioUseCase(IUsuarioRepository usuarios, IRefreshTokenRepository refreshTokens, IRelogio relogio)
{
    public static readonly Error NaoEncontrado = new("identidade.usuario.nao_encontrado", "Usuário não encontrado.");

    public async Task<Result<Usuario>> ExecutarAsync(AtualizarUsuarioComando comando, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObterPorIdAsync(comando.BusinessId, comando.UsuarioId, ct).ConfigureAwait(false);
        if (usuario is null)
        {
            return Result.Falhar<Usuario>(NaoEncontrado);
        }

        var vaiRebaixar = comando.NovoPapel is { } papel && papel != usuario.Papel;
        var vaiDesativar = comando.Ativo == false;

        if (vaiRebaixar || vaiDesativar)
        {
            var guard = await UltimoFounderGuard.ExigirNaoEhUltimoFounderAtivoAsync(usuarios, usuario, ct).ConfigureAwait(false);
            if (guard.Falha)
            {
                return Result.Falhar<Usuario>(guard.Erro);
            }
        }

        var agora = relogio.Agora();

        if (comando.NovoPapel is { } novoPapel && novoPapel != usuario.Papel)
        {
            usuario.AlterarPapel(novoPapel, agora);
        }

        if (comando.Ativo is { } ativo && ativo != usuario.Ativo)
        {
            if (ativo) usuario.Ativar(agora);
            else usuario.Desativar(agora);
        }

        if (!string.IsNullOrWhiteSpace(comando.ResetarSenhaPara))
        {
            var politica = PoliticaDeSenha.Validar(comando.ResetarSenhaPara, usuario.Nome, usuario.Email);
            if (politica.Falha)
            {
                return Result.Falhar<Usuario>(politica.Erro);
            }

            usuario.AlterarSenha(SenhaHasher.Hash(comando.ResetarSenhaPara), agora, mustChangePassword: true);

            // Reset de senha revoga TODAS as sessões ativas — um invasor com sessão já aberta não
            // sobrevive a um reset administrativo (mesmo racional da detecção de reuso do refresh).
            await refreshTokens.RevogarTodosDoUsuarioAsync(usuario.BusinessId, usuario.Id, agora, ct).ConfigureAwait(false);
        }

        // Desativar também derruba qualquer sessão em andamento — um usuário desativado não pode
        // continuar navegando com o access token de 15 min que ainda não expirou na prática (o
        // refresh já para de funcionar; o access token curto expira sozinho, risco aceito e
        // documentado, mesmo trade-off de qualquer JWT stateless).
        if (comando.Ativo == false)
        {
            await refreshTokens.RevogarTodosDoUsuarioAsync(usuario.BusinessId, usuario.Id, agora, ct).ConfigureAwait(false);
        }

        await usuarios.SalvarAsync(usuario, ct).ConfigureAwait(false);
        return Result.Ok(usuario);
    }
}
