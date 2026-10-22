namespace ChessPad.WpfUi.Views
{
    /// <summary>Defines the unscaled dimensions used to render the board and coordinate strips.</summary>
    public static class BoardLayoutConstants
    {
        /// <summary>The width of each vertical coordinate strip, in WPF units.</summary>
        public const double LabelWidth = 20;

        /// <summary>The height of each horizontal coordinate strip, in WPF units.</summary>
        public const double LabelHeight = LabelWidth;

        /// <summary>The unscaled side length of a board square, in WPF units.</summary>
        public const double SquareSize = 50;

        /// <summary>The unscaled board side length including both coordinate strips.</summary>
        public const double FullBoardLength = LabelWidth + (SquareSize * 8) + LabelWidth;
    }
}
