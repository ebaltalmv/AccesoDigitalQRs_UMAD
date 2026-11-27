namespace UMADAccesos.Services.AI;

/// <summary>
/// Representa el resultado estructurado de clasificar el motivo de una visita en lenguaje natural.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public sealed class ClasificacionVisitaResult
{
    // Categoría de visita sugerida por el clasificador.
    public TipoVisitaSugerido TipoVisitaSugerido { get; init; } = TipoVisitaSugerido.Otro;

    // Área institucional UMAD recomendada como destino principal de la visita.
    public string AreaDestinoSugerida { get; init; } = "Recepción General";

    // Nivel de confianza normalizado entre 0.0 y 1.0 asignado a la clasificación.
    public double NivelConfianza { get; init; }

    // Indica si el ingreso requiere validación o autorización adicional en caseta.
    public bool RequiereAprobacionEspecial { get; init; }

    // Explicación legible en español de los criterios que sustentan la sugerencia.
    public string Justificacion { get; init; } = string.Empty;

    // Identificador del proveedor de IA que generó el resultado.
    public string ProveedorUtilizado { get; init; } = string.Empty;

    // Lista de términos o frases clave relevantes detectadas en el texto analizado.
    public IReadOnlyList<string> PalabrasClaveDetectadas { get; init; } = Array.Empty<string>();
}
