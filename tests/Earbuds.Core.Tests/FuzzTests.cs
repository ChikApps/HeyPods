// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Spec A3: "10,000 random broadcasts, zero crashes". We run 10,000 fully random inputs plus
// 10,000 mutations of valid broadcasts (more likely to reach deep code paths), for three seeds each,
// and check that whatever decodes has only sane values.

using Earbuds.Core.Ble;
using Earbuds.Core.Models;
using Xunit;

namespace Earbuds.Core.Tests;

public class FuzzTests
{
    private static readonly int?[] AllowedBattery = { null, 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

    private static readonly byte[][] Seeds =
    {
        Hex.Bytes("07 19 01 0E 20 54 AA B5 31 00 00 E0 0C A7 8A 60 4B D3 7D F4 60 4F 2C 73 E9 A7 F4"),
        Hex.Bytes("07 19 01 24 20 53 AA 98 32 00 05 49 0A B8 BF 8E 29 D8 70 12 0D A7 0C CE 77 56 00"),
        Hex.Bytes("07 19 01 0A 20 62 04 80 01 0F 40 0D 70 50 16 F2 40 83 16 BF 10 16 34 9B 74 84 E8"),
        Hex.Bytes("07 19 01 2D 20 2B F6 8F 03 14 05 E7 17 3C 7D 65 B8 F3 94 45 12 90 EB E3 24 D3 3E"),
    };

    [Theory]
    [InlineData(1)]
    [InlineData(20261114)]
    [InlineData(424242)]
    public void RandomBytes_NeverThrow_AndValuesStaySane(int seed)
    {
        var random = new Random(seed);
        var decoded = 0;
        for (var i = 0; i < 10_000; i++)
        {
            var length = random.Next(0, 300);
            var data = new byte[length];
            random.NextBytes(data);
            if (length >= 2 && random.Next(4) == 0)
            {
                data[0] = 0x07; // make the 0x07 path likely
                data[1] = (byte)(random.Next(3) == 0 ? 0x19 : random.Next(256));
            }

            decoded += Check(data) ? 1 : 0;
        }

        Assert.True(decoded >= 0);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(99)]
    [InlineData(31337)]
    public void MutatedValidBroadcasts_NeverThrow_AndValuesStaySane(int seed)
    {
        var random = new Random(seed);
        var decoded = 0;
        for (var i = 0; i < 10_000; i++)
        {
            var data = (byte[])Seeds[random.Next(Seeds.Length)].Clone();
            var mutations = random.Next(1, 6);
            for (var m = 0; m < mutations; m++)
            {
                switch (random.Next(4))
                {
                    case 0: data[random.Next(data.Length)] = (byte)random.Next(256); break;            // change a byte
                    case 1: data[random.Next(2, 11)] ^= (byte)(1 << random.Next(8)); break;             // flip a status bit
                    case 2: data = data[..random.Next(data.Length + 1)]; break;                        // truncate
                    default: data = data.Concat(new byte[random.Next(1, 40)]).ToArray(); break;        // extend
                }

                if (data.Length < 11)
                {
                    break;
                }
            }

            decoded += Check(data) ? 1 : 0;
        }

        Assert.True(decoded > 100, $"mutations should still decode sometimes, got {decoded}");
    }

    private static bool Check(byte[] data)
    {
        var ok = ProximityPairingParser.TryParse(data, out var a); // must not throw
        if (!ok)
        {
            Assert.Null(a);
            return false;
        }

        Assert.NotNull(a);
        Assert.Contains(a!.LeftBattery, AllowedBattery);
        Assert.Contains(a.RightBattery, AllowedBattery);
        Assert.Contains(a.CaseBattery, AllowedBattery);
        Assert.Contains(a.HeadsetBattery, AllowedBattery);
        Assert.InRange(a.InEarCount, 0, 2);
        Assert.True(Enum.IsDefined(a.LeftEar));
        Assert.True(Enum.IsDefined(a.RightEar));
        Assert.True(Enum.IsDefined(a.Lid));
        if (a.Layout == PodsLayout.Single)
        {
            Assert.Null(a.LeftBattery);
            Assert.Null(a.CaseBattery);
        }

        return true;
    }
}
