using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Mozaiks.Auditing;
using MongoDB.Driver;
using System.Text;
using Mozaiks.Auth;
using User.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddMongoAdminAuditing("User.API");
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<AdminAuditActionFilter>();
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IMongoClient>(x =>
{
    var connectionString = builder.Configuration.GetValue<string>("MongoDB:ConnectionString");
    return new MongoClient(connectionString);
});

builder.Services.AddScoped<IMongoDatabase>(s =>
{
    var client = s.GetService<IMongoClient>();
    var databaseName = builder.Configuration.GetValue<string>("MongoDB:DatabaseName");
    return client.GetDatabase(databaseName);
});

// OIDC JWT Authentication (provider-agnostic; CIAM is the active issuer)
builder.AddMozaiksAuth();
builder.Services.AddAuthorization();

builder.Services.AddHttpClient<IServiceToServiceTokenProvider, ServiceToServiceTokenProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Register the repository
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddHttpClient("AuthServer", client =>
{
    var baseUrl = (builder.Configuration.GetValue<string>("AuthServerService:BaseUrl") ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("AuthServerService:BaseUrl must be configured for User.API in non-development environments");
        }

        baseUrl = "http://localhost:8020";
    }

    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/healthz");

app.Run();

static bool IsPlaceholderSecret(string secret)
{
    return string.Equals(secret, "replace_me", StringComparison.OrdinalIgnoreCase)
           || string.Equals(secret, "dev-secret-change-me", StringComparison.OrdinalIgnoreCase)
           || string.Equals(secret, "dev-secret-change-me-please", StringComparison.OrdinalIgnoreCase);
}
