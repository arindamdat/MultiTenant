using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MultiTenant.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController: ControllerBase
    {
        private readonly IMyRandomService _randomService;
        public TestController(IMyRandomService randomService)
        {
            _randomService = randomService;
        }
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return Ok(_randomService.GetData());
        }
    }
}
