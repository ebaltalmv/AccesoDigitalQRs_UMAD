using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedResources.Data;

namespace MigrationApp
{
    public class DataContextFactory : IDesignTimeDbContextFactory<UmadDbContext>
    {
        public UmadDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<UmadDbContext>();

            optionsBuilder.UseSqlite("Data Source=UmadLocal.db", b => b.MigrationsAssembly("MigrationApp"));

            return new UmadDbContext(optionsBuilder.Options);
        }
    }
}
