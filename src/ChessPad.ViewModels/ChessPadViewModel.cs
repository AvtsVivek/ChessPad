namespace ChessPad.ViewModels;

public sealed class ChessPadViewModel
{
    public ChessBoardViewModel ChessBoard { get; }

    public ChessPadViewModel(ChessBoardViewModel chessBoard)
    {
        ChessBoard = chessBoard;
    }
}
