using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Application.Auth;

/// <summary>Um refresh token recém-emitido — o <see cref="Bruto"/> vai para o cliente (única vez
/// que existe fora deste processo); <see cref="Hash"/> é o que persiste (ver
/// <c>RefreshTokenRegistro</c>).</summary>
public sealed record RefreshTokenEmitido(string Bruto, string Hash, DateTimeOffset ExpiraEm);

/// <summary>
/// Emissão de tokens — access JWT (claims <c>sub</c>/<c>businessId</c>/<c>papel</c>, §3 do escopo)
/// + refresh token opaco (256 bits aleatórios, nunca um JWT: um refresh não precisa ser
/// auto-descritivo, só imprevisível e revogável por hash — ver <c>IRefreshTokenRepository</c>).
/// </summary>
public sealed class GeradorDeTokens(JwtOptions options)
{
    public (string Token, DateTimeOffset ExpiraEm) GerarAccessToken(Usuario usuario, DateTimeOffset agora)
    {
        var expiraEm = agora + options.AccessTokenValidade;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtOptions.ClaimBusinessId, usuario.BusinessId),
            new Claim(JwtOptions.ClaimPapel, usuario.Papel.ToString()),
        };

        var credenciais = new SigningCredentials(ChaveDeAssinatura(options.ChaveSecreta), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Emissor,
            audience: options.Audiencia,
            claims: claims,
            notBefore: agora.UtcDateTime,
            expires: expiraEm.UtcDateTime,
            signingCredentials: credenciais);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }

    public RefreshTokenEmitido GerarRefreshToken(DateTimeOffset agora)
    {
        var bruto = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('='); // base64url — seguro em query/header/JSON sem escaping

        return new RefreshTokenEmitido(bruto, HashDoRefreshToken(bruto), agora + options.RefreshTokenValidade);
    }

    /// <summary>Hash rápido (SHA-256) do refresh token bruto — ver comentário de
    /// <c>RefreshTokenRegistro</c> sobre por que não é Argon2 aqui (o token já É 256 bits
    /// aleatórios, não uma senha humana com baixa entropia).</summary>
    public static string HashDoRefreshToken(string tokenBruto)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenBruto)));

    public static SymmetricSecurityKey ChaveDeAssinatura(string segredo)
        => new(Encoding.UTF8.GetBytes(segredo));
}
