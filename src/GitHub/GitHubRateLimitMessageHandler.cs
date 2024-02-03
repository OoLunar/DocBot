using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OoLunar.DocBot.GitHub
{
    public sealed class GitHubRateLimitMessageHandler : DelegatingHandler
    {
        private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
        private readonly ILogger<GitHubRateLimitMessageHandler> _logger = NullLogger<GitHubRateLimitMessageHandler>.Instance;

        private int _rateLimitRemaining;
        private DateTimeOffset _rateLimitReset = DateTimeOffset.MinValue;

        public GitHubRateLimitMessageHandler() : base(new HttpClientHandler()) { }
        public GitHubRateLimitMessageHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }
        public GitHubRateLimitMessageHandler(HttpMessageHandler innerHandler, ILogger<GitHubRateLimitMessageHandler> logger) : base(innerHandler) => _logger = logger ?? NullLogger<GitHubRateLimitMessageHandler>.Instance;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null)
            {
                // This should never happen.
                throw new InvalidOperationException("Request URI is null.");
            }
            else if (!request.RequestUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            {
                return await base.SendAsync(request, cancellationToken);
            }

            HttpResponseMessage response;
            do
            {
                // Pre-emptively wait for ratelimits to be reset.
                await _rateLimitSemaphore.WaitAsync(cancellationToken);
                try
                {
                    if (_rateLimitRemaining == 0)
                    {
                        TimeSpan waitTime = _rateLimitReset - DateTimeOffset.UtcNow;
                        _logger.LogWarning("Hit the ratelimit. Waiting until {RateLimitReset}...", _rateLimitReset);
                        await Task.Delay(waitTime, cancellationToken);
                    }
                }
                finally
                {
                    _rateLimitSemaphore.Release();
                }

                // Make the request
                response = await base.SendAsync(request, cancellationToken);

                // Update ratelimits
                if (!response.Headers.TryGetValues("x-ratelimit-reset", out IEnumerable<string>? resetValues) || !response.Headers.TryGetValues("x-ratelimit-remaining", out IEnumerable<string>? remainingValues))
                {
                    // This should never happen.
                    throw new InvalidOperationException("Missing x-ratelimit-reset or x-ratelimit-remaining headers.");
                }
                else if (!int.TryParse(remainingValues.Single(), out int remaining) || !long.TryParse(resetValues.Single(), out long reset))
                {
                    // This should never happen.
                    throw new InvalidOperationException("Unable to parse x-ratelimit-reset or x-ratelimit-remaining headers.");
                }
                else
                {
                    _rateLimitRemaining = remaining;
                    _rateLimitReset = DateTimeOffset.FromUnixTimeSeconds(reset);
                }
            } while (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests);

            return response;
        }
    }
}
