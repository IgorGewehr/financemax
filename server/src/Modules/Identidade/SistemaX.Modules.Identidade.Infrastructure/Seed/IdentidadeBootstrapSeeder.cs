using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Infrastructure.Seed;

/// <summary>
/// Semente de bootstrap de Identidade (§6 do escopo F2) — IDEMPOTENTE, roda em TODO boot (mesmo
/// espírito de <c>FinanceiroBootstrapSeeder</c>), mas SÓ QUANDO <c>FINANCEMAX_ADMIN_SENHA_INICIAL</c>
/// está setado. Esse gate é o que mantém este seed COERENTE com o onboarding self-service
/// (<c>RegistrarUseCase</c>): em PRODUÇÃO o dono não seta essa env — a instalação nasce com ZERO
/// usuários e é o próprio <c>POST /api/auth/registrar</c> (first-run, sem convite) quem cria o
/// founder de verdade, com a senha que o dono escolheu. Sem este gate, este método ANTES rodava
/// incondicionalmente em todo boot e, na ausência da env, semeava um
/// <c>admin@financemax.local</c> com senha ALEATÓRIA (só visível no log) — como isto acontece no
/// Program.cs ANTES do primeiro request ser servido, a instalação nunca chegava a ter zero
/// usuários no momento em que o dono batesse em <c>/registrar</c>, e o first-run nunca disparava.
///
/// DEV/TESTE preserva o comportamento de sempre: quem quer o admin pré-semeado (com senha
/// CONHECIDA, nunca mais gerada aleatoriamente) simplesmente seta a env — ver <c>.env.example</c>
/// e <c>FinancemaxApiFactory</c> (testes de integração), que já setavam as duas variáveis antes
/// desta mudança e continuam setando.
/// </summary>
public static class IdentidadeBootstrapSeeder
{
    private const string EmailPadrao = "admin@financemax.local";

    public static async Task SemearAsync(IServiceProvider provider, string businessId, CancellationToken ct = default)
    {
        var senhaConhecida = Environment.GetEnvironmentVariable("FINANCEMAX_ADMIN_SENHA_INICIAL");
        if (string.IsNullOrWhiteSpace(senhaConhecida))
        {
            return;
        }

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

        var agora = relogio.Agora();
        var criado = Usuario.Criar(businessId, "Administrador", email, SenhaHasher.Hash(senhaConhecida), Papel.Founder, agora, ativo: true, mustChangePassword: true);
        if (criado.Falha)
        {
            logger?.LogError("Falha ao semear usuário admin inicial: {Erro}", criado.Erro.Mensagem);
            return;
        }

        await usuarios.SalvarAsync(criado.Valor, ct).ConfigureAwait(false);

        logger?.LogInformation("financemax — usuário administrador inicial criado a partir de FINANCEMAX_ADMIN_SENHA_INICIAL (e-mail={Email}).", email);
    }
}
