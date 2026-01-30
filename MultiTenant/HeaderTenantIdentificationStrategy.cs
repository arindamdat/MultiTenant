using Autofac.Multitenant;

namespace MultiTenant
{
    public class HeaderTenantIdentificationStrategy : ITenantIdentificationStrategy
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HeaderTenantIdentificationStrategy(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public bool TryIdentifyTenant(out object tenantId)
        {
            tenantId = null;
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
                return false;
            if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader))
            {
                tenantId = tenantHeader.ToString();
                return true;
            }
            return false;
        }
    }
}
