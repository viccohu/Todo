using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Memo.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Memo
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private static Window? _window;

        /// <summary>
        /// Gets the main application window.
        /// </summary>
        public static Window? MainWindow => _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            // 崩溃前尽量落盘日志，便于定位闪退
            UnhandledException += (s, e) =>
            {
                AppLog.Error($"UnhandledException: {e.Exception}");
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                AppLog.Error($"AppDomain Unhandled: {e.ExceptionObject}");
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                AppLog.Error($"UnobservedTaskException: {e.Exception}");
                e.SetObserved();
            };
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // 初始化桌面固定模式（Show Desktop 免疫）
            WindowHelper.InitializeDesktopPin();

            _window = new MainWindow();

            _window.Activate();

            ReminderService.Instance.Initialize();
        }

        /// <summary>
        /// 设置应用主题（Light / Dark）
        /// </summary>
        public static void SetTheme(ElementTheme theme)
        {
            if (_window?.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;
            }
        }
    }
}
