# Handoff Fase 1 — Núcleo Arquitectónico, Seguridad y Continuidad Offline (UMAD Accesos v2.0)

- **Proyecto**: UMAD Accesos v2.0 (`AccesoDigitalQRs_UMAD`)
- **Rama de trabajo**: `feat/fase-1-core` (alineada mediante merge local desde `origin/develop` `b890201` -> commit `1bacf10`, sin `git push`)
- **Fecha de verificación**: 26/11/2026
- **Estado general**: **APROBADO EN VERDE** (`0 Errores, 0 Advertencias` en `net10.0-windows10.0.19041.0`, `0 paquetes NuGet vulnerables o en desuso` en los 4 TargetFrameworks, `0 hallazgos pendientes` en seguridad, anti-regresión, alcance y XML Doc)

---

## 1. Diagnóstico de v1.0 vs. Estado Actual (Fase 1 v2.0)

### 1.1 Hallazgos críticos heredados de v1.0 (`origin/develop`)
Durante la auditoría diagnóstica inicial se detectaron defectos estructurales y de seguridad bloqueantes para operación institucional:

1. **Rama desincronizada y solución fragmentada**: `feat/fase-1-core` había nacido desde `main` (`c3e050c`, esqueleto vacío), mientras el código real v1.0 vivía en `origin/develop` (`b890201`) repartido en tres proyectos (`UMADAccesos`, `SharedResources`, `MigrationApp`) con dos juegos de migraciones inconsistentes, `MigrationApp/Program.cs` corrupto y archivos `.csproj.lscache` versionados en Git.
2. **Contraseñas en texto plano**: `UserModel.Password` almacenaba y comparaba contraseñas en claro directamente en SQLite (`u.Email == Email && u.Password == Password`).
3. **Tokens QR estáticos, cortos y sin firma criptográfica**: `AccessTokenViewModel` generaba códigos fijos de 10 caracteres hexadecimales (`Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper()`, ~40 bits de entropía), sin firma criptográfica, sin revocación al reemitir, con vigencias de hasta 10 años y expuestos en texto visible bajo el QR (`MyQRView.xaml`).
4. **Modo de prueba abierto y atajos inseguros en producción**: `IsTestMode`, `SimulateValidAccess`, `SimulateInvalidAccess` y un `Switch` en `AccessPointSetupView` permitían aceptar cualquier QR e insertar registros reales asignados al primer usuario de la tabla. Además existía escalada de privilegios y *account takeover* en `SignUpView` / `UserViewModel`, y autorización basada en `string.Contains(...)`.
5. **Sin soporte offline-first ni trazabilidad de auditoría**: PKs puramente locales sin `Guid` global, uso de `DateTime.Now` manipulable, ausencia de estados de sincronización/reintentos, sin registro de accesos denegados ni vínculo al token presentado.
6. **Acoplamiento y defectos de ciclo de vida**: `UmadDbContext` (Scoped) inyectado en `AccessLogViewModel` (Singleton — *captive dependency*), estado global estático `App.CurrentUser`, Service Locator (`Application.Current.Handler.MauiContext.Services`), acoplamiento directo a `Shell.Current` y cero capa de servicios.

### 1.2 Decisiones arquitectónicas de Fase 1 (cómo y por qué)

| Dimensión | Estado v1.0 | Decisión v2.0 (Fase 1) | Justificación técnica / ITIL |
|---|---|---|---|
| **Estructura de solución** | 3 proyectos fragmentados (`UMADAccesos`, `SharedResources`, `MigrationApp`) + `.lscache` | Proyecto único consolidado `Acceso_UMAD_QRs/UMADAccesos/UMADAccesos.csproj` referenciado en `UMADAccesos.slnx` | Elimina duplicidad de migraciones y dependencias rotas; simplifica despliegue multiplataforma manteniendo capas internas claras (`Models`, `Data`, `Services`, `Extensions`, `Components`). |
| **UI Host** | MAUI XAML clásico + `AppShell` + 8 Views / 4 ViewModels acoplados | **.NET MAUI Blazor Hybrid** (`Microsoft.NET.Sdk.Razor` + `Microsoft.AspNetCore.Components.WebView.Maui 10.0.51` + `MainPage` con `<BlazorWebView>`) | Permite interfaz web responsiva unificada (Android y Windows) con DI real en componentes Razor, eliminando `Shell.Current`, `CurrentUser` estático y Service Locator. |
| **Escaneo QR nativo** | `ZXing.Net.Maui.Controls 0.7.4` acoplado a `ScannerView.xaml.cs` | Se conserva `ZXing.Net.Maui.Controls 0.7.4` y `.UseBarcodeReader()` en `MauiProgram.cs` (puente nativo reservado a Fase 2) | Evita limitaciones de cámara/WebRTC dentro de WebViews en Android/Windows, dejando listo el host nativo para la página modal de escaneo de Fase 2. |
| **Acceso a datos EF Core** | `AddDbContext<UmadDbContext>` (Scoped) con *captive dependency* en Singleton | `AddDbContextFactory<UmadDbContext>` (Singleton) con SQLite local (`UmadLocal.db`) | Patrón recomendado en Blazor Hybrid; cada operación usa un contexto corto y aislado (`await using`), eliminando condiciones de carrera y dependencias cautivas. |
| **Seguridad de contraseñas (ITIL Seguridad)** | Texto plano en `UserModel.Password` | `IPasswordHasher` / `PasswordHasherService` con **PBKDF2-SHA256** (`100_000` iteraciones, salt aleatoria 16 B, clave derivada 32 B, formato `v1$<iter>$<salt>$<hash>`, `FixedTimeEquals` y `ZeroMemory`) | Previene exposición de credenciales ante robo de SQLite y mitiga ataques de fuerza bruta y de temporización (*timing attacks*). |
| **Seguridad de tokens QR (ITIL Seguridad)** | 10 caracteres fijos sin firma | `ITokenAccesoService` / `TokenAccesoService` con **HMAC-SHA256** (payload `v2|<guid>|<personaId>|<tipoPase>|<emisionUnix>|<expiracionUnix>|<nonceHex>` en Base64Url + firma Base64Url) y clave de 256 bits gestionada por `SecureStorageTokenKeyProvider` (`ISecureStorage`) | Garantiza integridad y autenticidad sin conexión a servidor; incluye vigencias acotadas por `TipoPase`, revocación con motivo y auditoría, verificación en tiempo constante y validación UTC. |
| **Continuidad operativa (ITIL Continuidad)** | Sin cola offline ni metadatos de sync | `ISyncQueueService` / `SyncQueueService` offline-first sobre SQLite con `IdentificadorGlobal`, `Sincronizado`, `EstadoSincronizacion`, `Reintentos`, `FechaUltimoIntento`, `UltimoError` y bloqueo `SemaphoreSlim(1, 1)` | La caseta nunca se bloquea sin internet: cada acceso se persiste localmente en estado `Pendiente` y se despacha de forma idempotente y tolerante a fallos cuando hay red. |
| **Inicialización y semilla** | `EnsureCreated()` síncrono sin usuarios semilla | `IUmadDbInitializer` / `UmadDbInitializer` thread-safe (`SemaphoreSlim`) e idempotente; siembra 3 cuentas mínimas únicamente cuando `Personas` está vacía, hasheando siempre vía `IPasswordHasher` | Garantiza arranque seguro y repetible sin sobrescribir datos existentes ni introducir credenciales en claro. |

---

## 2. Inventario de Cambios y Árbol Resultante

### 2.1 Archivos modificados (4)
- `Acceso_UMAD_QRs/UMADAccesos.slnx` — consolidada para apuntar únicamente a `UMADAccesos/UMADAccesos.csproj`.
- `Acceso_UMAD_QRs/UMADAccesos/UMADAccesos.csproj` — migrado a `Microsoft.NET.Sdk.Razor`, agregado `<EnableDefaultCssItems>false</EnableDefaultCssItems>` y `Microsoft.AspNetCore.Components.WebView.Maui` (`10.0.51`), conservado `ZXing.Net.Maui.Controls` (`0.7.4`), actualizados los paquetes de servicio `.NET 10` de EF Core y Logging (`Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Tools` y `Microsoft.Extensions.Logging.Debug`) de `10.0.7` a `10.0.15` para remediar `NU1903` (`GHSA-2m69-gcr7-jv3q`), y removidas referencias a `SharedResources` y vistas XAML heredadas.
- `Acceso_UMAD_QRs/UMADAccesos/MauiProgram.cs` — configurado `.UseMauiApp<App>()`, `.UseBarcodeReader()`, `AddMauiBlazorWebView()`, `#if DEBUG` (`AddBlazorWebViewDeveloperTools()` + `AddDebug()`), `AddUmadCore()` e inicialización controlada de SQLite mediante `IUmadDbInitializer`.
- `Acceso_UMAD_QRs/UMADAccesos/App.xaml.cs` — eliminados `App.CurrentUser` estático y Service Locator; `CreateWindow` monta `new Window(new MainPage())`.

### 2.2 Archivos creados (32)
- **Host Blazor Hybrid y diagnóstico mínimo (8)**:
  - `Acceso_UMAD_QRs/UMADAccesos/MainPage.xaml`
  - `Acceso_UMAD_QRs/UMADAccesos/MainPage.xaml.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/wwwroot/index.html`
  - `Acceso_UMAD_QRs/UMADAccesos/wwwroot/css/app.css`
  - `Acceso_UMAD_QRs/UMADAccesos/Components/_Imports.razor`
  - `Acceso_UMAD_QRs/UMADAccesos/Components/Routes.razor`
  - `Acceso_UMAD_QRs/UMADAccesos/Components/Layout/MainLayout.razor`
  - `Acceso_UMAD_QRs/UMADAccesos/Components/Pages/Home.razor`
- **Modelos de dominio y enumeraciones (8)**:
  - `Acceso_UMAD_QRs/UMADAccesos/Models/RolPersona.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Models/TipoPase.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Models/TipoMovimientoAcceso.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Models/ResultadoAcceso.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Models/EstadoSincronizacion.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Models/Persona.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Models/TokenAcceso.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Models/RegistroAcceso.cs`
- **Persistencia SQLite y DI (4)**:
  - `Acceso_UMAD_QRs/UMADAccesos/Data/UmadDbContext.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Data/IUmadDbInitializer.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Data/UmadDbInitializer.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Extensions/ServiceCollectionExtensions.cs`
- **Servicios de Seguridad (7)**:
  - `Acceso_UMAD_QRs/UMADAccesos/Services/Security/IPasswordHasher.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Services/Security/PasswordHasherService.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Services/Security/ITokenKeyProvider.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Services/Security/SecureStorageTokenKeyProvider.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Services/Security/TokenValidationResult.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Services/Security/ITokenAccesoService.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Services/Security/TokenAccesoService.cs`
- **Servicios de Sincronización Offline (4)**:
  - `Acceso_UMAD_QRs/UMADAccesos/Services/Sync/SyncQueueSummary.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Services/Sync/SyncProcessResult.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Services/Sync/ISyncQueueService.cs`
  - `Acceso_UMAD_QRs/UMADAccesos/Services/Sync/SyncQueueService.cs`
- **Documentación de entrega (1)**:
  - `docs/handoff-fase-1.md` (este documento)

### 2.3 Archivos y proyectos eliminados (legado v1.0)
- Proyecto `Acceso_UMAD_QRs/MigrationApp/` completo (incl. `Program.cs` corrupto, `DataContextFactory.cs` y migraciones divergentes).
- Proyecto `Acceso_UMAD_QRs/SharedResources/` completo (modelos v1.0 con contraseña en claro y migraciones antiguas).
- Carpeta antigua `Acceso_UMAD_QRs/Acceso_UMAD_QRs/` y solución antigua `Acces_UMAD_QRs.slnx` (reemplazados al integrar `origin/develop`).
- Dentro de `Acceso_UMAD_QRs/UMADAccesos/`:
  - `AppShell.xaml`, `AppShell.xaml.cs`
  - `ViewModels/` (`AccessLogViewModel.cs`, `AccessTokenViewModel.cs`, `RoleViewModel.cs`, `UserViewModel.cs`)
  - `Views/` (`AccessGeneratorView`, `AccessPointSetupView`, `ApprovalsView`, `HistoryView`, `LoginView`, `MyQRView`, `ScannerView`, `SignUpView` — `.xaml` y `.xaml.cs`)
  - Todos los archivos `*.csproj.lscache`

### 2.4 Árbol de carpetas resultante
```text
AccesoDigitalQRs_UMAD/
├── docs/
│   └── handoff-fase-1.md
└── Acceso_UMAD_QRs/
    ├── UMADAccesos.slnx
    └── UMADAccesos/
        ├── App.xaml / App.xaml.cs
        ├── MainPage.xaml / MainPage.xaml.cs
        ├── MauiProgram.cs
        ├── UMADAccesos.csproj
        ├── Components/
        │   ├── _Imports.razor
        │   ├── Routes.razor
        │   ├── Layout/
        │   │   └── MainLayout.razor
        │   └── Pages/
        │       └── Home.razor
        ├── Data/
        │   ├── IUmadDbInitializer.cs
        │   ├── UmadDbContext.cs
        │   └── UmadDbInitializer.cs
        ├── Extensions/
        │   └── ServiceCollectionExtensions.cs
        ├── Models/
        │   ├── EstadoSincronizacion.cs
        │   ├── Persona.cs
        │   ├── RegistroAcceso.cs
        │   ├── ResultadoAcceso.cs
        │   ├── RolPersona.cs
        │   ├── TipoMovimientoAcceso.cs
        │   ├── TipoPase.cs
        │   └── TokenAcceso.cs
        ├── Platforms/ (Android, iOS, MacCatalyst, Windows)
        ├── Properties/
        │   └── launchSettings.json
        ├── Resources/ (AppIcon, Fonts, Images, Raw, Splash, Styles)
        ├── Services/
        │   ├── Security/
        │   │   ├── IPasswordHasher.cs
        │   │   ├── ITokenAccesoService.cs
        │   │   ├── ITokenKeyProvider.cs
        │   │   ├── PasswordHasherService.cs
        │   │   ├── SecureStorageTokenKeyProvider.cs
        │   │   ├── TokenAccesoService.cs
        │   │   └── TokenValidationResult.cs
        │   └── Sync/
        │       ├── ISyncQueueService.cs
        │       ├── SyncProcessResult.cs
        │       ├── SyncQueueService.cs
        │       └── SyncQueueSummary.cs
        └── wwwroot/
            ├── index.html
            └── css/
                └── app.css
```

---

## 3. Esquema de `UmadDbContext`, Inicialización y Extensibilidad

### 3.1 Entidades, llaves y restricciones

#### Tabla `Personas` (`UMADAccesos.Models.Persona`)
| Columna | Tipo C# | Restricciones / Default |
|---|---|---|
| `Id` | `int` | **PK** autoincremental |
| `IdentificadorGlobal` | `Guid` | Requerido (`Guid.NewGuid()`), **UNIQUE** |
| `Matricula` | `string?` | Max `50`, **UNIQUE filtrado** (`"Matricula" IS NOT NULL`) |
| `NombreCompleto` | `string` | Requerido, Max `150` |
| `Correo` | `string` | Requerido, Max `150`, **UNIQUE** |
| `PasswordHash` | `string` | Requerido, Max `512` (PBKDF2-SHA256) |
| `Rol` | `RolPersona` (`int`) | Requerido (`Estudiante=1..Administrador=6`) |
| `Activo` | `bool` | Requerido, Default `true` |
| `FechaCreacionUtc` | `DateTime` | Requerido (UTC) |
| `FechaActualizacionUtc` | `DateTime?` | Opcional (UTC) |

#### Tabla `TokensAcceso` (`UMADAccesos.Models.TokenAcceso`)
| Columna | Tipo C# | Restricciones / Default |
|---|---|---|
| `Id` | `int` | **PK** autoincremental |
| `IdentificadorGlobal` | `Guid` | Requerido, **UNIQUE** |
| `PersonaId` | `int` | **FK** -> `Personas.Id` (`DeleteBehavior.Restrict`), Requerido |
| `CodigoFirmado` | `string` | Requerido, Max `512`, **UNIQUE** |
| `TipoPase` | `TipoPase` (`int`) | Requerido (`Permanente=1, Temporal=2, Visitante=3, Evento=4`) |
| `FechaEmision` | `DateTime` | Requerido (UTC) |
| `FechaExpiracion` | `DateTime` | Requerido (UTC) |
| `Revocado` | `bool` | Requerido, Default `false` |
| `FechaRevocacionUtc` | `DateTime?` | Opcional (UTC) |
| `MotivoRevocacion` | `string?` | Opcional, Max `250` |

#### Tabla `RegistrosAcceso` (`UMADAccesos.Models.RegistroAcceso`)
| Columna | Tipo C# | Restricciones / Default |
|---|---|---|
| `Id` | `int` | **PK** autoincremental |
| `IdentificadorGlobal` | `Guid` | Requerido, **UNIQUE** |
| `PersonaId` | `int` | **FK** -> `Personas.Id` (`DeleteBehavior.Restrict`), Requerido |
| `TokenAccesoId` | `int?` | **FK** -> `TokensAcceso.Id` (`DeleteBehavior.SetNull`), Opcional |
| `PuntoAcceso` | `string` | Requerido, Max `120` |
| `TipoMovimiento` | `TipoMovimientoAcceso` (`int`) | Requerido (`Entrada=1, Salida=2`) |
| `Resultado` | `ResultadoAcceso` (`int`) | Requerido (`Autorizado=1, Denegado=2`) |
| `MotivoResultado` | `string?` | Opcional, Max `300` |
| `FechaHora` | `DateTime` | Requerido (UTC) |
| `Sincronizado` | `bool` | Requerido, Default `false` |
| `EstadoSincronizacion` | `EstadoSincronizacion` (`int`) | Requerido, Default `Pendiente` (`0`) |
| `FechaUltimoIntento` | `DateTime?` | Opcional (UTC) |
| `Reintentos` | `int` | Requerido, Default `0` |
| `UltimoError` | `string?` | Opcional, Max `500` |

### 3.2 Índices configurados (10)
- `Personas`:
  - `IX_Personas_IdentificadorGlobal` (UNIQUE)
  - `IX_Personas_Matricula` (UNIQUE filtrado: `"Matricula" IS NOT NULL`)
  - `IX_Personas_Correo` (UNIQUE)
- `TokensAcceso`:
  - `IX_TokensAcceso_IdentificadorGlobal` (UNIQUE)
  - `IX_TokensAcceso_CodigoFirmado` (UNIQUE)
  - `IX_TokensAcceso_Persona_Estado_Vigencia` (`PersonaId`, `Revocado`, `FechaExpiracion`)
- `RegistrosAcceso`:
  - `IX_RegistrosAcceso_IdentificadorGlobal` (UNIQUE)
  - `IX_RegistrosAcceso_FechaHora`
  - `IX_RegistrosAcceso_Sincronizado`
  - `IX_RegistrosAcceso_EstadoSincronizacion`

### 3.3 Estrategia de inicialización, semilla y extensibilidad para Fase 2
- **Inicialización (`UmadDbInitializer`)**: utiliza `IDbContextFactory<UmadDbContext>` y `SemaphoreSlim(1, 1)` para ejecutar `EnsureCreatedAsync` de forma idempotente y segura ante concurrencia.
- **Siembra condicional**: evalúa `!await db.Personas.AnyAsync(...)`; únicamente cuando la tabla está vacía crea 3 cuentas base (`admin@umad.edu.mx` / `RolPersona.Administrador`, `guardia@umad.edu.mx` / `RolPersona.Guardia`, `estudiante@umad.edu.mx` / `RolPersona.Estudiante`), derivando siempre su `PasswordHash` a través de `IPasswordHasher.HashPassword(...)`.
- **Extensibilidad sin romper Fase 1**: `UmadDbContext` está declarado como `public partial class UmadDbContext` e invoca `partial void OnModelCreatingExtensible(ModelBuilder modelBuilder)` al cierre de `OnModelCreating`, permitiendo que Fase 2 incorpore entidades adicionales (solicitudes de renovación, incidencias, eventos, configuración de puntos de acceso) en archivos parciales separados sin modificar el núcleo de Fase 1.

---

## 4. Interfaces y Servicios Registrados en `AddUmadCore()`

Registrados en `UMADAccesos.Extensions.ServiceCollectionExtensions.AddUmadCore(this IServiceCollection services, string? databasePath = null)` (archivo SQLite por defecto: `Path.Combine(FileSystem.AppDataDirectory, "UmadLocal.db")`):

| Servicio / Interfaz | Implementación | Ciclo de vida | Contrato resumido listo para Fase 2 |
|---|---|---|---|
| `IDbContextFactory<UmadDbContext>` | EF Core SQLite (`Data Source=...`) | Singleton | `CreateDbContextAsync(CancellationToken)` |
| `IUmadDbInitializer` | `UmadDbInitializer` | Singleton | `bool EstaInicializada`, `DateTime? FechaInicializacionUtc`, `Task InitializeAsync(CancellationToken = default)` |
| `ISecureStorage` | `SecureStorage.Default` (`TryAddSingleton`) | Singleton | Almacén seguro de plataforma sobreescribible con mocks en pruebas unitarias |
| `IPasswordHasher` | `PasswordHasherService` | Singleton | `string HashPassword(string password)`<br>`bool VerifyPassword(string password, string passwordHash)` |
| `ITokenKeyProvider` | `SecureStorageTokenKeyProvider` | Singleton | `Task<byte[]> ObtenerClaveFirmaAsync(CancellationToken = default)` (clave HMAC de 256 bits bajo `"umad_accesos_hmac_key_v2"`, respaldo oculto controlado si `SecureStorage` no opera en entornos Windows unpackaged, sin exposición en logs ni UI) |
| `ITokenAccesoService` | `TokenAccesoService` | Scoped | `Task<TokenAcceso> EmitirTokenAsync(int personaId, TipoPase tipoPase, TimeSpan? vigencia = null, bool revocarPreviosActivos = true, CancellationToken = default)`<br>`Task<TokenValidationResult> ValidarTokenAsync(string codigoFirmado, CancellationToken = default)`<br>`Task<bool> RevocarTokenAsync(int tokenId, string motivoRevocacion, CancellationToken = default)`<br>`Task<IReadOnlyList<TokenAcceso>> ObtenerTokensActivosPorPersonaAsync(int personaId, CancellationToken = default)` |
| `ISyncQueueService` | `SyncQueueService` | Scoped | `Task<RegistroAcceso> EncolarRegistroAccesoAsync(RegistroAcceso registro, CancellationToken = default)`<br>`Task<IReadOnlyList<RegistroAcceso>> ObtenerPendientesAsync(int limite = 50, CancellationToken = default)`<br>`Task MarcarSincronizadoAsync(int registroAccesoId, CancellationToken = default)`<br>`Task RegistrarErrorSincronizacionAsync(int registroAccesoId, string mensajeError, CancellationToken = default)`<br>`Task<SyncQueueSummary> ObtenerResumenColaAsync(CancellationToken = default)`<br>`Task<SyncProcessResult> ProcesarColaPendienteAsync(Func<RegistroAcceso, CancellationToken, Task<bool>>? despachadorRemoto = null, int loteMaximo = 50, CancellationToken = default)` |

**DTOs auxiliares disponibles**:
- `TokenValidationResult`: `EsValido`, `Motivo`, `Token`, `PersonaId`, `TipoPase`, `FechaExpiracionUtc`, fábricas `Valido(TokenAcceso)` e `Invalido(string, TokenAcceso?)`.
- `SyncQueueSummary`: `Pendientes`, `Sincronizados`, `ConError`, `UltimoIntentoUtc`.
- `SyncProcessResult`: `Procesados`, `Exitosos`, `Fallidos`, `SinRed`, `Mensaje`.

---

## 5. Evidencia de Compilación y Resultados de Auditoría

### 5.1 Evidencia de `dotnet build` y Remediación de Seguridad NuGet (`NU1903`)
- **Comando ejecutado**:
  ```powershell
  dotnet build Acceso_UMAD_QRs/UMADAccesos/UMADAccesos.csproj -f net10.0-windows10.0.19041.0
  ```
- **Entorno**: Windows (`win-x64`), `.NET SDK 11.0.100`, target `net10.0-windows10.0.19041.0`.
- **Artefacto generado**: `Acceso_UMAD_QRs\UMADAccesos\bin\Debug\net10.0-windows10.0.19041.0\win-x64\UMADAccesos.dll`
- **Resultado**: **Compilación correcta (`0 Errores, 0 Advertencias`, tiempo `00:00:19.58`)**.
- **Remediación de `NU1903` (`GHSA-2m69-gcr7-jv3q` / `CVE-2025-6965`)**:
  - **Causa raíz**: `Microsoft.EntityFrameworkCore.Sqlite 10.0.7` arrastraba transitivamente `SQLitePCLRaw.bundle_e_sqlite3 2.1.11`, que incluía binarios nativos SQLite `< 3.50.2` (`SQLitePCLRaw.lib.e_sqlite3`, `.android` e `.ios` `2.1.11`), vulnerables a corrupción de memoria (`GHSA-2m69-gcr7-jv3q`) y marcados en desuso.
  - **Solución oficial aplicada (sin `<NoWarn>` ni `<NuGetAudit>false</NuGetAudit>`)**: actualización de los paquetes oficiales de servicio `.NET 10` en `Acceso_UMAD_QRs/UMADAccesos/UMADAccesos.csproj` de `10.0.7` a `10.0.15`:
    - `Microsoft.EntityFrameworkCore`: `10.0.15`
    - `Microsoft.EntityFrameworkCore.Sqlite`: `10.0.15`
    - `Microsoft.EntityFrameworkCore.Tools`: `10.0.15`
    - `Microsoft.Extensions.Logging.Debug`: `10.0.15`
  - **Dependencias transitivas SQLite resultantes (en los 4 TargetFrameworks)**:
    - `Microsoft.EntityFrameworkCore.Sqlite.Core` `10.0.15` y `Microsoft.Data.Sqlite.Core` `10.0.15`
    - `SQLitePCLRaw.bundle_e_sqlite3` `2.1.13`, `SQLitePCLRaw.core` `2.1.13`
    - `SQLitePCLRaw.lib.e_sqlite3` `2.1.13` (`net10.0-windows10.0.19041.0`, `net10.0-maccatalyst`), `SQLitePCLRaw.lib.e_sqlite3.android` `2.1.13` (`net10.0-android`) y `SQLitePCLRaw.lib.e_sqlite3.ios` `2.1.13` (`net10.0-ios`)
  - **Auditoría NuGet**:
    - `dotnet list Acceso_UMAD_QRs/UMADAccesos/UMADAccesos.csproj package --vulnerable --include-transitive` -> **0 paquetes vulnerables** en los 4 TargetFrameworks.
    - `dotnet list Acceso_UMAD_QRs/UMADAccesos/UMADAccesos.csproj package --deprecated --include-transitive` -> **0 paquetes en desuso** en los 4 TargetFrameworks.
  - **Verificación de SQLite en tiempo de ejecución (`win-x64`)**:
    - Binario nativo presente en `Acceso_UMAD_QRs\UMADAccesos\bin\Debug\net10.0-windows10.0.19041.0\win-x64\e_sqlite3.dll` (`1,978,880` bytes).
    - `SQLitePCL.Batteries_V2.Init()` (`2.1.13.3146`) y `Microsoft.Data.Sqlite.SqliteConnection` inicializaron y ejecutaron lectura/escritura correctamente sin `DllNotFoundException`, reportando `SELECT sqlite_version()` = **`3.53.3`** (parchado contra `CVE-2025-6965`, que afecta a `< 3.50.2`).

### 5.2 Resultado de auditoría anti-regresión v1.0 y seguridad
- **Búsqueda exhaustiva en `Acceso_UMAD_QRs/`**: `0` coincidencias de `IsTestMode`, `SimulateValidAccess`, `SimulateInvalidAccess`, `CurrentUser`, `Substring(0, 10)`, `MauiContext.Services`, `Shell.Current`, `UserModel`, `AccessTokenModel`, `AccessLogModel`, `RoleModel`, `EnsureCreated()` síncrono y archivos `.lscache`.
- **Sin contraseñas en claro**: todas las contraseñas en `Persona.PasswordHash` y la siembra de `UmadDbInitializer` pasan por `IPasswordHasher.HashPassword` (PBKDF2-SHA256 + `FixedTimeEquals` + `ZeroMemory`).
- **Sin exposición de secretos**: `SecureStorageTokenKeyProvider` nunca escribe la clave HMAC en logs, excepciones ni UI; `Home.razor` solo muestra conteos agregados y estado de inicialización/cola.
- **Sin dependencias cautivas**: todos los consumidores Singleton (`UmadDbInitializer`, `SecureStorageTokenKeyProvider`) inyectan únicamente servicios Singleton (`IDbContextFactory<UmadDbContext>`, `IPasswordHasher`, `ISecureStorage`, `ILogger`); ningún `UmadDbContext` Scoped es inyectado en Singleton.

### 5.3 Resultado de auditoría estricta de XML Doc
- **Archivos de Fase 1 auditados (`30` archivos `.cs` y `.razor` bajo `Acceso_UMAD_QRs/UMADAccesos/`, excluyendo plantillas nativas de `Platforms/` y `Resources/`)**:
  - **100% conformes**: todas las clases, interfaces, enums, constructores, métodos públicos y métodos dentro de `@code` cuentan con documentación XML en español completa (`<summary>`, `<author>Emmanuel Baltazar López</author>`, `<date>25/11/2026</date>`, `<version>1.0</version>`, `<modification>25/11/2026</modification>`).
  - **0 incumplimientos en propiedades, campos y constantes**: el barrido completo de `<summary>` confirmó que **ninguna** propiedad, campo ni constante utiliza `<summary>`; todos usan exclusivamente comentarios simples (`// ...`).
  - *Nota de transparencia*: los 8 archivos boilerplate de arranque nativo de plantilla MAUI heredados sin cambios desde `origin/develop` (`Platforms/Android/{MainActivity,MainApplication}.cs`, `Platforms/Windows/App.xaml.cs`, `Platforms/iOS/{AppDelegate,Program}.cs`, `Platforms/MacCatalyst/{AppDelegate,Program}.cs` y `Resources/Styles/ThemeColors.xaml.cs`) permanecieron intactos fuera del alcance de modificación de Fase 1.

### 5.4 Verificación de alcance (Fase 1)
- Cumplidos al 100% los entregables de Fase 1 (`Microsoft.NET.Sdk.Razor`, `ZXing.Net.Maui.Controls 0.7.4` + `.UseBarcodeReader()`, `AddMauiBlazorWebView()`, `AddUmadCore()`, `BlazorWebView` + `Routes.razor` + `MainLayout.razor` + `Home.razor` diagnóstico, entidades/índices SQLite, PBKDF2-SHA256, HMAC-SHA256 + `ISecureStorage`, cola offline-first).
- Sin invasión de Fases 2, 3 o 4 (sin escaneo QR activo ni modal de cámara, sin UI Fiori final, sin IA ni backend remoto).
