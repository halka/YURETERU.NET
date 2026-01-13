using System.Windows;
using Microsoft.Win32;
using YureteruWPF.ViewModels;
using YureteruWPF.Views;

namespace YureteruWPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        
        // Subscribe to view model events
        _viewModel.RequestSettingsWindow += OnRequestSettingsWindow;
        _viewModel.RequestSaveFile += OnRequestSaveFile;
    }

    private void OnRequestSettingsWindow()
    {
        var settingsWindow = new ConnectionWindow(_viewModel);
        settingsWindow.ShowDialog();
    }

    private void OnRequestSaveFile(string defaultFilename, Action<string?> callback)
    {
        var sfd = new SaveFileDialog
        {
            FileName = defaultFilename,
            DefaultExt = ".csv",
            Filter = "CSV Data (*.csv)|*.csv|All Files (*.*)|*.*",
            Title = "Export Seismic Event History"
        };

        if (sfd.ShowDialog() == true)
        {
            callback(sfd.FileName);
        }
        else
        {
            callback(null);
        }
    }
}