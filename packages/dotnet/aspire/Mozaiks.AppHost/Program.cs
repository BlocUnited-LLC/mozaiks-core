var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.AuthServer_Api>("authserver-api");
builder.AddProject<Projects.User_API>("users-api");
builder.AddProject<Projects.App_API>("app-api");
builder.AddProject<Projects.Notification_API>("notifications-api");



builder.AddProject<Projects.Payment_API>("payment-api");



builder.Build().Run();
