namespace MultiTenant
{
    public class KeycloakSettings
    {
        public string Url { get; set; } = string.Empty;
        public string AdminUser { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
        public bool RequireHttps { get; set; } = false;
    }
}
