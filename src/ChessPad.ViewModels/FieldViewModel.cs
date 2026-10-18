namespace ChessPad.ViewModels;

public sealed record FieldViewModel(int Row, int Column)
{
    // Autofac matches delegate parameters to constructor parameters by name.
    public delegate FieldViewModel Factory(int Row, int Column);

    public bool IsLight => (Row + Column) % 2 == 0;
}
