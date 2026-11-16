# HeyPods (for AirPods)

[עברית למטה](#עברית)

A small, free Windows tray app that shows the battery level of your AirPods and pauses your music when you take one out.

> HeyPods is not affiliated with Apple. AirPods is a trademark of Apple Inc.

## Features
- Battery of the left pod, right pod and case, from the Bluetooth LE broadcast the case/pods send (no pairing protocol, no driver).
- Tray icon, quick popup when the case opens, main window next to the clock. Light/dark, follows the Windows accent color.
- Pause when a pod leaves the ear, resume when it comes back (only if HeyPods paused it).
- "My AirPods" detection (model, color, signal strength), so neighbours' pods are ignored.
- Low battery notification (20%), global shortcut Ctrl+Alt+A, optional start with Windows.
- Privacy: no telemetry, no accounts. The only network request is the "Check for update now" button (one HTTPS request to github.com, only when clicked).
- The interface is currently in Hebrew (right-to-left).

Status: version 0.9.2, early release. 317 automated tests (224 engine + 93 interface), and it runs on Windows 10/11 with real AirPods, but it has been tried on only a few PCs so far. Noise control (ANC) is not supported.

## Download
1. Open the [Releases page](https://github.com/ChikApps/HeyPods/releases) and download `HeyPods-0.9.2-win-x64.zip` (Windows 10/11, 64-bit). A `.sha256` file is attached to check the download.
2. Right-click the zip, "Extract All", then double-click `HeyPods.exe`. Nothing to install (.NET is included). The first start can take up to half a minute.
3. The exe is not code-signed yet, so Windows SmartScreen shows a blue "Windows protected your PC" window. Click "More info", then "Run anyway".

To remove: right-click the tray icon, "יציאה" (Exit), then delete `HeyPods.exe`.

## Build
Requires the .NET 8 SDK (with the WindowsDesktop workload for the app; the core and tests run on Linux too).
```
dotnet test Earbuds.sln -c Release
./publish.sh        # or publish.cmd on Windows -> dist/HeyPods.exe (single file, self-contained, win-x64)
```
Layout: `src/Earbuds.Core` (engine, no Windows code), `src/Earbuds.Windows` (Bluetooth, media, hotkey, update check), `src/Earbuds.Presentation` (texts and view logic), `src/Earbuds.App` (WPF). Design notes and drawings: `docs/design/`. Supported models: `docs/models.md`.

## License and credits
GPL-3.0-only, see `LICENSE`. Copyright (C) 2026 ChikApps.
The protocol knowledge comes from the open-source projects CAPod, AirPodsDesktop, LibrePods, OpenPods and others; see `THIRD_PARTY_NOTICES.md` for credits and licenses. No Apple code, images or binaries are included.

---

<a id="עברית"></a>
<div dir="rtl">

# HeyPods (for AirPods)

אפליקציה חינמית וקטנה לווינדוס, ליד השעון: מראה את הסוללה של ה-AirPods ועוצרת את המוזיקה כשמוציאים אוזנייה.

> HeyPods אינה קשורה לאפל. AirPods הוא סימן מסחרי של Apple Inc.

## תכונות
- סוללה של אוזנייה שמאלית, ימנית והקופסה, מתוך שידור הבלוטות' (BLE) של האוזניות. בלי התקנת מנהל התקן.
- סמל במגש, חלון קופץ בפתיחת הקופסה, חלון ראשי ליד השעון. בהיר/כהה וצבע הדגשה של ווינדוס.
- עצירה כשאוזנייה יוצאת מהאוזן, והמשך כשהיא חוזרת (רק אם HeyPods עצרה).
- זיהוי "האוזניות שלי" (דגם, צבע, עוצמת אות), כך שאוזניות של השכנים לא מוצגות.
- התראה על סוללה חלשה (20%), מקש קיצור Ctrl+Alt+A, הפעלה עם ווינדוס (אופציונלי).
- פרטיות: בלי איסוף מידע ובלי חשבונות. הפנייה היחידה לרשת היא כפתור "בדוק עדכון עכשיו" (בקשה אחת ל-github.com, רק בלחיצה).
- הממשק בעברית (מימין לשמאל).

מצב: גרסה 0.9.2, גרסה ראשונית. 317 בדיקות אוטומטיות (224 מנוע + 93 ממשק). עובדת בווינדוס 10/11 עם AirPods אמיתיות, אבל נוסתה עד עכשיו רק על מחשבים מעטים. סינון רעשים לא נתמך.

## הורדה
1. נכנסים ל[דף ה-Releases](https://github.com/ChikApps/HeyPods/releases) ומורידים את `HeyPods-0.9.2-win-x64.zip` (ווינדוס 10/11, ‏64 ביט). מצורף קובץ `.sha256` לבדיקת ההורדה.
2. לחיצה ימנית על הזיפ, "חלץ הכל", ואז לחיצה כפולה על `HeyPods.exe`. לא צריך להתקין כלום (‏.NET כלול). בפעם הראשונה זה יכול לקחת עד חצי דקה.
3. הקובץ עדיין לא חתום דיגיטלית, ולכן ווינדוס מציג חלון כחול "Windows protected your PC" ("Windows הגן על המחשב שלך"). לוחצים "More info" ("מידע נוסף") ואז "Run anyway" ("הפעל בכל זאת").

להסרה: לחיצה ימנית על הסמל ליד השעון, "יציאה", ואז מוחקים את `HeyPods.exe`.

## בנייה
נדרש .NET 8 SDK. הפקודות מופיעות למעלה (`dotnet test`, `./publish.sh`).

## רישיון וקרדיטים
GPL-3.0-only, ראו `LICENSE`. זכויות יוצרים (C) 2026 ChikApps.
הידע על הפרוטוקול מגיע מפרויקטי קוד פתוח (CAPod, AirPodsDesktop, LibrePods, OpenPods ועוד): ראו `THIRD_PARTY_NOTICES.md`. אין קוד, תמונות או קבצים של אפל.

</div>
