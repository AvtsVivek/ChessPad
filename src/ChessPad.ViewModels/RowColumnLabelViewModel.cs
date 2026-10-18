namespace ChessPad.ViewModels;

public sealed record RowColumnLabelViewModel(int Index, string Label)
{
    // Autofac matches delegate parameters to constructor parameters by name.
    public delegate RowColumnLabelViewModel Factory(int Index, string Label);
}
