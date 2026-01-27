using Microsoft.OpenApi.Models;
using Mozaiks.Auditing;
using MongoDB.Driver;
using Payment.API.Middlewares;
using Payment.API.Repository;
using Payment.API.Repository.Interfaces;
using Payment.API.Services;
using Payment.API.Shared;
using Payment.API.Infrastructure;
using Payment.API.Infrastructure.Observability;
using Serilog;

using Payment.API.Services.Workers;
using Mozaiks.Auth;
using Payment.API.Controllers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddForwardedHeadersForReverseProxy();

MongoConventions.Register();

// Add services to the container.

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddMongoAdminAuditing("Payment.API");
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<AdminAuditActionFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

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
builder.Services.AddSingleton<ICorrelationContextAccessor, CorrelationContextAccessor>();
builder.Services.AddSingleton<ObservabilityMetrics>();
builder.Services.AddSingleton<ObservabilityTracing>();
builder.Services.AddSingleton<StructuredLogEmitter>();
builder.Services.AddSingleton<AnalyticsEventEmitter>();
builder.Services.AddTransient<CorrelationDelegatingHandler>();
builder.Services.AddHostedService<MetricBackfillHostedService>();
builder.Services.AddHttpClient<SubscriptionPlanClient>()
    .AddHttpMessageHandler<CorrelationDelegatingHandler>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Moz Server", Version = "v1" });

    // Include the Authorization header in Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    //options.OperationFilter<FileUploadOperationFilter>();

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] { }
        }
    });
});

var Cors = builder.Configuration["AllowedOrigins"].Split(new string[1] { "," }, StringSplitOptions.RemoveEmptyEntries);

// CORS + JWT hardening (wildcard only in Development; fail-fast in non-dev)
builder.AddStrictCorsPolicy("CorsPolicy", "AllowedOrigins");
builder.AddMozaiksAuth();
builder.Services.AddAuthorization();


builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
    loggingBuilder.SetMinimumLevel(LogLevel.Debug);
});

//Register the services 

builder.Services.AddScoped<CustomExceptionHandlerMiddleware>();

builder.Services.AddScoped<ILedgerRepository, LedgerRepository>();
builder.Services.AddScoped<LedgerService>();

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<TransactionService>();

builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<MozaiksPayService>();
builder.Services.AddScoped<SettlementService>();
builder.Services.AddScoped<EconomicEventAppender>();

// Entitlement sync infrastructure
builder.Services.AddScoped<IEntitlementManifestStore, EntitlementManifestRepository>();
builder.Services.AddScoped<IUsageEventStore, UsageEventRepository>();

builder.Services.AddHostedService<RefundWorker>();
builder.Services.AddHostedService<SettlementWorker>();
builder.Services.AddScoped<IEconomicEventRepository, EconomicEventRepository>();
builder.Services.AddScoped<MongoIndexInitializer>();


try
{
    //var logger = new LoggerConfiguration()
    //                    .ReadFrom.Configuration(builder.Configuration)
    //                    .Enrich.FromLogContext()
    //                    .CreateLogger();
    //builder.Host.UseSerilog(logger);

    var app = builder.Build();


    app.UseMiddleware<CustomExceptionHandlerMiddleware>();

    app.UseForwardedHeaders();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseCors("CorsPolicy");

     app.UseSwagger();
     app.UseSwaggerUI();
 
     app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseHttpsRedirection();
    //app.UseStaticFiles();

    //app.UseSerilogRequestLogging();

     app.UseAuthentication();
     app.UseMiddleware<PaymentWriteRateLimitMiddleware>();
     app.UseAuthorization();

    app.MapControllers();

    using (var scope = app.Services.CreateScope())
    {
        var metrics = scope.ServiceProvider.GetRequiredService<ObservabilityMetrics>();
        var indexInitializer = scope.ServiceProvider.GetRequiredService<MongoIndexInitializer>();
        indexInitializer.InitializeAsync().GetAwaiter().GetResult();
        var txService = scope.ServiceProvider.GetRequiredService<TransactionService>();
        metrics.SetCurrentOpenRounds(0);
        metrics.SetTotalRaised(0);
        metrics.RefillPendingRefundsGauge(await txService.CountPendingAsync("Refund"));
        metrics.RefillPendingSettlementsGauge(await txService.CountPendingAsync("Settlement"));
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}
