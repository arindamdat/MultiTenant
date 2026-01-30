using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace MultiTenant
{
    public static class TenantMigrationAndSeed
    {
        public static async Task MigrateAndSeedAsync(string tenantConfigPath)
        {
            var configJson = await File.ReadAllTextAsync(tenantConfigPath);
            var config = JsonSerializer.Deserialize<TenantConfiguration>(configJson);
            if (config == null) return;

            foreach (var tenant in config.Tenants)
            {
                var optionsBuilder = new DbContextOptionsBuilder<PersonDbContext>();
                optionsBuilder.UseSqlServer(tenant.ConnectionString);

                // Retry logic for DB connection
                var retries = 10;
                var delay = TimeSpan.FromSeconds(5);
                for (int i = 0; i < retries; i++)
                {
                    try
                    {
                        using var db = new PersonDbContext(optionsBuilder.Options);
                        await db.Database.MigrateAsync();
                        if (!db.Persons.Any())
                        {
                            db.Persons.AddRange(RandomPersons(tenant.Id));
                            await db.SaveChangesAsync();
                        }
                        break; // Success
                    }
                    catch (SqlException)
                    {
                        if (i == retries - 1) throw;
                        await Task.Delay(delay);
                    }
                }
            }
        }

        private static IEnumerable<Person> RandomPersons(string tenantId)
        {
            return new List<Person>
            {
                new Person { FirstName = $"{tenantId}_John", LastName = "Doe", DateOfBirth = new DateTime(1990, 1, 1) },
                new Person { FirstName = $"{tenantId}_Jane", LastName = "Smith", DateOfBirth = new DateTime(1985, 5, 23) },
                new Person { FirstName = $"{tenantId}_Alice", LastName = "Johnson", DateOfBirth = new DateTime(2000, 12, 12) }
            };
        }
    }
}
