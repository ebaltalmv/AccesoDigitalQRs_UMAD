using Microsoft.EntityFrameworkCore;
using SharedResources.Models;

namespace SharedResources.Data;

public class UmadDbContext : DbContext
{
    // Aquí declaramos las tablas que existirán en la BD
    public DbSet<RoleModel> Roles { get; set; }
    public DbSet<UserModel> Users { get; set; }
    public DbSet<AccessTokenModel> AccessTokens { get; set; }
    public DbSet<AccessLogModel> AccessLogs { get; set; }

    public UmadDbContext() { }

    public UmadDbContext(DbContextOptions<UmadDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserModel>(entity =>
        {
            entity.HasKey(u => u.IdUser);
            entity.HasIndex(u => u.StudentId).IsUnique();
            entity.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Password).HasMaxLength(256).IsRequired();
            entity.Property(u => u.StudentId).HasMaxLength(50);

            entity.HasOne(u => u.Role)
                  .WithMany(r => r.Users)
                  .HasForeignKey(u => u.IdRole)
                  .IsRequired();
        });

        modelBuilder.Entity<RoleModel>(entity =>
        {
            entity.HasKey(r => r.IdRole);
            entity.Property(r => r.RoleName).HasMaxLength(50).IsRequired();

            // Predefinir los roles
            entity.HasData(
                new RoleModel { IdRole = 1, RoleName = "Estudiante" },
                new RoleModel { IdRole = 2, RoleName = "Docente" },
                new RoleModel { IdRole = 3, RoleName = "Administrativo" },
                new RoleModel { IdRole = 4, RoleName = "Visitante" },
                new RoleModel { IdRole = 5, RoleName = "Guardia" }
            );
        });

        modelBuilder.Entity<AccessLogModel>(entity =>
        {
            entity.HasKey(r => r.IdLog);
            entity.Property(r => r.AccessPoint).HasMaxLength(100).IsRequired();

            entity.HasOne(r => r.User)
                  .WithMany(u => u.AccessLogs)
                  .HasForeignKey(r => r.IdUser)
                  .IsRequired();
        });

        modelBuilder.Entity<AccessTokenModel>(entity =>
        {
            entity.HasKey(t => t.IdToken);
            entity.Property(t => t.QrHash).HasMaxLength(256).IsRequired();

            entity.HasOne(t => t.User)
                  .WithMany(u => u.Tokens)
                  .HasForeignKey(t => t.IdUser)
                  .IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}