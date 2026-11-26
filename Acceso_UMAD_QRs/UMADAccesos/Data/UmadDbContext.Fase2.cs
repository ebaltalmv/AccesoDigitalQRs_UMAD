using Microsoft.EntityFrameworkCore;
using UMADAccesos.Models;

namespace UMADAccesos.Data
{
    /// <summary>
    /// Extensión parcial de <see cref="UmadDbContext"/> para las entidades operativas de Fase 2 (renovaciones, incidencias, eventos y extensiones de acceso).
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public partial class UmadDbContext
    {
        // Conjunto de solicitudes de renovación de pases digitales.
        public DbSet<SolicitudRenovacion> SolicitudesRenovacion => Set<SolicitudRenovacion>();

        // Conjunto de incidencias operativas y de seguridad reportadas en caseta.
        public DbSet<Incidencia> Incidencias => Set<Incidencia>();

        // Conjunto de pases temporales emitidos para eventos institucionales.
        public DbSet<PaseEvento> PasesEvento => Set<PaseEvento>();

        /// <summary>
        /// Configura el esquema SQLite de las entidades de Fase 2 y las columnas extendidas de <see cref="RegistroAcceso"/>.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        partial void OnModelCreatingExtensible(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RegistroAcceso>(entity =>
            {
                entity.Property(r => r.RutaEvidenciaFoto)
                    .HasMaxLength(500);

                entity.Property(r => r.HashEvidenciaFoto)
                    .HasMaxLength(128);

                entity.Property(r => r.GuardiaPersonaId);

                entity.Property(r => r.EntradaRelacionadaId);

                entity.HasIndex(r => r.GuardiaPersonaId)
                    .HasDatabaseName("IX_RegistrosAcceso_GuardiaPersonaId");

                entity.HasIndex(r => r.EntradaRelacionadaId)
                    .HasDatabaseName("IX_RegistrosAcceso_EntradaRelacionadaId");

                entity.HasOne(r => r.Guardia)
                    .WithMany()
                    .HasForeignKey(r => r.GuardiaPersonaId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(r => r.EntradaRelacionada)
                    .WithMany()
                    .HasForeignKey(r => r.EntradaRelacionadaId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SolicitudRenovacion>(entity =>
            {
                entity.ToTable("SolicitudesRenovacion");
                entity.HasKey(s => s.Id);

                entity.Property(s => s.IdentificadorGlobal)
                    .IsRequired();

                entity.Property(s => s.PersonaId)
                    .IsRequired();

                entity.Property(s => s.TokenAccesoOriginalId);

                entity.Property(s => s.TipoPaseDeseado)
                    .IsRequired()
                    .HasConversion<int>();

                entity.Property(s => s.VigenciaSolicitada);

                entity.Property(s => s.Justificacion)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(s => s.Estado)
                    .IsRequired()
                    .HasConversion<int>()
                    .HasDefaultValue(EstadoRenovacion.Pendiente);

                entity.Property(s => s.MotivoResolucion)
                    .HasMaxLength(500);

                entity.Property(s => s.ResueltoPorPersonaId);

                entity.Property(s => s.TokenAccesoNuevoId);

                entity.Property(s => s.FechaSolicitudUtc)
                    .IsRequired();

                entity.Property(s => s.FechaResolucionUtc);

                entity.Property(s => s.Sincronizado)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(s => s.EstadoSincronizacion)
                    .IsRequired()
                    .HasConversion<int>()
                    .HasDefaultValue(EstadoSincronizacion.Pendiente);

                entity.Property(s => s.FechaUltimoIntento);

                entity.Property(s => s.Reintentos)
                    .IsRequired()
                    .HasDefaultValue(0);

                entity.Property(s => s.UltimoError)
                    .HasMaxLength(500);

                entity.HasIndex(s => s.IdentificadorGlobal)
                    .IsUnique()
                    .HasDatabaseName("IX_SolicitudesRenovacion_IdentificadorGlobal");

                entity.HasIndex(s => s.Sincronizado)
                    .HasDatabaseName("IX_SolicitudesRenovacion_Sincronizado");

                entity.HasIndex(s => s.EstadoSincronizacion)
                    .HasDatabaseName("IX_SolicitudesRenovacion_EstadoSincronizacion");

                entity.HasIndex(s => s.PersonaId)
                    .HasDatabaseName("IX_SolicitudesRenovacion_PersonaId");

                entity.HasIndex(s => s.Estado)
                    .HasDatabaseName("IX_SolicitudesRenovacion_Estado");

                entity.HasIndex(s => s.TokenAccesoOriginalId)
                    .HasDatabaseName("IX_SolicitudesRenovacion_TokenAccesoOriginalId");

                entity.HasIndex(s => s.ResueltoPorPersonaId)
                    .HasDatabaseName("IX_SolicitudesRenovacion_ResueltoPorPersonaId");

                entity.HasIndex(s => s.TokenAccesoNuevoId)
                    .HasDatabaseName("IX_SolicitudesRenovacion_TokenAccesoNuevoId");

                entity.HasOne(s => s.Persona)
                    .WithMany()
                    .HasForeignKey(s => s.PersonaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.ResueltoPor)
                    .WithMany()
                    .HasForeignKey(s => s.ResueltoPorPersonaId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(s => s.TokenAccesoOriginal)
                    .WithMany()
                    .HasForeignKey(s => s.TokenAccesoOriginalId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(s => s.TokenAccesoNuevo)
                    .WithMany()
                    .HasForeignKey(s => s.TokenAccesoNuevoId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Incidencia>(entity =>
            {
                entity.ToTable("Incidencias");
                entity.HasKey(i => i.Id);

                entity.Property(i => i.IdentificadorGlobal)
                    .IsRequired();

                entity.Property(i => i.ReportadoPorPersonaId)
                    .IsRequired();

                entity.Property(i => i.RegistroAccesoId);

                entity.Property(i => i.Severidad)
                    .IsRequired()
                    .HasConversion<int>();

                entity.Property(i => i.Estado)
                    .IsRequired()
                    .HasConversion<int>()
                    .HasDefaultValue(EstadoIncidencia.Abierta);

                entity.Property(i => i.Titulo)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(i => i.Descripcion)
                    .IsRequired()
                    .HasMaxLength(2000);

                entity.Property(i => i.RutaEvidenciaFoto)
                    .HasMaxLength(500);

                entity.Property(i => i.HashEvidenciaFoto)
                    .HasMaxLength(128);

                entity.Property(i => i.FechaReporteUtc)
                    .IsRequired();

                entity.Property(i => i.FechaResolucionUtc);

                entity.Property(i => i.Sincronizado)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(i => i.EstadoSincronizacion)
                    .IsRequired()
                    .HasConversion<int>()
                    .HasDefaultValue(EstadoSincronizacion.Pendiente);

                entity.Property(i => i.FechaUltimoIntento);

                entity.Property(i => i.Reintentos)
                    .IsRequired()
                    .HasDefaultValue(0);

                entity.Property(i => i.UltimoError)
                    .HasMaxLength(500);

                entity.HasIndex(i => i.IdentificadorGlobal)
                    .IsUnique()
                    .HasDatabaseName("IX_Incidencias_IdentificadorGlobal");

                entity.HasIndex(i => i.Sincronizado)
                    .HasDatabaseName("IX_Incidencias_Sincronizado");

                entity.HasIndex(i => i.EstadoSincronizacion)
                    .HasDatabaseName("IX_Incidencias_EstadoSincronizacion");

                entity.HasIndex(i => i.ReportadoPorPersonaId)
                    .HasDatabaseName("IX_Incidencias_ReportadoPorPersonaId");

                entity.HasIndex(i => i.RegistroAccesoId)
                    .HasDatabaseName("IX_Incidencias_RegistroAccesoId");

                entity.HasIndex(i => i.Estado)
                    .HasDatabaseName("IX_Incidencias_Estado");

                entity.HasIndex(i => i.Severidad)
                    .HasDatabaseName("IX_Incidencias_Severidad");

                entity.HasOne(i => i.ReportadoPor)
                    .WithMany()
                    .HasForeignKey(i => i.ReportadoPorPersonaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.RegistroAcceso)
                    .WithMany()
                    .HasForeignKey(i => i.RegistroAccesoId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PaseEvento>(entity =>
            {
                entity.ToTable("PasesEvento");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.IdentificadorGlobal)
                    .IsRequired();

                entity.Property(p => p.NombreEvento)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(p => p.Descripcion)
                    .HasMaxLength(500);

                entity.Property(p => p.FechaInicioEvento)
                    .IsRequired();

                entity.Property(p => p.FechaFinEvento)
                    .IsRequired();

                entity.Property(p => p.PersonaId)
                    .IsRequired();

                entity.Property(p => p.TokenAccesoId);

                entity.Property(p => p.CreadoPorPersonaId)
                    .IsRequired();

                entity.Property(p => p.FechaCreacionUtc)
                    .IsRequired();

                entity.Property(p => p.Sincronizado)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(p => p.EstadoSincronizacion)
                    .IsRequired()
                    .HasConversion<int>()
                    .HasDefaultValue(EstadoSincronizacion.Pendiente);

                entity.Property(p => p.FechaUltimoIntento);

                entity.Property(p => p.Reintentos)
                    .IsRequired()
                    .HasDefaultValue(0);

                entity.Property(p => p.UltimoError)
                    .HasMaxLength(500);

                entity.HasIndex(p => p.IdentificadorGlobal)
                    .IsUnique()
                    .HasDatabaseName("IX_PasesEvento_IdentificadorGlobal");

                entity.HasIndex(p => p.Sincronizado)
                    .HasDatabaseName("IX_PasesEvento_Sincronizado");

                entity.HasIndex(p => p.EstadoSincronizacion)
                    .HasDatabaseName("IX_PasesEvento_EstadoSincronizacion");

                entity.HasIndex(p => p.PersonaId)
                    .HasDatabaseName("IX_PasesEvento_PersonaId");

                entity.HasIndex(p => p.CreadoPorPersonaId)
                    .HasDatabaseName("IX_PasesEvento_CreadoPorPersonaId");

                entity.HasIndex(p => p.TokenAccesoId)
                    .HasDatabaseName("IX_PasesEvento_TokenAccesoId");

                entity.HasIndex(p => new { p.FechaInicioEvento, p.FechaFinEvento })
                    .HasDatabaseName("IX_PasesEvento_VentanaVigencia");

                entity.HasOne(p => p.Persona)
                    .WithMany()
                    .HasForeignKey(p => p.PersonaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.CreadoPor)
                    .WithMany()
                    .HasForeignKey(p => p.CreadoPorPersonaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.TokenAcceso)
                    .WithMany()
                    .HasForeignKey(p => p.TokenAccesoId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
