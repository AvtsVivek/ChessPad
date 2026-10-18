using ChessPad.ViewModels;
using MahApps.Metro.Controls;

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
    }
}