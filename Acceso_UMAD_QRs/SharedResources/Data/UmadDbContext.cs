using Microsoft.EntityFrameworkCore;
using SharedResources.Models;

namespace SharedResources.Data;

public class UmadDbContext : DbContext
{
    // Aquí declaramos las tablas que existirán en la BD
    public DbSet<RolModel> Roles { get; set; }
    public DbSet<UsuarioModel> Usuarios { get; set; }
    public DbSet<TokenAccesoModel> TokensAcceso { get; set; }
    public DbSet<RegistroModel> Registros { get; set; }

    public UmadDbContext() { }

    public UmadDbContext(DbContextOptions<UmadDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UsuarioModel>(entity =>
        {
            entity.HasKey(u => u.IdUsuario);
            entity.HasIndex(u => u.Matricula).IsUnique();
            entity.Property(u => u.NombreCompleto).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Correo).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Contrasena).HasMaxLength(256).IsRequired();
            entity.Property(u => u.Matricula).HasMaxLength(50);

            entity.HasOne(u => u.Rol)
                  .WithMany(r => r.Usuarios)
                  .HasForeignKey(u => u.IdRol)
                  .IsRequired();
        });

        modelBuilder.Entity<RolModel>(entity =>
        {
            entity.HasKey(r => r.IdRol);
            entity.Property(r => r.NombreRol).HasMaxLength(50).IsRequired();

            // Predefinir los roles
            entity.HasData(
                new RolModel { IdRol = 1, NombreRol = "Estudiante" },
                new RolModel { IdRol = 2, NombreRol = "Docente" },
                new RolModel { IdRol = 3, NombreRol = "Administrativo" },
                new RolModel { IdRol = 4, NombreRol = "Visitante" },
                new RolModel { IdRol = 5, NombreRol = "Guardia" }
            );
        });

        modelBuilder.Entity<RegistroModel>(entity =>
        {
            entity.HasKey(r => r.IdRegistro);
            entity.Property(r => r.PuntoAcceso).HasMaxLength(100).IsRequired();

            entity.HasOne(r => r.Usuario)
                  .WithMany(u => u.Registros)
                  .HasForeignKey(r => r.IdUsuario)
                  .IsRequired();
        });

        modelBuilder.Entity<TokenAccesoModel>(entity =>
        {
            entity.HasKey(t => t.IdToken);
            entity.Property(t => t.HashQr).HasMaxLength(256).IsRequired();

            entity.HasOne(t => t.Usuario)
                  .WithMany(u => u.Tokens)
                  .HasForeignKey(t => t.IdUsuario)
                  .IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}