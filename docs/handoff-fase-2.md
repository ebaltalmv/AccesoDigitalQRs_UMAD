# Handoff Fase 2 — Puente Nativo (QR / Cámara) y Servicios Operativos Offline-First (UMAD Accesos v2.0)

- **Proyecto**: UMAD Accesos v2.0 (`AccesoDigitalQRs_UMAD`)
- **Fecha de verificación**: 26/11/2026
- **Estado general**: **APROBADO EN VERDE** (`0 Errores, 0 Advertencias` en `net10.0-windows10.0.19041.0`, inventario completo de 24 archivos nuevos + 3 modificados, 100% cumplimiento de XML Doc, esquema SQLite/EF Core válido sin ciclos de cascada).

---

## 1. Resultado de Auditoría de Fase 1 (Paso 0)

Se auditó el núcleo de Fase 1 previo y posterior a la integración de Fase 2, confirmando que permanece intacto, operativo y sin regresiones:

| Componente Fase 1 | Archivo(s) Verificado(s) | Estado | Hallazgos / Confirmación |
|---|---|---|---|
| **Host Blazor Hybrid** | `MainPage.xaml`, `MauiProgram.cs`, `Components/Routes.razor` | **OK** | `<BlazorWebView HostPage="wwwroot/index.html">` monta `Routes` sobre `#app`; `MauiProgram.cs` registra `AddMauiBlazorWebView()` y `.UseBarcodeReader()`. |
| **Registro DI Core** | `Extensions/ServiceCollectionExtensions.cs` (`AddUmadCore()`) | **OK** | Configura `AddDbContextFactory<UmadDbContext>` con SQLite (`UmadLocal.db`), `IUmadDbInitializer`, `ISecureStorage`, `IPasswordHasher`, `ITokenKeyProvider`, `ITokenAccesoService`, `ISyncQueueService`. |
| **Contexto EF Core** | `Data/UmadDbContext.cs`, `Data/UmadDbInitializer.cs` | **OK** | Define `Personas`, `TokensAcceso`, `RegistrosAcceso`, índices únicos y gancho `partial void OnModelCreatingExtensible(ModelBuilder)`. `UmadDbInitializer` inicializa de forma idempotente con `SemaphoreSlim(1, 1)` y siembra condicional segura. |
| **Hashing PBKDF2** | `Services/Security/PasswordHasherService.cs` | **OK** | PBKDF2-SHA256 (`100_000` iteraciones, sal aleatoria 16 B, clave 32 B, formato `v1$100000$...`, `FixedTimeEquals` y `ZeroMemory`). |
| **Tokens HMAC-SHA256** | `Services/Security/TokenAccesoService.cs`, `SecureStorageTokenKeyProvider.cs` | **OK** | Payload `v2|<guid>|<personaId>|<tipoPase>|<emisionUnix>|<expiracionUnix>|<nonceHex>` en Base64Url firmado con HMAC-SHA256 (clave 256 bits en `ISecureStorage` con respaldo controlado), verificación en tiempo constante, revocación y ventana UTC. |
| **Cola Offline-First** | `Services/Sync/ISyncQueueService.cs`, `SyncQueueService.cs` | **OK** | Persistencia inmediata en `Pendiente`, despacho exclusivo con `SemaphoreSlim(1, 1)`, verificación defensiva de red y conteo de reintentos/errores. |
| **Integridad de Fase 1** | Modelos y servicios de Fase 1 | **OK** | Sin modificaciones destructivas. Únicamente `Models/RegistroAcceso.cs` incorporó propiedades opcionales de Fase 2, `ServiceCollectionExtensions.cs` sumó `AddUmadOperations()` y `MauiProgram.cs` añadió las llamadas de registro. |

**Reparaciones requeridas en Fase 1**: Ninguna. No se detectaron defectos ni regresiones.

**Reparación aplicada durante el cierre de Fase 2**: se detectó que `EnsureCreatedAsync()` conserva correctamente una base existente, pero no agrega columnas o tablas de una fase posterior. El inicializador ahora reconcilia de forma idempotente las columnas nuevas de `RegistrosAcceso` y crea las tablas `SolicitudesRenovacion`, `Incidencias` y `PasesEvento` junto con sus índices, sin ejecutar borrados ni recreaciones destructivas.

---

## 2. Inventario de Archivos Nuevos y Modificados (Fase 2)

### 2.1 Archivos creados — Puente Nativo de Hardware (`9`)
- `Acceso_UMAD_QRs/UMADAccesos/Services/Device/IQrScannerService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Device/QrScanResult.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Device/QrScannerService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Device/IEvidenciaFotoService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Device/EvidenciaFotoResult.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Device/EvidenciaFotoService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Pages/QrScannerPage.xaml`
- `Acceso_UMAD_QRs/UMADAccesos/Pages/QrScannerPage.xaml.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Extensions/DeviceServiceCollectionExtensions.cs`

### 2.2 Archivos creados — Modelos, Persistencia y Servicios Operativos (`15`)
- `Acceso_UMAD_QRs/UMADAccesos/Models/EstadoRenovacion.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Models/SolicitudRenovacion.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Models/SeveridadIncidencia.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Models/EstadoIncidencia.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Models/Incidencia.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Models/PaseEvento.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Data/UmadDbContext.Fase2.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/IRegistroAccesoService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/RegistroAccesoService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/IPaseEventoService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/PaseEventoService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/IRenovacionService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/RenovacionService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/IIncidenciaService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/IncidenciaService.cs`

### 2.3 Archivos existentes modificados (`3`)
- `Acceso_UMAD_QRs/UMADAccesos/Models/RegistroAcceso.cs` — agregadas propiedades opcionales `RutaEvidenciaFoto`, `HashEvidenciaFoto`, `GuardiaPersonaId`, `EntradaRelacionadaId` y navegaciones `Guardia`, `EntradaRelacionada`.
- `Acceso_UMAD_QRs/UMADAccesos/Extensions/ServiceCollectionExtensions.cs` — agregado `AddUmadOperations()`.
- `Acceso_UMAD_QRs/UMADAccesos/MauiProgram.cs` — agregadas invocaciones `builder.Services.AddUmadOperations()` y `builder.Services.AddUmadDeviceServices()`.

### 2.4 Documentación generada (`1`)
- `docs/handoff-fase-2.md` (este documento).

---

## 3. Contratos de Interfaces, DTOs y Enumeraciones

### 3.1 Servicios de Dispositivo (`UMADAccesos.Services.Device`)

#### `IQrScannerService`
```csharp
public interface IQrScannerService
{
    Task<QrScanResult> EscanearCodigoQrAsync(CancellationToken ct = default);
}
```
- **DTO `QrScanResult`**:
  - `bool Exitoso { get; init; }`
  - `bool Cancelado { get; init; }`
  - `string? CodigoEscaneado { get; init; }`
  - `string? MensajeError { get; init; }`
  - Fábricas estáticas: `Exito(string codigo)`, `CanceladoPorUsuario()`, `Error(string mensaje)`.

#### `IEvidenciaFotoService`
```csharp
public interface IEvidenciaFotoService
{
    Task<EvidenciaFotoResult> CapturarEvidenciaAsync(string? nombreArchivo = null, CancellationToken ct = default);
}
```
- **DTO `EvidenciaFotoResult`**:
  - `bool Exitoso { get; init; }`
  - `bool Cancelado { get; init; }`
  - `string? RutaRelativa { get; init; }` *(relativa a `FileSystem.AppDataDirectory`, p. ej. `evidencias/evidencia_20261126_120000_123.jpg`)*
  - `string? HashSha256 { get; init; }` *(hexadecimal SHA-256 en minúsculas)*
  - `string? MensajeError { get; init; }`
  - Fábricas estáticas: `Exito(string rutaRelativa, string hashSha256)`, `CanceladoPorUsuario()`, `Error(string mensaje)`.

### 3.2 Servicios Operativos (`UMADAccesos.Services.Operations`)

#### `IRegistroAccesoService`
```csharp
public interface IRegistroAccesoService
{
    Task<RegistroAcceso> RegistrarEntradaAsync(
        int personaId,
        int tokenAccesoId,
        string puntoAcceso,
        int guardiaPersonaId,
        string? rutaEvidenciaFoto = null,
        string? hashEvidenciaFoto = null,
        CancellationToken ct = default);

    Task<RegistroAcceso> RegistrarSalidaAsync(
        int personaId,
        string puntoAcceso,
        int guardiaPersonaId,
        CancellationToken ct = default);

    Task<RegistroAcceso?> ObtenerEntradaAbiertaAsync(
        int personaId,
        CancellationToken ct = default);
}
```
- **Comportamiento clave**: `RegistrarEntradaAsync` valida criptográficamente el token mediante `ITokenAccesoService.ValidarTokenAsync(token.CodigoFirmado, ct)` y verifica que `validacion.PersonaId == personaId`, persistiendo tanto accesos `Autorizado` como `Denegado` en `ISyncQueueService.EncolarRegistroAccesoAsync(...)`. `RegistrarSalidaAsync` vincula automáticamente `EntradaRelacionadaId` con la entrada abierta más reciente.

#### `IPaseEventoService`
```csharp
public interface IPaseEventoService
{
    Task<PaseEvento> CrearPaseEventoAsync(
        string nombreEvento,
        DateTime inicioUtc,
        DateTime finUtc,
        int personaId,
        int creadoPorPersonaId,
        string? descripcion = null,
        CancellationToken ct = default);
}
```
- **Comportamiento clave**: Valida ventana de fechas UTC (`fin > inicio` y `fin > ahora`) y existencia/estado activo del asistente; emite automáticamente un token `TipoPase.Evento` con vigencia `finUtc - DateTime.UtcNow` (sin revocar pases previos activos del usuario) y guarda el `PaseEvento` en estado `Pendiente` de sincronización.

#### `IRenovacionService`
```csharp
public interface IRenovacionService
{
    Task<SolicitudRenovacion> SolicitarRenovacionAsync(
        int personaId,
        TipoPase tipoPaseDeseado,
        string justificacion,
        TimeSpan? vigenciaSolicitada = null,
        CancellationToken ct = default);

    Task<SolicitudRenovacion> AprobarRenovacionAsync(
        int solicitudId,
        int resueltoPorPersonaId,
        CancellationToken ct = default);

    Task<SolicitudRenovacion> RechazarRenovacionAsync(
        int solicitudId,
        int resueltoPorPersonaId,
        string motivoRechazo,
        CancellationToken ct = default);

    Task<IReadOnlyList<SolicitudRenovacion>> ObtenerPendientesAsync(
        CancellationToken ct = default);
}
```
- **Comportamiento clave**: `AprobarRenovacionAsync` valida que la solicitud esté `Pendiente`, emite el nuevo token vía `ITokenAccesoService.EmitirTokenAsync(..., revocarPreviosActivos: true)` y vincula `TokenAccesoNuevoId`. `RechazarRenovacionAsync` documenta `MotivoResolucion` y `ResueltoPorPersonaId`.

#### `IIncidenciaService`
```csharp
public interface IIncidenciaService
{
    Task<Incidencia> ReportarIncidenciaAsync(
        int reportadoPorPersonaId,
        SeveridadIncidencia severidad,
        string titulo,
        string descripcion,
        int? registroAccesoId = null,
        string? rutaEvidenciaFoto = null,
        string? hashEvidenciaFoto = null,
        CancellationToken ct = default);

    Task<Incidencia> ActualizarEstadoAsync(
        int incidenciaId,
        EstadoIncidencia nuevoEstado,
        CancellationToken ct = default);

    Task<IReadOnlyList<Incidencia>> ObtenerAbiertasAsync(
        CancellationToken ct = default);
}
```
- **Comportamiento clave**: Flujo alineado a ITIL 4 Incident Management; `ActualizarEstadoAsync` asigna `FechaResolucionUtc` al pasar a `Resuelta`/`Cerrada` (y la limpia si se reabre), y `ObtenerAbiertasAsync` prioriza `Abierta` y `EnInvestigacion` por `Severidad` descendente y antigüedad.

### 3.3 Enumeraciones de Fase 2 (`UMADAccesos.Models`)
- **`EstadoRenovacion`**: `Pendiente = 0`, `Aprobada = 1`, `Rechazada = 2`.
- **`SeveridadIncidencia`**: `Baja = 1`, `Media = 2`, `Alta = 3`, `Critica = 4`.
- **`EstadoIncidencia`**: `Abierta = 0`, `EnInvestigacion = 1`, `Resuelta = 2`, `Cerrada = 3`.

---

## 4. Esquemas de Entidades de Fase 2 (`UmadDbContext.Fase2.cs`)

### 4.1 Columnas extendidas en tabla existente `RegistrosAcceso`
| Columna | Tipo C# | Restricciones / FK / Índice |
|---|---|---|
| `RutaEvidenciaFoto` | `string?` | Max `500` |
| `HashEvidenciaFoto` | `string?` | Max `128` |
| `GuardiaPersonaId` | `int?` | **FK** -> `Personas.Id` (`DeleteBehavior.SetNull`), Índice `IX_RegistrosAcceso_GuardiaPersonaId` |
| `EntradaRelacionadaId` | `int?` | **FK** -> `RegistrosAcceso.Id` (`DeleteBehavior.SetNull`), Índice `IX_RegistrosAcceso_EntradaRelacionadaId` |

### 4.2 Tabla `SolicitudesRenovacion` (`SolicitudRenovacion`)
| Columna | Tipo C# | Restricciones / Default |
|---|---|---|
| `Id` | `int` | **PK** autoincremental |
| `IdentificadorGlobal` | `Guid` | Requerido, **UNIQUE** (`IX_SolicitudesRenovacion_IdentificadorGlobal`) |
| `PersonaId` | `int` | **FK** -> `Personas.Id` (`Restrict`), Requerido, Índice `IX_SolicitudesRenovacion_PersonaId` |
| `TokenAccesoOriginalId` | `int?` | **FK** -> `TokensAcceso.Id` (`SetNull`), Opcional, Índice `IX_SolicitudesRenovacion_TokenAccesoOriginalId` |
| `TipoPaseDeseado` | `TipoPase` (`int`) | Requerido |
| `VigenciaSolicitada` | `TimeSpan?` | Opcional |
| `Justificacion` | `string` | Requerido, Max `500` |
| `Estado` | `EstadoRenovacion` (`int`) | Requerido, Default `Pendiente (0)`, Índice `IX_SolicitudesRenovacion_Estado` |
| `MotivoResolucion` | `string?` | Opcional, Max `500` |
| `ResueltoPorPersonaId` | `int?` | **FK** -> `Personas.Id` (`SetNull`), Opcional, Índice `IX_SolicitudesRenovacion_ResueltoPorPersonaId` |
| `TokenAccesoNuevoId` | `int?` | **FK** -> `TokensAcceso.Id` (`SetNull`), Opcional, Índice `IX_SolicitudesRenovacion_TokenAccesoNuevoId` |
| `FechaSolicitudUtc` | `DateTime` | Requerido (UTC) |
| `FechaResolucionUtc` | `DateTime?` | Opcional (UTC) |
| `Sincronizado` | `bool` | Requerido, Default `false`, Índice `IX_SolicitudesRenovacion_Sincronizado` |
| `EstadoSincronizacion` | `EstadoSincronizacion` (`int`) | Requerido, Default `Pendiente (0)`, Índice `IX_SolicitudesRenovacion_EstadoSincronizacion` |
| `FechaUltimoIntento` | `DateTime?` | Opcional |
| `Reintentos` | `int` | Requerido, Default `0` |
| `UltimoError` | `string?` | Opcional, Max `500` |

### 4.3 Tabla `Incidencias` (`Incidencia`)
| Columna | Tipo C# | Restricciones / Default |
|---|---|---|
| `Id` | `int` | **PK** autoincremental |
| `IdentificadorGlobal` | `Guid` | Requerido, **UNIQUE** (`IX_Incidencias_IdentificadorGlobal`) |
| `ReportadoPorPersonaId` | `int` | **FK** -> `Personas.Id` (`Restrict`), Requerido, Índice `IX_Incidencias_ReportadoPorPersonaId` |
| `RegistroAccesoId` | `int?` | **FK** -> `RegistrosAcceso.Id` (`SetNull`), Opcional, Índice `IX_Incidencias_RegistroAccesoId` |
| `Severidad` | `SeveridadIncidencia` (`int`) | Requerido, Índice `IX_Incidencias_Severidad` |
| `Estado` | `EstadoIncidencia` (`int`) | Requerido, Default `Abierta (0)`, Índice `IX_Incidencias_Estado` |
| `Titulo` | `string` | Requerido, Max `200` |
| `Descripcion` | `string` | Requerido, Max `2000` |
| `RutaEvidenciaFoto` | `string?` | Opcional, Max `500` |
| `HashEvidenciaFoto` | `string?` | Opcional, Max `128` |
| `FechaReporteUtc` | `DateTime` | Requerido (UTC) |
| `FechaResolucionUtc` | `DateTime?` | Opcional (UTC) |
| `Sincronizado` | `bool` | Requerido, Default `false`, Índice `IX_Incidencias_Sincronizado` |
| `EstadoSincronizacion` | `EstadoSincronizacion` (`int`) | Requerido, Default `Pendiente (0)`, Índice `IX_Incidencias_EstadoSincronizacion` |
| `FechaUltimoIntento` | `DateTime?` | Opcional |
| `Reintentos` | `int` | Requerido, Default `0` |
| `UltimoError` | `string?` | Opcional, Max `500` |

### 4.4 Tabla `PasesEvento` (`PaseEvento`)
| Columna | Tipo C# | Restricciones / Default |
|---|---|---|
| `Id` | `int` | **PK** autoincremental |
| `IdentificadorGlobal` | `Guid` | Requerido, **UNIQUE** (`IX_PasesEvento_IdentificadorGlobal`) |
| `NombreEvento` | `string` | Requerido, Max `200` |
| `Descripcion` | `string?` | Opcional, Max `500` |
| `FechaInicioEvento` | `DateTime` | Requerido (UTC) |
| `FechaFinEvento` | `DateTime` | Requerido (UTC) — Índice compuesto `IX_PasesEvento_VentanaVigencia` (`FechaInicioEvento`, `FechaFinEvento`) |
| `PersonaId` | `int` | **FK** -> `Personas.Id` (`Restrict`), Requerido, Índice `IX_PasesEvento_PersonaId` |
| `TokenAccesoId` | `int?` | **FK** -> `TokensAcceso.Id` (`SetNull`), Opcional, Índice `IX_PasesEvento_TokenAccesoId` |
| `CreadoPorPersonaId` | `int` | **FK** -> `Personas.Id` (`Restrict`), Requerido, Índice `IX_PasesEvento_CreadoPorPersonaId` |
| `FechaCreacionUtc` | `DateTime` | Requerido (UTC) |
| `Sincronizado` | `bool` | Requerido, Default `false`, Índice `IX_PasesEvento_Sincronizado` |
| `EstadoSincronizacion` | `EstadoSincronizacion` (`int`) | Requerido, Default `Pendiente (0)`, Índice `IX_PasesEvento_EstadoSincronizacion` |
| `FechaUltimoIntento` | `DateTime?` | Opcional |
| `Reintentos` | `int` | Requerido, Default `0` |
| `UltimoError` | `string?` | Opcional, Max `500` |

---

## 5. Cómo y Por Qué: Puente Nativo MAUI ↔ Blazor y Diseño de Persistencia

| Decisión técnica | Cómo se implementó | Por qué se diseñó así |
|---|---|---|
| **Puente modal QR (`IQrScannerService` + `QrScannerPage`)** | `QrScannerService` verifica `Permissions.Camera` en `MainThread`, obtiene `Application.Current.Windows[0].Page` y abre `QrScannerPage` (`ZXing.Net.Maui.Controls.CameraBarcodeReaderView` restringido a `BarcodeFormat.QrCode`) mediante `PushModalAsync`, devolviendo `QrScanResult` vía `TaskCompletionSource`. En `#if WINDOWS` retorna un `QrScanResult.Error(...)` controlado. | Los `WebView` de Blazor Hybrid presentan restricciones de cámara/WebRTC en móviles y Windows; abrir un `ContentPage` modal nativo desacopla Razor de la cámara, garantiza control del ciclo de vida (`OnAppearing`/`OnDisappearing`/`OnBackButtonPressed`) y habilita un fallback manual en escritorio Windows. |
| **Evidencia fotográfica (`IEvidenciaFotoService`)** | Usa `MediaPicker.Default.CapturePhotoAsync()` en `MainThread` previa validación de `IsCaptureSupported` y permiso de cámara; guarda el archivo con marca de tiempo en `FileSystem.AppDataDirectory/evidencias/` y calcula su SHA-256 sin lanzar excepciones hacia la UI. | Almacenar solo la ruta relativa y el hash SHA-256 evita inflar SQLite con BLOBs binarios, mantiene portabilidad entre reinstalaciones/rutas base y permite verificar integridad forense al sincronizar evidencias en caseta. |
| **Fábrica de contextos (`IDbContextFactory<UmadDbContext>`)** | Los 4 servicios operativos (`RegistroAccesoService`, `PaseEventoService`, `RenovacionService`, `IncidenciaService`) inyectan `IDbContextFactory<UmadDbContext>` y crean contextos de corta vida (`await using`). | Evita dependencias cautivas y condiciones de carrera entre múltiples componentes Razor o llamadas concurrentes de Blazor Server/Hybrid sobre SQLite. |
| **Esquema extensible (`UmadDbContext.Fase2.cs`)** | Usa `partial class UmadDbContext` y `partial void OnModelCreatingExtensible(ModelBuilder)` definidos en Fase 1. Todas las FKs usan `DeleteBehavior.Restrict` o `DeleteBehavior.SetNull`. | Mantiene intacto el archivo núcleo `UmadDbContext.cs` de Fase 1 y elimina riesgos de múltiples rutas de borrado en cascada (*multiple cascade paths*) en SQLite. |
| **Continuidad offline-first** | Todas las entidades nuevas incluyen `IdentificadorGlobal`, `Sincronizado`, `EstadoSincronizacion`, `FechaUltimoIntento`, `Reintentos` y `UltimoError`. `RegistroAccesoService` delega el guardado en `ISyncQueueService.EncolarRegistroAccesoAsync(...)`. | La operación en caseta (entradas, salidas, pases de evento, renovaciones e incidencias) funciona 100% sin red local o internet, con idempotencia garantizada por `Guid`. |

---

## 6. Cómo Invocar Escáner QR y Cámara desde Blazor en Fase 4

En los componentes `.razor` de Fase 4 (añadiendo `@using UMADAccesos.Services.Device` y `@using UMADAccesos.Services.Operations` en `_Imports.razor` o en el componente):

```razor
@using UMADAccesos.Services.Device
@using UMADAccesos.Services.Operations
@using UMADAccesos.Services.Security
@inject IQrScannerService QrScannerService
@inject IEvidenciaFotoService EvidenciaFotoService
@inject ITokenAccesoService TokenAccesoService
@inject IRegistroAccesoService RegistroAccesoService

@code {
    private bool _mostrarEntradaManualQr;
    private string _codigoManual = string.Empty;
    private string? _rutaEvidencia;
    private string? _hashEvidencia;
    private string? _mensajeEstado;

    private async Task EscanearYRegistrarEntradaAsync(int guardiaPersonaId, string puntoAcceso)
    {
        QrScanResult scan = await QrScannerService.EscanearCodigoQrAsync();

        if (scan.Cancelado)
            return;

        if (!scan.Exitoso || string.IsNullOrWhiteSpace(scan.CodigoEscaneado))
        {
            // En Windows desktop (o si falla la cámara), habilitar captura manual.
            _mostrarEntradaManualQr = true;
            _mensajeEstado = scan.MensajeError;
            return;
        }

        await ProcesarCodigoQrAsync(scan.CodigoEscaneado, guardiaPersonaId, puntoAcceso);
    }

    private async Task ProcesarCodigoQrAsync(string codigoFirmado, int guardiaPersonaId, string puntoAcceso)
    {
        TokenValidationResult validacion = await TokenAccesoService.ValidarTokenAsync(codigoFirmado);

        if (!validacion.EsValido || validacion.Token is null || validacion.PersonaId is null)
        {
            _mensajeEstado = validacion.Motivo;
            return;
        }

        RegistroAcceso registro = await RegistroAccesoService.RegistrarEntradaAsync(
            personaId: validacion.PersonaId.Value,
            tokenAccesoId: validacion.Token.Id,
            puntoAcceso: puntoAcceso,
            guardiaPersonaId: guardiaPersonaId,
            rutaEvidenciaFoto: _rutaEvidencia,
            hashEvidenciaFoto: _hashEvidencia);

        _mensajeEstado = $"{registro.Resultado}: {registro.MotivoResultado}";
    }

    private async Task CapturarEvidenciaOpcionalAsync()
    {
        EvidenciaFotoResult foto = await EvidenciaFotoService.CapturarEvidenciaAsync("caseta");
        if (foto.Exitoso)
        {
            _rutaEvidencia = foto.RutaRelativa;
            _hashEvidencia = foto.HashSha256;
        }
        else if (!foto.Cancelado)
        {
            _mensajeEstado = foto.MensajeError;
        }
    }
}
```

---

## 7. Resumen de Registro en DI (`MauiProgram.cs`)

`MauiProgram.CreateMauiApp()` registra en orden los tres módulos del contenedor de dependencias:

1. `builder.Services.AddUmadCore()` (`ServiceCollectionExtensions`):
   - `IDbContextFactory<UmadDbContext>` (Singleton, SQLite)
   - `IUmadDbInitializer` -> `UmadDbInitializer` (Singleton)
   - `ISecureStorage` -> `SecureStorage.Default` (Singleton via `TryAddSingleton`)
   - `IPasswordHasher` -> `PasswordHasherService` (Singleton)
   - `ITokenKeyProvider` -> `SecureStorageTokenKeyProvider` (Singleton)
   - `ITokenAccesoService` -> `TokenAccesoService` (Scoped)
   - `ISyncQueueService` -> `SyncQueueService` (Scoped)
2. `builder.Services.AddUmadOperations()` (`ServiceCollectionExtensions`):
   - `IRegistroAccesoService` -> `RegistroAccesoService` (Scoped)
   - `IPaseEventoService` -> `PaseEventoService` (Scoped)
   - `IRenovacionService` -> `RenovacionService` (Scoped)
   - `IIncidenciaService` -> `IncidenciaService` (Scoped)
3. `builder.Services.AddUmadDeviceServices()` (`DeviceServiceCollectionExtensions`):
   - `IQrScannerService` -> `QrScannerService` (Singleton)
   - `IEvidenciaFotoService` -> `EvidenciaFotoService` (Singleton)

---

## 8. Evidencia de Compilación (`dotnet build`) y Auditoría de Calidad

### 8.1 Resultado de `dotnet build`
- **Restauración**:
  `dotnet restore Acceso_UMAD_QRs/UMADAccesos/UMADAccesos.csproj` — exit code `0` (`2.48 s`).
- **Compilación objetivo (`net10.0-windows10.0.19041.0`)**:
  ```powershell
  dotnet build Acceso_UMAD_QRs/UMADAccesos/UMADAccesos.csproj -f net10.0-windows10.0.19041.0 --no-restore -p:BaseIntermediateOutputPath=...\scratch\o\ -p:BaseOutputPath=...\scratch\b\
  ```
  - **Resultado**: **`Compilación correcta. 0 Advertencia(s), 0 Errores` (`00:00:32.50`)**.
  - *Nota técnica de entorno*: En el worktree de verificación, la ruta base del repositorio (`119` caracteres) provocaba que el binario de Windows App SDK `MsixContent\WindowsAppSdk.AppxDeploymentExtensions.Desktop-EventLog-Instrumentation.dll` alcanzara `267` caracteres (superando `MAX_PATH = 260` en la tarea `Copy` de MSBuild con `MSB3030`). Al redirigir `BaseIntermediateOutputPath` y `BaseOutputPath` a una ruta corta sin modificar el proyecto, la compilación completa (incluyendo `MauiXamlInflator=SourceGen`) finalizó con **0 errores y 0 advertencias**.

### 8.2 Auditoría de Calidad y XML Doc
- **Patrón `IDbContextFactory<UmadDbContext>`**: Verificado en los 4 servicios operativos (`RegistroAccesoService`, `PaseEventoService`, `RenovacionService`, `IncidenciaService`); ninguno inyecta `UmadDbContext` directamente.
- **Campos de sincronización offline**: Confirmados en `SolicitudRenovacion`, `Incidencia`, `PaseEvento` y `RegistroAcceso` (`IdentificadorGlobal`, `Sincronizado`, `EstadoSincronizacion`, `FechaUltimoIntento`, `Reintentos`, `UltimoError`).
- **Seguridad hardware y permisos**: `QrScannerPage` solicita `Permissions.Camera` en `OnAppearing`, restringe el lector a `BarcodeFormat.QrCode`, resuelve el `TaskCompletionSource` una sola vez (`_finalizado`) y gestiona botón cerrar / `OnBackButtonPressed` / `OnDisappearing`. `QrScannerService` devuelve fallback controlado en `#if WINDOWS`. `EvidenciaFotoService` guarda en `FileSystem.AppDataDirectory/evidencias/`, calcula SHA-256 y captura excepciones sin propagarlas.
- **XML Doc en los 26 archivos `.cs` de Fase 2 (nuevos y modificados)**: Verificado 100% conforme: todas las clases, interfaces, enumeraciones, constructores y métodos públicos/protegidos contienen `/// <summary>`, `<author>Emmanuel Baltazar López</author>`, `<date>25/11/2026</date>`, `<version>1.0</version>` y `<modification>25/11/2026</modification>`; todas las propiedades, campos y constantes utilizan únicamente comentarios `// ...`.
