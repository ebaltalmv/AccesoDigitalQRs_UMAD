# Propuesta Técnica Ejecutiva — UMAD Accesos v2.0

## 1. Dominio y Plataforma Elegida
**UMAD Accesos v2.0** moderniza el control de acceso universitario en la **Universidad Madero (UMAD)** mediante una aplicación multiplataforma en **.NET MAUI Blazor Hybrid** sobre **.NET 10** (`Android`, `iOS`, `macOS`, `Windows`). Integra interfaz **SAP Fiori** en `BlazorWebView` (`MainPage.xaml`), persistencia *offline-first* sobre SQLite (`UmadLocal.db`), control RBAC y gestión alineada a **ITIL 4**.

## 2. Problema Específico en la Universidad Madero (v1.0)
La auditoría a v1.0 reveló defectos críticos para operación institucional:
- **Credenciales y QR vulnerables**: contraseñas en texto plano (`UserModel.Password`) y códigos QR estáticos de 10 hex sin firma, expiración acotada ni revocación.
- **Controles rotos**: Modo de prueba abierto en producción (`IsTestMode`), escalada de privilegios y estado global estático (`App.CurrentUser`).
- **Deuda técnica y operativa**: *captive dependency* (`UmadDbContext` Scoped en Singleton), sin cola *offline-first* ni trazabilidad ITIL 4.

## 3. Justificación de .NET MAUI Blazor Hybrid
- **Vs. Web PWA**: carece de acceso nativo confiable a cámara, `ISecureStorage` y SQLite transaccional sin conexión en caseta.
- **Vs. Flutter / React Native**: exigirían reescribir en Dart o JS la lógica C#, criptografía y EF Core, impidiendo compartir código con un BFF ASP.NET Core.
- **Vs. Nativo (Kotlin/Swift)**: duplicaría mantenimiento móvil sin cubrir escritorio. **Blazor Hybrid** unifica UI Fiori en Razor/CSS con acceso C# nativo a hardware y `IDbContextFactory<UmadDbContext>`.

## 4. Entidades Principales y Relaciones
Modeladas en `UmadDbContext` y `UmadDbContext.Fase2.cs` con `Guid` y `DeleteBehavior.Restrict`/`SetNull` para sincronización idempotente:
- **Núcleo**: `Persona` (PK `int`, `Guid`, `PasswordHash` PBKDF2-SHA256) se vincula `1:N` con `TokenAcceso` (QR **HMAC-SHA256**, FK `PersonaId`) y `RegistroAcceso` (entrada/salida, evidencia, cola *offline*; FKs `PersonaId`, `TokenAccesoId`, `GuardiaPersonaId`, `EntradaRelacionadaId`).
- **Operación ITIL**: `SolicitudRenovacion`, `PaseEvento` e `Incidencia` (FKs hacia `Persona`, `TokenAcceso` y `RegistroAcceso`), con `EstadoSincronizacion` y reintentos.

## 5. Capacidades Nativas del Dispositivo
`UMADAccesos.Services.Device` desacopla el hardware de Razor en caseta:
- **Escáner QR (`IQrScannerService` / `QrScannerPage`)**: modal nativo con `ZXing.Net.Maui.Controls` (`BarcodeFormat.QrCode`), permisos en `MainThread` y *fallback* manual en `#if WINDOWS`.
- **Evidencia fotográfica (`IEvidenciaFotoService`)**: captura vía `MediaPicker`, valida formato y límite (10 MB), guarda en `AppDataDirectory/evidencias/` y registra el hash **SHA-256** en `RegistroAcceso`.

## 6. Componente de IA y Propuesta Azure
`UMADAccesos.Services.AI` asiste dos flujos sin bloquear el paso físico:
- **Clasificación NLP (`SolicitudPase`)**: `IIaService.ClasificarMotivoVisitaAsync` pre-llena `TipoVisitaSugerido`, `AreaDestinoSugerida`, `NivelConfianza` y `RequiereAprobacionEspecial` desde lenguaje natural.
- **Anomalías no bloqueantes (`CasetaVigilancia` / `DetalleAcceso`)**: `RegistrarEntradaConEvaluacionIaAsync` persiste la entrada y evalúa exceso diario (`>4`), *tailgating*, horario (`06:30–22:00`), desviación y reintentos denegados (`>=2` en 30 min), sugiriendo incidencias ITIL.
- **Producción Azure**: `IaService` consume un **BFF HTTPS** tras **Azure API Management**, **Managed Identity** y **Key Vault** hacia **Azure AI Language** y **Azure OpenAI** (cero secretos/PII en cliente), degradando a `OfflineLocalIaProvider`.
