using Microsoft.EntityFrameworkCore;

namespace MultiTenant
{
    public class PersonDbContext : DbContext
    {
        public PersonDbContext(DbContextOptions<PersonDbContext> options) : base(options) { }

        public DbSet<Person> Persons { get; set; }
    }
}
