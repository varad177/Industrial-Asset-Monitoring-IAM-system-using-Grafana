using IAM.Application.Configuration;
using IAM.Application.Interfaces;
using IAM.Domain.Interfaces;
using IAM.Infrastructure.Persistence;
using IAM.Infrastructure.Services;
using IAM.Infrastructure.Services.Auth;
using IAM.Infrastructure.Services.OpenFga;
using IAM.Infrastructure.Services.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IAM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        // ── Repositories / UoW ────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Auth Services ─────────────────────────────────────
        services.AddSingleton<RsaKeyService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();

        // ── OpenFGA Configuration + Service ───────────────────
        services.Configure<OpenFgaSettings>(
            configuration.GetSection(OpenFgaSettings.SectionName));
        services.AddHttpClient("OpenFGA");
        services.AddSingleton<IOpenFgaService, OpenFgaService>();

        // ── InfluxDB Configuration ────────────────────────────
        services.Configure<InfluxDbSettings>(
            configuration.GetSection(InfluxDbSettings.SectionName));

        // ── Telemetry Services ────────────────────────────────
        services.AddSingleton<IAssetConfigurationProvider, AssetConfigurationProvider>();
        services.AddSingleton<IInfluxDbService, InfluxDbService>();

        // ── Telemetry Seeder Background Service ───────────────
        services.AddHostedService<TelemetrySeederBackgroundService>();

        return services;
    }
}