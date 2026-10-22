namespace ChessPad.ViewModels;

/// <summary>Base type for view models that can be displayed in the main window's side panel.</summary>
public abstract class SidePanelViewModel
{
    /// <summary>Gets the title shown by the corresponding side-panel view.</summary>
    public string Title { get; }

    /// <summary>Initializes the side panel with its display title.</summary>
    /// <param name="title">The non-empty title for the panel.</param>
    protected SidePanelViewModel(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title;
    }
}
