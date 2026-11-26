namespace UMADAccesos.Services.Security;

/// <summary>
/// Define el contrato criptográfico para el hash y la verificación segura de contraseñas de usuario.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface IPasswordHasher
{
    /// <summary>
    /// Genera un hash criptográfico con sal aleatoria y formato versionado a partir de una contraseña en texto plano.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    string HashPassword(string password);

    /// <summary>
    /// Verifica de forma segura y en tiempo constante si una contraseña corresponde al hash almacenado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    bool VerifyPassword(string password, string passwordHash);
}
