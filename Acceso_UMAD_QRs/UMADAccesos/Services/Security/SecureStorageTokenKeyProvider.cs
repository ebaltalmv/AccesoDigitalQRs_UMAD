using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace UMADAccesos.Services.Security;

/// <summary>
/// Gestiona la clave simétrica de 256 bits para firma HMAC-SHA256 respaldada por SecureStorage y caché sincronizada en memoria.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class SecureStorageTokenKeyProvider : ITokenKeyProvider
{
    // Identificador de la clave almacenada en SecureStorage.
    private const string LlaveAlmacenamientoSeguro = "umad_accesos_hmac_key_v2";

    // Nombre del archivo oculto de respaldo para entornos sin soporte nativo de SecureStorage.
    private const string NombreArchivoRespaldoLocal = ".umad_accesos_hmac_key_v2.bin";

    // Tamaño requerido de la clave simétrica en bytes (256 bits).
    private const int LongitudClaveBytes = 32;

    // Abstracción de almacenamiento seguro de plataforma.
    private readonly ISecureStorage _secureStorage;

    // Registrador de eventos operativos y advertencias de seguridad.
    private readonly ILogger<SecureStorageTokenKeyProvider> _logger;

    // Semáforo para serializar la inicialización y acceso concurrente a la clave.
    private readonly SemaphoreSlim _sincronizacion = new(1, 1);

    // Caché en memoria de la clave simétrica para evitar lecturas repetidas del almacén.
    private byte[]? _claveEnCache;

    /// <summary>
    /// Inicializa una nueva instancia del proveedor de clave respaldado por almacenamiento seguro.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public SecureStorageTokenKeyProvider(
        ISecureStorage secureStorage,
        ILogger<SecureStorageTokenKeyProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(secureStorage);
        ArgumentNullException.ThrowIfNull(logger);

        _secureStorage = secureStorage;
        _logger = logger;
    }

    /// <summary>
    /// Recupera la clave de firma HMAC de 32 bytes desde caché, SecureStorage o respaldo local controlado.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public async Task<byte[]> ObtenerClaveFirmaAsync(CancellationToken cancellationToken = default)
    {
        byte[]? claveActual = _claveEnCache;
        if (claveActual is not null)
        {
            return (byte[])claveActual.Clone();
        }

        await _sincronizacion.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_claveEnCache is not null)
            {
                return (byte[])_claveEnCache.Clone();
            }

            bool secureStorageDisponible = true;
            byte[]? claveCargada = null;

            try
            {
                string? valorGuardado = await _secureStorage.GetAsync(LlaveAlmacenamientoSeguro).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(valorGuardado))
                {
                    if (IntentarDecodificarClave(valorGuardado, out byte[]? claveDecodificada))
                    {
                        claveCargada = claveDecodificada;
                    }
                    else
                    {
                        _logger.LogWarning("La clave recuperada desde SecureStorage no tiene un formato válido; se regenerará una nueva clave.");
                    }
                }
            }
            catch (Exception ex)
            {
                secureStorageDisponible = false;
                _logger.LogWarning(ex, "SecureStorage no está disponible en este entorno; se utilizará el mecanismo de respaldo local controlado.");
                claveCargada = await IntentarLeerDesdeArchivoRespaldoAsync(cancellationToken).ConfigureAwait(false);
            }

            if (claveCargada is null)
            {
                claveCargada = RandomNumberGenerator.GetBytes(LongitudClaveBytes);
                string claveBase64 = Convert.ToBase64String(claveCargada);
                bool persistida = false;

                if (secureStorageDisponible)
                {
                    try
                    {
                        await _secureStorage.SetAsync(LlaveAlmacenamientoSeguro, claveBase64).ConfigureAwait(false);
                        persistida = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No fue posible persistir la clave en SecureStorage; se intentará respaldo local controlado.");
                    }
                }

                if (!persistida)
                {
                    bool guardadaEnRespaldo = await IntentarGuardarEnArchivoRespaldoAsync(claveBase64, cancellationToken).ConfigureAwait(false);
                    if (!guardadaEnRespaldo)
                    {
                        _logger.LogWarning("No fue posible persistir la clave en disco; se mantendrá únicamente en memoria durante la sesión actual.");
                    }
                }
            }

            _claveEnCache = claveCargada;
            return (byte[])_claveEnCache.Clone();
        }
        finally
        {
            _sincronizacion.Release();
        }
    }

    // Valida y decodifica una clave codificada en Base64 comprobando su longitud exacta de 256 bits.
    private static bool IntentarDecodificarClave(string valorBase64, out byte[]? clave)
    {
        clave = null;

        try
        {
            byte[] bytes = Convert.FromBase64String(valorBase64.Trim());
            if (bytes.Length != LongitudClaveBytes)
            {
                CryptographicOperations.ZeroMemory(bytes);
                return false;
            }

            clave = bytes;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Resuelve la ruta del archivo local oculto en el directorio de datos de la aplicación.
    private string? ObtenerRutaArchivoRespaldo()
    {
        try
        {
            string directorioDatos = FileSystem.AppDataDirectory;
            if (string.IsNullOrWhiteSpace(directorioDatos))
            {
                return null;
            }

            return Path.Combine(directorioDatos, NombreArchivoRespaldoLocal);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible resolver el directorio de datos local para el respaldo de clave.");
            return null;
        }
    }

    // Intenta recuperar la clave simétrica desde el archivo local oculto de respaldo.
    private async Task<byte[]?> IntentarLeerDesdeArchivoRespaldoAsync(CancellationToken cancellationToken)
    {
        string? ruta = ObtenerRutaArchivoRespaldo();
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
        {
            return null;
        }

        try
        {
            string contenido = await File.ReadAllTextAsync(ruta, cancellationToken).ConfigureAwait(false);
            if (IntentarDecodificarClave(contenido, out byte[]? clave))
            {
                return clave;
            }

            _logger.LogWarning("El archivo de respaldo local de clave no contiene un formato válido; se regenerará.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible leer la clave desde el archivo local de respaldo.");
            return null;
        }
    }

    // Persiste la clave en un archivo local oculto como respaldo cuando SecureStorage no opera en el entorno.
    private async Task<bool> IntentarGuardarEnArchivoRespaldoAsync(string claveBase64, CancellationToken cancellationToken)
    {
        string? ruta = ObtenerRutaArchivoRespaldo();
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return false;
        }

        try
        {
            string? directorio = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrWhiteSpace(directorio))
            {
                Directory.CreateDirectory(directorio);
            }

            if (File.Exists(ruta))
            {
                try
                {
                    FileAttributes atributosActuales = File.GetAttributes(ruta);
                    if ((atributosActuales & FileAttributes.Hidden) == FileAttributes.Hidden)
                    {
                        File.SetAttributes(ruta, atributosActuales & ~FileAttributes.Hidden);
                    }
                }
                catch
                {
                    // Se omite si el sistema de archivos no admite atributos de visibilidad.
                }
            }

            await File.WriteAllTextAsync(ruta, claveBase64, cancellationToken).ConfigureAwait(false);

            try
            {
                File.SetAttributes(ruta, File.GetAttributes(ruta) | FileAttributes.Hidden);
            }
            catch
            {
                // Se omite si la plataforma no soporta marcar archivos como ocultos.
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible guardar la clave en el archivo local de respaldo.");
            return false;
        }
    }
}
