using System.Text;
using CreditRiskScoring.Api.Infrastructure.BackgroundJobs;
using CreditRiskScoring.Api.Infrastructure.Data;
using CreditRiskScoring.Api.Infrastructure.Security;
using CreditRiskScoring.Api.Infrastructure.Services;
using CreditRiskScoring.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
// Default: SQLite (file-based, works out of the box)
// Production: swap for SQL Server or PostgreSQL via environment variable DB_PROVIDER
var dbProvider = builder.Configuration["Database:Provider"] ?? "sqlite";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (dbProvider == "inmemory")
        options.UseInMemoryDatabase("CreditRiskDb");
    else
        options.UseSqlite(builder.Configuration.GetConnectionString("Default")
                          ?? "Data Source=creditrisk.db");
});

// ── GC: Server GC is enabled via project property ServerGarbageCollection=true ─
// Fine-tune at runtime via environment variables:
//   DOTNET_GCConserve=1       (low-memory mode)
//   DOTNET_GCServer=1         (server GC, multi-threaded)
//   DOTNET_GCHeapHardLimit=…  (heap byte limit)
GC.Collect(2, GCCollectionMode.Optimized, blocking: false);   // compaction pass at startup

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<ScoringEngine>();
builder.Services.AddScoped<AiExplanationService>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddHostedService<RiskRecalculationJob>();

// ── MediatR (CQRS) ────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret must be configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting (ASP.NET Core built-in, OWASP: prevent brute-force) ────────
builder.Services.AddRateLimiter(opt =>
{
    opt.AddFixedWindowLimiter("ApiLimit", cfg =>
    {
        cfg.PermitLimit         = 100;
        cfg.Window              = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit          = 5;
    });
    opt.RejectionStatusCode = 429;
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opt =>
    opt.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ── Controllers + Swagger ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "AI-Powered Credit Risk Scoring API",
        Version     = "v1",
        Description = """
            **Capleon Fintech** — Deterministic credit risk scoring with AI-generated explanations.

            Submit a financial profile to `POST /api/v1/risk/score` to get a score (0–100)
            and a risk tier. Use `POST /api/v1/risk/explain` to get a plain-English explanation.
            Simulate hypothetical changes via `POST /api/v1/risk/simulate`.

            All protected endpoints require a Bearer JWT.
            Register → login → copy the token → click **Authorize**.
            """,
        Contact = new OpenApiContact { Name = "Capleon Engineering", Email = "engineering@capleon.com" }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type        = SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token (without the 'Bearer ' prefix)"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference
                { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// ── Build ──────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Ensure DB schema exists (dev convenience; use migrations in production)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbProvider != "inmemory")
        db.Database.EnsureCreated();
}

// ── Middleware pipeline (order matters) ───────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Credit Risk Scoring API v1");
        c.RoutePrefix     = string.Empty;   // serve Swagger UI at root "/"
        c.EnableDeepLinking();
    });
}

// OWASP security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("X-Content-Type-Options",    "nosniff");
    ctx.Response.Headers.Append("X-Frame-Options",           "DENY");
    ctx.Response.Headers.Append("X-XSS-Protection",          "1; mode=block");
    ctx.Response.Headers.Append("Referrer-Policy",           "strict-origin-when-cross-origin");
    ctx.Response.Headers.Append("Permissions-Policy",        "camera=(), microphone=()");
    await next();
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("ApiLimit");

app.Run();
