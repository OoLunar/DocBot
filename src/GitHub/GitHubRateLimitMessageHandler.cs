using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OoLunar.DocBot.GitHub
{
    public sealed class GitHubRateLimitMessageHandler : DelegatingHandler
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly ILogger<GitHubRateLimitMessageHandler> _logger;

        public GitHubRateLimitMessageHandler(HttpMessageHandler innerHandler, ILogger<GitHubRateLimitMessageHandler>? logger = null) : base(innerHandler)
        {
            _logger = logger ?? NullLogger<GitHubRateLimitMessageHandler>.Instance;
            _semaphore = new SemaphoreSlim(1, 1);
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null && request.RequestUri.Host != "api.github.com")
            {
                return base.Send(request, cancellationToken);
            }

            _semaphore.Wait(cancellationToken);
            try
            {
                HttpResponseMessage responseMessage = base.Send(request, cancellationToken);
                if (IsRatelimited(responseMessage, out TimeSpan? ratelimitExpires))
                {
                    _logger.LogInformation("Rate limit reached. Waiting {TimeSpan} before continuing.", ratelimitExpires);
                    ValueTask<bool> ratelimit = new PeriodicTimer(ratelimitExpires.Value).WaitForNextTickAsync(cancellationToken);
                    if (!ratelimit.IsCompleted)
                    {
                        ratelimit.AsTask().GetAwaiter().GetResult();
                    }

                    responseMessage = base.Send(request, cancellationToken);
                }

                return responseMessage;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null && request.RequestUri.Host != "api.github.com")
            {
                return await base.SendAsync(request, cancellationToken);
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                HttpResponseMessage responseMessage = await base.SendAsync(request, cancellationToken);
                if (IsRatelimited(responseMessage, out TimeSpan? ratelimitExpires))
                {
                    _logger.LogInformation("Rate limit reached. Waiting {TimeSpan} before continuing.", ratelimitExpires);
                    await new PeriodicTimer(ratelimitExpires.Value).WaitForNextTickAsync(cancellationToken);
                    responseMessage = await base.SendAsync(request, cancellationToken);
                }

                return responseMessage;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static bool IsRatelimited(HttpResponseMessage responseMessage, [NotNullWhen(true)] out TimeSpan? ratelimitExpires)
        {
            ratelimitExpires = null;
            if (responseMessage.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? remaining)
                && remaining.Any()
                && int.TryParse(remaining.First(), out int remainingRequests)
                && remainingRequests == 0
                && responseMessage.Headers.TryGetValues("X-RateLimit-Reset", out IEnumerable<string>? reset)
                && reset.Any()
                && long.TryParse(reset.First(), out long resetTime))
            {
                ratelimitExpires = TimeSpan.FromSeconds(resetTime - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                return true;
            }

            return false;
        }
    }
}
