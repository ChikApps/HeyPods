// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

namespace Earbuds.Core.Models;

/// <summary>Drawing family used by the UI (three of our own drawings plus a generic one).</summary>
public enum ModelFamily
{
    /// <summary>Unknown model: generic drawing.</summary>
    Generic,
    /// <summary>Short-stem earbuds (AirPods 1, 2, 3, 4, 5).</summary>
    Stem,
    /// <summary>Earbuds with ear tips (AirPods Pro family).</summary>
    Pro,
    /// <summary>Over-ear headphones (AirPods Max family). No case, no left/right.</summary>
    Max,
}

/// <summary>How the broadcast is laid out.</summary>
public enum PodsLayout
{
    /// <summary>Left pod, right pod and a case.</summary>
    Dual,
    /// <summary>One battery value, no case (over-ear headphones).</summary>
    Single,
}

/// <summary>How "being worn" is read for <see cref="PodsLayout.Single"/> models.</summary>
public enum WearBits
{
    /// <summary>Dual models: per-pod in-ear bits (status bits 1 and 3, see the parser).</summary>
    PerPod,
    /// <summary>Status bit 5 = worn (AirPods Max, Max USB-C).</summary>
    Bit5,
    /// <summary>Status bit 1 or bit 3 = worn (AirPods Max 2).</summary>
    Bit1OrBit3,
}

/// <summary>One row of the model table.</summary>
/// <param name="Code">
/// Model code as the two bytes appear in the broadcast, read big-endian: bytes "14 20" = 0x1420.
/// This is the convention of LibrePods, OpenPods, CAPod and JamesCJ60/WinPods.
/// AirPodsDesktop reads the same two bytes little-endian (0x2014); see <see cref="ModelCatalog.ToWindowsProductId"/>.
/// </param>
/// <param name="Name">Display name of the model (describing the hardware is fine; it is not our app name).</param>
/// <param name="Sources">Where the code comes from: project, file and line, at the commits listed in docs/models.md.</param>
public sealed record ModelInfo(
    ushort Code,
    string Name,
    ModelFamily Family,
    PodsLayout Layout,
    WearBits Wear,
    IReadOnlyList<string> Sources)
{
    /// <summary>True when the code is not in our table.</summary>
    public bool IsUnknown => Sources.Count == 0;

    /// <summary>True when the model has a charging case (Dual layout).</summary>
    public bool HasCase => Layout == PodsLayout.Dual;
}

/// <summary>
/// Table of AirPods models. Every row was taken from open-source code, never from memory.
/// A model without a source is not in the table and is shown as "unknown model".
/// Beats and non-Apple earbuds are deliberately not here (out of scope for phase 1).
/// </summary>
public static class ModelCatalog
{
    // Source abbreviations (full commit hashes in docs/models.md):
    //   APD = SpriteOvO/AirPodsDesktop  Source/Core/AppleCP.cpp      (GPL-3.0) - codes written little-endian there
    //   LP  = kavishdevar/librepods     linux/ble/blemanager.cpp      (GPL-3.0)
    //   OP  = adolfintel/OpenPods       app/src/main/java/com/dosse/airpods/pods/PodsStatus.java (GPL-3.0)
    //   CAP = d4rken-org/capod          app/src/main/java/eu/darken/capod/pods/core/apple/ble/devices/airpods/*.kt (GPL-3.0)
    //   JCJ = JamesCJ60/WinPods         WinPods/Models/AirPodsProductCatalogue.cs (GPL-3.0)
    private static readonly ModelInfo[] Rows =
    {
        Row(0x0220, "AirPods", ModelFamily.Stem,
            "APD AppleCP.cpp:45 (0x2002)", "LP blemanager.cpp:12", "OP PodsStatus.java:76", "CAP AirPodsGen1.kt:83", "JCJ AirPodsProductCatalogue.cs:10"),
        Row(0x0F20, "AirPods 2", ModelFamily.Stem,
            "APD AppleCP.cpp:47 (0x200F)", "LP blemanager.cpp:13", "OP PodsStatus.java:78", "CAP AirPodsGen2.kt:83", "JCJ AirPodsProductCatalogue.cs:11"),
        Row(0x1320, "AirPods 3", ModelFamily.Stem,
            "APD AppleCP.cpp:49 (0x2013)", "LP blemanager.cpp:14", "OP PodsStatus.java:80", "CAP AirPodsGen3.kt:94", "JCJ AirPodsProductCatalogue.cs:12"),
        Row(0x1920, "AirPods 4", ModelFamily.Stem,
            "APD AppleCP.cpp:51 (0x2019)", "LP blemanager.cpp:15", "CAP AirPodsGen4.kt:94", "JCJ AirPodsProductCatalogue.cs:13"),
        Row(0x1B20, "AirPods 4 (ANC)", ModelFamily.Stem,
            "APD AppleCP.cpp:53 (0x201B)", "LP blemanager.cpp:16", "CAP AirPodsGen4Anc.kt:92", "JCJ AirPodsProductCatalogue.cs:14"),
        // AirPods 5 has two codes in both sources. CAPod names 0x3020 "Gen 5 Wireless Case" and 0x3620 "Gen 5";
        // AirPodsDesktop maps both to AirPods_5 (display name "AirPods 5", Source/Core/Base.h:106).
        Row(0x3020, "AirPods 5", ModelFamily.Stem,
            "APD AppleCP.cpp:55 (0x2030)", "CAP AirPodsGen5Wireless.kt:92"),
        Row(0x3620, "AirPods 5", ModelFamily.Stem,
            "APD AppleCP.cpp:56 (0x2036)", "CAP AirPodsGen5.kt:92"),
        Row(0x0E20, "AirPods Pro", ModelFamily.Pro,
            "APD AppleCP.cpp:58 (0x200E)", "LP blemanager.cpp:19", "OP PodsStatus.java:82", "CAP AirPodsPro.kt:100", "JCJ AirPodsProductCatalogue.cs:15"),
        Row(0x1420, "AirPods Pro 2", ModelFamily.Pro,
            "APD AppleCP.cpp:60 (0x2014)", "LP blemanager.cpp:20", "OP PodsStatus.java:84", "CAP AirPodsPro2.kt:97", "JCJ AirPodsProductCatalogue.cs:16"),
        Row(0x2420, "AirPods Pro 2 (USB-C)", ModelFamily.Pro,
            "APD AppleCP.cpp:62 (0x2024)", "LP blemanager.cpp:21", "OP PodsStatus.java:84", "CAP AirPodsPro2Usbc.kt:97", "JCJ AirPodsProductCatalogue.cs:17"),
        Row(0x2720, "AirPods Pro 3", ModelFamily.Pro,
            "APD AppleCP.cpp:64 (0x2027)", "OP PodsStatus.java:86", "CAP AirPodsPro3.kt:100", "JCJ AirPodsProductCatalogue.cs:18"),
        Row(0x0A20, "AirPods Max", ModelFamily.Max, PodsLayout.Single, WearBits.Bit5,
            "APD AppleCP.cpp:66 (0x200A)", "LP blemanager.cpp:17", "OP PodsStatus.java:88 ('A')", "CAP AirPodsMax.kt:82", "JCJ AirPodsProductCatalogue.cs:19"),
        Row(0x1F20, "AirPods Max (USB-C)", ModelFamily.Max, PodsLayout.Single, WearBits.Bit5,
            "APD AppleCP.cpp:68 (0x201F)", "LP blemanager.cpp:18", "CAP AirPodsMaxUsbc.kt:82", "JCJ AirPodsProductCatalogue.cs:20"),
        Row(0x2D20, "AirPods Max 2", ModelFamily.Max, PodsLayout.Single, WearBits.Bit1OrBit3,
            "CAP AirPodsMax2.kt:92", "JCJ AirPodsProductCatalogue.cs:21"),
    };

    private static readonly Dictionary<ushort, ModelInfo> ByCode = Rows.ToDictionary(r => r.Code);

    /// <summary>All known models, in table order.</summary>
    public static IReadOnlyList<ModelInfo> All => Rows;

    /// <summary>Looks up a model code. Unknown codes get a generic Dual entry (spec: "unknown model", battery still shown).</summary>
    public static ModelInfo Find(ushort code) =>
        ByCode.TryGetValue(code, out var info)
            ? info
            : new ModelInfo(code, "Unknown", ModelFamily.Generic, PodsLayout.Dual, WearBits.PerPod, Array.Empty<string>());

    /// <summary>True when the code is in the table.</summary>
    public static bool IsKnown(ushort code) => ByCode.ContainsKey(code);

    /// <summary>
    /// Product ID that Windows reports for the paired (classic Bluetooth) device of this model.
    /// AirPodsDesktop passes the Windows ProductId straight into the same lookup it uses for the
    /// little-endian BLE code (Source/Core/AirPods.cpp:178 and :335-338), so it is the byte-swapped code.
    /// Not verified on hardware here.
    /// </summary>
    public static ushort ToWindowsProductId(ushort code) => (ushort)((code >> 8) | (code << 8));

    /// <summary>Inverse of <see cref="ToWindowsProductId"/>.</summary>
    public static ushort FromWindowsProductId(ushort productId) => ToWindowsProductId(productId);

    private static ModelInfo Row(ushort code, string name, ModelFamily family, params string[] sources) =>
        new(code, name, family, PodsLayout.Dual, WearBits.PerPod, sources);

    private static ModelInfo Row(ushort code, string name, ModelFamily family, PodsLayout layout, WearBits wear, params string[] sources) =>
        new(code, name, family, layout, wear, sources);
}
