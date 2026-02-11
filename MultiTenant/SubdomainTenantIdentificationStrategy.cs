using Autofac.Multitenant;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace MultiTenant
{
    public class SubdomainTenantIdentificationStrategy : ITenantIdentificationStrategy
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TenantConfiguration _tenantConfiguration;

        public SubdomainTenantIdentificationStrategy(IHttpContextAccessor httpContextAccessor, TenantConfiguration tenantConfiguration)
        {
            _httpContextAccessor = httpContextAccessor;
            _tenantConfiguration = tenantConfiguration;
        }

        public bool TryIdentifyTenant(out object tenantId)
        {
            tenantId = null;
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
                return false;

            var host = context.Request.Host.Host;
            if (string.IsNullOrEmpty(host))
                return false;

            // Extract subdomain (assumes format: subdomain.domain.tld)
            var parts = host.Split('.');
            if (parts.Length < 3)
                return false;
            var subdomain = parts[0];

            var tenant = _tenantConfiguration.Tenants.FirstOrDefault(t => t.ShortName.Equals(subdomain, StringComparison.OrdinalIgnoreCase));
            if (tenant == null)
                return false;

            tenantId = tenant.Id;
            return true;
        }
    }
}
