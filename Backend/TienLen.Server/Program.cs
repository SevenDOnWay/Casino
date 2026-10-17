using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IO;
using System.Text;
using TienLen.Server.Data;
using TienLen.Server.Services;

// Load local .env secrets before building configuration
LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Dynamic port binding for Render and cloud hosting
var port = Environment.GetEnvironmentVariable("PORT");
if ( !string.IsNullOrEmpty(port) ) {
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// 1. Database and Data Context
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<IDataBaseContext>(sp => sp.GetRequiredService<MongoDbContext>());
builder.Services.AddSingleton<IDatabaseContext>(sp => sp.GetRequiredService<MongoDbContext>());
#pragma warning disable CS0618
builder.Services.AddSingleton<IMongoDbContext>(sp => sp.GetRequiredService<MongoDbContext>());
#pragma warning restore CS0618

// 2. Application Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<ILobbyService, LobbyService>();

// 3. CORS Support (for Unity WebGL, web dashboards, and browser clients)
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", policy => {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 4. Authentication & JWT
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? builder.Configuration["Jwt__Key"]
    ?? builder.Configuration["JWT_SECRET"]
    ?? "TienLenSecretSuperSecureKey2026!@#$%^&*()";

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options => {
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? builder.Configuration["Jwt__Issuer"] ?? "TienLenServer",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? builder.Configuration["Jwt__Audience"] ?? "TienLenClient",
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

if ( app.Environment.IsProduction() && jwtKey.Contains("TienLenSecretSuperSecureKey2026") ) {
    Console.WriteLine("[SECURITY WARNING] Running in Production with default JWT Secret Key! Configure 'Jwt__Key' or 'JWT_SECRET' in environment variables.");
}

// Ensure database indexes asynchronously without blocking startup or integration tests
_ = Task.Run(async () => {
    try {
        if ( app.Services.GetService<IDataBaseContext>() is MongoDbContext mongoDb ) {
            await mongoDb.EnsureIndexesAsync();
        }
    }
    catch ( Exception ex ) {
        Console.WriteLine($"[Startup] Notice: Index initialization deferred/failed: {ex.Message}");
    }
});

app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Render and cloud health check endpoints
app.MapGet("/", () => Results.Ok(new {
    service = "TienLen Backend Server",
    status = "healthy",
    environment = app.Environment.EnvironmentName,
    timestamp = DateTime.UtcNow
}));

app.MapGet("/health", () => Results.Ok(new {
    status = "healthy",
    timestamp = DateTime.UtcNow
}));

app.MapControllers();

app.Run();

static void LoadEnvFile() {
    string[] candidatePaths = {
        Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        Path.Combine(AppContext.BaseDirectory, ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "Backend", "TienLen.Server", ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), "TienLen.Server", ".env")
    };

    foreach ( var path in candidatePaths ) {
        if ( File.Exists(path) ) {
            foreach ( var rawLine in File.ReadAllLines(path) ) {
                var line = rawLine.Trim();
                if ( string.IsNullOrEmpty(line) || line.StartsWith("#") ) continue;
                int eqIndex = line.IndexOf('=');
                if ( eqIndex > 0 ) {
                    string key = line.Substring(0, eqIndex).Trim();
                    string value = line.Substring(eqIndex + 1).Trim();
                    if ( value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2 ) {
                        value = value.Substring(1, value.Length - 2);
                    }
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
            break;
        }
    }
}

// Make Program visible for WebApplicationFactory in integration tests
public partial class Program { }
