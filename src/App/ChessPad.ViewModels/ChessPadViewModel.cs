using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChessPad.ViewModels;

/// <summary>Provides the application-level view-model composition for the main window.</summary>
public sealed partial class ChessPadViewModel : ObservableObject
{
    private readonly MovesPanelViewModel _movesPanel;
    private readonly BoardColorsSettingsViewModel _boardColorsSettings;

    /// <summary>Gets the board supplied to this application view model.</summary>
    public ChessBoardViewModel ChessBoard { get; }

    /// <summary>Gets or sets the panel currently displayed beside the board.</summary>
    [ObservableProperty]
    private SidePanelViewModel _activeSidePanel;

    /// <summary>Initializes the application view model with its board and available side panels.</summary>
    /// <param name="chessBoard">The board instance to expose without copying or recreating it.</param>
    /// <param name="movesPanel">The panel that displays move history.</param>
    /// <param name="boardColorsSettings">The panel that displays the board color settings.</param>
    public ChessPadViewModel(
        ChessBoardViewModel chessBoard,
        MovesPanelViewModel movesPanel,
        BoardColorsSettingsViewModel boardColorsSettings)
    {
        ChessBoard = chessBoard;
        _movesPanel = movesPanel;
        _boardColorsSettings = boardColorsSettings;
        _activeSidePanel = movesPanel;
    }

    /// <summary>Selects the move-history panel.</summary>
    [RelayCommand]
    private void ShowMoves() => ActiveSidePanel = _movesPanel;

    /// <summary>Selects the board-color settings panel.</summary>
    [RelayCommand]
    private void ShowBoardColors() => ActiveSidePanel = _boardColorsSettings;
}
