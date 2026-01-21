using Microsoft.OpenApi.Models;
using Mozaiks.Auditing;
using MongoDB.Driver;
using Plugins.API.Services;
using Plugins.API.Repository;
using Plugins.API.Infrastructure;
using Mozaiks.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddForwardedHeadersForReverseProxy();

MongoConventions.Register();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddMongoAdminAuditing("Plugins.API");

// Controllers
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<AdminAuditActionFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();

// MongoDB
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

builder.Services.AddHttpContextAccessor();

// Services
builder.Services.AddScoped<IPluginRepository, PluginRepository>();
builder.Services.AddScoped<IPluginRegistryService, PluginRegistryService>();
builder.Services.AddScoped<IPluginInstallationService, PluginInstallationService>();

// Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Mozaiks Plugins API", 
        Version = "v1",
        Description = "Plugin catalog, manifests, and installations management"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Auth
builder.AddMozaiksAuth();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
