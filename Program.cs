using Microsoft.AspNetCore.Authentication.JwtBearer;
using Marketplace.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// Core services
// --------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();

// --------------------
// PostgreSQL (EF Core)
// --------------------
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connStr))
    throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection");

builder.Services.AddDbContext<MarketplaceDbContext>(opt =>
    opt.UseNpgsql(connStr));

// --------------------
// Bind Options (MATCH appsettings*.json)
// --------------------
builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection("Keycloak"));
builder.Services.Configure<AuthTesterOptions>(builder.Configuration.GetSection("AuthTester"));

// --------------------
// Swagger (JWT Bearer)
// --------------------
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Marketplace API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste ONLY the JWT access token (WITHOUT 'Bearer ')."
    });

    c.OperationFilter<Marketplace.Api.Swagger.AuthorizeRolesOperationFilter>();
});

// --------------------
// JWT Auth (Keycloak)
// --------------------
var kc = builder.Configuration.GetSection("Keycloak");

var authority = (kc["Authority"] ?? "").TrimEnd('/');
var audience = kc["Audience"];
var requireHttps = bool.TryParse(kc["RequireHttpsMetadata"], out var rhm) && rhm;

if (string.IsNullOrWhiteSpace(authority))
    throw new InvalidOperationException("Missing config: Keycloak:Authority");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = requireHttps;

        // Pin discovery to the marketplace realm explicitly (avoids wrong realm discovery)
        options.MetadataAddress = $"{authority}/.well-known/openid-configuration";

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authority,

            // Local dev safe: Keycloak tokens often have aud = "account" unless you add an audience mapper.
            ValidateAudience = false,

            ValidateLifetime = true,
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var identity = ctx.Principal?.Identity as ClaimsIdentity;
                if (identity is null) return Task.CompletedTask;

                // Map Keycloak realm_access.roles -> ClaimTypes.Role
                var realmAccessJson = ctx.Principal?.FindFirst("realm_access")?.Value;

                if (!string.IsNullOrWhiteSpace(realmAccessJson))
                {
                    using var doc = JsonDocument.Parse(realmAccessJson);

                    if (doc.RootElement.TryGetProperty("roles", out var roles) &&
                        roles.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var r in roles.EnumerateArray())
                        {
                            var role = r.GetString();
                            if (!string.IsNullOrWhiteSpace(role))
                                identity.AddClaim(new Claim(ClaimTypes.Role, role));
                        }
                    }
                }

                return Task.CompletedTask;
            },

            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine("AUTH FAILED: " + ctx.Exception);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// --------------------
// Pipeline
// --------------------
var app = builder.Build();

// Startup proof that config is loaded
var boundAuthority = app.Configuration["Keycloak:Authority"];
var boundTokenUrl = app.Configuration["Keycloak:TokenUrl"];
Console.WriteLine("=== CONFIG CHECK ===");
Console.WriteLine($"Keycloak:Authority = {boundAuthority}");
Console.WriteLine($"Keycloak:TokenUrl  = {boundTokenUrl}");
Console.WriteLine("====================");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Marketplace API v1"));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();

// --------------------
// Options classes
// --------------------
public class KeycloakOptions
{
    public string Authority { get; set; } = "";
    public bool RequireHttpsMetadata { get; set; } = false;
    public string Audience { get; set; } = "";
    public string TokenUrl { get; set; } = "";
    public string UserInfoUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
}

public class AuthTesterOptions
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5037";
}
