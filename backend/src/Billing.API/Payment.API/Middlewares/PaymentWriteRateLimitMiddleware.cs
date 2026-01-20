using Microsoft.Extensions.Caching.Memory;
using Mozaiks.Auth;

namespace Payment.API.Middlewares;

public sealed class PaymentWriteRateLimitMiddleware
{
    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST",
        "PUT",
        "PATCH",
        "DELETE"
    };

    private static readonly PathString[] RateLimitedPrefixes =
    {
        new("/api/mozaiks/pay"),
        new("/api/payment"),
        new("/api/wallet"),
        new("/api/transaction"),
        new("/api/ledger")
    };

    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly IUserContextAccessor _userContextAccessor;
    private readonly ILogger<PaymentWriteRateLimitMiddleware> _logger;
    private readonly int _limitPerMinute;

    public PaymentWriteRateLimitMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        IUserContextAccessor userContextAccessor,
        IConfiguration configuration,
        ILogger<PaymentWriteRateLimitMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _userContextAccessor = userContextAccessor;
        _logger = logger;
        _limitPerMinute = Math.Max(1, configuration.GetValue<int?>("PaymentRateLimit:RequestsPerMinute") ?? 120);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!WriteMethods.Contains(context.Request.Method)
            || !IsRateLimitedPath(context.Request.Path)
            || IsExcludedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var userId = _userContextAccessor.GetUser(context.User)?.UserId
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var key = $"payment-rate:{userId}:{context.Request.Method}:{path}";

        var state = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return new RateState();
        });

        if (state.Count >= _limitPerMinute)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/problem+json";
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "RateLimited",
                message = "Too many payment write requests.",
                limit = _limitPerMinute
            });
            return;
        }

        state.Count += 1;
        _logger.LogDebug("Payment rate limit increment userId={UserId} count={Count}", userId, state.Count);

        await _next(context);
    }

    private static bool IsRateLimitedPath(PathString path)
        => RateLimitedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsExcludedPath(PathString path)
    {
        if (path.StartsWithSegments("/api/internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.Value?.IndexOf("webhook", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private sealed class RateState
    {
        public int Count { get; set; }
    }
}
