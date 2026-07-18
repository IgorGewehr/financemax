using System.Security.Cryptography;
using Konscious.Security.Cryptography;

namespace SistemaX.Modules.Identidade.Application.Auth;

/// <summary>
/// Hash de senha — Argon2id (RFC 9106), o vencedor da Password Hashing Competition e a
/// recomendação atual da OWASP para hash de senha humana (§1 do escopo F2: "nunca guardar senha
/// em claro"). Parâmetros abaixo seguem o piso recomendado pelo OWASP Cheat Sheet para Argon2id
/// (m=19 MiB, t=2, p=1) arredondado para cima (m=64 MiB, t=3, p=2): o financemax roda numa VM
/// pequena de UM dono (não um serviço multi-tenant de milhares de logins/segundo), então o custo
/// extra de CPU/memória por login é aceitável e compra margem de segurança maior.
///
/// FORMATO — string auto-descritiva (molde PHC simplificado, sem depender de parser externo):
/// <c>argon2id$v=19$m=65536,t=3,p=2$&lt;saltBase64&gt;$&lt;hashBase64&gt;</c>. Os parâmetros viajam
/// DENTRO do hash — permite subir memória/iterações no futuro sem invalidar hashes antigos
/// (verificação sempre usa os parâmetros GRAVADOS, nunca os "atuais" da classe).
/// </summary>
public static class SenhaHasher
{
    private const int MemoriaKiB = 64 * 1024; // 64 MiB
    private const int Iteracoes = 3;
    private const int Paralelismo = 2;
    private const int TamanhoSalt = 16;
    private const int TamanhoHash = 32;
    private const string Prefixo = "argon2id";

    public static string Hash(string senha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senha);

        var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
        var hash = Derivar(senha, salt, MemoriaKiB, Iteracoes, Paralelismo, TamanhoHash);

        return $"{Prefixo}$v=19$m={MemoriaKiB},t={Iteracoes},p={Paralelismo}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>Verifica em tempo constante-o-suficiente (comparação via
    /// <see cref="CryptographicOperations.FixedTimeEquals"/>, nunca <c>==</c> de array/string —
    /// evita timing attack no byte que diverge primeiro). Devolve <c>false</c> (nunca lança) para
    /// hash em formato desconhecido/corrompido — trata como senha errada, não como erro 500.</summary>
    public static bool Verificar(string senha, string hashArmazenado)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(hashArmazenado)) return false;

        if (!TentarDecompor(hashArmazenado, out var memoriaKiB, out var iteracoes, out var paralelismo, out var salt, out var hashEsperado))
        {
            return false;
        }

        var hashCalculado = Derivar(senha, salt, memoriaKiB, iteracoes, paralelismo, hashEsperado.Length);
        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }

    private static byte[] Derivar(string senha, byte[] salt, int memoriaKiB, int iteracoes, int paralelismo, int tamanhoSaida)
    {
        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(senha))
        {
            Salt = salt,
            DegreeOfParallelism = paralelismo,
            MemorySize = memoriaKiB,
            Iterations = iteracoes,
        };

        return argon2.GetBytes(tamanhoSaida);
    }

    private static bool TentarDecompor(
        string hashArmazenado, out int memoriaKiB, out int iteracoes, out int paralelismo, out byte[] salt, out byte[] hash)
    {
        memoriaKiB = 0;
        iteracoes = 0;
        paralelismo = 0;
        salt = [];
        hash = [];

        var partes = hashArmazenado.Split('$');
        // ["argon2id", "v=19", "m=...,t=...,p=...", "<salt>", "<hash>"]
        if (partes.Length != 5 || partes[0] != Prefixo) return false;

        var parametros = partes[2].Split(',');
        if (parametros.Length != 3) return false;

        if (!TentarLerParametro(parametros[0], 'm', out memoriaKiB)) return false;
        if (!TentarLerParametro(parametros[1], 't', out iteracoes)) return false;
        if (!TentarLerParametro(parametros[2], 'p', out paralelismo)) return false;

        try
        {
            salt = Convert.FromBase64String(partes[3]);
            hash = Convert.FromBase64String(partes[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }

    private static bool TentarLerParametro(string trecho, char prefixoEsperado, out int valor)
    {
        valor = 0;
        var partido = trecho.Split('=');
        if (partido.Length != 2 || partido[0].Length == 0 || partido[0][0] != prefixoEsperado) return false;
        return int.TryParse(partido[1], out valor) && valor > 0;
    }
}
