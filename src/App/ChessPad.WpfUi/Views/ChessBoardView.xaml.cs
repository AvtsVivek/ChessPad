using System.Windows.Controls;

namespace ChessPad.WpfUi.Views
{
    /// <summary>Renders the board and coordinate labels using the supplied board view model.</summary>
    public partial class ChessBoardView : UserControl
    {
        /// <summary>Loads the board layout without creating or replacing its binding source.</summary>
        public ChessBoardView()
        {
            InitializeComponent();
        }
    }
}
