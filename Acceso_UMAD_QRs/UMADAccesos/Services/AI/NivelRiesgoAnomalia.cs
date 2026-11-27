namespace UMADAccesos.Services.AI;

/// <summary>
/// Clasifica la severidad del riesgo detectado durante la evaluación de anomalías de acceso.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public enum NivelRiesgoAnomalia
{
    // Comportamiento acorde al perfil esperado sin señales de alerta.
    Bajo = 0,

    // Desviación moderada que amerita verificación adicional por parte del guardia.
    Medio = 1,

    // Indicador crítico o múltiple de anomalía que requiere protocolo reforzado de seguridad.
    Alto = 2
}
