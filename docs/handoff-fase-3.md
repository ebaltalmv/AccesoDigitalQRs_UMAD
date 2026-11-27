# Handoff Fase 3 — Inteligencia Artificial Dual, Matriz RBAC y Control de Entorno (UMAD Accesos v2.0)

- **Proyecto**: UMAD Accesos v2.0 (`AccesoDigitalQRs_UMAD`)
- **Fecha de verificación**: 26/11/2026
- **Estado general**: **APROBADO EN VERDE** (`0 Errores, 0 Advertencias` en `CoreCompile` tanto en `Debug` como en `Release` para `net10.0-maccatalyst`; `46/46` pruebas runtime aprobadas en `Debug` y `45/45` en `Release` contra SQLite real; `39/39` archivos con 100% cumplimiento XML Doc; `0` vestigios v1.0 y `0` secretos hardcodeados).

---

## 1. Verificación y Reparaciones Aplicadas a Fases 1 y 2 (Paso 0 + Hallazgo QA)

Antes de integrar la Fase 3, se auditó integralmente el código base de Fases 1 y 2 (`Models/`, `Data/`, `Services/Security/`, `Services/Sync/`, `Services/Device/`, `Services/Operations/`, `Extensions/`, `MauiProgram.cs` y manifiestos nativos). La arquitectura base resultó limpia de regresiones v1.0 (`0` coincidencias de `IsTestMode`, `SimulateValidAccess`, `SimulateInvalidAccess`, `App.CurrentUser`, `Shell.Current`, `DateTime.Now`).

Se aplicaron las siguientes reparaciones puntuales para eliminar brechas de seguridad, concurrencia y consistencia antes de entrar a Fase 4:

| # | Archivo(s) | Reparación aplicada | Porqué (riesgo mitigado) |
|---|---|---|---|
| 1 | `Services/Operations/RegistroAccesoService.cs`, `RenovacionService.cs`, `PaseEventoService.cs`, `IncidenciaService.cs` | Validación explícita de `Activo == true` y rol autorizado (`Guardia`/`Administrador` según operación); bloqueo de auto-aprobación/auto-rechazo en `RenovacionService` (`resueltoPorPersonaId == solicitud.PersonaId`). | Evitar escalada de privilegios y auto-aprobación de pases si una capa de UI fallara en ocultar una acción. Refuerza la defensa en profundidad junto con RBAC de Fase 3. |
| 2 | `Pages/QrScannerPage.xaml.cs` | Reemplazo de `bool _finalizado` por bandera atómica `int _finalizado` con `Interlocked.Exchange` / `Volatile.Read` y traslado de `BarcodeReader.IsDetecting = false` dentro de `MainThread.BeginInvokeOnMainThread`. | `ZXing.Net.MAUI` invoca `OnBarcodesDetected` en hilos de fondo; mutar propiedades de controles UI fuera del hilo principal causa cierres inesperados o doble `PopModalAsync` en Android/iOS. |
| 3 | `Services/Device/QrScannerService.cs` | Envoltorio `try/catch` defensivo en el callback de `CancellationToken` que ejecuta `PopModalAsync`. | Impedir excepciones no observadas en callbacks `async void` cuando una cancelación coincide con el cierre manual del modal. |
| 4 | `Services/Device/EvidenciaFotoService.cs` | Lista blanca de extensiones (`.jpg`, `.jpeg`, `.png`, `.webp`), sanitización estricta de nombre y verificación de frontera de directorio (`AppDataDirectory/evidencias/`), límite máximo de 10 MB y limpieza de archivos parciales ante fallo/cancelación. | Prevenir *path traversal*, almacenamiento de tipos de archivo arbitrarios y consumo descontrolado de disco por evidencias corruptas o incompletas. |
| 5 | `Services/Sync/SyncQueueService.cs` | Cambio de `_procesamientoLock` (`SemaphoreSlim`) de campo de instancia a `static readonly`. | `ISyncQueueService` opera con ciclo de vida `Scoped`; un semáforo de instancia no garantizaba exclusión mutua entre scopes concurrentes, arriesgando despachos duplicados de la cola offline. |
| 6 | `Platforms/Android/AndroidManifest.xml` | Cambio de `android:allowBackup="true"` a `android:allowBackup="false"`. | Evitar extracción no autorizada vía `adb backup` de `UmadLocal.db` y del almacenamiento local de la aplicación en Android. |
| 7 | `Data/UmadDbInitializer.cs` | (a) Siembra de cuentas demo y escenarios acotada estrictamente a `#if DEBUG`; en `Release` no se siembran credenciales literales. (b) Reconciliación de `VigenciaSolicitada` como `TEXT NULL` (alineado a EF Core SQLite `TimeSpan?`) + 3 índices faltantes en `SolicitudesRenovacion`. (c) **Fix QA**: fechas semilla ancladas a la zona horaria institucional `America/Mexico_City` (con respaldo UTC-6) y garantizadas estrictamente en el pasado respecto a `DateTime.UtcNow`. | (a) Mitigar **CWE-798** (credenciales hardcodeadas en binarios de producción). (b) Evitar discrepancias de esquema/índices en bases creadas en Fase 1. (c) En horario vespertino de México (>18:00 local = día UTC siguiente), usar `DateTime.UtcNow.Date` desplazaba los registros semilla al futuro/día siguiente, ocultando las 4 entradas anómalas al evaluar en vivo. |
| 8 | `Data/UmadDbInitializer.cs`, `Services/Operations/{RegistroAcceso,Incidencia,PaseEvento,Renovacion}Service.cs`, `Services/Sync/SyncQueueService.cs` | Normalización de bloques XML Doc en métodos auxiliares privados y fecha `<date>25/11/2026</date>`. | Garantizar 100% de cumplimiento homogéneo de la convención de documentación en todos los archivos intervenidos en Fase 3. |
| 9 | `UMADAccesos.csproj` | En `maccatalyst` Debug se usa el RID nativo único (`maccatalyst-arm64` en Apple Silicon / `maccatalyst-x64` en Intel) manteniendo universal `x64;arm64` en `Release`; se removió `<MauiXamlInflator>SourceGen</MauiXamlInflator>`. | Evitar conflicto incremental de `Resizetizer` sobre `MauiInfo.plist` en `MergeAppBundles` y eliminar 3 avisos informativos de `DesignTimeBuild` en IDE. |
| 10 | `Data/UmadDbInitializer.cs` | Adición de `ReconciliarEsquemaFase1Async` previo a `ReconciliarEsquemaFase2Async`. | En equipos con una base SQLite `UmadLocal.db` heredada de v1.0, `EnsureCreatedAsync()` omite crear tablas nuevas y fallaba al crear índices sobre `RegistrosAcceso`. |
| 11 | `Services/Security/{ITokenAccesoService,TokenAccesoService}.cs`, `Services/Operations/PaseEventoService.cs` | Nueva sobrecarga `EmitirTokenVentanaAsync(personaId, tipoPase, inicioVigenciaUtc, finVigenciaUtc)` usada por `PaseEventoService`. | Acotar criptográficamente el token de evento a su ventana exacta `[inicioUtc, finUtc]` para impedir ingresos antes del inicio del evento. |
| 12 | `Services/Sync/{SyncQueueSummary,SyncQueueService}.cs` | Extensión retrocompatible de `SyncQueueSummary` con `PasesEventoPendientes`, `RenovacionesPendientes`, `IncidenciasPendientes` y `TotalPendientesOperativos`. | Exponer a Fase 4 la visibilidad completa de la cola offline en todas las entidades operativas de Fase 2 sin romper contratos de Fase 1. |

---

## 2. Inventario de Archivos Creados y Modificados (Fase 3)

### 2.1 Archivos creados — Subsistema de IA Dual (`15` `.cs` en `Services/AI/`)
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/ModoProveedorIa.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/AzureAiEndpointOptions.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/IaOptions.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/TipoVisitaSugerido.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/ClasificacionVisitaResult.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/NivelRiesgoAnomalia.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/ContextoEvaluacionAcceso.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/EvaluacionAnomaliaResult.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/IIaProvider.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/OfflineLocalIaProvider.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/IAzureAiClient.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/AzureAiHttpClient.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/AzureAiProductionProvider.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/IIaService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/AI/IaService.cs`

### 2.2 Archivos creados — Roles RBAC, Sesión Reactiva y Control de Entorno (`11` `.cs` en `Services/Auth/`)
- `Acceso_UMAD_QRs/UMADAccesos/Services/Auth/RolUsuario.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Auth/PermisoSistema.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Auth/SesionUsuario.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Auth/ResultadoAutenticacion.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Auth/IAutorizacionRolService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Auth/AutorizacionRolService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Auth/EntornoEjecucionOptions.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Auth/IEntornoEjecucionService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Auth/EntornoEjecucionService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Auth/ISesionActualService.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Auth/SesionActualService.cs`

### 2.3 Archivos creados — Integración DI, Operación y Documentación (`4`)
- `Acceso_UMAD_QRs/UMADAccesos/Extensions/AiAndAuthServiceCollectionExtensions.cs`
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/RegistroEntradaConAnalisisResult.cs`
- `docs/arquitectura-ia-azure.md`
- `docs/handoff-fase-3.md` *(este documento)*

### 2.4 Archivos existentes modificados (`13`)
- `Acceso_UMAD_QRs/UMADAccesos/MauiProgram.cs` — registra `builder.Services.AddUmadAiAndAuth()`.
- `Acceso_UMAD_QRs/UMADAccesos/Components/_Imports.razor` — agrega `@using` de `Services.AI`, `Services.Auth`, `Services.Device` y `Services.Operations`.
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/IRegistroAccesoService.cs` — agrega `RegistrarEntradaConEvaluacionIaAsync(...)`.
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/RegistroAccesoService.cs` — inyecta opcionalmente `IIaService?`, valida rol/estado del guardia e implementa evaluación de anomalías no bloqueante con sugerencia de incidencia ITIL.
- `Acceso_UMAD_QRs/UMADAccesos/Data/UmadDbInitializer.cs` — acota siembra demo a `#if DEBUG`, siembra escenarios normal + anómalo anclados a `America/Mexico_City`, corrige reconciliación de Fase 2.
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/RenovacionService.cs` — exige responsable `Administrador` activo y prohíbe auto-resolución.
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/PaseEventoService.cs` — exige creador `Administrador` activo y valida titular activo.
- `Acceso_UMAD_QRs/UMADAccesos/Services/Operations/IncidenciaService.cs` — exige reportante activo.
- `Acceso_UMAD_QRs/UMADAccesos/Pages/QrScannerPage.xaml.cs` — sincronización atómica y cierre de detector en `MainThread`.
- `Acceso_UMAD_QRs/UMADAccesos/Services/Device/QrScannerService.cs` — protección `try/catch` en callback de cancelación.
- `Acceso_UMAD_QRs/UMADAccesos/Services/Device/EvidenciaFotoService.cs` — lista blanca de extensiones, sanitización de ruta, límite 10 MB y limpieza parcial.
- `Acceso_UMAD_QRs/UMADAccesos/Services/Sync/SyncQueueService.cs` — semáforo `_procesamientoLock` `static`.
- `Acceso_UMAD_QRs/UMADAccesos/Platforms/Android/AndroidManifest.xml` — `android:allowBackup="false"`.

---

## 3. Contratos Públicos, DTOs y Registro DI

### 3.1 Registro DI — `AddUmadAiAndAuth()` (`UMADAccesos.Extensions`)
```csharp
public static IServiceCollection AddUmadAiAndAuth(
    this IServiceCollection services,
    Action<IaOptions>? configurarIa = null,
    Action<EntornoEjecucionOptions>? configurarEntorno = null)
```
Registra todos los servicios como **`Singleton`** (sin dependencias cautivas, todos consumen `IDbContextFactory<UmadDbContext>` y singletons):
- `IaOptions`, `EntornoEjecucionOptions`
- `IAzureAiClient` -> `AzureAiHttpClient`
- `IIaProvider` -> `OfflineLocalIaProvider` y `AzureAiProductionProvider`
- `IIaService` -> `IaService`
- `IEntornoEjecucionService` -> `EntornoEjecucionService`
- `IAutorizacionRolService` -> `AutorizacionRolService`
- `ISesionActualService` -> `SesionActualService`

### 3.2 Contrato de Inteligencia Artificial (`UMADAccesos.Services.AI`)

#### `IIaService`
```csharp
public interface IIaService
{
    ModoProveedorIa ModoActivo { get; }
    Task<ClasificacionVisitaResult> ClasificarMotivoVisitaAsync(string textoLibre, CancellationToken ct = default);
    Task<EvaluacionAnomaliaResult> EvaluarAnomaliaAccesoAsync(
        int personaId, string? puntoAcceso = null, DateTime? fechaHoraUtc = null, CancellationToken ct = default);
    Task<EvaluacionAnomaliaResult> EvaluarAnomaliaAccesoAsync(
        ContextoEvaluacionAcceso contexto, CancellationToken ct = default);
}
```
- **Enumeraciones**:
  - `ModoProveedorIa`: `OfflineLocal = 0`, `AzureAiProduction = 1`
  - `TipoVisitaSugerido`: `Academico = 1`, `Administrativo = 2`, `ProveedorEntrega = 3`, `Evento = 4`, `Mantenimiento = 5`, `Otro = 6`
  - `NivelRiesgoAnomalia`: `Bajo = 0`, `Medio = 1`, `Alto = 2`
- **Opciones**:
  - `IaOptions`: `Modo` (`OfflineLocal`), `MaximoEntradasDiariasNormales` (`4`), `HoraInicioPermitidaLocal` (`06:30`), `HoraFinPermitidaLocal` (`22:00`), `VentanaReintentosMinutos` (`30`), `UmbralIntentosDenegados` (`2`), `ZonaHorariaInstitucionalId` (`"America/Mexico_City"`), `UmbralDesviacionHorarioHoras` (`4`), `MinimoHistorialParaCambioPatron` (`3`), `Azure` (`AzureAiEndpointOptions`).
  - `AzureAiEndpointOptions` *(solo configuración no secreta)*: `BackendProxyBaseUrl`, `DeploymentName` (`"umad-accesos-ia-prod"`), `TimeoutSegundos` (`10`), `DegradarAOfflineEnFallo` (`true`), método `bool EstaConfiguradoHttps()`.
- **DTOs**:
  - `ClasificacionVisitaResult`: `TipoVisitaSugerido`, `AreaDestinoSugerida`, `NivelConfianza` (`0.0–1.0`), `RequiereAprobacionEspecial`, `Justificacion`, `ProveedorUtilizado`, `IReadOnlyList<string> PalabrasClaveDetectadas`.
  - `ContextoEvaluacionAcceso`: `PersonaId`, `PuntoAcceso`, `TipoMovimiento`, `FechaHoraEventoUtc`, `TokenAccesoId`, `TokenValido`, `MotivoValidacionToken`, `RegistroAccesoActualId`.
  - `EvaluacionAnomaliaResult`: `EsAnomalo`, `NivelRiesgo`, `IReadOnlyList<string> ReglasActivadas`, `RecomendacionGuardia`, `ProveedorUtilizado`, `FechaEvaluacionUtc`, `DegradadoPorFallo`; fábricas estáticas `Normal(string proveedor)` y `SinEvaluacionPorFallo(string detalleSeguro)`.

### 3.3 Integración Operativa No Bloqueante (`UMADAccesos.Services.Operations`)

#### `RegistroEntradaConAnalisisResult`
```csharp
public sealed class RegistroEntradaConAnalisisResult
{
    public required RegistroAcceso Registro { get; init; }
    public required EvaluacionAnomaliaResult EvaluacionAnomalia { get; init; }
    public bool SugerirLevantarIncidencia { get; init; }
    public SeveridadIncidencia SeveridadSugerida { get; init; } = SeveridadIncidencia.Baja;
}
```
Invocado desde `IRegistroAccesoService.RegistrarEntradaConEvaluacionIaAsync(...)`:
1. Persiste primero la entrada mediante `RegistrarEntradaAsync` (validando rol del guardia, token HMAC-SHA256 y cola SQLite).
2. Evalúa anomalías pasando `RegistroAccesoActualId = registro.Id` para evitar doble conteo.
3. Si `IIaService` no está registrado o lanza cualquier excepción inesperada, captura el error, registra advertencia y devuelve `EvaluacionAnomaliaResult.SinEvaluacionPorFallo(...)` **sin bloquear el acceso físico en caseta**.
4. Mapea `NivelRiesgoAnomalia` -> `SeveridadIncidencia` (`Alto -> Alta`, `Medio -> Media`, `Bajo -> Baja`) y activa `SugerirLevantarIncidencia = evaluacion.EsAnomalo`.

### 3.4 Contratos de Autenticación, RBAC y Entorno (`UMADAccesos.Services.Auth`)

#### `ISesionActualService`
```csharp
public interface ISesionActualService
{
    event Action? OnSesionCambiada;
    SesionUsuario? SesionActual { get; }
    bool EstaAutenticado { get; }
    RolUsuario? RolActual { get; }
    Task<ResultadoAutenticacion> IniciarSesionAsync(string correo, string password, CancellationToken ct = default);
    Task CerrarSesionAsync();
    bool TienePermiso(PermisoSistema permiso);
    void ExigirPermiso(PermisoSistema permiso);
    bool PuedeAccederRecursosDePersona(int personaObjetivoId);
    Task<ResultadoAutenticacion> CambiarRolModoPruebaAsync(RolUsuario rolDestino, CancellationToken ct = default);
}
```
- **`SesionUsuario`** *(inmutable, sin `PasswordHash`)*: `PersonaId`, `IdentificadorGlobal`, `NombreCompleto`, `Correo`, `Matricula`, `RolInstitucional`, `RolOperativo`, `InicioSesionUtc`, `ExpiraSesionUtc`, `EsSesionModoPrueba`, método `bool EstaExpirada(DateTime? ahoraUtc = null)`.
- **`ResultadoAutenticacion`**: `Exitoso`, `Mensaje`, `Sesion`, fábricas `Exito(SesionUsuario, string)` y `Fallo(string)`.

#### `IAutorizacionRolService`
```csharp
public interface IAutorizacionRolService
{
    RolUsuario MapearRolUsuario(RolPersona rolPersona);
    IReadOnlySet<PermisoSistema> ObtenerPermisos(RolUsuario rol);
    IReadOnlySet<PermisoSistema> ObtenerPermisos(RolPersona rolPersona);
    bool TienePermiso(RolUsuario rol, PermisoSistema permiso);
    bool TienePermiso(RolPersona rolPersona, PermisoSistema permiso);
    bool PuedeAccederRecursosDePersona(SesionUsuario? sesion, int personaObjetivoId);
    void ExigirPermiso(SesionUsuario? sesion, PermisoSistema permiso);
}
```

#### `IEntornoEjecucionService` y `EntornoEjecucionOptions`
```csharp
public interface IEntornoEjecucionService
{
    bool EsCompilacionDebug { get; }
    bool BanderaModoPruebaActiva { get; }
    bool EsModoPruebaHabilitado { get; }
    string NombreEntorno { get; }
    TimeSpan DuracionSesion { get; }
}
```
- `EntornoEjecucionOptions`: `HabilitarModoPruebaEnDebug = false` (deshabilitado por defecto), `DuracionSesion = TimeSpan.FromHours(8)` (acotada en runtime entre 15 min y 24 h).

### 3.5 Matriz Completa de Permisos por Rol (RBAC Estricto, 15 Permisos Disjuntos)

Mapeo institucional -> operativo (`MapearRolUsuario`, política *fail-closed* que lanza `ArgumentOutOfRangeException` ante valores desconocidos):
- `Estudiante`, `Docente`, `Administrativo`, `Visitante` -> **`RolUsuario.UsuarioComunidad`**
- `Guardia` -> **`RolUsuario.GuardiaSeguridad`**
- `Administrador` -> **`RolUsuario.AdministradorAccesos`**

| # | `PermisoSistema` | Valor | `UsuarioComunidad` | `GuardiaSeguridad` | `AdministradorAccesos` |
|---:|---|---:|:---:|:---:|:---:|
| 1 | `ConsultarPropiosPases` | `1` | **Sí** | No | No |
| 2 | `SolicitarPaseORenovacion` | `2` | **Sí** | No | No |
| 3 | `VerPropioHistorial` | `3` | **Sí** | No | No |
| 4 | `ClasificarMotivoVisitaIa` | `4` | **Sí** | No | No |
| 5 | `EscanearCodigoQr` | `10` | No | **Sí** | No |
| 6 | `CapturarEvidenciaFoto` | `11` | No | **Sí** | No |
| 7 | `RegistrarEntradaSalida` | `12` | No | **Sí** | No |
| 8 | `VerAlertasAnomalia` | `13` | No | **Sí** | No |
| 9 | `ReportarIncidencia` | `14` | No | **Sí** | No |
| 10 | `AprobarRechazarRenovaciones` | `20` | No | No | **Sí** |
| 11 | `GestionarPasesEvento` | `21` | No | No | **Sí** |
| 12 | `RevocarTokens` | `22` | No | No | **Sí** |
| 13 | `ConsultarHistorialGlobal` | `23` | No | No | **Sí** |
| 14 | `GestionarIncidencias` | `24` | No | No | **Sí** |
| 15 | `SupervisarSincronizacionOffline` | `25` | No | No | **Sí** |

**Propiedad de recursos (`PuedeAccederRecursosDePersona`)**:
- Sesión `null`, `PersonaId <= 0`, `personaObjetivoId <= 0` o expirada -> `false`.
- `AdministradorAccesos` -> `true` (acceso institucional global).
- `UsuarioComunidad` y `GuardiaSeguridad` -> `sesion.PersonaId == personaObjetivoId` (los guardias operan en caseta únicamente a través de sus permisos explícitos de caseta, nunca consultando perfiles privados arbitrarios).

---

## 4. Decisiones de Diseño (Cómo y Porqué)

### 4.1 Heurísticas NLP y Detección de Anomalías Offline (`OfflineLocalIaProvider`)
- **Porqué**: una caseta universitaria debe mantener latencia <50 ms y disponibilidad total incluso sin conexión a internet (*Offline-First*).
- **Cómo (Capacidad A — NLP)**: normaliza texto español mediante descomposición Unicode `NormalizationForm.FormD` (elimina diacríticos/puntuación, convierte a minúsculas), combina coincidencia de frases compuestas y tokens individuales contra léxicos ponderados (6 categorías + 10 áreas destino UMAD), calcula confianza acotada `[0.0, 1.0]` considerando fuerza de coincidencia y margen frente a la segunda categoría, y marca `RequiereAprobacionEspecial = true` para `ProveedorEntrega`, `Mantenimiento`, `Otro`, áreas sensibles (`Rectoría`, `Caja y Finanzas`, `Almacén / Recepción de Proveedores`, `Servicios Generales y Mantenimiento`) o confianza `< 0.60`.
- **Cómo (Capacidad B — Anomalías SQLite)**: consulta hasta 500 registros recientes de `RegistrosAcceso` con `AsNoTracking()` (excluyendo `RegistroAccesoActualId` para evitar doble conteo) y aplica 4 reglas deterministas:
  1. **Exceso de entradas diarias**: entradas autorizadas en el día local (`America/Mexico_City`, fallback UTC-6) `> MaximoEntradasDiariasNormales` (`4`). Si alcanza `>= 8`, escala directamente a riesgo `Alto`.
  2. **Entrada abierta sin salida (*tailgating*)**: detecta cualquier entrada autorizada previa `<= fechaEventoUtc` que no tenga salida vinculada (`EntradaRelacionadaId`) ni salida posterior.
  3. **Fuera de horario institucional**: hora local fuera del rango `06:30–22:00`.
  4. **Cambio brusco de patrón o intentos reiterados inválidos**:
     - Desviación horaria circular `>= 4 h` respecto al promedio histórico (con `>= 3` entradas previas).
     - Cambio de `PuntoAcceso` en menos de `5 min` (riesgo `Alto` directo).
     - `>= 2` intentos `Denegado` (incluyendo intento actual si `!TokenValido`) en los últimos `30 min` (riesgo `Alto` directo).
  - Si se activan `>= 2` reglas o cualquier indicador severo -> `NivelRiesgoAnomalia.Alto`; si se activa `1` regla moderada -> `Medio`; `0` reglas -> `Bajo`.

### 4.2 Arquitectura de Producción en Azure AI
- **Porqué**: separar inferencia entrenable en nube del cliente móvil sin exponer API keys ni enviar PII innecesaria.
- **Cómo**: `IaService` implementa el **Patrón Estrategia** (`IIaProvider`) con degradación automática de `AzureAiProductionProvider` a `OfflineLocalIaProvider` ante falta de URL HTTPS, error HTTP o timeout. `AzureAiHttpClient` exige `Uri.UriSchemeHttps`, aplica timeout acotado (`1–60 s`) y envía payloads pseudonimizados al BFF institucional (`POST api/v1/ai/classify-visit` y `api/v1/ai/evaluate-anomaly`), donde el backend se autentica hacia **Azure AI Language (CLU / Custom Classification)**, **Azure OpenAI** y **Azure Key Vault** usando **Managed Identity**. Ver especificación completa y diagramas Mermaid en [`docs/arquitectura-ia-azure.md`](arquitectura-ia-azure.md).

### 4.3 Matriz RBAC Fail-Closed y Sesión Reactiva
- **Porqué**: garantizar principio de mínimo privilegio, eliminar estados globales inseguros (`App.CurrentUser` de v1.0) y prevenir enumeración de usuarios o exposición de hashes en memoria/logs.
- **Cómo**: `AutorizacionRolService` usa `FrozenDictionary`/`FrozenSet` inmutables; toda sesión posee vigencia obligatoria (`ExpiraSesionUtc`) con invalidación automática al expirar; `IniciarSesionAsync` retorna siempre el mensaje genérico `"Credenciales inválidas o cuenta inactiva."`; `SesionActualService` protege el estado con `lock (_bloqueoSesion)` e invoca `OnSesionCambiada` **fuera del lock** aislando excepciones por suscriptor para evitar *deadlocks* en Blazor Hybrid.

### 4.4 Doble Blindaje del Modo de Prueba (Control de Cambios ITIL)
- **Porqué**: en v1.0 el modo de prueba era accesible libremente en cualquier compilación, violando separación de ambientes y control de cambios ITIL.
- **Cómo**: se exige el cumplimiento simultáneo de **dos barreras independientes**:
  1. **Compilación (`#if DEBUG`)**: en `Release`, `EntornoEjecucionService` devuelve `false` **literal** en `EsCompilacionDebug`, `BanderaModoPruebaActiva` y `EsModoPruebaHabilitado` (ignorando incluso si alguien pasara `HabilitarModoPruebaEnDebug = true`), y el método privado `CambiarRolModoPruebaDebugAsync` **ni siquiera existe en el IL compilado de `Release`**.
  2. **Bandera explícita en desarrollo (`EntornoEjecucionOptions.HabilitarModoPruebaEnDebug`)**: por defecto es `false`, por lo que incluso en `Debug` requiere activación deliberada al invocar `AddUmadAiAndAuth()`. Cuando se activa en `Debug`, autentica contra cuentas reales existentes en SQLite y marca `SesionUsuario.EsSesionModoPrueba = true`.

---

## 5. Ejemplos y Casos Semilla Listos para Demo de Fase 4

### 5.1 Ejemplos Verificados de Clasificación NLP (`IIaService.ClasificarMotivoVisitaAsync`)

| # | Texto libre de entrada | `TipoVisitaSugerido` | `AreaDestinoSugerida` | `NivelConfianza` | `RequiereAprobacionEspecial` |
|---:|---|---|---:|---:|:---:|
| 1 | *"Vengo a entregar papelería al área de rectoría"* | `ProveedorEntrega` | `Rectoría` | `0.98` | `true` |
| 2 | *"Asisto al congreso de ingeniería en el auditorio"* | `Evento` | `Auditorio Principal` | `0.98` | `false` |
| 3 | *"Cita con control escolar para recoger certificado"* | `Administrativo` | `Control Escolar` | `0.98` | `false` |
| 4 | *"Revisión de aires acondicionados en edificio B"* | `Mantenimiento` | `Servicios Generales y Mantenimiento` | `0.98` | `true` |
| 5 | *"Asesoría de tesis en laboratorio de ingeniería"* | `Academico` | `Laboratorios de Ingeniería` | `0.98` | `false` |
| 6a | `""` / `"   "` *(vacío)* | `Otro` | `Recepción General` | `0.00` | `true` |
| 6b | *"vengo a ver a un conocido rápido"* *(ambiguo)* | `Otro` | `Recepción General` | `0.15` | `true` |

### 5.2 Cuentas y Escenarios Semilla en SQLite (`#if DEBUG` únicamente)

| Cuenta semilla (`DEBUG`) | Contraseña demo (`PBKDF2-SHA256` en SQLite) | Rol institucional / operativo | Escenario sembrado para Fase 4 |
|---|---|---|---|
| `admin@umad.edu.mx` | `UmadAdmin#2026!` | `Administrador` / `AdministradorAccesos` | Administración global (renovaciones, eventos, revocación, cola offline). |
| `guardia@umad.edu.mx` | `UmadGuardia#2026!` | `Guardia` / `GuardiaSeguridad` | Operación en caseta (escaneo QR, evidencia, alertas de anomalías, incidencias). |
| `estudiante@umad.edu.mx` | `UmadEstudiante#2026!` | `Estudiante` / `UsuarioComunidad` | **Caso Normal**: 1 entrada a las 08:30 local y salida vinculada a las 13:30 local del día previo -> al evaluar en horario hábil produce `EsAnomalo = false`, `NivelRiesgo = Bajo`, `0` reglas activadas. |
| `visitante.demo@umad.edu.mx` | `UmadVisitante#2026!` | `Visitante` / `UsuarioComunidad` | **Caso Anómalo Real**: 4 entradas autorizadas hoy (incluyendo madrugada y entrada #4 abierta sin salida) + 2 intentos denegados con token revocado en los últimos 10 min -> al evaluar en `DateTime.UtcNow` produce `EsAnomalo = true`, `NivelRiesgo = Alto` y dispara múltiples reglas (exceso diario `5 > 4`, entrada abierta/*tailgating*, desviación de patrón y reintentos denegados). |

> **Seguridad en `Release`**: `UmadDbInitializer` omite completamente la siembra demo en compilaciones `Release` (`Personas.Count == 0` al iniciar una base nueva), eliminando credenciales literales del binario de producción.

---

## 6. Evidencia Exacta de Compilación, Pruebas y Auditoría

### 6.1 Compilación `Debug` y `Release` (`UMADAccesos.csproj`)
- **Compilación completa MacCatalyst (`Debug`, incluyendo ILLink y empaquetado `.app` en macOS Apple Silicon):**
  ```bash
  dotnet build Acceso_UMAD_QRs/UMADAccesos/UMADAccesos.csproj -f net10.0-maccatalyst
  # Resultado: Compilación correcta. 0 Advertencia(s), 0 Errores
  ```
- **Validación `Release` (`net10.0-maccatalyst`, `-t:CoreCompile`):**
  ```bash
  DOTNET_CLI_HOME=/tmp dotnet build Acceso_UMAD_QRs/UMADAccesos/UMADAccesos.csproj -f net10.0-maccatalyst -c Release --no-restore -t:CoreCompile
  # Resultado: Compilación correcta. 0 Advertencia(s), 0 Errores
  ```

### 6.2 Pruebas Funcionales Reales contra SQLite (`Debug` y `Release`)
Ejecutadas mediante harness consola `net10.0` temporal fuera del repositorio (enlazando directamente `Models/`, `Data/`, `Services/AI/`, `Services/Auth/`, `Services/Security/`, `Services/Operations/` y `Extensions/AiAndAuthServiceCollectionExtensions.cs` sobre `Microsoft.EntityFrameworkCore.Sqlite 10.0.15` y archivo SQLite temporal real):

| Suite / Bloque de Pruebas | Resultado `Debug` | Resultado `Release` | Evidencia clave verificada |
|---|---:|---:|---|
| Inicialización y siembra `UmadDbInitializer` | **3 / 3 PASS** | **2 / 2 PASS** | `Debug`: 4 cuentas PBKDF2 (`v1$100000$...`). `Release`: `Count=0` (mitigación CWE-798 confirmada). |
| Capacidad A — NLP heurístico offline (6 casos + vacío/ambiguo) | **8 / 8 PASS** | **8 / 8 PASS** | Clasificación exacta en los 5 casos institucionales (`Conf=0.98`) + vacío (`0.0`) y ambiguo (`0.15`) en `Otro` con `RequiereAprobacionEspecial = true`. |
| Capacidad B — Detección de anomalías en SQLite real | **3 / 3 PASS** | **3 / 3 PASS** | Caso normal `estudiante@umad.edu.mx` -> `Bajo` (`0` reglas). Caso anómalo `visitante.demo@umad.edu.mx` -> `Alto` (`4` reglas activadas: exceso diario `5>4`, tailgating, desviación horaria y reintentos denegados) + caso fuera de horario (`01:30`). |
| Degradación automática `AzureAiProduction -> OfflineLocal` | **3 / 3 PASS** | **3 / 3 PASS** | Sin endpoint HTTPS configurado degrada automáticamente con `DegradadoPorFallo = true` y sufijo `(Degradado desde Azure)`. |
| Integración operativa `RegistroAccesoService` + reparación Paso 0 | **4 / 4 PASS** | **4 / 4 PASS** | `RegistrarEntradaConEvaluacionIaAsync` persiste entrada y adjunta análisis; ante fallo fatal del motor IA responde `FallbackSeguro` sin bloquear acceso; rechaza con `UnauthorizedAccessException` operador sin rol Guardia/Admin. |
| Matriz RBAC (15 permisos) y sesión reactiva `ISesionActualService` | **20 / 20 PASS** | **20 / 20 PASS** | 15 permisos 100% disjuntos (`4 / 5 / 6`); `MapearRolUsuario(999)` fail-closed; login inválido con mensaje genérico único; `OnSesionCambiada` disparado en login/logout; `ExigirPermiso` y `PuedeAccederRecursosDePersona` verificados para los 3 roles. |
| Doble blindaje Modo de prueba (`#if DEBUG` + `IEntornoEjecucionService`) | **5 / 5 PASS** | **5 / 5 PASS** | En `Debug` con bandera `false`: bloqueado. En `Debug` con bandera `true`: cambia entre los 3 roles con cuentas reales de SQLite. En `Release` **incluso con `HabilitarModoPruebaEnDebug = true`**: `EsCompilacionDebug == false`, `EsModoPruebaHabilitado == false` y `CambiarRolModoPruebaAsync` falla cerrado. |
| **TOTAL** | **46 / 46 PASS** | **45 / 45 PASS** | **0 fallos en ambas configuraciones.** |

### 6.3 Auditoría de XML Doc, Seguridad y Documentación
- **XML Doc (`39/39` archivos `.cs` nuevos y modificados en Fase 3)**: **0 incidencias**. Todas las clases, interfaces, enumeraciones, constructores y métodos cumplen `<summary>`, `<author>Emmanuel Baltazar López</author>`, `<date>25/11/2026</date>`, `<version>1.0</version>`, `<modification>25/11/2026</modification>`. Todas las propiedades, campos, constantes, eventos y miembros de enum emplean exclusivamente comentarios simples `// ...` (`0` usos indebidos de `<summary>`).
- **Seguridad y Anti-regresión v1.0 (`89/89` archivos `.cs` y `.razor`)**:
  - `0` vestigios de v1.0 (`IsTestMode`, `SimulateValidAccess`, `SimulateInvalidAccess`, `App.CurrentUser`, `Shell.Current`, `DateTime.Now`).
  - `0` API keys, cabeceras de suscripción o secretos hardcodeados.
  - `0` exposiciones de `PasswordHash` en `SesionUsuario` o en llamadas `ILogger`.
- **`docs/arquitectura-ia-azure.md`**: completo y validado (256 líneas; arquitectura Azure AI Language + Azure OpenAI, patrón BFF + Managed Identity + Key Vault, diagramas Mermaid `flowchart LR` y `sequenceDiagram` válidos sin HTML, contratos REST/SDK, presupuesto de latencia, minimización de PII y matriz de degradación).

---

## 7. Guía Rápida de Integración para Fase 4 (UI Blazor Hybrid)

1. **Inyección directa en páginas/componentes Razor** (espacios ya importados en `Components/_Imports.razor`):
   - `@inject ISesionActualService SesionActual` — suscribirse a `OnSesionCambiada` en `OnInitialized` y desuscribirse en `Dispose` (usando `InvokeAsync(StateHasChanged)`).
   - `@inject IEntornoEjecucionService Entorno` — renderizar el selector rápido de rol demo **exclusivamente** cuando `Entorno.EsModoPruebaHabilitado` sea `true` (y nunca mostrarlo en `Release`).
   - `@inject IIaService IaService` — usar `ClasificarMotivoVisitaAsync` en el formulario de solicitud de pase/visita y mostrar categoría, área sugerida, nivel de confianza y distintivo de aprobación especial.
   - `@inject IRegistroAccesoService RegistroAccesoService` — en la pantalla de caseta del guardia, invocar `RegistrarEntradaConEvaluacionIaAsync(...)` para mostrar el semáforo de `NivelRiesgo` (`Bajo`/`Medio`/`Alto`), las `ReglasActivadas`, la `RecomendacionGuardia` y el botón de acceso rápido para levantar incidencia cuando `SugerirLevantarIncidencia == true`.
2. **Protección de vistas y acciones por permiso**: evaluar `SesionActual.TienePermiso(PermisoSistema.*)` en menús y rutas, manteniendo siempre la verificación de respaldo en servicios.
3. **Activación controlada del modo de prueba en desarrollo**: si Fase 4 requiere el selector demo durante pruebas locales, habilitarlo explícitamente en `MauiProgram.cs` dentro de `#if DEBUG` mediante `builder.Services.AddUmadAiAndAuth(configurarEntorno: opt => opt.HabilitarModoPruebaEnDebug = true);` (garantizando que en `Release` continúe deshabilitado por compilación).
