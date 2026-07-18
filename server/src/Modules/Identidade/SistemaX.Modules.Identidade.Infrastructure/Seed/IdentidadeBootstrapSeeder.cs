using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Infrastructure.Seed;

/// <summary>
/// Semente de bootstrap de Identidade (§6 do escopo F2) — IDEMPOTENTE, roda em TODO boot (mesmo
/// espírito de <c>FinanceiroBootstrapSeeder</c>): sem ela, uma instalação nova nasce sem NENHUM
/// usuário e ninguém consegue logar pela primeira vez (galinha-e-ovo — só um usuário já logado
/// pode criar outro via <c>POST /api/usuarios</c>).
///
/// Cria UM usuário founder (<c>admin@financemax.local</c> por padrão) com senha inicial CONHECIDA
/// (via <c>FINANCEMAX_ADMIN_SENHA_INICIAL</c>) ou GERADA (aleatória, logada uma única vez no
/// primeiro boot — nunca gravada em lugar nenhum além do log) e <c>MustChangePassword=true</c>: o
/// dono troca a senha no primeiro login e a inicial deixa de valer para sempre depois disso
/// (nenhuma pista fica no banco).
/// </summary>
public static class IdentidadeBootstrapSeeder
{
    private const string EmailPadrao = "admin@financemax.local";

    public static async Task SemearAsync(IServiceProvider provider, string businessId, CancellationToken ct = default)
    {
        await using var scope = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var usuarios = sp.GetRequiredService<IUsuarioRepository>();
        var relogio = sp.GetRequiredService<IRelogio>();
        var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("IdentidadeBootstrapSeeder");

        // Idempotência: só semeia se o negócio ainda não tem NENHUM usuário — depois do primeiro
        // boot, o dono pode ter renomeado/desativado o admin@financemax.local à vontade; recriar
        // um segundo founder por engano a cada restart seria o oposto de idempotente.
        var existentes = await usuarios.ListarAsync(businessId, ct).ConfigureAwait(false);
        if (existentes.Count > 0)
        {
            return;
        }

        var email = Environment.GetEnvironmentVariable("FINANCEMAX_ADMIN_EMAIL_INICIAL") is { Length: > 0 } emailEnv
            ? emailEnv
            : EmailPadrao;

        var senhaConhecida = Environment.GetEnvironmentVariable("FINANCEMAX_ADMIN_SENHA_INICIAL");
        var senha = string.IsNullOrWhiteSpace(senhaConhecida) ? GerarSenhaAleatoria() : senhaConhecida;

        var agora = relogio.Agora();
        var criado = Usuario.Criar(businessId, "Administrador", email, SenhaHasher.Hash(senha), Papel.Founder, agora, ativo: true, mustChangePassword: true);
        if (criado.Falha)
        {
            logger?.LogError("Falha ao semear usuário admin inicial: {Erro}", criado.Erro.Mensagem);
            return;
        }

        await usuarios.SalvarAsync(criado.Valor, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(senhaConhecida))
        {
            // Só aparece no log quando a senha foi GERADA (não quando veio de
            // FINANCEMAX_ADMIN_SENHA_INICIAL, que o operador já conhece) — é a ÚNICA vez que esta
            // senha existe em texto plano em qualquer lugar; se for perdida, o único caminho é
            // apagar o usuário direto no banco e deixar o boot semear de novo.
            logger?.LogWarning(
                "financemax — usuário administrador inicial criado: e-mail={Email} senha={Senha} " +
                "(TROQUE NO PRIMEIRO LOGIN — esta senha não aparece em nenhum outro lugar).",
                email, senha);
        }
        else
        {
            logger?.LogInformation("financemax — usuário administrador inicial criado a partir de FINANCEMAX_ADMIN_SENHA_INICIAL (e-mail={Email}).", email);
        }
    }

    /// <summary>24 caracteres, alfabeto sem ambíguos (sem 0/O/1/l/I) — pensada para ser lida e
    /// digitada por um humano direto do log, não só copiada.</summary>
    private static string GerarSenhaAleatoria()
    {
        const string alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(24);
        return string.Create(24, bytes, (span, buf) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = alfabeto[buf[i] % alfabeto.Length];
        });
    }
}
