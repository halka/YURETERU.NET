using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using YureteruWPF.Services;
using YureteruWPF.ViewModels;

namespace YureteruWPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Setup dependency injection
            var services = new ServiceCollection();

            // Register services
            services.AddSingleton<ISerialService, SerialService>();
            services.AddSingleton<IDataParser, DataParser>();
            services.AddSingleton<IAudioAlertService>(sp => new AudioAlertService(3.0, 6000));
            services.AddSingleton<IEventRecordingService>(sp => new EventRecordingService(0.5));

            // Register ViewModels
            services.AddSingleton<MainViewModel>();

            // Register MainWindow
            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            // Show main window
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
