using System.Windows;
using YureteruWPF.ViewModels;

namespace YureteruWPF.Views
{
    public partial class ConnectionWindow : Window
    {
        public ConnectionWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Owner = Application.Current.MainWindow;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
