namespace ChessPad.ViewModels.Tests;

[TestClass]
public sealed class FieldViewModelTests
{
    /// <summary>Verifies coordinate preservation and checkerboard color for all eight fields in a supplied row.</summary>
    /// <param name="row">The zero-based board row exercised by this data case.</param>
    /// <param name="expectedColors">Eight expected colors, with L for light and D for dark, ordered by column.</param>
    /// <remarks>The eight data cases collectively check all 64 fields, including the light top-left field.</remarks>
    [TestMethod]
    [DataRow(0, "LDLDLDLD")]
    [DataRow(1, "DLDLDLDL")]
    [DataRow(2, "LDLDLDLD")]
    [DataRow(3, "DLDLDLDL")]
    [DataRow(4, "LDLDLDLD")]
    [DataRow(5, "DLDLDLDL")]
    [DataRow(6, "LDLDLDLD")]
    [DataRow(7, "DLDLDLDL")]
    public void Constructor_PreservesCoordinatesAndComputesExpectedColors(int row, string expectedColors)
    {
        for (int column = 0; column < 8; column++)
        {
            var field = new FieldViewModel(row, column);

            Assert.AreEqual(row, field.Row);
            Assert.AreEqual(column, field.Column);
            Assert.AreEqual(expectedColors[column] == 'L', field.IsLight,
                $"Unexpected color at row {row}, column {column}.");
        }
    }
}
