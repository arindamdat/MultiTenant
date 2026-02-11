using Autofac;
using Autofac.Multitenant;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using MultiTenant;
using Serilog;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

internal class Program
{
    private static MultitenantContainer? _mtc;

    private static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .Enrich.FromLogContext()
            .CreateLogger();

        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .WriteTo.Console());

        builder.Services.AddControllers().AddControllersAsServices();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddHttpContextAccessor();

        //Autofac registration
        builder.Services.AddAutofacMultitenantRequestServices();

        var tenantConfigPath = Path.Combine(AppContext.BaseDirectory, "tenants.json");
        var tenantConfigJson = File.ReadAllText(tenantConfigPath);
        var tenantConfig = JsonSerializer.Deserialize<TenantConfiguration>(tenantConfigJson);

        builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            containerBuilder.RegisterType<SubdomainTenantIdentificationStrategy>()
                .As<ITenantIdentificationStrategy>()
                .SingleInstance();
            containerBuilder.RegisterInstance(tenantConfig!);
        });
        // Register the multitenant container accessor
        builder.Host.UseServiceProviderFactory(new AutofacMultitenantServiceProviderFactory(container =>
        {
            var strategy = container.Resolve<ITenantIdentificationStrategy>();
            _mtc = new MultitenantContainer(strategy, container);
            var tenantConfig = container.Resolve<TenantConfiguration>();
            foreach (var tenant in tenantConfig!.Tenants)
            {
                _mtc.ConfigureTenant(tenant.Id, cb =>
                {
                    cb.Register(ctx =>
                    {
                        var optionsBuilder = new DbContextOptionsBuilder<PersonDbContext>();
                        optionsBuilder.UseSqlServer(tenant.ConnectionString);
                        return new PersonDbContext(optionsBuilder.Options);
                    }).InstancePerLifetimeScope();

                    cb.RegisterType<MyRandomService>()
                      .As<IMyRandomService>()
                      .SingleInstance();
                });
            }
            return _mtc!;
        }));

        builder.Services.Configure<KeycloakSettings>(builder.Configuration.GetSection("Keycloak"));
        var keycloakSettings = builder.Configuration.GetSection("Keycloak").Get<KeycloakSettings>();
        // Keycloak multi-tenant JWT authentication
        // 1. Create a thread-safe cache to hold a Manager for each Tenant/Realm
        var configManagerCache = new ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var httpContext = context.HttpContext;
                        var host = httpContext.Request.Host.Host;
                        var parts = host.Split('.');
                        if (parts.Length < 3)
                            return Task.CompletedTask;
                        var subdomain = parts[0];
                        var authority = $"{keycloakSettings!.Url}/realms/{subdomain}";

                        // ----------------------------------------------------------------
                        // 2. GET OR CREATE THE CONFIG MANAGER FOR THIS TENANT
                        // ----------------------------------------------------------------
                        if (!configManagerCache.TryGetValue(authority, out var manager))
                        {
                            var httpClientHandler = new HttpClientHandler();
                            if (!keycloakSettings.RequireHttps)
                            {
                                httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                            }

                            var httpClient = new HttpClient(httpClientHandler);
                            // Create the manager manually
                            manager = new ConfigurationManager<OpenIdConnectConfiguration>(
                                $"{authority}/.well-known/openid-configuration",
                                new OpenIdConnectConfigurationRetriever(),
                                new HttpDocumentRetriever(httpClient) { RequireHttps = keycloakSettings.RequireHttps }
                            );

                            configManagerCache.TryAdd(authority, manager);
                        }

                        context.Options.Authority = authority;
                        context.Options.TokenValidationParameters.ValidateIssuer = true;
                        context.Options.TokenValidationParameters.ValidIssuer = authority;
                        context.Options.TokenValidationParameters.ConfigurationManager = manager;
                        context.Options.ConfigurationManager = manager;

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        //var httpContext = context.HttpContext;
                        //var host = httpContext.Request.Host.Host;
                        //var parts = host.Split('.');
                        //if (parts.Length < 3)
                        //{
                        //    context.Fail("Invalid host format for subdomain tenant identification.");
                        //    return Task.CompletedTask;
                        //}
                        //var subdomain = parts[0];
                        //var expectedIssuer = $"{keycloakSettings!.Url}/realms/{subdomain}";
                        //var actualIssuer = context.Principal?.FindFirst("iss")?.Value;
                        //if (!string.Equals(expectedIssuer, actualIssuer, StringComparison.OrdinalIgnoreCase))
                        //{
                        //    context.Fail($"Issuer does not match the expected realm for tenant: {subdomain}");
                        //}
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        Log.Information("[Custom] Authentication failed: {Message}", context.Exception.Message);
                        Log.Error(context.Exception, "Authentication failed");
                        return Task.CompletedTask;
                    }
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true, // Must be true!
                    ValidAudience = "multi-tenant-api"
                };
                options.Authority = null; // Will be set dynamically in OnMessageReceived
                options.RequireHttpsMetadata = keycloakSettings!.RequireHttps;
            });



        // Provision Keycloak realms for all tenants on startup
        var realmProvisioner = new KeycloakRealmProvisioner(
            keycloakSettings!.Url,
            keycloakSettings.AdminUser,
            keycloakSettings.AdminPassword);
        await realmProvisioner.ProvisionRealmsAsync(tenantConfig!.Tenants);

        var app = builder.Build();

        app.UseSerilogRequestLogging();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "MultiTenant API V1");
            c.RoutePrefix = string.Empty;
        });

        //app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/ping", () => "pong");

        await TenantMigrationAndSeed.MigrateAndSeedAsync(tenantConfigPath);

        app.Run();
    }
}