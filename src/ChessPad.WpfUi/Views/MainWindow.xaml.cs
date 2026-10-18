using ChessPad.ViewModels;
using MahApps.Metro.Controls;
using System.Windows;
using System.Windows.Input;

namespace ChessPad.WpfUi.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow(ChessPadViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void OnExitExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            Application.Current.Shutdown();
        }
    }
}