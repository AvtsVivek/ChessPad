namespace ChessPad.ViewModels;

/// <summary>
/// Defines the fields and edge labels of an empty, eight-by-eight chessboard.
/// </summary>
public sealed class ChessBoardViewModel
{
    /// <summary>Gets the 64 fields in row-major order, starting at the top-left.</summary>
    public IReadOnlyList<FieldViewModel> Fields { get; }

    /// <summary>Gets the rank labels, ordered from 8 at the top to 1 at the bottom.</summary>
    public IReadOnlyList<RowColumnLabelViewModel> RowLabels { get; }

    /// <summary>Gets the file labels, ordered from A on the left to H on the right.</summary>
    public IReadOnlyList<RowColumnLabelViewModel> ColumnLabels { get; }

    /// <summary>
    /// Creates all board fields and coordinate labels using the supplied factories.
    /// </summary>
    /// <param name="createField">Creates a field for each zero-based row and column, from 0 through 7.</param>
    /// <param name="createLabel">Creates each rank and file label with its zero-based edge index and display text.</param>
    /// <remarks>
    /// Invokes the label factory 16 times and the field factory 64 times. Returned instances
    /// are preserved in read-only collections; no container or WPF dependencies are required.
    /// </remarks>
    public ChessBoardViewModel(
        FieldViewModel.Factory createField,
        RowColumnLabelViewModel.Factory createLabel)
    {
        var rowLabels = new List<RowColumnLabelViewModel>(8);
        var columnLabels = new List<RowColumnLabelViewModel>(8);

        for (int index = 0; index < 8; index++)
        {
            rowLabels.Add(createLabel(index, ((char)('8' - index)).ToString()));
            columnLabels.Add(createLabel(index, ((char)('A' + index)).ToString()));
        }

        RowLabels = rowLabels.AsReadOnly();
        ColumnLabels = columnLabels.AsReadOnly();

        var fields = new List<FieldViewModel>(64);

        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                fields.Add(createField(row, column));
            }
        }

        Fields = fields.AsReadOnly();
    }
}
