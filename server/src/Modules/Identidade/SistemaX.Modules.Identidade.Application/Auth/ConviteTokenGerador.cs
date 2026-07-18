using System.Security.Cryptography;
using System.Text;

namespace SistemaX.Modules.Identidade.Application.Auth;

/// <summary>
/// Token de convite — 256 bits aleatórios, mesmo padrão de opacidade do refresh token
/// (<see cref="GeradorDeTokens.GerarRefreshToken"/>): o BRUTO só existe uma vez, na resposta de
/// <c>POST /api/convites</c> (e no link que o founder repassa ao convidado); só o HASH
/// (<see cref="Hash"/>) é persistido — hash rápido (SHA-256) por design, já que o objetivo é
/// não-adivinhação de um segredo de altíssima entropia, não resistência a rainbow table de senha
/// humana (mesmo racional do comentário em <see cref="RefreshTokenEmitido"/>).
/// </summary>
public static class ConviteTokenGerador
{
    public static (string Bruto, string Hash) Gerar()
    {
        var bruto = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('='); // base64url — seguro em query/header/JSON sem escaping

        return (bruto, Hash(bruto));
    }

    public static string Hash(string tokenBruto)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenBruto)));
}
