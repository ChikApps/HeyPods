// Copyright (C) 2026 ChikApps
// SPDX-License-Identifier: GPL-3.0-only
//
// Windows notification on low battery (2026-11-14), for an app without an installer:
// the app id is registered for the current user under HKCU\Software\Classes\AppUserModelId (name + icon),
// then Windows.UI.Notifications shows a normal toast. Picture on the side: both pods, drawn at run time.

using System.IO;
using System.Security;
using Earbuds.App.Art;
using Earbuds.Presentation;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Earbuds.App.Platform;

internal sealed class Toasts
{
    private bool _ready;

    public event EventHandler? Clicked;

    public void Register()
    {
        try
        {
            Native.SetCurrentProcessExplicitAppUserModelID(AppInfo.AppUserModelId);
            var iconPath = Path.Combine(ErrorLog.Folder, "app-icon.png");
            ArtLibrary.SavePng(iconPath, new[] { ArtLibrary.AppIcon }, 64, 0);
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + AppInfo.AppUserModelId, writable: true);
            key.SetValue("DisplayName", AppInfo.Name);
            key.SetValue("IconUri", iconPath);
            _ready = true;
        }
        catch (Exception e)
        {
            ErrorLog.Write("toast-register", e);
        }
    }

    public void ShowLowBattery(string title, string body, ArtFamily family, bool dark)
    {
        if (!_ready)
        {
            return;
        }

        try
        {
            var image = Path.Combine(ErrorLog.Folder, $"toast-{family}-{(dark ? "dark" : "light")}.png".ToLowerInvariant());
            if (!File.Exists(image))
            {
                var parts = family == ArtFamily.Max
                    ? new[] { ArtLibrary.Bud(family, 'l', dark) }
                    : new[] { ArtLibrary.Bud(family, 'l', dark), ArtLibrary.Bud(family, 'r', dark) };
                ArtLibrary.SavePng(image, parts, 48, 4);
            }

            var xml = new XmlDocument();
            xml.LoadXml(
                "<toast><visual><binding template=\"ToastGeneric\">" +
                $"<text>{SecurityElement.Escape(title)}</text><text>{SecurityElement.Escape(body)}</text>" +
                $"<image placement=\"appLogoOverride\" src=\"{SecurityElement.Escape(new Uri(image).AbsoluteUri)}\"/>" +
                "</binding></visual></toast>");
            var toast = new ToastNotification(xml);
            toast.Activated += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
            ToastNotificationManager.CreateToastNotifier(AppInfo.AppUserModelId).Show(toast);
        }
        catch (Exception e)
        {
            ErrorLog.Write("toast", e);
        }
    }
}
