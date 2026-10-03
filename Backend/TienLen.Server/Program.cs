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

// 1. Database and Data Context
builder.Services.AddSingleton<IMongoDbContext, MongoDbContext>();

// 2. Application Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMatchService, MatchService>();

// 3. Authentication & JWT
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? builder.Configuration["Jwt__Key"]
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

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
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
