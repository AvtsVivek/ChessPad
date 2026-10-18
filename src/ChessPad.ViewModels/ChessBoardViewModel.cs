namespace ChessPad.ViewModels;

public sealed class ChessBoardViewModel
{
    public IReadOnlyList<FieldViewModel> Fields { get; }
    public IReadOnlyList<RowColumnLabelViewModel> RowLabels { get; }
    public IReadOnlyList<RowColumnLabelViewModel> ColumnLabels { get; }

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
