// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Every text the user sees. Source: the texts file , keys kept as in that file.
// [שם] there = AppInfo.Name here. Texts marked TEMP were not in the texts file (new features from 2026-11-14,
// or states the file did not cover): provisional wording, pending review.

namespace Earbuds.Presentation;

public static class S
{
    private const string N = AppInfo.Name;

    // ---- 0. start-up failure (shown once, the folder with the log opens next to it) ----
    public const string StartupFailed = N + " נתקל בתקלה בהפעלה. אפשר לשלוח את הקובץ שנפתח לצוות.";   // TEMP
    public const string RuntimeFailed = "משהו לא עבד כמו שצריך. אפשר לשלוח את הקובץ שנפתח לצוות.";       // TEMP (2026-11-15)

    // ---- 1. near the clock (tray) ----
    public const string TrayTooltip = "שמאל {0} · ימין {1} · קופסה {2}";              // tray.tooltip
    public const string TrayTooltipCharging = "{0} בטעינה";                              // tray.tooltip.charging
    public const string TrayTooltipMax = "סוללה {0}";                                     // tray.tooltip.max
    public const string TrayTooltipNone = N + ": לא מצאנו את האוזניות";                  // tray.tooltip.none
    public const string TrayTooltipBtOff = N + ": הבלוטות' כבוי";                         // tray.tooltip.btoff
    public const string TrayTooltipNoChoice = N + ": עוד לא בחרת אוזניות";                // tray.tooltip.nochoice
    public const string TrayTooltipNoBle = N + ": המחשב לא תומך ב-Bluetooth LE";          // TEMP
    public const string TrayTooltipLastSeen = N + ": {0}";                                // TEMP, {0} = st.lastseen.*
    public const string MenuOpen = "פתח";                                                 // tray.menu.open
    public const string MenuSettings = "הגדרות";                                          // tray.menu.settings
    public const string MenuAbout = "אודות";                                              // tray.menu.about
    public const string MenuExit = "יציאה";                                               // tray.menu.exit

    // ---- 2. popup ----
    public const string Left = "שמאל";                                                    // popup.left / main.left
    public const string Right = "ימין";                                                   // popup.right / main.right
    public const string Case = "קופסה";                                                   // popup.case / main.case
    public const string Battery = "סוללה";                                                // popup.battery
    public const string Charging = "בטעינה";                                              // popup.charging (screen reader)
    public const string NotMine = "לא האוזניות שלך?";                                     // popup.notmine
    public const string Close = "סגירה";                                                  // popup.close (screen reader)
    public const string Unknown = "לא ידוע";
    public const string Minimize = "מזעור";                                               // TEMP (screen reader, title bar)                                              // screen reader for "--" (design)

    // ---- 3. main window ----
    public const string StateInEar = "באוזן";                                             // main.state.inear
    public const string StateOut = "בחוץ";                                                // main.state.out
    public const string StateInCase = "בקופסה";                                           // main.state.incase
    public const string UpdatedNow = "עודכן עכשיו";                                       // main.updated.now
    public const string UpdatedSeconds = "עודכן לפני {0} שניות";                          // main.updated.sec
    public const string UpdatedMinute = "עודכן לפני דקה";                                 // main.updated.min1
    public const string UpdatedMinutes = "עודכן לפני {0} דקות";                           // main.updated.min
    public const string UpdatedHour = "עודכן לפני שעה";                                   // TEMP (same pattern as st.lastseen)
    public const string UpdatedTwoHours = "עודכן לפני שעתיים";                            // TEMP
    public const string UpdatedHours = "עודכן לפני {0} שעות";                             // TEMP
    public const string SettingsA11y = "הגדרות";                                          // main.settings
    public const string ProHint = "להחלפת מצב סינון רעשים בדגמי Pro: לחיצה ארוכה על רגל האוזנייה"; // 2026-11-14

    // ---- 4. wizard ----
    public const string WizTitle = "בואו נכיר את האוזניות שלך";                           // wiz.title
    public const string Wiz1Head = "פותחים את הקופסה ליד המחשב";                           // wiz.1.head
    public const string Wiz1Body = "משאירים את האוזניות בפנים ומחזיקים את הקופסה פתוחה קרוב למחשב. זה לוקח כמה שניות."; // wiz.1.body
    public const string Wiz1Searching = "מחפשים…";                                        // wiz.1.searching
    public const string Wiz1Slow = "עדיין מחפשים. כדאי לבדוק שהבלוטות' במחשב דלוק ושהקופסה פתוחה."; // wiz.1.slow
    public const string Wiz2Head = "אלה האוזניות שלך?";                                   // wiz.2.head
    public const string Wiz2Details = "{0} · {1} · סוללה: שמאל {2}, ימין {3}, קופסה {4}"; // wiz.2.details
    public const string Wiz2DetailsNoColor = "{0} · סוללה: שמאל {1}, ימין {2}, קופסה {3}"; // wiz.2.details, color unknown
    public const string Wiz2DetailsMax = "{0} · {1} · סוללה: {2}";                        // TEMP (Max has one battery)
    public const string Wiz2DetailsMaxNoColor = "{0} · סוללה: {1}";                       // TEMP
    public const string Wiz2Yes = "כן, אלה שלי";                                          // wiz.2.yes
    public const string Wiz2No = "לא, אלה לא שלי";                                        // wiz.2.no
    public const string Wiz2ListHead = "מצאנו כמה אוזניות. אילו מהן שלך?";                // wiz.2.list.head
    public const string Wiz2ListHint = "הקרובות ביותר למחשב מופיעות ראשונות.";            // wiz.2.list.hint
    public const string Wiz2Nearest = "הכי קרובות";                                       // design (wizard list tag)
    public const string Wiz2None = "לא מצאנו עוד אוזניות. אפשר לסגור את הקופסה, לפתוח אותה שוב ולנסות שוב."; // wiz.2.none
    public const string Wiz2Retry = "לנסות שוב";                                          // wiz.2.retry
    public const string Wiz3Head = "מעולה, סיימנו";                                       // wiz.3.head
    public const string Wiz3Body = "מעכשיו, כשפותחים את הקופסה, יופיע חלון עם הסוללה. את הסמל של " + N + " אפשר למצוא תמיד ליד השעון."; // wiz.3.body
    public const string Wiz3Done = "סגירה";                                               // wiz.3.done
    public const string WizCancel = "אחר כך";                                             // wiz.cancel
    public const string WizStep = "צעד {0} מתוך 3";                                       // design (screen reader)
    public const string ListBatteryDual = "שמאל {0} · ימין {1} · קופסה {2}";              // design (wizard list row)
    public const string ListBatteryMax = "סוללה {0}";                                     // design (wizard list row)

    // ---- 5. settings ----
    public const string SetTitle = "הגדרות";                                              // set.title
    public const string SetStartup = "להפעיל את " + N + " יחד עם ווינדוס";                // set.startup
    public const string SetStartupHint = "הסמל יופיע ליד השעון מיד כשהמחשב נדלק.";        // set.startup.hint
    public const string SetPause = "לעצור את הניגון כשמוציאים אוזנייה";                   // set.pause
    public const string SetPauseHint = "מחזירים את שתי האוזניות לאוזניים, והניגון ממשיך מאותו מקום."; // set.pause.hint
    public const string SetPopup = "להציג חלון כשפותחים את הקופסה";                        // set.popup
    public const string SetLowBattery = "התראה על סוללה חלשה";                            // TEMP
    public const string SetLowBatteryHint = "הודעה של ווינדוס כשאחת האוזניות יורדת ל-20%."; // TEMP
    public const string SetHotkey = "מקש קיצור לפתיחת החלון";                             // TEMP
    public const string SetHotkeyHint = "פותח וסוגר את חלון הסוללה מכל מקום.";            // TEMP
    public const string SetHotkeyBusy = "המקש הזה כבר תפוס בתוכנה אחרת, ולכן הוא כבוי. אפשר לסגור את התוכנה האחרת ולהדליק שוב."; // TEMP (dev)
    public const string SetOn = "פועל";                                                   // design (toggle label)
    public const string SetOff = "כבוי";                                                  // design (toggle label)
    public const string SetMine = "האוזניות שלי";                                         // design (section head)
    public const string SetChange = "החלף אוזניות";                                       // set.change
    public const string SetForget = "שכח את האוזניות";                                    // set.forget
    public const string SetForgetHead = "לשכוח את האוזניות?";                             // set.forget.confirm (first sentence)
    public const string SetForgetBody = N + " ימחק את מה שנשמר ויתחיל מההתחלה.";           // set.forget.confirm (rest)
    public const string SetForgetYes = "כן, לשכוח";                                       // set.forget.yes
    public const string SetForgetNo = "ביטול";                                            // set.forget.no
    public const string SetSaveFailed = "לא הצלחנו לשמור את ההגדרות. השינוי יישאר רק עד שהמחשב ייכבה."; // TEMP (dev)

    // ---- 6. states ----
    public const string StNoChoice = "עוד לא בחרת את האוזניות שלך.";                       // st.nochoice
    public const string StNoChoiceBtn = "בחר את האוזניות שלך";                             // st.nochoice.btn
    public const string StLastSeenMinute = "נראו לאחרונה לפני דקה";                        // st.lastseen.min (1)
    public const string StLastSeenMinutes = "נראו לאחרונה לפני {0} דקות";                  // st.lastseen.min
    public const string StLastSeenHour = "נראו לאחרונה לפני שעה";                          // st.lastseen.hr (1)
    public const string StLastSeenTwoHours = "נראו לאחרונה לפני שעתיים";                   // st.lastseen.hr (2)
    public const string StLastSeenHours = "נראו לאחרונה לפני {0} שעות";                    // st.lastseen.hr
    public const string StNotFound = "לא מצאנו את האוזניות. פותחים את הקופסה ליד המחשב, והן יופיעו כאן."; // st.notfound
    public const string StBtOff = "הבלוטות' כבוי. כשמדליקים אותו, " + N + " חוזר לעבוד לבד."; // st.btoff
    public const string StBtOffBtn = "פתח הגדרות בלוטות'";                                 // st.btoff.btn
    public const string StNoBle = "המחשב הזה לא תומך בסוג הבלוטות' ש-" + N + " צריך (Bluetooth LE). אפשר לחבר מתאם בלוטות' קטן ליציאת USB."; // st.noble
    public const string StNotConnected = "האוזניות לא מחוברות למחשב, ולכן עצירת הניגון לא תפעל עד שיתחברו."; // st.notconnected
    public const string StNotConnectedLink = "פתח הגדרות בלוטות'";                         // st.notconnected.link
    public const string StUnknownModel = "אוזניות אפל (דגם לא מוכר)";                      // st.unknownmodel

    // ---- low battery notification (2026-11-14) ----
    public const string ToastTitle = "הסוללה של האוזניות חלשה";                            // TEMP
    public const string ToastBodyDual = "שמאל {0} · ימין {1}. כדאי להחזיר אותן לקופסה לטעינה."; // TEMP
    public const string ToastBodyMax = "סוללה {0}. כדאי לחבר אותן לטעינה.";                // TEMP (dev)

    // ---- 7. about ----
    public const string AboutTitle = "אודות " + N;                                        // about.title
    public const string AboutTagline = "הסוללה של האוזניות שלך, ליד השעון.";              // about.tagline
    public const string AboutVersion = "גרסה {0}";                                        // about.version
    public const string AboutBy = "מאת ChikApps";                                         // about.publisher
    public const string AboutFree = "חינם וקוד פתוח, ברישיון GPL-3.0.";                    // about.free
    public const string AboutCode = "לקוד המקור";                                         // about.code
    // about.privacy, rewritten for the "check for update now" button (2026-11-14). TEMP until reviewed.
    public const string AboutPrivacy = N + " לא אוסף שום מידע ולא פונה לאינטרנט, חוץ מבדיקת עדכון כשלוחצים על הכפתור. הוא רק מקשיב לאוזניות שליד המחשב.";
    public const string AboutHonestLead = "כדאי לדעת:";                                   // about.honest (bold part)
    public const string AboutHonest = "אם יש לידך מישהו עם אוזניות מאותו דגם ובאותו צבע בדיוק, " + N + " עלול להתבלבל לרגע. אם זה קורה, לוחצים על \"לא האוזניות שלך?\" ובוחרים שוב."; // about.honest
    public const string AboutContact = "מצאת בעיה? כתבו לנו: ";                           // about.contact
    public const string AboutCreditsHead = "תודה";                                        // about.credits.head
    public const string AboutCreditsBody = N + " נבנה על הידע של פרויקטים פתוחים שפיענחו לפנינו איך האוזניות מדברות. תודה גדולה להם:"; // about.credits.body
    public static readonly IReadOnlyList<string> AboutCreditsList = new[]                  // about.credits.list (+ CAPod, H.NotifyIcon)
    {
        "LibrePods", "AirPodsDesktop (SpriteOvO)", "WinPods (JamesCJ60)", "OpenPods", "CAPod (d4rken)", "H.NotifyIcon (HavenDV)",
    };
    public const string AboutLicenses = "רישיונות מלאים";                                 // about.licenses
    public const string AboutTm = "AirPods ו-Apple הם סימני מסחר של Apple Inc. " + N + " היא אפליקציה עצמאית ואין לה שום קשר לחברת אפל. אפל לא יצרה אותה, לא אישרה אותה ולא תומכת בה."; // about.tm
    public const string AboutWarranty = N + " ניתן כמו שהוא, בלי שום אחריות. משתמשים בו על אחריותך. הפרטים המלאים ברישיון GPL-3.0."; // about.warranty

    // ---- "check for update now" (2026-11-14). Designer's texts; failure states added by the developer. All TEMP. ----
    public const string UpdTitle = "עדכונים";
    public const string UpdIdleHint = "בודקים רק כשלוחצים.";
    public const string UpdButton = "בדוק עדכון עכשיו";
    public const string UpdChecking = "בודק…";
    public const string UpdAvailable = "יש גרסה חדשה: {0}";
    public const string UpdAvailableHint = "אצלך גרסה {0}. ההורדה נפתחת בדפדפן.";
    public const string UpdDownload = "להורדה";
    public const string UpdUpToDate = "יש לך את הגרסה האחרונה";
    public const string UpdUpToDateHint = "נבדק עכשיו.";
    public const string UpdFailed = "לא הצלחנו לבדוק";
    public const string UpdFailedHint = "אולי אין חיבור לאינטרנט, או שהאתר חסום. אפשר לנסות שוב אחר כך.";
    public const string UpdNotConfigured = "עוד אין דף הורדות";
    public const string UpdNotConfiguredHint = "הבדיקה תעבוד אחרי שהגרסה הראשונה תפורסם.";
    public const string UpdNoRelease = "עוד לא פורסמה גרסה";
    public const string UpdNoReleaseHint = "אצלך גרסה {0}.";

    public const string LicensesTitle = "רישיונות מלאים";                                 // about.licenses (window title)

    // ---- colors (wiz.2.details). Color list + the rest of the color table. ----
    public static string? ColorName(Earbuds.Core.Models.DeviceColor? color) => color switch
    {
        Earbuds.Core.Models.DeviceColor.White => "לבן",
        Earbuds.Core.Models.DeviceColor.Black => "שחור",
        Earbuds.Core.Models.DeviceColor.Red => "אדום",
        Earbuds.Core.Models.DeviceColor.Blue => "כחול",
        Earbuds.Core.Models.DeviceColor.Pink => "ורוד",
        Earbuds.Core.Models.DeviceColor.Gray => "אפור",
        Earbuds.Core.Models.DeviceColor.Silver => "כסוף",
        Earbuds.Core.Models.DeviceColor.Gold => "זהב",
        Earbuds.Core.Models.DeviceColor.RoseGold => "זהב ורוד",
        Earbuds.Core.Models.DeviceColor.SpaceGray => "אפור חלל",
        Earbuds.Core.Models.DeviceColor.DarkBlue => "כחול כהה",
        Earbuds.Core.Models.DeviceColor.LightBlue => "תכלת",
        Earbuds.Core.Models.DeviceColor.Yellow => "צהוב",
        _ => null,
    };
}
