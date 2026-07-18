using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Convites;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

public sealed record RegistrarComando(string BusinessId, string Nome, string Email, string Senha, string? ConviteToken = null);

/// <summary>
/// <c>POST /api/auth/registrar</c> (ANÔNIMO) — as DUAS portas de entrada do onboarding do
/// financemax (1 negócio, N usuários):
///
/// 1) FIRST-RUN — <c>usuarios.ListarAsync</c> devolve ZERO: a instalação acabou de nascer (ver
///    <c>IdentidadeBootstrapSeeder</c>, que agora só semeia se FINANCEMAX_ADMIN_SENHA_INICIAL
///    estiver setado — em produção não está, então o primeiro <c>POST</c> aqui É o dono de
///    verdade). Nenhum convite exigido; papel sempre <see cref="Papel.Founder"/>.
///
/// 2) JÁ HÁ ALGUÉM — exige <see cref="RegistrarComando.ConviteToken"/> válido (pendente, não
///    expirado, e-mail batendo com o informado) e o papel vem do convite, nunca do request (um
///    request não pode se autopromover a Founder inventando o campo).
///
/// Em qualquer dos dois casos: cria com <c>mustChangePassword=false</c> (a pessoa escolheu a
/// própria senha, diferente do seed de bootstrap) e já emite o par de tokens (reusa
/// <see cref="LoginUseCase.EmitirParAsync"/> — mesmo helper que <see cref="RefreshTokenUseCase"/>
/// usa, login automático pós-cadastro).
/// </summary>
public sealed class RegistrarUseCase(
    IUsuarioRepository usuarios, IConviteRepository convites, LoginUseCase loginUseCase, IRelogio relogio)
{
    public static readonly Error EmailInvalido = new("identidade.registrar.email_invalido", "E-mail inválido.");
    public static readonly Error ConviteObrigatorio = new("identidade.registrar.convite_obrigatorio", "É preciso um convite para se cadastrar.");
    public static readonly Error ConviteInvalido = new("identidade.registrar.convite_invalido", "Convite inválido, expirado ou e-mail não corresponde.");

    /// <summary>Mensagem deliberadamente genérica — não confirma DE QUAL jeito o cadastro falhou
    /// além de "e-mail em uso" (ela já precisa dizer isso, é a UX padrão de qualquer cadastro
    /// self-service: "faça login em vez disso"). O que ela NÃO faz é distinguir de outros motivos
    /// de recusa (convite inválido também vira uma mensagem igualmente curta, código diferente).</summary>
    public static readonly Error EmailEmUso = new("identidade.registrar.email_em_uso", "Não foi possível concluir o cadastro com este e-mail.");

    public async Task<Result<TokensEmitidosResultado>> ExecutarAsync(RegistrarComando comando, CancellationToken ct = default)
    {
        var emailNormalizado = Usuario.NormalizarEmail(comando.Email);
        if (emailNormalizado is null)
        {
            return Result.Falhar<TokensEmitidosResultado>(EmailInvalido);
        }

        var politica = PoliticaDeSenha.Validar(comando.Senha, comando.Nome, emailNormalizado);
        if (politica.Falha)
        {
            return Result.Falhar<TokensEmitidosResultado>(politica.Erro);
        }

        var agora = relogio.Agora();

        var existentes = await usuarios.ListarAsync(comando.BusinessId, ct).ConfigureAwait(false);
        var primeiroUso = existentes.Count == 0;

        Papel papel;
        Convite? convite = null;

        if (primeiroUso)
        {
            papel = Papel.Founder;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(comando.ConviteToken))
            {
                return Result.Falhar<TokensEmitidosResultado>(ConviteObrigatorio);
            }

            var hash = ConviteTokenGerador.Hash(comando.ConviteToken);
            convite = await convites.ObterPorTokenHashAsync(hash, ct).ConfigureAwait(false);

            var conviteValido = convite is not null
                && convite.BusinessId == comando.BusinessId
                && convite.Email == emailNormalizado
                && convite.Status(agora) == StatusConvite.Pendente;

            if (!conviteValido)
            {
                return Result.Falhar<TokensEmitidosResultado>(ConviteInvalido);
            }

            papel = convite!.Papel;
        }

        var jaExiste = await usuarios.ObterPorEmailAsync(comando.BusinessId, emailNormalizado, ct).ConfigureAwait(false);
        if (jaExiste is not null)
        {
            return Result.Falhar<TokensEmitidosResultado>(EmailEmUso);
        }

        var criado = Usuario.Criar(
            comando.BusinessId, comando.Nome, emailNormalizado, SenhaHasher.Hash(comando.Senha), papel, agora,
            ativo: true, mustChangePassword: false);
        if (criado.Falha)
        {
            return Result.Falhar<TokensEmitidosResultado>(criado.Erro);
        }

        await usuarios.SalvarAsync(criado.Valor, ct).ConfigureAwait(false);

        if (convite is not null)
        {
            // Só falharia por uma corrida de milissegundos entre a checagem acima e aqui (mesmo
            // token aceito duas vezes em paralelo) — quem ganhou a corrida do SalvarAsync do
            // usuário já criou a conta; não desfazemos o cadastro por causa da marcação do convite
            // perder essa corrida específica.
            var aceito = convite.Aceitar(agora);
            if (aceito.Sucesso)
            {
                await convites.SalvarAsync(convite, ct).ConfigureAwait(false);
            }
        }

        var (resultado, _) = await loginUseCase.EmitirParAsync(criado.Valor, agora, ct).ConfigureAwait(false);
        return Result.Ok(resultado);
    }
}
