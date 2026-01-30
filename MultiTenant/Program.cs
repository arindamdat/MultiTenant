using Autofac;
using Autofac.Extensions.DependencyInjection;
using Autofac.Multitenant;
using Microsoft.EntityFrameworkCore;
using MultiTenant;
using Serilog;
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
            containerBuilder.RegisterType<HeaderTenantIdentificationStrategy>()
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
                });
            }
            return _mtc!;
        }));

       

        var app = builder.Build();


        app.UseSerilogRequestLogging();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "MultiTenant API V1");
            c.RoutePrefix = string.Empty;
        });

        //app.UseHttpsRedirection();

        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/ping", () => "pong");

        //await TenantMigrationAndSeed.MigrateAndSeedAsync(tenantConfigPath);

        app.Run();
    }
}