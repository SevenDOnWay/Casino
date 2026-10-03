using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TienLen.Server.Data;
using TienLen.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Database and Data Context
builder.Services.AddSingleton<IMongoDbContext, MongoDbContext>();

// 2. Application Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMatchService, MatchService>();

// 3. Authentication & JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "TienLenSecretSuperSecureKey2026!@#$%^&*()";
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
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TienLenServer",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TienLenClient",
        ClockSkew = System.TimeSpan.Zero
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

// Make Program visible for WebApplicationFactory in integration tests
public partial class Program { }
