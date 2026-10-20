using ChessPad.ViewModels;
using MahApps.Metro.Controls;
using System.Windows;
using System.Windows.Controls.Primitives;
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

            if (!string.IsNullOrWhiteSpace(MainWindowSettings.Default.ChessGameColumnWidth))
                ChessGameColumn.Width = (GridLength)_gridLengthConverter.ConvertFromString(MainWindowSettings.Default.ChessGameColumnWidth)!;

            if (!string.IsNullOrWhiteSpace(MainWindowSettings.Default.SidePanelColumnWidth))
                SidePanelColumn.Width = (GridLength)_gridLengthConverter.ConvertFromString(MainWindowSettings.Default.SidePanelColumnWidth)!;
        }

        private readonly GridLengthConverter _gridLengthConverter = new();

        /// <summary>Uses the designer-backed user settings for MahApps window placement persistence.</summary>
        /// <returns>The settings that store the normal window bounds and display state.</returns>
        public override IWindowPlacementSettings GetWindowPlacementSettings() => MainWindowSettings.Default;

        /// <summary>Persists the column widths after the user finishes dragging the splitter.</summary>
        /// <param name="sender">The splitter that raised the event.</param>
        /// <param name="e">The drag-completed event data.</param>
        private void VerticalGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            MainWindowSettings.Default.ChessGameColumnWidth = _gridLengthConverter.ConvertToString(ChessGameColumn.Width) ?? string.Empty;
            MainWindowSettings.Default.SidePanelColumnWidth = _gridLengthConverter.ConvertToString(SidePanelColumn.Width) ?? string.Empty;
            MainWindowSettings.Default.Save();
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