// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// "Check for update now" button (2026-11-14). The ONLY network access of the app,
// and only when the user clicks. No automatic checks, no setting. The core has no network code:
// the request itself is made by an IReleaseSource (Windows: GitHubReleaseSource).

namespace Earbuds.Core.Updates;

/// <summary>Where the app's releases live. One variable.</summary>
public static class UpdateConfig
{
    /// <summary>"owner/repo" on github.com.</summary>
    public const string Repository = "ChikApps/HeyPods";

    public static bool IsConfigured(string repository = Repository) =>
        !string.Equals(repository, "OWNER/REPO", StringComparison.Ordinal) && IsValidRepository(repository);

    /// <summary>"owner/repo" with GitHub's allowed characters only.</summary>
    public static bool IsValidRepository(string repository)
    {
        var parts = repository.Split('/');
        return parts.Length == 2 && parts.All(p =>
            p.Length is > 0 and <= 100 && p != "." && p != ".." &&
            p.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'));
    }

    /// <summary>The page that redirects to the newest release: https://github.com/{repo}/releases/latest.</summary>
    public static Uri LatestReleaseUrl(string repository = Repository) => new($"https://github.com/{repository}/releases/latest");
}

/// <summary>A version like 1.2.3 (up to 4 numbers) with an optional "-beta.1" pre-release part.</summary>
public sealed record AppVersion(IReadOnlyList<int> Numbers, string? PreRelease) : IComparable<AppVersion>
{
    /// <summary>Parses "1.2.3", "v1.2", "V2.0.0-beta.1". Rejects anything else.</summary>
    public static bool TryParse(string? text, out AppVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 64)
        {
            return false;
        }

        var s = text.Trim();
        if (s[0] is 'v' or 'V')
        {
            s = s[1..];
        }

        string? pre = null;
        var dash = s.IndexOf('-');
        if (dash >= 0)
        {
            pre = s[(dash + 1)..];
            s = s[..dash];
            if (pre.Length == 0 || !pre.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-'))
            {
                return false;
            }
        }

        var parts = s.Split('.');
        if (parts.Length is < 1 or > 4)
        {
            return false;
        }

        var numbers = new List<int>(4);
        foreach (var p in parts)
        {
            if (p.Length is 0 or > 9 || !p.All(char.IsAsciiDigit))
            {
                return false;
            }

            numbers.Add(int.Parse(p, System.Globalization.CultureInfo.InvariantCulture));
        }

        version = new AppVersion(numbers, pre);
        return true;
    }

    public int CompareTo(AppVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        for (var i = 0; i < 4; i++)
        {
            var a = i < Numbers.Count ? Numbers[i] : 0;
            var b = i < other.Numbers.Count ? other.Numbers[i] : 0;
            if (a != b)
            {
                return a.CompareTo(b);
            }
        }

        // 1.0.0-beta < 1.0.0
        return (PreRelease, other.PreRelease) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            _ => string.CompareOrdinal(PreRelease, other.PreRelease),
        };
    }

    public override string ToString() => string.Join('.', Numbers) + (PreRelease is null ? string.Empty : "-" + PreRelease);

    public static bool operator >(AppVersion a, AppVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(AppVersion a, AppVersion b) => a.CompareTo(b) < 0;
}

/// <summary>Fetches where "releases/latest" points. Implemented outside the core (it is the only network code).</summary>
public interface IReleaseSource
{
    /// <summary>
    /// Requests https://github.com/{repo}/releases/latest without following the redirect and returns the
    /// redirect target (…/releases/tag/{tag}), or null when there is none (no release yet).
    /// Throws on network errors.
    /// </summary>
    Task<Uri?> GetLatestReleaseLocationAsync(Uri latestUrl, CancellationToken cancellationToken);
}

public enum UpdateStatus
{
    /// <summary>Repository address not set yet in <see cref="UpdateConfig"/>.</summary>
    NotConfigured,
    UpToDate,
    UpdateAvailable,
    /// <summary>No release published yet.</summary>
    NoRelease,
    /// <summary>No internet, blocked by a filter, timeout, or an unexpected answer.</summary>
    Failed,
}

/// <param name="DownloadPage">Only for <see cref="UpdateStatus.UpdateAvailable"/>: the release page to open in the browser.</param>
public sealed record UpdateResult(UpdateStatus Status, AppVersion? Latest = null, Uri? DownloadPage = null);

/// <summary>The button's logic.</summary>
public sealed class UpdateChecker
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly IReleaseSource _source;
    private readonly string _repository;

    public UpdateChecker(IReleaseSource source, string repository = UpdateConfig.Repository)
    {
        _source = source;
        _repository = repository;
    }

    public async Task<UpdateResult> CheckAsync(AppVersion current, CancellationToken cancellationToken = default)
    {
        if (!UpdateConfig.IsConfigured(_repository))
        {
            return new UpdateResult(UpdateStatus.NotConfigured);
        }

        Uri? location;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            location = await _source.GetLatestReleaseLocationAsync(UpdateConfig.LatestReleaseUrl(_repository), timeout.Token)
                .ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new UpdateResult(UpdateStatus.Failed);
        }

        if (location is null)
        {
            return new UpdateResult(UpdateStatus.NoRelease);
        }

        if (!TryReadRelease(location, _repository, out var latest, out var page))
        {
            return new UpdateResult(UpdateStatus.Failed);
        }

        return latest! > current
            ? new UpdateResult(UpdateStatus.UpdateAvailable, latest, page)
            : new UpdateResult(UpdateStatus.UpToDate, latest);
    }

    /// <summary>
    /// Accepts only https://github.com/{repo}/releases/tag/{version} for OUR repository, so the browser is
    /// never sent to an address that came from somewhere else. Returns the parsed version and a clean page URL.
    /// </summary>
    public static bool TryReadRelease(Uri location, string repository, out AppVersion? version, out Uri? page)
    {
        version = null;
        page = null;
        if (!location.IsAbsoluteUri || location.Scheme != Uri.UriSchemeHttps || !location.IsDefaultPort ||
            !string.Equals(location.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(location.UserInfo))
        {
            return false;
        }

        var prefix = $"/{repository}/releases/tag/";
        var path = location.AbsolutePath;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var tag = Uri.UnescapeDataString(path[prefix.Length..]);
        if (tag.Contains('/') || !AppVersion.TryParse(tag, out version))
        {
            return false;
        }

        page = new Uri($"https://github.com/{repository}/releases/tag/{Uri.EscapeDataString(tag)}");
        return true;
    }
}
