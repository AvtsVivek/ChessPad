namespace ChessPad.ViewModels;

/// <summary>Initializes an immutable rank or file label for a board edge.</summary>
/// <param name="Index">The zero-based position along the edge; board construction supplies 0 through 7.</param>
/// <param name="Label">The display text, normally a rank from 8 to 1 or a file from A to H.</param>
/// <remarks>The index and text are stored as supplied without validation or formatting.</remarks>
public sealed record RowColumnLabelViewModel(int Index, string Label)
{
    /// <summary>Creates an edge label using its runtime position and display text.</summary>
    /// <param name="Index">The zero-based edge position passed to the label constructor.</param>
    /// <param name="Label">The display text passed unchanged to the label constructor.</param>
    /// <returns>The label created for the supplied position and text.</returns>
    /// <remarks>Parameter names match the constructor because Autofac binds delegate arguments by name.</remarks>
    public delegate RowColumnLabelViewModel Factory(int Index, string Label);
}
