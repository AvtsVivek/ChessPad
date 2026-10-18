namespace ChessPad.ViewModels.Tests;

[TestClass]
public sealed class RowColumnLabelViewModelTests
{
    /// <summary>Verifies that constructing a coordinate label preserves its supplied index and text.</summary>
    /// <param name="index">The edge position to retain unchanged.</param>
    /// <param name="label">The rank or file text to retain unchanged.</param>
    /// <remarks>Data cases cover both endpoints of the rank and file label ranges.</remarks>
    [TestMethod]
    [DataRow(0, "8")]
    [DataRow(7, "1")]
    [DataRow(0, "A")]
    [DataRow(7, "H")]
    public void Constructor_PreservesIndexAndLabel(int index, string label)
    {
        var viewModel = new RowColumnLabelViewModel(index, label);

        Assert.AreEqual(index, viewModel.Index);
        Assert.AreEqual(label, viewModel.Label);
    }
}
