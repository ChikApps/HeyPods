// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only

using Earbuds.Core.Models;
using Xunit;

namespace Earbuds.Core.Tests;

public class ModelCatalogTests
{
    [Theory] // spec §4 "must be in the table"
    [InlineData(0x0220, "AirPods", ModelFamily.Stem)]
    [InlineData(0x0F20, "AirPods 2", ModelFamily.Stem)]
    [InlineData(0x1320, "AirPods 3", ModelFamily.Stem)]
    [InlineData(0x1920, "AirPods 4", ModelFamily.Stem)]
    [InlineData(0x1B20, "AirPods 4 (ANC)", ModelFamily.Stem)]
    [InlineData(0x0E20, "AirPods Pro", ModelFamily.Pro)]
    [InlineData(0x1420, "AirPods Pro 2", ModelFamily.Pro)]
    [InlineData(0x2420, "AirPods Pro 2 (USB-C)", ModelFamily.Pro)]
    [InlineData(0x2720, "AirPods Pro 3", ModelFamily.Pro)]
    [InlineData(0x0A20, "AirPods Max", ModelFamily.Max)]
    [InlineData(0x1F20, "AirPods Max (USB-C)", ModelFamily.Max)]
    [InlineData(0x2D20, "AirPods Max 2", ModelFamily.Max)]
    [InlineData(0x3020, "AirPods 5", ModelFamily.Stem)]
    [InlineData(0x3620, "AirPods 5", ModelFamily.Stem)]
    public void RequiredModels(int code, string name, ModelFamily family)
    {
        var m = ModelCatalog.Find((ushort)code);
        Assert.False(m.IsUnknown);
        Assert.Equal(name, m.Name);
        Assert.Equal(family, m.Family);
        Assert.Equal(family == ModelFamily.Max ? PodsLayout.Single : PodsLayout.Dual, m.Layout);
    }

    [Fact]
    public void EveryRowHasAtLeastTwoSources_AndUniqueCode()
    {
        Assert.Equal(ModelCatalog.All.Count, ModelCatalog.All.Select(m => m.Code).Distinct().Count());
        foreach (var m in ModelCatalog.All)
        {
            Assert.True(m.Sources.Count >= 2, $"{m.Name} needs two independent sources");
            Assert.All(m.Sources, s => Assert.Matches(@"^(APD|LP|OP|CAP|JCJ) \S+:\d+", s));
        }
    }

    [Fact]
    public void NoBeatsInTable() => Assert.DoesNotContain(ModelCatalog.All, m => m.Name.Contains("Beats"));

    [Fact]
    public void UnknownCode_GenericDual()
    {
        var m = ModelCatalog.Find(0x1220); // Beats Fit Pro in other projects: out of scope -> unknown here
        Assert.True(m.IsUnknown);
        Assert.Equal(ModelFamily.Generic, m.Family);
        Assert.Equal(PodsLayout.Dual, m.Layout);
    }

    [Fact] // AirPodsDesktop: Windows ProductId 0x2014 == BLE bytes "14 20"
    public void WindowsProductId_IsByteSwapped()
    {
        Assert.Equal(0x2014, ModelCatalog.ToWindowsProductId(0x1420));
        Assert.Equal(0x1420, ModelCatalog.FromWindowsProductId(0x2014));
    }

    [Fact]
    public void Colors()
    {
        Assert.Equal(DeviceColor.White, DeviceColors.TryGet(0x00));
        Assert.Equal(DeviceColor.Yellow, DeviceColors.TryGet(0x0C));
        Assert.Null(DeviceColors.TryGet(0x11));
    }
}
