namespace AuthServer.Api.Services
{
    public enum AppModuleProxyFailureKind
    {
        NotConfigured,
        InvalidConfiguration,
        CircuitOpen,
        Timeout,
        NetworkError,
        UpstreamError,
        ResponseTooLarge,
        Unknown
    }

    public sealed class AppModuleProxyResult
    {
        public bool Succeeded { get; init; }
        public int? UpstreamStatusCode { get; init; }
        public string? ContentType { get; init; }
        public byte[]? Body { get; init; }

        public AppModuleProxyFailureKind? FailureKind { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }

        public static AppModuleProxyResult Success(int upstreamStatusCode, string? contentType, byte[] body)
            => new()
            {
                Succeeded = true,
                UpstreamStatusCode = upstreamStatusCode,
                ContentType = contentType,
                Body = body
            };

        public static AppModuleProxyResult Fail(AppModuleProxyFailureKind kind, string errorCode, string errorMessage, int? upstreamStatusCode = null)
            => new()
            {
                Succeeded = false,
                FailureKind = kind,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                UpstreamStatusCode = upstreamStatusCode
            };
    }
}

