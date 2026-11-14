// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Real broadcasts come from the tests of CAPod (GPL-3.0, d4rken-org/capod @ d066460,
// app/src/test/java/eu/darken/capod/pods/core/apple/ble/devices/airpods/<File>:<line>).
// Expected values are the ones CAPod asserts there, except where our documented rules differ
// (battery nibbles 11-14 -> unknown; one combined EarState per pod).

using Earbuds.Core.Ble;
using Earbuds.Core.Models;
using Xunit;

namespace Earbuds.Core.Tests;

public class ParserTests
{
    internal static byte[] Packet(ushort model, byte status, byte pods, byte flagsCase, byte lid = 0x31, byte color = 0x00, byte prefix = 0x01, byte conn = 0x04)
    {
        var data = new byte[27];
        data[0] = 0x07;
        data[1] = 0x19;
        data[2] = prefix;
        data[3] = (byte)(model >> 8);
        data[4] = (byte)model;
        data[5] = status;
        data[6] = pods;
        data[7] = flagsCase;
        data[8] = lid;
        data[9] = color;
        data[10] = conn;
        return data;
    }

    private static PodsAdvertisement Parse(string hex)
    {
        Assert.True(ProximityPairingParser.TryParse(Hex.Bytes(hex), out var result));
        return result!;
    }

    // ---------- real broadcasts ----------

    [Fact] // AirPodsProTest.kt:16 "default changed and in case"
    public void AirPodsPro_BothInCase_Flipped()
    {
        var a = Parse("07 19 01 0E 20 54 AA B5 31 00 00 E0 0C A7 8A 60 4B D3 7D F4 60 4F 2C 73 E9 A7 F4");
        Assert.Equal(0x0E20, a.ModelCode);
        Assert.Equal("AirPods Pro", a.Model.Name);
        Assert.Equal(ModelFamily.Pro, a.Model.Family);
        Assert.True(a.ValuesFlipped);
        Assert.Equal(PodSide.Left, a.PrimarySide); // CAPod: isLeftPodMicrophone = true
        Assert.Equal(100, a.LeftBattery);
        Assert.Equal(100, a.RightBattery);
        Assert.Equal(50, a.CaseBattery);
        Assert.True(a.LeftCharging);
        Assert.True(a.RightCharging);
        Assert.False(a.CaseCharging);
        Assert.Equal(EarState.InCase, a.LeftEar);
        Assert.Equal(EarState.InCase, a.RightEar);
        Assert.Equal(LidState.Open, a.Lid);
        Assert.Equal(DeviceColor.White, a.Color);
    }

    [Fact] // AirPodsProTest.kt:47 "AirPods from my downstairs neighbour"
    public void AirPodsPro_UnknownValuesAreNull()
    {
        var a = Parse("07 19 01 0E 20 00 F3 8F 02 00 04 79 C6 3F F9 C3 15 D9 11 A1 3C B1 58 66 B9 8B 67");
        Assert.Equal(PodSide.Right, a.PrimarySide);
        Assert.Null(a.LeftBattery);
        Assert.Equal(30, a.RightBattery);
        Assert.Null(a.CaseBattery);
        Assert.False(a.LeftCharging || a.RightCharging || a.CaseCharging);
        Assert.Equal(LidState.NotInCase, a.Lid);
    }

    [Theory] // AirPodsProTest.kt:84-144, data from OpenPods issue #34 (all-zero encrypted part)
    [InlineData("0719010e202b668f01000500000000000000000000000000000000")]
    [InlineData("0719010e202b668f01000400000000000000000000000000000000")]
    [InlineData("0719010e200b668f01000500000000000000000000000000000000")]
    [InlineData("0719010e2003668f01000500000000000000000000000000000000")]
    [InlineData("0719010e2001668f01000500000000000000000000000000000000")]
    [InlineData("0719010e2009668f01000500000000000000000000000000000000")]
    public void AirPodsPro_OpenPodsIssue34(string hex)
    {
        var a = Parse(hex);
        Assert.Equal(60, a.LeftBattery);
        Assert.Equal(60, a.RightBattery);
        Assert.Null(a.CaseBattery);
        Assert.False(a.LeftCharging || a.RightCharging || a.CaseCharging);
    }

    [Fact] // AirPodsGen1Test.kt:16
    public void AirPods1_OneUnknownPod_CaseCharging_LidOpen()
    {
        var a = Parse("07 19 01 02 20 55 AF 56 31 00 00 6F E4 DF 10 AF 10 60 81 03 3B 76 D9 C7 11 22 88");
        Assert.Equal("AirPods", a.Model.Name);
        Assert.Equal(100, a.LeftBattery);
        Assert.Null(a.RightBattery);
        Assert.Equal(60, a.CaseBattery);
        Assert.True(a.CaseCharging);
        Assert.False(a.LeftCharging);
        Assert.True(a.RightCharging);
        Assert.Equal(LidState.Open, a.Lid);
        Assert.NotEqual(EarState.InEar, a.LeftEar);
        Assert.NotEqual(EarState.InEar, a.RightEar);
    }

    [Fact] // AirPodsGen2Test.kt:15 "random Neighbor AirPodsGen2"
    public void AirPods2_RightInEar_NotInCase()
    {
        var a = Parse("07 19 01 0F 20 02 F9 8F 01 00 05 F2 7E 14 E0 54 0A 53 69 5B 7D F2 15 1F D7 B1 12");
        Assert.Null(a.LeftBattery);
        Assert.Equal(90, a.RightBattery);
        Assert.Null(a.CaseBattery);
        Assert.Equal(EarState.OutOfEar, a.LeftEar);
        Assert.Equal(EarState.InEar, a.RightEar);
        Assert.Equal(LidState.NotInCase, a.Lid);
        Assert.Equal(0x05, a.ConnectionStateByte); // CAPod: MUSIC
        Assert.Equal(1, a.InEarCount);
    }

    [Fact] // AirPodsGen3Test.kt:15
    public void AirPods3_InCaseCharging()
    {
        var a = Parse("07 19 01 13 20 75 AA B9 31 00 04 67 9A 57 DF BE F6 90 52 B0 04 1F 8D 89 DA F4 9E");
        Assert.Equal("AirPods 3", a.Model.Name);
        Assert.Equal(100, a.LeftBattery);
        Assert.Equal(100, a.RightBattery);
        Assert.Equal(90, a.CaseBattery);
        Assert.True(a.LeftCharging && a.RightCharging);
        Assert.False(a.CaseCharging);
        Assert.Equal(LidState.Open, a.Lid);
        Assert.Equal(EarState.InCase, a.LeftEar);
        Assert.Equal(EarState.InCase, a.RightEar);
    }

    [Theory] // both pods worn
    [InlineData("07 19 01 13 20 2B 88 8F 01 00 08 E2 0E 84 37 C5 98 16 D4 B7 37 ED 23 8B 08 EA A1", "AirPods 3", 80, 80)]        // Gen3Test.kt:49
    [InlineData("07 19 01 19 20 2B 33 8F 11 00 04 59 D4 57 20 0F 1C 13 38 B2 00 74 E9 DD 70 D7 A5", "AirPods 4", 30, 30)]        // Gen4Test.kt:16
    [InlineData("07 19 01 1B 20 0B 9A 8F 10 00 04 43 DF EC 1D D3 F1 C3 F4 A1 9B 29 26 B9 E7 3A A0", "AirPods 4 (ANC)", 90, 100)] // Gen4AncTest.kt:15
    [InlineData("07 19 01 14 20 2B 9A 8F 01 00 04 0F 26 1A C4 2B FA 2F B9 B6 08 CD 60 CB DF 75 AB", "AirPods Pro 2", 100, 90)]   // Pro2Test.kt:54
    [InlineData("07 19 01 24 20 0B 99 8F 11 00 04 BD A7 3B FF 2D 8A 3C AF 9B 1A 7C 74 B7 A9 D1 C3", "AirPods Pro 2 (USB-C)", 90, 90)] // Pro2UsbcTest.kt:17
    [InlineData("07 19 01 27 20 0B 99 8F 11 00 05 43 A8 85 17 07 FF 41 62 2C FE 5E 20 08 1A 52 40", "AirPods Pro 3", 90, 90)]    // Pro3Test.kt:80
    [InlineData("07 19 01 30 20 0B A9 8F 13 00 05 EA DB 3C 0E 2E 09 42 A7 22 FF B6 CD D7 FD E8 1A", "AirPods 5", 100, 90)]       // Gen5WirelessTest.kt:139
    public void BothInEar(string hex, string name, int expectedLeft, int expectedRight)
    {
        var a = Parse(hex);
        Assert.Equal(name, a.Model.Name);
        Assert.Equal(EarState.InEar, a.LeftEar);
        Assert.Equal(EarState.InEar, a.RightEar);
        Assert.Equal(2, a.InEarCount);
        Assert.Equal(expectedLeft, a.LeftBattery);
        Assert.Equal(expectedRight, a.RightBattery);
        Assert.Equal(LidState.NotInCase, a.Lid);
    }

    [Fact] // AirPodsPro2UsbcTest.kt:51 "via #164 - in case": sender in the case -> in-ear bits flip again
    public void SenderInCase_LeftInEar_RightCharging()
    {
        var a = Parse("07 19 01 24 20 53 AA 98 32 00 05 49 0A B8 BF 8E 29 D8 70 12 0D A7 0C CE 77 56 00");
        Assert.True(a.ThisPodInCase);
        Assert.Equal(PodSide.Left, a.PrimarySide);
        Assert.Equal(EarState.InEar, a.LeftEar);
        Assert.Equal(EarState.InCase, a.RightEar);
        Assert.True(a.RightCharging);
        Assert.False(a.LeftCharging);
        Assert.Equal(80, a.CaseBattery);
        Assert.Equal(LidState.Open, a.Lid);
    }

    [Fact] // AirPodsGen5WirelessTest.kt:103 "one pod worn, one charging": lid byte not trustworthy (bit 4 only)
    public void OnePodWorn_OtherCharging_LidUnknown()
    {
        var a = Parse("07 19 01 30 20 13 A9 A9 13 00 04 55 94 59 C6 1C C9 6D 13 4F EA D9 52 71 AE EB E4");
        Assert.Equal(EarState.InCase, a.LeftEar);
        Assert.Equal(EarState.InEar, a.RightEar);
        Assert.Equal(100, a.LeftBattery);
        Assert.Equal(90, a.RightBattery);
        Assert.Equal(90, a.CaseBattery);
        Assert.True(a.LeftCharging);
        Assert.False(a.RightCharging);
        Assert.Equal(PodSide.Right, a.PrimarySide);
        Assert.Equal(LidState.Unknown, a.Lid);
    }

    [Fact] // AirPodsPro2Test.kt:17
    public void AirPodsPro2_InCase_AllCharging()
    {
        var a = Parse("07 19 01 14 20 55 88 F9 51 00 04 20 50 03 CA D5 C9 AC 0F FA 84 78 94 5A 4D DF F5");
        Assert.Equal(PodSide.Left, a.PrimarySide);
        Assert.Equal(80, a.LeftBattery);
        Assert.Equal(80, a.RightBattery);
        Assert.Equal(90, a.CaseBattery);
        Assert.True(a.LeftCharging && a.RightCharging && a.CaseCharging);
        Assert.Equal(LidState.Open, a.Lid); // lid 0x51: bit 3 clear
    }

    [Fact] // AirPodsGen5Test.kt:15 (both in case, sender not in case: still trustworthy because of bit 2)
    public void AirPods5_BothInCase_LidOpen_RightPrimary()
    {
        var a = Parse("07 19 01 36 20 15 99 F9 51 00 04 FB 0F 0C 65 33 51 1A CE 8F 04 32 66 9B D1 BD 4E");
        Assert.Equal("AirPods 5", a.Model.Name);
        Assert.Equal(PodSide.Right, a.PrimarySide);
        Assert.Equal(LidState.Open, a.Lid);
        Assert.True(a.CaseCharging);
    }

    [Fact] // AirPodsMaxTest.kt:16
    public void AirPodsMax_SingleBattery()
    {
        var a = Parse("07 19 01 0A 20 62 04 80 01 0F 40 0D 70 50 16 F2 40 83 16 BF 10 16 34 9B 74 84 E8");
        Assert.Equal(PodsLayout.Single, a.Layout);
        Assert.Equal(ModelFamily.Max, a.Model.Family);
        Assert.Equal(40, a.HeadsetBattery);
        Assert.False(a.HeadsetCharging);
        Assert.Null(a.LeftBattery);
        Assert.Null(a.CaseBattery);
        Assert.Null(a.PrimarySide);
        Assert.Equal(LidState.NotInCase, a.Lid);
        Assert.Null(a.Color); // 0x0F is not one of the 13 known colors
    }

    [Theory] // AirPodsMaxTest.kt:80/86, AirPodsMax2Test.kt:13-61
    [InlineData("07 19 01 0A 20 03 07 80 03 03 65 1F 28 32 D0 D9 71 43 00 9A 40 E7 6B EA 6C 2C FB", false, 70)]
    [InlineData("07 19 01 0A 20 23 07 80 03 03 65 1F 28 32 D0 D9 71 43 00 9A 40 E7 6B EA 6C 2C FB", true, 70)]
    [InlineData("07 19 01 2D 20 2B F6 8F 03 14 05 E7 17 3C 7D 65 B8 F3 94 45 12 90 EB E3 24 D3 3E", true, 60)]
    [InlineData("07 19 01 2D 20 20 F6 8F 03 14 04 91 DF 89 19 E4 87 FB E9 DA 39 CE 26 BC AD 57 05", false, 60)]
    [InlineData("07 19 01 2D 20 21 F6 8F 03 14 04 54 5D 3F 3D 3D D9 6E F0 26 B9 0F 93 69 5F FB 46", false, 60)]
    [InlineData("07 19 01 2D 20 23 F6 8F 03 14 04 E1 8B 99 98 20 0B C3 C1 12 2D B0 43 98 94 D6 A0", true, 60)]
    [InlineData("07 19 01 2D 20 29 F6 8F 03 14 04 E1 8B 99 98 20 0B C3 C1 12 2D B0 43 98 94 D6 A0", true, 60)]
    [InlineData("07 19 01 1F 20 2B 05 80 03 12 C5 2E 8B F9 9A 7E 19 7B 63 0F 30 6E D7 3B E2 EC 32", true, 50)] // MaxUsbcTest.kt:14
    public void Max_WearAndBattery(string hex, bool worn, int battery)
    {
        var a = Parse(hex);
        Assert.Equal(worn, a.HeadsetWorn);
        Assert.Equal(battery, a.HeadsetBattery);
        Assert.Equal(worn ? 1 : 0, a.InEarCount);
    }

    // ---------- synthetic: one rule per test ----------

    [Fact]
    public void Flip_LeftPrimary_LowerNibbleIsLeft()
    {
        // status bit 5 set: left values in the lower nibble; flags bit 0 = left charging
        var a = ProximityPairingParser.Parse(Packet(0x1420, status: 0x20, pods: 0x37, flagsCase: 0x15))!;
        Assert.False(a.ValuesFlipped);
        Assert.Equal(70, a.LeftBattery);
        Assert.Equal(30, a.RightBattery);
        Assert.True(a.LeftCharging);
        Assert.False(a.RightCharging);
        Assert.Equal(50, a.CaseBattery);
    }

    [Fact]
    public void Flip_RightPrimary_LowerNibbleIsRight()
    {
        var a = ProximityPairingParser.Parse(Packet(0x1420, status: 0x00, pods: 0x37, flagsCase: 0x15))!;
        Assert.True(a.ValuesFlipped);
        Assert.Equal(30, a.LeftBattery);
        Assert.Equal(70, a.RightBattery);
        Assert.False(a.LeftCharging);
        Assert.True(a.RightCharging);
    }

    [Theory]
    [InlineData(0x0, 0)]
    [InlineData(0x5, 50)]
    [InlineData(0xA, 100)]
    [InlineData(0xB, null)]
    [InlineData(0xE, null)]
    [InlineData(0xF, null)]
    public void BatteryNibble(int nibble, int? expected) =>
        Assert.Equal(expected, ProximityPairingParser.DecodeBattery(nibble));

    [Fact]
    public void CaseClosed_WhenSenderInCase()
    {
        var a = ProximityPairingParser.Parse(Packet(0x1420, status: 0x44, pods: 0x55, flagsCase: 0x75, lid: 0x09))!;
        Assert.Equal(LidState.Closed, a.Lid);
    }

    [Fact]
    public void UnknownModel_StillDecodedAsDual()
    {
        var a = ProximityPairingParser.Parse(Packet(0x9920, status: 0x2B, pods: 0x88, flagsCase: 0x8F))!;
        Assert.True(a.Model.IsUnknown);
        Assert.Equal(ModelFamily.Generic, a.Model.Family);
        Assert.Equal(80, a.LeftBattery);
        Assert.Equal(2, a.InEarCount);
    }

    [Fact]
    public void ProximityMessageAfterAnotherContinuityMessage()
    {
        // e.g. a "Nearby Info" (0x10) message first, then the 0x07 message.
        var inner = Packet(0x1420, status: 0x2B, pods: 0x88, flagsCase: 0x8F);
        var data = new byte[] { 0x10, 0x03, 0xAA, 0xBB, 0xCC }.Concat(inner).ToArray();
        Assert.Equal(80, ProximityPairingParser.Parse(data)!.LeftBattery);
    }

    [Fact] // AirPodsGen4Test.kt:53: prefix 0x07 frame from the identity address must not create a device
    public void Prefix07_Rejected() =>
        Assert.Null(ProximityPairingParser.Parse(Hex.Bytes("07 19 07 19 20 35 DF DE 46 76 CE 66 5C F4 6E BA B6 AC 1B 69 4C A7 D8 09 BF 9A 04")));

    [Fact]
    public void PairingModePrefix00_Rejected() =>
        Assert.Null(ProximityPairingParser.Parse(Packet(0x1420, 0x2B, 0x88, 0x8F, prefix: 0x00)));

    public static IEnumerable<object[]> Malformed()
    {
        var good = Packet(0x1420, 0x2B, 0x88, 0x8F);
        yield return new object[] { Array.Empty<byte>() };
        yield return new object[] { new byte[] { 0x07 } };
        yield return new object[] { new byte[] { 0x07, 0x19 } };
        yield return new object[] { good[..26] };                                         // one byte short
        yield return new object[] { good[..10] };                                         // truncated
        yield return new object[] { new byte[] { 0x07, 0x18 }.Concat(good[2..26]).ToArray() }; // length 24
        yield return new object[] { new byte[] { 0x07, 0x1A }.Concat(good[2..]).Concat(new byte[] { 0 }).ToArray() }; // length 26
        yield return new object[] { new byte[] { 0x07, 0xFF }.Concat(good[2..]).ToArray() }; // length past the end
        yield return new object[] { new byte[] { 0x10, 0xFF, 0x00 } };                    // other type, length past the end
        yield return new object[] { new byte[] { 0x10, 0x05, 1, 2, 3, 4, 5 } };            // no 0x07 at all
        yield return new object[] { new byte[256] };                                      // too long
        yield return new object[] { Enumerable.Repeat((byte)0xFF, 27).ToArray() };
        yield return new object[] { new byte[27] };
    }

    [Theory]
    [MemberData(nameof(Malformed))]
    public void MalformedInput_ReturnsFalse_NoThrow(byte[] data)
    {
        Assert.False(ProximityPairingParser.TryParse(data, out var result));
        Assert.Null(result);
    }

    [Fact]
    public void TrailingBytesAfterMessage_AreIgnored()
    {
        var data = Packet(0x1420, 0x2B, 0x88, 0x8F).Concat(new byte[] { 0x01, 0x00 }).ToArray();
        Assert.NotNull(ProximityPairingParser.Parse(data));
    }

    // ---------- ear-state combination (our rule) ----------

    [Fact]
    public void EarStates_ChargingPodIsInCase_EvenIfInEarBitSet() =>
        Assert.Equal((EarState.InCase, EarState.InEar),
            ProximityPairingParser.ResolveEarStates(true, true, leftCharging: true, rightCharging: false, onePodInCase: true, bothPodsInCase: false));

    [Fact]
    public void EarStates_OnePodInCase_DeadCase_PicksTheOneOutOfEar() =>
        Assert.Equal((EarState.InEar, EarState.InCase),
            ProximityPairingParser.ResolveEarStates(true, false, false, false, onePodInCase: true, bothPodsInCase: false));

    [Fact]
    public void EarStates_OnePodInCase_Ambiguous_StaysOutOfEar() =>
        Assert.Equal((EarState.OutOfEar, EarState.OutOfEar),
            ProximityPairingParser.ResolveEarStates(false, false, false, false, onePodInCase: true, bothPodsInCase: false));
}
