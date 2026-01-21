using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using MassTransit;
using EventBus.Messages.Common;
using Notification.API;
using Notification.API.Consumers;
using Notification.API.Repository;
using Notification.API.Repository.Contract;
using Notification.API.Services;
using System.Text;
using Mozaiks.Auth;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure Key Vault (if URL is provided)
var keyVaultUrl = builder.Configuration["KeyVault:Url"] 
    ?? Environment.GetEnvironmentVariable("KEYVAULT_URL");
if (!string.IsNullOrEmpty(keyVaultUrl))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUrl),
        new DefaultAzureCredential());
    Console.WriteLine($"[Notification.API] Azure Key Vault configured: {keyVaultUrl}");
}


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

builder.Services.AddSwaggerGen(options =>
{
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Notification Service", Version = "v1" });
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { scheme, Array.Empty<string>() } });
});

var mongoConnectionString = builder.Configuration.GetValue<string>("MongoDB:ConnectionString");
var mongoDatabaseName = builder.Configuration.GetValue<string>("MongoDB:DatabaseName");
if (string.IsNullOrWhiteSpace(mongoConnectionString))
{
    throw new InvalidOperationException("MongoDB:ConnectionString must be configured for Notification.API");
}
if (string.IsNullOrWhiteSpace(mongoDatabaseName))
{
    throw new InvalidOperationException("MongoDB:DatabaseName must be configured for Notification.API");
}

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddScoped<IMongoDatabase>(s =>
{
    var client = s.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDatabaseName);
});

builder.AddMozaiksAuth(signalRHubPathPrefix: "/notificationHub");

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        var commaSeparated = (builder.Configuration["AllowedOrigins"] ?? string.Empty)
            .Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sectionOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        var origins = commaSeparated.Length > 0 ? commaSeparated : sectionOrigins;

        if (origins.Length > 0)
        {
            policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            return;
        }

        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("AllowedOrigins must be configured for Notification.API in non-development environments.");
        }

        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();

var eventBusHostAddress = builder.Configuration.GetValue<string>("EventBusSettings:HostAddress");
if (string.IsNullOrWhiteSpace(eventBusHostAddress))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("EventBusSettings:HostAddress must be configured for Notification.API");
    }
}

if (!string.IsNullOrWhiteSpace(eventBusHostAddress))
{
    builder.Services.AddMassTransit(config =>
    {
        config.AddConsumer<DirectMessageSentConsumer>();

        config.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(eventBusHostAddress);

            cfg.ReceiveEndpoint(EventBusConstants.DIRECT_MESSAGE_SENT_QUEUE, e =>
            {
                e.ConfigureConsumer<DirectMessageSentConsumer>(ctx);
            });
        });
    });
}

builder.Services.AddTransient<INotificationService, NotificationService>();
builder.Services.AddTransient<INotificationRepository, NotificationRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseRouting();

app.UseCors("CorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");
app.MapHealthChecks("/healthz");

app.Run();
