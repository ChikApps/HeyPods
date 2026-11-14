# Model table and sources

Codes are bytes 3-4 of the broadcast, read big-endian (bytes `14 20` = 0x1420). Windows reports the
paired device's ProductId byte-swapped (0x2014). Every row has at least two independent sources.
The code holds the same list: `src/Earbuds.Core/Models/ModelCatalog.cs`.

| Code | Name | Drawing | AirPodsDesktop `AppleCP.cpp` | LibrePods `blemanager.cpp` | OpenPods `PodsStatus.java` | CAPod `devices/airpods/` | WinPods `AirPodsProductCatalogue.cs` |
|---|---|---|---|---|---|---|---|
| 0x0220 | AirPods | stem | :45 | :12 | :76 | AirPodsGen1.kt:83 | :10 |
| 0x0F20 | AirPods 2 | stem | :47 | :13 | :78 | AirPodsGen2.kt:83 | :11 |
| 0x1320 | AirPods 3 | stem | :49 | :14 | :80 | AirPodsGen3.kt:94 | :12 |
| 0x1920 | AirPods 4 | stem | :51 | :15 | | AirPodsGen4.kt:94 | :13 |
| 0x1B20 | AirPods 4 (ANC) | stem | :53 | :16 | | AirPodsGen4Anc.kt:92 | :14 |
| 0x3020 | AirPods 5 | stem | :55 | | | AirPodsGen5Wireless.kt:92 | |
| 0x3620 | AirPods 5 | stem | :56 | | | AirPodsGen5.kt:92 | |
| 0x0E20 | AirPods Pro | pro | :58 | :19 | :82 | AirPodsPro.kt:100 | :15 |
| 0x1420 | AirPods Pro 2 | pro | :60 | :20 | :84 | AirPodsPro2.kt:97 | :16 |
| 0x2420 | AirPods Pro 2 (USB-C) | pro | :62 | :21 | :84 | AirPodsPro2Usbc.kt:97 | :17 |
| 0x2720 | AirPods Pro 3 | pro | :64 | | :86 | AirPodsPro3.kt:100 | :18 |
| 0x0A20 | AirPods Max | max | :66 | :17 | :88 | AirPodsMax.kt:82 | :19 |
| 0x1F20 | AirPods Max (USB-C) | max | :68 | :18 | | AirPodsMaxUsbc.kt:82 | :20 |
| 0x2D20 | AirPods Max 2 | max | | | | AirPodsMax2.kt:92 | :21 |

Max family: one battery, no case, no left/right. Worn = status bit 5 (Max, Max USB-C), bit 1 or 3 (Max 2).
Beats: deliberately not in the table (out of scope); they show as "unknown model".

## Colors (byte 9) - same values in AirPodsDesktop `AppleCP.h:47-59`, LibrePods `blemanager.cpp:27-59`, CAPod `HasAppleColor.kt`

| Byte | Color | Suggested Hebrew |
|---|---|---|
| 0x00 | White | לבן |
| 0x01 | Black | שחור |
| 0x02 | Red | אדום |
| 0x03 | Blue | כחול |
| 0x04 | Pink | ורוד |
| 0x05 | Gray | אפור |
| 0x06 | Silver | כסוף |
| 0x07 | Gold | זהב |
| 0x08 | Rose gold | זהב ורוד |
| 0x09 | Space gray | אפור חלל |
| 0x0A | Dark blue | כחול כהה |
| 0x0B | Light blue | תכלת |
| 0x0C | Yellow | צהוב |

AirPodsDesktop's author notes the colors are untested beyond white. AirPods Max uses other values
(e.g. 0x11 green, WinPods `AirPodsProductCatalogue.cs:113-128`): not mapped, the color is omitted.

## Commits read (2026-11-14)

| Project | Commit |
|---|---|
| d4rken-org/capod | d0664602236a07df122e3611ddf4d044bb7e9196 |
| SpriteOvO/AirPodsDesktop | 56b04001f75044dbd15507303a9438c71e93c6e4 |
| kavishdevar/librepods | 89356c08cea3db0b6b54792d9329d09a87cc85fa |
| adolfintel/OpenPods | 9cd800875f594fa6eb7325dd33d9216c476a5782 |
| JamesCJ60/WinPods | d37977dc321fe3cb024a7b8b282ce9295f775e3b |
