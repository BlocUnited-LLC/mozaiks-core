using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AuthServer.Api.Shared
{
    public class StructuredLogContext
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? AppId { get; set; }
        public string? ProposalId { get; set; }
        public string? RoundId { get; set; }
    }

    public class StructuredLogRecord
    {
        public string Ts { get; set; } = DateTime.UtcNow.ToString("O");
        public string Level { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;
        public StructuredLogContext Context { get; set; } = new();
        public object Payload { get; set; } = new { };
    }

    public class StructuredLogEmitter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private readonly ILogger<StructuredLogEmitter> _logger;

        public StructuredLogEmitter(ILogger<StructuredLogEmitter> logger)
        {
            _logger = logger;
        }

        public void Info(string message, StructuredLogContext context, object? payload = null)
            => Log(LogLevel.Information, "INFO", message, context, payload);

        public void Warn(string message, StructuredLogContext context, object? payload = null)
            => Log(LogLevel.Warning, "WARN", message, context, payload);

        public void Error(string message, StructuredLogContext context, object? payload = null)
            => Log(LogLevel.Error, "ERROR", message, context, payload);

        private void Log(LogLevel level, string levelLabel, string message, StructuredLogContext context, object? payload)
        {
            var record = new StructuredLogRecord
            {
                Ts = DateTime.UtcNow.ToString("O"),
                Level = levelLabel,
                Message = message,
                Context = context,
                Payload = payload ?? new { }
            };

            var json = JsonSerializer.Serialize(record, JsonOptions);
            _logger.Log(level, "{StructuredLog}", json);
        }
    }
}
