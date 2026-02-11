using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MultiTenant.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class PersonsController : ControllerBase
    {
        private readonly PersonDbContext _dbContext;

        public PersonsController(PersonDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var persons = await _dbContext.Persons.ToListAsync();
            return Ok(persons);
        }
    }
}
