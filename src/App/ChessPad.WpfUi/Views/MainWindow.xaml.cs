using ChessPad.ViewModels;
using MahApps.Metro.Controls;
using System.Windows;
using System.Windows.Input;

namespace ChessPad.WpfUi.Views
{
    /// <summary>
    /// Displays the Metro-styled chessboard and handles presentation-level window commands.
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        /// <summary>Loads the window's XAML and assigns its dependency-injected binding source.</summary>
        /// <param name="viewModel">The application view model exposed through the window's data context.</param>
        public MainWindow(ChessPadViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>Handles the routed Exit command by requesting normal application shutdown.</summary>
        /// <param name="sender">The command-binding event source.</param>
        /// <param name="e">The execution event, marked handled before shutdown is requested.</param>
        private void OnExitExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            Application.Current.Shutdown();
        }
    }
}