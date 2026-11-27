namespace UMADAccesos.Services.AI;

/// <summary>
/// Categoriza el tipo de visita sugerido a partir del motivo declarado en lenguaje natural.
/// </summary>
/// <author>Emmanuel Baltazar López</author>
/// <date>25/11/2026</date>
/// <version>1.0</version>
/// <modification>25/11/2026</modification>
public enum TipoVisitaSugerido
{
    // Visita relacionada con docencia, asesorías, tesis, investigación o actividades académicas.
    Academico = 1,

    // Trámite escolar, financiero, documental o gestión ante áreas administrativas.
    Administrativo = 2,

    // Entrega de insumos, paquetería, suministros o servicios de proveedores externos.
    ProveedorEntrega = 3,

    // Asistencia a congresos, conferencias, ceremonias, talleres o eventos institucionales.
    Evento = 4,

    // Trabajos técnicos, reparación, inspección de infraestructura o mantenimiento de instalaciones.
    Mantenimiento = 5,

    // Motivo no clasificable con certeza o fuera de las categorías institucionales estándar.
    Otro = 6
}
