using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MultiTenant
{
    public class PersonDbContextFactory : IDesignTimeDbContextFactory<PersonDbContext>
    {
        public PersonDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PersonDbContext>();
            // Use a default connection string for design-time (update as needed)
            optionsBuilder.UseSqlServer("Server=localhost,1433;Database=tenant-1;User=sa;Password=Your_password123;TrustServerCertificate=True;");
            return new PersonDbContext(optionsBuilder.Options);
        }
    }
}