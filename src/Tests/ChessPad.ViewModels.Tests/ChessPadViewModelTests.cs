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

        var movesPanel = new MovesPanelViewModel();
        var boardColorsSettings = new BoardColorsSettingsViewModel();
        var viewModel = new ChessPadViewModel(board, movesPanel, boardColorsSettings);

        Assert.AreSame(board, viewModel.ChessBoard);
        Assert.AreSame(movesPanel, viewModel.ActiveSidePanel);
    }

    /// <summary>Verifies selecting each side panel updates the observable active-panel property.</summary>
    [TestMethod]
    public void PanelCommands_SelectPanelAndNotifyBindings()
    {
        var viewModel = new ChessPadViewModel(
            new ChessBoardViewModel(
                (row, column) => new FieldViewModel(row, column),
                (index, label) => new RowColumnLabelViewModel(index, label)),
            new MovesPanelViewModel(),
            new BoardColorsSettingsViewModel());
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        viewModel.ShowBoardColorsCommand.Execute(null);
        Assert.IsInstanceOfType<BoardColorsSettingsViewModel>(viewModel.ActiveSidePanel);
        Assert.AreEqual(nameof(viewModel.ActiveSidePanel), changedProperties.Single());

        changedProperties.Clear();
        viewModel.ShowMovesCommand.Execute(null);
        Assert.IsInstanceOfType<MovesPanelViewModel>(viewModel.ActiveSidePanel);
        Assert.AreEqual(nameof(viewModel.ActiveSidePanel), changedProperties.Single());
    }
}
