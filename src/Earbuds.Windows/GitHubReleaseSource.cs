// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// THE ONLY NETWORK CODE IN THE APP (2026-11-14): runs only when the user clicks
// "check for update now". One HTTPS request to github.com, no cookies, no personal data, no body sent.
// We ask https://github.com/{repo}/releases/latest and read where it redirects (…/releases/tag/vX.Y.Z),
// instead of the JSON API on api.github.com: smaller answer, no rate limit, one host to allow in a filter.
// NOT RUN against the real site (no repository yet; this environment has no route to it either).

using System.Net;
using System.Net.Http.Headers;
using Earbuds.Core.Updates;

namespace Earbuds.Windows;

/// <summary><see cref="IReleaseSource"/> over HttpClient.</summary>
public sealed class GitHubReleaseSource : IReleaseSource
{
    public async Task<Uri?> GetLatestReleaseLocationAsync(Uri latestUrl, CancellationToken cancellationToken)
    {
        if (latestUrl.Scheme != Uri.UriSchemeHttps || !string.Equals(latestUrl.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only https://github.com is allowed.", nameof(latestUrl));
        }

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseDefaultCredentials = false,
        };
        using var client = new HttpClient(handler) { Timeout = UpdateChecker.Timeout };
        using var request = new HttpRequestMessage(HttpMethod.Head, latestUrl);
        // Generic client name: nothing about the user (GitHub rejects requests without a User-Agent).
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("update-check", "1.0"));

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.MovedPermanently or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
        {
            var location = response.Headers.Location;
            return location is null ? null : location.IsAbsoluteUri ? location : new Uri(latestUrl, location);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null; // repository has no release yet
        }

        throw new HttpRequestException($"Unexpected status {(int)response.StatusCode}");
    }
}
