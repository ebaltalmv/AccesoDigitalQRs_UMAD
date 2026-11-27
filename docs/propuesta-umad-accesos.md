# Propuesta Ejecutiva — UMAD Accesos v2.0

**Institución:** Universidad Madero (UMAD) · **Autor:** Emmanuel Baltazar López · **Fecha:** 25/11/2026 · **Versión:** 2.0

---

## 1. Dominio y Plataforma Elegidos
**Control de acceso digital universitario UMAD** sobre **.NET MAUI Blazor Hybrid** y **.NET 10** (`Android`, `iOS`, `macOS`, `Windows`). Integra persistencia *offline-first* en **SQLite** con **Entity Framework Core 10** (`IDbContextFactory<UmadDbContext>`), contraseñas **PBKDF2-SHA256**, tokens QR firmados **HMAC-SHA256**, control RBAC (`UsuarioComunidad`, `GuardiaSeguridad`, `AdministradorAccesos`) e interfaz **SAP Fiori Horizon/Quartz** alineada a **ITIL 4**.

## 2. Problema Específico en la Universidad Madero (UMAD)
El control tradicional y la versión previa (v1.0) presentaban riesgos operativos críticos en campus:
- **Suplantación y credenciales vulnerables:** credenciales físicas/QR estáticos sin firma, expiración ni revocación (y contraseñas en texto plano en v1.0).
- **Caídas de red en casetas:** cortes de Wi-Fi/datos móviles detenían la validación y generaban filas en horas pico.
- **Trazabilidad insuficiente:** ausencia de gestión formal de incidencias ITIL, evidencia fotográfica verificable, renovaciones auditables, pases por evento y segregación estricta por roles.

## 3. Justificación de Plataforma (`.NET MAUI Blazor Hybrid` vs. Alternativas)
- **Por qué `.NET MAUI Blazor Hybrid`:** combina acceso C# directo al hardware nativo (`ZXing.Net.Maui.Controls`, `MediaPicker`, `ISecureStorage`) y SQLite transaccional sin conexión con una sola UI responsiva Razor/CSS Fiori para Windows y Android/iOS, compartiendo contratos C# con el backend institucional.
- **Por qué no SAP Build Apps / low-code:** introduce *vendor lock-in*, licenciamiento elevado y límites técnicos para criptografía local HMAC-SHA256 con `ISecureStorage` y colas SQLite transaccionales personalizadas.
- **Por qué no Web PWA ni MAUI XAML puro:** una **Web PWA** depende de WebRTC/IndexedDB con permisos inconsistentes de cámara móvil y sin `ISecureStorage` ni SQLite nativo confiable offline; **MAUI XAML puro** encarece el estilizado responsivo SAP Fiori frente a HTML/CSS estándar.

## 4. Entidades Principales y Relaciones (`UmadDbContext` + `UmadDbContext.Fase2`)
Todas las tablas operativas incluyen `IdentificadorGlobal` (`Guid` único) y columnas de sincronización (`Sincronizado`, `EstadoSincronizacion`, `Reintentos`, `FechaUltimoIntento`, `UltimoError`):

| Entidad | Claves y atributos clave | Relaciones (FK / borrado) |
| :--- | :--- | :--- |
| **`Persona`** | `Id` (PK), `Matricula` (UK), `Correo` (UK), `PasswordHash`, `Rol`, `Activo` | `1:N` con `TokenAcceso` y `RegistroAcceso` (`Restrict`). |
| **`TokenAcceso`** | `Id` (PK), `CodigoFirmado` (UK HMAC-SHA256), `TipoPase`, `FechaEmision`, `FechaExpiracion`, `Revocado`, `MotivoRevocacion` | `PersonaId` → `Persona` (`Restrict`). |
| **`RegistroAcceso`** | `Id` (PK), `PuntoAcceso`, `TipoMovimiento`, `Resultado`, `FechaHora`, `RutaEvidenciaFoto`, `HashEvidenciaFoto` | `PersonaId` → `Persona` (`Restrict`); `TokenAccesoId` → `TokenAcceso`, `GuardiaPersonaId` → `Persona`, `EntradaRelacionadaId` → `RegistroAcceso` (`SetNull`). |
| **`SolicitudRenovacion`** | `Id` (PK), `TipoPaseDeseado`, `VigenciaSolicitada`, `Justificacion`, `Estado`, `MotivoResolucion` | `PersonaId` → `Persona` (`Restrict`); `ResueltoPorPersonaId` → `Persona`, `TokenAccesoOriginalId` / `TokenAccesoNuevoId` → `TokenAcceso` (`SetNull`). |
| **`Incidencia`** | `Id` (PK), `Severidad` (ITIL 4), `Estado`, `Titulo`, `Descripcion`, `RutaEvidenciaFoto`, `HashEvidenciaFoto` | `ReportadoPorPersonaId` → `Persona` (`Restrict`); `RegistroAccesoId` → `RegistroAcceso` (`SetNull`). |
| **`PaseEvento`** | `Id` (PK), `NombreEvento`, `Descripcion`, `FechaInicioEvento`, `FechaFinEvento` | `PersonaId` y `CreadoPorPersonaId` → `Persona` (`Restrict`); `TokenAccesoId` → `TokenAcceso` (`SetNull`). |

## 5. Uso de Capacidades del Dispositivo en el Flujo Principal
- **Escáner QR (`IQrScannerService` / `QrScannerPage`):** cámara modal con `ZXing.Net.Maui.Controls` (`BarcodeFormat.QrCode`), permisos en `MainThread` y validación HMAC en caseta (respaldo manual en `#if WINDOWS`).
- **Evidencia fotográfica (`IEvidenciaFotoService`):** captura con `MediaPicker.Default.CapturePhotoAsync`, validación de formato y límite de 10 MB, almacenamiento seguro en `AppDataDirectory/evidencias/` y hash **SHA-256** persistido en `RegistroAcceso`/`Incidencia`.
- **Almacenamiento seguro y red (`ISecureStorage` + `Connectivity.Current`):** `SecureStorageTokenKeyProvider` resguarda la clave HMAC de 256 bits (`umad_accesos_hmac_key_v2`); `SyncQueueService` despacha lotes offline al recuperar red.

## 6. Componente de IA, Flujos Modificados y API/Modelo Configurado
`UMADAccesos.Services.AI` (`IIaService` / `IaService`) implementa un **patrón Estrategia dual** no bloqueante (`DegradarAOfflineEnFallo = true`):
- **Producción (`AzureAiProductionProvider` + `AzureAiHttpClient`):** consume vía HTTPS estricto un **BFF Proxy institucional** sin secretos ni PII en el cliente (`DeploymentName`: `umad-accesos-ia-prod`, timeout 10 s, `/api/v1/ai/classify-visit` y `/api/v1/ai/evaluate-anomaly`) respaldado por **Azure AI Language (Custom Classification + CLU `es-MX`)** y **Azure OpenAI** con **Managed Identity** y **Key Vault**.
- **Respaldo offline (`OfflineLocalIaProvider`):** clasificador NLP heurístico en español (6 categorías y 10 áreas UMAD) y motor determinista sobre SQLite (`AsNoTracking`).
- **Flujos modificados:** (1) **Renovaciones y solicitudes (`SolicitudPase` / `Admin`)**, clasificando motivo, área sugerida, confianza y aprobación especial; (2) **Caseta (`Caseta` / `RegistrarEntradaConEvaluacionIaAsync`)**, detectando sin bloquear el paso 4 anomalías (exceso diario `>4`, entrada abierta/*tailgating*, fuera de horario `06:30–22:00`, desviación horaria `>=4 h`, cambio de caseta `<5 min` y `>=2` denegados en 30 min) para orientar incidencias ITIL.
