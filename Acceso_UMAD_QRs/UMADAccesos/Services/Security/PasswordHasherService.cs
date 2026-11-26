using System.Globalization;
using System.Security.Cryptography;

namespace UMADAccesos.Services.Security;

/// <summary>
/// Implementa el hash y la verificación de contraseñas utilizando PBKDF2-SHA256 con comparación en tiempo constante.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class PasswordHasherService : IPasswordHasher
{
    // Prefijo de versión del formato serializado del hash.
    private const string VersionFormato = "v1";

    // Longitud en bytes de la sal criptográfica aleatoria.
    private const int LongitudSaltBytes = 16;

    // Longitud en bytes de la clave derivada generada por PBKDF2.
    private const int LongitudHashBytes = 32;

    // Número de iteraciones PBKDF2-SHA256 aplicado en nuevas contraseñas.
    private const int IteracionesPorDefecto = 100_000;

    // Límite inferior aceptado de iteraciones al validar hashes existentes.
    private const int IteracionesMinimasPermitidas = 10_000;

    // Límite superior aceptado de iteraciones para prevenir agotamiento de CPU.
    private const int IteracionesMaximasPermitidas = 1_000_000;

    // Separador utilizado entre los segmentos del hash versionado.
    private const char SeparadorFormato = '$';

    /// <summary>
    /// Deriva un hash seguro PBKDF2-SHA256 con sal aleatoria de 16 bytes y 100,000 iteraciones.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("La contraseña no puede ser nula ni vacía.", nameof(password));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(LongitudSaltBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            IteracionesPorDefecto,
            HashAlgorithmName.SHA256,
            LongitudHashBytes);

        try
        {
            string saltBase64 = Convert.ToBase64String(salt);
            string hashBase64 = Convert.ToBase64String(hash);
            return $"{VersionFormato}{SeparadorFormato}{IteracionesPorDefecto}{SeparadorFormato}{saltBase64}{SeparadorFormato}{hashBase64}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    /// <summary>
    /// Verifica en tiempo constante si la contraseña proporcionada coincide con el hash versionado almacenado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        string[] partes = passwordHash.Split(SeparadorFormato);
        if (partes.Length != 4)
        {
            return false;
        }

        if (!string.Equals(partes[0], VersionFormato, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(partes[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iteraciones)
            || iteraciones < IteracionesMinimasPermitidas
            || iteraciones > IteracionesMaximasPermitidas)
        {
            return false;
        }

        byte[] salt;
        byte[] hashEsperado;

        try
        {
            salt = Convert.FromBase64String(partes[2]);
            hashEsperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length != LongitudSaltBytes || hashEsperado.Length != LongitudHashBytes)
        {
            return false;
        }

        byte[]? hashCalculado = null;

        try
        {
            hashCalculado = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iteraciones,
                HashAlgorithmName.SHA256,
                hashEsperado.Length);

            return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        finally
        {
            if (hashCalculado is not null)
            {
                CryptographicOperations.ZeroMemory(hashCalculado);
            }

            CryptographicOperations.ZeroMemory(hashEsperado);
        }
    }
}
