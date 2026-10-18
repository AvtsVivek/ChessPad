namespace ChessPad.ViewModels;

/// <summary>Initializes an immutable board field and derives its color from its coordinates.</summary>
/// <param name="Row">The zero-based row, increasing from top to bottom; board construction supplies 0 through 7.</param>
/// <param name="Column">The zero-based column, increasing from left to right; board construction supplies 0 through 7.</param>
/// <remarks>Coordinates are stored as supplied without range validation.</remarks>
public sealed record FieldViewModel(int Row, int Column)
{
    /// <summary>Creates a field using runtime coordinates and any dependencies registered with Autofac.</summary>
    /// <param name="Row">The zero-based row passed to the field constructor.</param>
    /// <param name="Column">The zero-based column passed to the field constructor.</param>
    /// <returns>The field created for the supplied coordinates.</returns>
    /// <remarks>Parameter names match the constructor because Autofac binds delegate arguments by name.</remarks>
    public delegate FieldViewModel Factory(int Row, int Column);

    /// <summary>Gets whether the coordinate sum is even, making the top-left field light.</summary>
    public bool IsLight => (Row + Column) % 2 == 0;
}
