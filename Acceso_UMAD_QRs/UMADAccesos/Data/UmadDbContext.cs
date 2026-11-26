using Microsoft.EntityFrameworkCore;
using UMADAccesos.Models;

namespace UMADAccesos.Data
{
    /// <summary>
    /// Contexto principal de Entity Framework Core para la persistencia local SQLite de UMAD Accesos v2.0.
    /// </summary>
    /// <author>Emmanuel Baltazar López</author>
    /// <date>25/11/2026</date>
    /// <version>1.0</version>
    /// <modification>25/11/2026</modification>
    public partial class UmadDbContext : DbContext
    {
        // Conjunto de personas registradas en el sistema.
        public DbSet<Persona> Personas => Set<Persona>();

        // Conjunto de tokens criptográficos de acceso emitidos.
        public DbSet<TokenAcceso> TokensAcceso => Set<TokenAcceso>();

        // Conjunto de eventos de entrada y salida capturados en caseta.
        public DbSet<RegistroAcceso> RegistrosAcceso => Set<RegistroAcceso>();

        /// <summary>
        /// Inicializa una nueva instancia del contexto de base de datos con las opciones configuradas.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        public UmadDbContext(DbContextOptions<UmadDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Configura las entidades, llaves, relaciones, restricciones e índices del modelo de datos en SQLite.
        /// </summary>
        /// <author>Emmanuel Baltazar López</author>
        /// <date>25/11/2026</date>
        /// <version>1.0</version>
        /// <modification>25/11/2026</modification>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Persona>(entity =>
            {
                entity.ToTable("Personas");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.IdentificadorGlobal)
                    .IsRequired();

                entity.Property(p => p.Matricula)
                    .HasMaxLength(50);

                entity.Property(p => p.NombreCompleto)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(p => p.Correo)
                    .IsRequired()
                    .HasMaxLength(150);

                entity.Property(p => p.PasswordHash)
                    .IsRequired()
                    .HasMaxLength(512);

                entity.Property(p => p.Rol)
                    .IsRequired()
                    .HasConversion<int>();

                entity.Property(p => p.Activo)
                    .IsRequired()
                    .HasDefaultValue(true);

                entity.Property(p => p.FechaCreacionUtc)
                    .IsRequired();

                entity.Property(p => p.FechaActualizacionUtc);

                entity.HasIndex(p => p.IdentificadorGlobal)
                    .IsUnique()
                    .HasDatabaseName("IX_Personas_IdentificadorGlobal");

                entity.HasIndex(p => p.Matricula)
                    .IsUnique()
                    .HasFilter("\"Matricula\" IS NOT NULL")
                    .HasDatabaseName("IX_Personas_Matricula");

                entity.HasIndex(p => p.Correo)
                    .IsUnique()
                    .HasDatabaseName("IX_Personas_Correo");
            });

            modelBuilder.Entity<TokenAcceso>(entity =>
            {
                entity.ToTable("TokensAcceso");
                entity.HasKey(t => t.Id);

                entity.Property(t => t.IdentificadorGlobal)
                    .IsRequired();

                entity.Property(t => t.PersonaId)
                    .IsRequired();

                entity.Property(t => t.CodigoFirmado)
                    .IsRequired()
                    .HasMaxLength(512);

                entity.Property(t => t.TipoPase)
                    .IsRequired()
                    .HasConversion<int>();

                entity.Property(t => t.FechaEmision)
                    .IsRequired();

                entity.Property(t => t.FechaExpiracion)
                    .IsRequired();

                entity.Property(t => t.Revocado)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(t => t.FechaRevocacionUtc);

                entity.Property(t => t.MotivoRevocacion)
                    .HasMaxLength(250);

                entity.HasIndex(t => t.IdentificadorGlobal)
                    .IsUnique()
                    .HasDatabaseName("IX_TokensAcceso_IdentificadorGlobal");

                entity.HasIndex(t => t.CodigoFirmado)
                    .IsUnique()
                    .HasDatabaseName("IX_TokensAcceso_CodigoFirmado");

                entity.HasIndex(t => new { t.PersonaId, t.Revocado, t.FechaExpiracion })
                    .HasDatabaseName("IX_TokensAcceso_Persona_Estado_Vigencia");

                entity.HasOne(t => t.Persona)
                    .WithMany(p => p.TokensAcceso)
                    .HasForeignKey(t => t.PersonaId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<RegistroAcceso>(entity =>
            {
                entity.ToTable("RegistrosAcceso");
                entity.HasKey(r => r.Id);

                entity.Property(r => r.IdentificadorGlobal)
                    .IsRequired();

                entity.Property(r => r.PersonaId)
                    .IsRequired();

                entity.Property(r => r.TokenAccesoId);

                entity.Property(r => r.PuntoAcceso)
                    .IsRequired()
                    .HasMaxLength(120);

                entity.Property(r => r.TipoMovimiento)
                    .IsRequired()
                    .HasConversion<int>();

                entity.Property(r => r.Resultado)
                    .IsRequired()
                    .HasConversion<int>();

                entity.Property(r => r.MotivoResultado)
                    .HasMaxLength(300);

                entity.Property(r => r.FechaHora)
                    .IsRequired();

                entity.Property(r => r.Sincronizado)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(r => r.EstadoSincronizacion)
                    .IsRequired()
                    .HasConversion<int>()
                    .HasDefaultValue(EstadoSincronizacion.Pendiente);

                entity.Property(r => r.FechaUltimoIntento);

                entity.Property(r => r.Reintentos)
                    .IsRequired()
                    .HasDefaultValue(0);

                entity.Property(r => r.UltimoError)
                    .HasMaxLength(500);

                entity.HasIndex(r => r.IdentificadorGlobal)
                    .IsUnique()
                    .HasDatabaseName("IX_RegistrosAcceso_IdentificadorGlobal");

                entity.HasIndex(r => r.FechaHora)
                    .HasDatabaseName("IX_RegistrosAcceso_FechaHora");

                entity.HasIndex(r => r.Sincronizado)
                    .HasDatabaseName("IX_RegistrosAcceso_Sincronizado");

                entity.HasIndex(r => r.EstadoSincronizacion)
                    .HasDatabaseName("IX_RegistrosAcceso_EstadoSincronizacion");

                entity.HasOne(r => r.Persona)
                    .WithMany(p => p.RegistrosAcceso)
                    .HasForeignKey(r => r.PersonaId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.TokenAcceso)
                    .WithMany(t => t.RegistrosAcceso)
                    .HasForeignKey(r => r.TokenAccesoId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            OnModelCreatingExtensible(modelBuilder);
        }

        // Punto de extensión parcial reservado para entidades adicionales de Fase 2 (renovaciones, incidencias y eventos).
        partial void OnModelCreatingExtensible(ModelBuilder modelBuilder);
    }
}
