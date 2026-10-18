namespace ChessPad.ViewModels.Tests;

/// <summary>Tests the application view model's constructor-injected board composition.</summary>
/// <remarks>Constructs dependencies directly and verifies reference identity without starting the UI or a container.</remarks>
[TestClass]
public sealed class ChessPadViewModelTests
{
    /// <summary>Verifies that the application view model exposes the exact board instance supplied by its caller.</summary>
    /// <remarks>Uses reference equality to detect replacement or reconstruction of the injected dependency.</remarks>
    [TestMethod]
    public void Constructor_ExposesTheInjectedBoardInstance()
    {
        var board = new ChessBoardViewModel(
            (row, column) => new FieldViewModel(row, column),
            (index, label) => new RowColumnLabelViewModel(index, label));

        var viewModel = new ChessPadViewModel(board);

        Assert.AreSame(board, viewModel.ChessBoard);
    }
}
