namespace UMADAccesos.Services.Security;

/// <summary>
/// Define el proveedor responsable de obtener y resguardar la clave simétrica usada para firmar tokens de acceso.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public interface ITokenKeyProvider
{
    /// <summary>
    /// Obtiene la clave criptográfica de firma HMAC-SHA256, generándola y persistiéndola de forma segura si aún no existe.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    Task<byte[]> ObtenerClaveFirmaAsync(CancellationToken cancellationToken = default);
}
