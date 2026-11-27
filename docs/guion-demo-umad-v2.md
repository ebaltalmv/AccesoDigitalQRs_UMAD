# Guion de Video Demo — UMAD Accesos v2.0 (Sin Diapositivas)

- **Duración total**: `04:45` (dentro del rango obligatorio de 3 a 5 minutos)
- **Formato**: Grabación continua de pantalla directa sobre la aplicación real en compilación `DEBUG` (sin diapositivas ni edición externa)
- **Requisitos previos antes de grabar**:
  - Ejecutar la app `UMADAccesos` en configuración `DEBUG` (para habilitar el selector controlado de entorno de prueba vía `IEntornoEjecucionService` y la siembra segura en `UmadDbInitializer`).
  - Tener a la mano las credenciales semilla de desarrollo (hasheadas con PBKDF2-SHA256 en SQLite):
    - Comunidad UMAD: `estudiante@umad.edu.mx` / `UmadEstudiante#2026!`
    - Guardia de Caseta: `guardia@umad.edu.mx` / `UmadGuardia#2026!`
    - Administrador ITIL: `admin@umad.edu.mx` / `UmadAdmin#2026!`
    - Caso anómalo semilla: `visitante.demo@umad.edu.mx`
  - Nota de captura en escritorio Windows: `QrScannerService` devuelve el *fallback* controlado en `#if WINDOWS` para ingresar o pegar el token firmado HMAC-SHA256 directamente en caseta; en móvil Android/iOS abre `QrScannerPage` modal con `ZXing.Net.Maui.Controls`.

---

## Tabla Resumen de Bloques

| Bloque | Cronometraje | Duración | Pantalla / Ruta / Componente | Objetivo clave |
|---|---|---|---|---|
| **1. Contexto en la app real** | `0:00–0:30` | `0:30` | `MainLayout.razor` / `Home.razor` / `Login.razor` | Mostrar arquitectura Blazor Hybrid, estética SAP Fiori, indicadores RBAC, cola *offline* y doble blindaje del Modo de prueba (`#if DEBUG`). |
| **2. Flujo Comunidad UMAD** | `0:30–1:45` | `1:15` | `Login.razor` -> `SolicitudPase.razor` | Autenticación PBKDF2, QR dinámico HMAC-SHA256, `EditForm` + NLP (`IIaService`) y solicitud persistente en SQLite. |
| **3. Flujo Guardia de Caseta** | `1:45–3:05` | `1:20` | `CasetaVigilancia.razor` -> `HistorialAccesos.razor` -> `DetalleAcceso.razor` (`/accesos/{Id}`) | Escaneo QR, validación HMAC, foto SHA-256, evaluación IA no bloqueante (`visitante.demo@umad.edu.mx`, riesgo `Alto`), sugerencia ITIL y detalle `/accesos/{Id}`. |
| **4. Flujo Administrador / ITIL 4** | `3:05–3:45` | `0:40` | `PanelAdmin.razor` | Aprobación con rotación de token, pase de evento, revocación, cierre de incidencia ITIL y cola *offline*. |
| **5. Reflexión técnica final** | `3:45–4:45` | `1:00` | Vista general de la app | Comparativa técnica rigurosa: Flutter vs. React Native vs. Nativo puro (Kotlin/Swift) frente a Blazor Hybrid. |

---

## Desarrollo Detallado por Bloque

### Bloque 1 — Contexto en la App Real (`0:00–0:30`)

- **Acciones exactas en pantalla**:
  1. (`0:00`) Iniciar con la aplicación abierta mostrando el shell principal (`MainLayout.razor` dentro de `<BlazorWebView>` en `MainPage.xaml`) con estética SAP Fiori.
  2. (`0:10`) Señalar con el cursor el encabezado/barra superior: indicador de entorno, insignia de rol activo (`ISesionActualService` / matriz RBAC de 15 permisos disjuntos en `AutorizacionRolService`) y resumen de la cola de sincronización *offline* (`ISyncQueueService` / `SyncQueueSummary` sobre `UmadLocal.db`).
  3. (`0:20`) Señalar el aviso controlado de **Modo de prueba** visible únicamente porque `EntornoEjecucionService.EsModoPruebaHabilitado` es `true` en `#if DEBUG` (en `Release` ni siquiera compila el método de cambio rápido).

- **Guion hablado (~65 palabras)**:
  > "Hola, presento **UMAD Accesos v2.0**, construido en **.NET MAUI Blazor Hybrid** sobre **.NET 10** con estética **SAP Fiori** y persistencia *offline-first* en SQLite. A diferencia de la v1.0, aquí eliminamos secretos en texto plano y estados globales inseguros: el sistema aplica RBAC estricto con 15 permisos disjuntos, monitorea la cola de sincronización *offline* en vivo y acota el Modo de prueba exclusivamente a compilaciones `DEBUG` mediante doble blindaje."

---

### Bloque 2 — Flujo Comunidad UMAD (`0:30–1:45`)

- **Acciones exactas en pantalla**:
  1. (`0:30`) En `Login.razor`, iniciar sesión con `estudiante@umad.edu.mx` y contraseña `UmadEstudiante#2026!` (validada por `PasswordHasherService` con PBKDF2-SHA256).
  2. (`0:40`) Mostrar el pase QR dinámico del estudiante emitido por `ITokenAccesoService` (`TokenAccesoService`): destacar que ya no es un código estático de 10 hex, sino un payload `v2|...` firmado con **HMAC-SHA256** cuya clave de 256 bits reside en `ISecureStorage` (`SecureStorageTokenKeyProvider`).
  3. (`0:55`) Navegar a `SolicitudPase.razor`. Intentar enviar vacío brevemente para mostrar la validación de `EditForm` (`DataAnnotationsValidator`).
  4. (`1:05`) Escribir en el campo de motivo en lenguaje natural: `"Vengo a entregar papelería al área de rectoría"` y disparar la clasificación con `IIaService.ClasificarMotivoVisitaAsync`.
  5. (`1:20`) Mostrar en pantalla cómo la IA pre-llena `TipoVisitaSugerido` (`ProveedorEntrega`), `AreaDestinoSugerida` (`Rectoría`), `NivelConfianza` (`0.98`), el distintivo `RequiereAprobacionEspecial` y permite editar el campo de justificación.
  6. (`1:35`) Enviar la solicitud (`IRenovacionService.SolicitarRenovacionAsync`) y confirmar que queda persistida en SQLite (`SolicitudesRenovacion`, estado `Pendiente`).

- **Guion hablado (~165 palabras)**:
  > "Comenzamos como **Comunidad UMAD** en `Login.razor`. Al autenticar a `estudiante@umad.edu.mx`, `PasswordHasherService` verifica el hash PBKDF2-SHA256 sin tocar texto plano. Su pase digital ahora es un token **HMAC-SHA256** firmado con una clave de 256 bits en `ISecureStorage`, con vigencia acotada y revocable.
  >
  > Pasamos a `SolicitudPase.razor`, implementado con `EditForm` y validaciones declarativas. Al redactar un motivo en lenguaje natural —por ejemplo, *'Vengo a entregar papelería al área de rectoría'*—, `IIaService` lo clasifica localmente sin latencia de red: sugiere la categoría `ProveedorEntrega`, el área destino `Rectoría`, un nivel de confianza de `0.98` y marca aprobación especial. El usuario puede ajustar la justificación antes de guardar; al enviar, `IRenovacionService` persiste la solicitud en SQLite en estado `Pendiente` para revisión administrativa."

---

### Bloque 3 — Flujo Guardia de Caseta (`1:45–3:05`)

- **Acciones exactas en pantalla**:
  1. (`1:45`) Cerrar sesión o usar el selector controlado `DEBUG` para ingresar como `guardia@umad.edu.mx` (`RolUsuario.GuardiaSeguridad`) y abrir `CasetaVigilancia.razor`.
  2. (`1:55`) Presionar el botón de escaneo QR (`IQrScannerService.EscanearCodigoQrAsync`). Mostrar el comportamiento nativo (`QrScannerPage` con `ZXing.Net.Maui.Controls` en móvil o *fallback* manual controlado en Windows) e ingresar/seleccionar el token válido del caso semilla `visitante.demo@umad.edu.mx`, verificando la firma **HMAC-SHA256** (`ITokenAccesoService.ValidarTokenAsync`).
  3. (`2:15`) Pulsar el botón de evidencia fotográfica (`IEvidenciaFotoService.CapturarEvidenciaAsync` sobre `MediaPicker`), mostrando la ruta relativa generada en `AppDataDirectory/evidencias/` y su huella **SHA-256**.
  4. (`2:25`) Ejecutar el registro de entrada (`IRegistroAccesoService.RegistrarEntradaConEvaluacionIaAsync`). Mostrar que la entrada se persiste inmediatamente sin bloquearse, y señalar la tarjeta de alerta IA en `NivelRiesgoAnomalia.Alto`: exceso diario (`>4`), entrada abierta previa (*tailgating*), acceso fuera de horario/desviación de patrón y reintentos denegados recientes, junto con `RecomendacionGuardia` y el botón rápido para levantar incidencia (`SugerirLevantarIncidencia = true`, `SeveridadIncidencia.Alta` -> `IIncidenciaService.ReportarIncidenciaAsync`).
  5. (`2:50`) Levantar la incidencia desde la alerta, navegar a `HistorialAccesos.razor` y hacer clic en el registro recién creado para abrir `DetalleAcceso.razor` (`/accesos/{Id}`), mostrando la foto de evidencia, su hash SHA-256, el desglose completo de reglas IA y el estado *offline-first* (`EstadoSincronizacion.Pendiente`, `IdentificadorGlobal` `Guid`).

- **Guion hablado (~175 palabras)**:
  > "Cambiamos al perfil **Guardia de Caseta** en `CasetaVigilancia.razor`. Aquí integramos capacidades nativas del dispositivo: `IQrScannerService` abre el escáner modal `ZXing` en móvil o el *fallback* manual controlado en Windows, validando la firma **HMAC-SHA256** sin internet. Además, `IEvidenciaFotoService` captura fotografía mediante `MediaPicker`, la resguarda en `AppDataDirectory/evidencias/` y calcula su hash **SHA-256** para integridad forense.
  >
  > Al registrar la entrada de `visitante.demo@umad.edu.mx` mediante `RegistrarEntradaConEvaluacionIaAsync`, el acceso se guarda primero en la cola *offline* y la IA evalúa en paralelo sin detener la fila: detecta riesgo **Alto** por exceso diario, *tailgating*, desviación de horario y reintentos denegados recientes, y sugiere levantar una incidencia ITIL de severidad `Alta`.
  >
  > Generamos la incidencia con un clic y navegamos de `HistorialAccesos.razor` hacia `DetalleAcceso.razor` en `/accesos/{Id}`, donde auditamos la evidencia fotográfica, la explicación IA y el estado `Pendiente` de sincronización con su `Guid` idempotente."

---

### Bloque 4 — Flujo Administrador / ITIL 4 (`3:05–3:45`)

- **Acciones exactas en pantalla**:
  1. (`3:05`) Cambiar al perfil `admin@umad.edu.mx` (`RolUsuario.AdministradorAccesos`) y abrir `PanelAdmin.razor`.
  2. (`3:12`) En la pestaña/sección de renovaciones, aprobar la solicitud del estudiante (`IRenovacionService.AprobarRenovacionAsync`), señalando la rotación automática de token (`revocarPreviosActivos: true`).
  3. (`3:22`) Crear un pase por evento con ventana horaria acotada (`IPaseEventoService.CrearPaseEventoAsync` -> `EmitirTokenVentanaAsync`) y mostrar la acción de revocación manual de un token (`ITokenAccesoService.RevocarTokenAsync` con motivo de auditoría).
  4. (`3:33`) Pasar a incidencias ITIL, actualizar a `EstadoIncidencia.Resuelta` / `Cerrada` la incidencia reportada por el guardia (`IIncidenciaService.ActualizarEstadoAsync`), y mostrar finalmente el monitor de la cola de sincronización *offline* (`ISyncQueueService.ObtenerResumenColaAsync` y `ProcesarColaPendienteAsync` con los contadores de `SyncQueueSummary`).

- **Guion hablado (~90 palabras)**:
  > "Finalmente, en `PanelAdmin.razor`, el **Administrador** ejecuta el gobierno **ITIL 4**. Aprobamos la renovación pendiente rotando automáticamente el token anterior para evitar credenciales duplicadas; emitimos un pase por evento acotado criptográficamente a su ventana horaria; y demostramos la revocación inmediata de un token comprometido con motivo de auditoría. Cerramos además la incidencia abierta por el guardia registrando su fecha de resolución, y supervisamos en `ISyncQueueService` el resumen completo de accesos, pases, renovaciones e incidencias listas para sincronizarse en cuanto exista conectividad."

---

### Bloque 5 — Reflexión Final Obligatoria (`3:45–4:45`)

- **Acciones exactas en pantalla**:
  1. (`3:45–4:45`) Mantener visible el tablero principal / `PanelAdmin.razor` de la aplicación mientras se expone la comparativa técnica de 1 minuto (sin diapositivas).

- **Guion hablado (~140 palabras — ritmo pausado, 60 segundos exactos)**:
  > "Para cerrar, si implementáramos **UMAD Accesos v2.0** en otras plataformas, cada opción tendría compensaciones claras:
  >
  > En **Flutter**, sería más fácil lograr una interfaz visualmente idéntica y animaciones fluidas sin depender de un `WebView`. Sin embargo, sería más difícil reescribir desde cero en Dart toda nuestra criptografía HMAC-SHA256, el motor de reglas IA *offline* y la capa relacional, ya que carece de un ORM tipado tan maduro como **Entity Framework Core** y no comparte contratos con un backend .NET.
  >
  > En **React Native**, sería más rápido aprovechar librerías web de JavaScript, pero más complejo mantener puentes nativos estables para cámara `ZXing`, hashing binario de imágenes y almacenamiento seguro ante actualizaciones de Android e iOS.
  >
  > En **nativo puro (Kotlin y Swift)**, el acceso directo a CameraX, AVFoundation y Keystore/Keychain sería óptimo, pero duplicaríamos toda la lógica de negocio, SQLite y pruebas en dos bases de código, además de perder soporte nativo en Windows y macOS. Por ello, **.NET MAUI Blazor Hybrid** resultó la arquitectura más equilibrada."
