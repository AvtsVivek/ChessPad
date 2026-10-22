namespace ChessPad.ViewModels;

/// <summary>Provides the side-panel state for the move history during a game.</summary>
public sealed class MovesPanelViewModel : SidePanelViewModel
{
    /// <summary>Gets the message shown until move history is supplied by a game.</summary>
    public string EmptyMessage => "Moves will appear here when a game is active.";

    /// <summary>Creates the move-history panel view model.</summary>
    public MovesPanelViewModel() : base("Moves")
    {
    }
}
