using System.Collections.Generic;

namespace MultiTenant
{
    public class Tenant
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
    }

    public class TenantConfiguration
    {
        public List<Tenant> Tenants { get; set; } = new();
    }
}
