namespace UMADAccesos.Services.Auth;

/// <summary>
/// Enumera los permisos granulares del sistema asignados por rol bajo el principio de mínimo privilegio.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public enum PermisoSistema
{
    // Permite consultar únicamente los pases y tokens de acceso propios del usuario autenticado.
    ConsultarPropiosPases = 1,

    // Permite solicitar un pase temporal o la renovación de un pase propio.
    SolicitarPaseORenovacion = 2,

    // Permite consultar exclusivamente el historial propio de entradas y salidas.
    VerPropioHistorial = 3,

    // Permite clasificar el motivo de una visita asistido por el módulo local de inteligencia artificial.
    ClasificarMotivoVisitaIa = 4,

    // Permite escanear y validar códigos QR en la caseta de acceso.
    EscanearCodigoQr = 10,

    // Permite capturar evidencia fotográfica asociada a un evento de acceso o incidencia en caseta.
    CapturarEvidenciaFoto = 11,

    // Permite registrar movimientos persistentes de entrada y salida en caseta.
    RegistrarEntradaSalida = 12,

    // Permite visualizar alertas de anomalías detectadas durante la validación de accesos.
    VerAlertasAnomalia = 13,

    // Permite reportar incidencias de seguridad ocurridas en el punto de acceso.
    ReportarIncidencia = 14,

    // Permite aprobar o rechazar solicitudes de renovación de pases de acceso.
    AprobarRechazarRenovaciones = 20,

    // Permite crear, consultar y administrar pases especiales por evento.
    GestionarPasesEvento = 21,

    // Permite revocar tokens de acceso activos ante pérdidas, robos o baja institucional.
    RevocarTokens = 22,

    // Permite consultar el historial global de accesos de toda la institución.
    ConsultarHistorialGlobal = 23,

    // Permite dar seguimiento, actualizar estado y resolver incidencias reportadas.
    GestionarIncidencias = 24,

    // Permite supervisar y gestionar el estado de la cola de sincronización offline.
    SupervisarSincronizacionOffline = 25
}
