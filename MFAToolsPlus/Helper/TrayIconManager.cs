using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Lang.Avalonia.MarkupExtensions;
using MFAToolsPlus.Configuration;
using MFAToolsPlus.ViewModels;
using MFAToolsPlus.Views;
using System;
using System.IO;
using System.Linq;


namespace MFAToolsPlus.Helper;

public class TrayIconManager
{
    private static TrayIcon? _trayIcon;
    private static DateTime _lastClickTime = DateTime.MinValue;

    public static void InitializeTrayIcon(Application application, RootView mainWindow, RootViewModel viewModel)
    {
        // 先清理旧的托盘实例，避免残留
        DisposeTrayIcon(application);

        // 创建 TrayIcon 实例
        _trayIcon = new TrayIcon
        {
            IsVisible = true
        };

        _trayIcon.Bind(TrayIcon.ToolTipTextProperty, new I18nBinding(LangKeys.AppTitle));
        
        var menu = new NativeMenu();
        // 绑定 Icon
        _trayIcon.Bind(TrayIcon.IconProperty, new Binding
        {
            Source = IconHelper.WindowIcon
        });
        
        var menuItem3 = new NativeMenuItem()
        {
        };

        menuItem3.Bind(NativeMenuItem.HeaderProperty, new I18nBinding(LangKeys.SwitchLanguage));
        menuItem3.Menu = new NativeMenu();

        foreach (var lang in LanguageHelper.SupportedLanguages)
        {
            var langMenu = new NativeMenuItem
            {
                Header = lang.Name
            };
            langMenu.Click += (sender, _) =>
            {
                LanguageHelper.ChangeLanguage(lang);
                var index = LanguageHelper.SupportedLanguages.ToList().FindIndex(language => language.Key == lang.Key);
                ConfigurationManager.Current.SetValue(ConfigurationKeys.CurrentLanguage, index == -1 ? 0 : index);
            };
            menuItem3.Menu.Add(langMenu);
        }

        var menuItem4 = new NativeMenuItem();
        menuItem4.Bind(NativeMenuItem.HeaderProperty, new I18nBinding(LangKeys.Hide));
        menuItem4.Bind(NativeMenuItem.IsVisibleProperty, new Binding("IsWindowVisible")
        {
            Source = viewModel,
        });
        menuItem4.Click += App_hide;
        var menuItem5 = new NativeMenuItem();
        menuItem5.Bind(NativeMenuItem.HeaderProperty, new I18nBinding(LangKeys.Show));
        menuItem5.Bind(NativeMenuItem.IsVisibleProperty, new Binding("!IsWindowVisible")
        {
            Source = viewModel,
        });
        menuItem5.Click += App_show;
        var menuItem6 = new NativeMenuItem();
        menuItem6.Bind(NativeMenuItem.HeaderProperty, new I18nBinding(LangKeys.Quit));
        menuItem6.Click += App_exit;
        menu.Add(menuItem3);
        menu.Add(menuItem4);
        menu.Add(menuItem5);
        menu.Add(menuItem6);
        // 将菜单绑定到 TrayIcon
        _trayIcon.Menu = menu;
        // 监听 Clicked 事件
        _trayIcon.Clicked += TrayIconOnClicked;

        // 将 TrayIcon 添加到托盘
        TrayIcon.SetIcons(application, [_trayIcon]);
    }

    public static void DisposeTrayIcon(Application? application)
    {
        if (_trayIcon == null)
            return;

        try
        {
            _trayIcon.Clicked -= TrayIconOnClicked;
        }
        catch
        {
            // ignore
        }

        if (_trayIcon.Menu is NativeMenu menu)
        {
            foreach (var item in menu.Items.OfType<NativeMenuItem>())
            {
                item.Click -= App_hide;
                item.Click -= App_show;
                item.Click -= App_exit;
            }
        }

        try
        {
            _trayIcon.IsVisible = false;
        }
        catch
        {
            // ignore
        }

        if (application != null)
        {
            try
            {
                TrayIcon.SetIcons(application, null);
            }
            catch
            {
                // ignore
            }
        }

        _trayIcon.Dispose();
        _trayIcon = null;
    }


    private static void TrayIconOnClicked(object sender, EventArgs args)
    {
        var now = DateTime.Now;
        var clickInterval = now - _lastClickTime;

        // 判断是否为双击（时间间隔小于 500 毫秒）
        if (clickInterval.TotalMilliseconds < 500)
        {
            DispatcherHelper.PostOnMainThread(() =>
            {
                Instances.RootView.ShowWindow();
            });
        }

        _lastClickTime = now;
    }
    
#pragma warning disable CS4014 // 由于此调用不会等待，因此在此调用完成之前将会继续执行当前方法。请考虑将 "await" 运算符应用于调用结果。

    private static void App_exit(object sender, EventArgs e)
    {
        Instances.RootView.BeforeClosed();
        Instances.ShutdownApplication();
    }

    private static void App_hide(object sender, EventArgs e) =>
        Instances.RootViewModel.IsWindowVisible = false;


    private static void App_show(object sender, EventArgs e)
        => Instances.RootViewModel.IsWindowVisible = true;
}
