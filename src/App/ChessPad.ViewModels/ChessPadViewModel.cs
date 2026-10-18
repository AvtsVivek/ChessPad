namespace ChessPad.ViewModels;

/// <summary>Provides the application-level view-model composition for the main window.</summary>
public sealed class ChessPadViewModel
{
    /// <summary>Gets the board supplied to this application view model.</summary>
    public ChessBoardViewModel ChessBoard { get; }

    /// <summary>Initializes the application view model with an existing board.</summary>
    /// <param name="chessBoard">The board instance to expose without copying or recreating it.</param>
    public ChessPadViewModel(ChessBoardViewModel chessBoard)
    {
        ChessBoard = chessBoard;
    }
}
