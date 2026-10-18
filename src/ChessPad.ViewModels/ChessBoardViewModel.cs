namespace ChessPad.ViewModels;

public sealed class ChessBoardViewModel
{
    public IReadOnlyList<FieldViewModel> Fields { get; }

    public ChessBoardViewModel(FieldViewModel.Factory createField)
    {
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
