using System.Text;
using IAM.API.Auth;
using IAM.API.Middleware;
using IAM.Infrastructure;
using IAM.Infrastructure.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
// using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using IAM.Domain.Entities;
using IAM.Application.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure layer (DB, Repos, Services) ───────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── Authentication: JWT + Grafana API Key (PolicyScheme) ──────
// The "Smart" policy scheme dynamically routes to either:
//   - "GrafanaApiKey" if X-Grafana-Api-Key header is present
//   - "Bearer" (JWT) for all other requests (cookie, Authorization header, etc.)
builder.Services.AddAuthentication("Smart")
    .AddPolicyScheme("Smart", "JWT or Grafana API Key", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.ContainsKey("X-Grafana-Api-Key"))
                return GrafanaApiKeyAuthHandler.SchemeName;
            return JwtBearerDefaults.AuthenticationScheme;
        };
    })
    .AddScheme<AuthenticationSchemeOptions, GrafanaApiKeyAuthHandler>(
        GrafanaApiKeyAuthHandler.SchemeName, null)
    .AddJwtBearer(options =>
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
        };

        // Set up lazy key resolution
        var serviceProvider = builder.Services.BuildServiceProvider();
        var rsaKeyService = serviceProvider.GetRequiredService<RsaKeyService>();
        tokenValidationParameters.IssuerSigningKey = rsaKeyService.GetPublicKey();

        options.TokenValidationParameters = tokenValidationParameters;
        
        // Support token from Authorization header, X-JWT-Assertion header, or HTTP-only cookie
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // 1. Try to get token from Authorization header (default)
                // 2. Try X-JWT-Assertion header (for Grafana)
                if (string.IsNullOrEmpty(context.Token) && 
                    context.Request.Headers.TryGetValue("X-JWT-Assertion", out var headerValue))
                {
                    context.Token = headerValue.ToString();
                }
                
                // 3. Try to get token from HTTP-only cookie (for React SPA)
                if (string.IsNullOrEmpty(context.Token) &&
                    context.Request.Cookies.TryGetValue("iam_access_token", out var cookieToken))
                {
                    context.Token = cookieToken;
                }
                
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── CORS ──────────────────────────────────────────────────────
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("ReactApp", policy =>
        policy.WithOrigins(
                  "http://localhost:5173",
                  "http://localhost",
                  "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ── Controllers + Swagger ─────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient(); // For GrafanaProxyController to make requests to Grafana
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "IAM - Industrial Asset Monitoring Auth API",
        Version = "v1",
        Description = "Authentication & Authorization service for IAM POC"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token"
    });

   c.AddSecurityRequirement(new OpenApiSecurityRequirement
{
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        Array.Empty<string>()
    }
});
});
builder.WebHost.UseUrls("http://0.0.0.0:5500");


// ─────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<GrafanaProxyMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── JWKS Endpoint for Grafana ──────────────────────────────────
app.MapGet("/.well-known/jwks.json", (RsaKeyService rsaService) =>
{
    var jwks = rsaService.GetJwks();
    return Results.Ok(jwks);
}).WithName("GetJWKS")
  .Produces(200);

// ── Health Check Endpoint ──────────────────────────────
app.MapGet("/health", () =>
{
    return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
}).WithName("Health")
  .Produces(200);
app.UseHttpsRedirection();
app.UseCors("ReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Auto-apply migrations on startup ─────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<IAM.Infrastructure.Persistence.AppDbContext>();
    db.Database.EnsureCreated();

    // Read admin credentials from configuration
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var adminEmail = config["AdminSettings:Email"] ?? "admin@gmail.com";
    var adminPassword = config["AdminSettings:Password"] ?? "Admin@123456";
    var adminUsername = config["AdminSettings:Username"] ?? "admin";
    var adminRole = config["AdminSettings:Role"] ?? "Admin";

    // Seed admin if not present
    if (!db.Users.Any(u => u.Email == adminEmail))
    {
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var hash = hasher.Hash(adminPassword);
        var admin = User.Create(adminUsername, adminEmail, hash, adminRole);
        db.Users.Add(admin);
        db.SaveChanges();
    }
}

app.Run();