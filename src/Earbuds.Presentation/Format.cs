// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace Earbuds.Presentation;

/// <summary>Number and time texts.</summary>
public static class Format
{
    /// <summary>Left-to-right mark. Keeps "80%" as one left-to-right unit inside Hebrew text (the texts file: "%80" must never appear).</summary>
    public const char Lrm = '‎';

    /// <summary>Right-to-left mark. Starts a mixed string so Windows lays it out right-to-left.</summary>
    public const char Rlm = '‏';

    /// <summary>"80%" or "--". Plain text, for places that are already left-to-right (inside the ring).</summary>
    public static string Percent(int? value) => value is { } v ? v.ToString(CultureInfo.InvariantCulture) + "%" : "--";

    /// <summary>"80%" wrapped in LRM marks, for use inside Hebrew sentences.</summary>
    public static string PercentInText(int? value) => Lrm + Percent(value) + Lrm;

    private static string N(long n) => n.ToString(CultureInfo.InvariantCulture);

    /// <summary>main.updated.*: "עודכן עכשיו" / "לפני 12 שניות" / "לפני דקה" / "לפני 5 דקות" / hours.</summary>
    public static string Updated(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        var seconds = (long)age.TotalSeconds;
        if (seconds < 5)
        {
            return S.UpdatedNow;
        }

        if (seconds < 60)
        {
            return string.Format(CultureInfo.InvariantCulture, S.UpdatedSeconds, N(seconds));
        }

        var minutes = seconds / 60;
        if (minutes < 60)
        {
            return minutes == 1 ? S.UpdatedMinute : string.Format(CultureInfo.InvariantCulture, S.UpdatedMinutes, N(minutes));
        }

        var hours = minutes / 60;
        return hours switch
        {
            1 => S.UpdatedHour,
            2 => S.UpdatedTwoHours,
            _ => string.Format(CultureInfo.InvariantCulture, S.UpdatedHours, N(hours)),
        };
    }

    /// <summary>st.lastseen.*: "נראו לאחרונה לפני דקה / X דקות / שעה / שעתיים / X שעות". Under a minute counts as one minute.</summary>
    public static string LastSeen(TimeSpan age)
    {
        var minutes = Math.Max(1, (long)age.TotalMinutes);
        if (minutes < 60)
        {
            return minutes == 1 ? S.StLastSeenMinute : string.Format(CultureInfo.InvariantCulture, S.StLastSeenMinutes, N(minutes));
        }

        var hours = minutes / 60;
        return hours switch
        {
            1 => S.StLastSeenHour,
            2 => S.StLastSeenTwoHours,
            _ => string.Format(CultureInfo.InvariantCulture, S.StLastSeenHours, N(hours)),
        };
    }
}
