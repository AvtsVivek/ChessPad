# WPF presentation

[Documentation index](README.md)

## The window's DataContext

The window receives the application view model through constructor injection:

```csharp
public MainWindow(ChessPadViewModel viewModel)
{
	InitializeComponent();
	DataContext = viewModel;
}
```

There is no `<Window.DataContext>` block constructing a view model in XAML. The
runtime data context comes from DI, so the view uses the composed object graph
created by Autofac. See [dependency injection](dependency-injection.md) for how
the window and view models are resolved.

## Binding to the composed board

`MainWindow` directly hosts a board user control:

```xml
<views:ChessBoardView Grid.Column="0" DataContext="{Binding ChessBoard}" />
```

`ChessBoardView` owns the entire board `Viewbox`, frame, square templates, and
coordinate-label resources. It receives the existing `ChessBoardViewModel`
from the window's DI-composed model; its constructor only loads XAML and never
creates or resolves a view model. The window retains the menu, splitter, side
panel, and window/column persistence.

Inside the control, the board's items source is:

```xml
<ItemsControl ItemsSource="{Binding Fields}">
```

An 8-row, 8-column `UniformGrid` arranges the 64 items in their collection order.

Because the view models live in a separate assembly, the board control declares:

```xml
xmlns:viewmodels="clr-namespace:ChessPad.ViewModels;assembly=ChessPad.ViewModels"
```

The UI project reference makes that assembly available to XAML and to the
composition root. `App.xaml.cs` and `MainWindow.xaml.cs` import
`ChessPad.ViewModels`. Local board brushes use the `views:BoardColors` XAML
prefix instead.

For each field, the data template declares:

```xml
<DataTemplate DataType="{x:Type viewmodels:FieldViewModel}">
```

This identifies the type displayed by the template. It does **not** instantiate
a `FieldViewModel` or bypass DI. Each template receives an existing item from
`Fields` as its data context.

## Colors, labels, and scaling

Each field template uses a `Border` with a dark background by default. A
`DataTrigger` switches the background to the light brush when `IsLight` is true.
Window, frame, light-field, and dark-field brushes reference `BoardColors`.

A `Viewbox` with `Stretch="Uniform"` scales the fixed-size board container while
preserving its aspect ratio. `Views\BoardLayoutConstants.cs` in `ChessPad.WpfUi`
defines these presentation-only dimensions alongside `BoardColors`, rather than
making the view-model library responsible for a particular view's sizing.
The public static class is referenced through `views:BoardLayoutConstants` in
XAML. Its 20-unit coordinate strips and 50-unit squares form a 440-by-440-unit
container with a 400-by-400-unit field area and no additional border thickness.
The viewbox has a margin of 10. A three-by-three layout grid
reserves a label strip on each edge. Row heights and column widths use one-time
bindings to the numeric constants so WPF converts them to `GridLength` values;
direct `x:Static` assignment supplies a `double` and fails during XAML loading.
label centers align with field centers while resizing.

An implicit `RowColumnLabelViewModel` data template renders centered text using
the existing light-field brush against the dark board frame. Field templates
continue to use their existing background trigger; no label overlays a field.

Within `ChessBoardView`, the left and right strips bind to `RowLabels`; the
top and bottom strips bind to `ColumnLabels`. See
[view models and coordinates](view-models.md#rank-and-file-labels) for label
ordering and the relationship between display positions and chess notation.
See [change notifications](view-models.md#change-notifications) before adding
state that changes after the board is displayed.

## Dynamic side panel

Below the menu, a three-column grid contains `ChessBoardView`, a 5-unit
vertical `GridSplitter`, and a `ContentControl` bound to
`ChessPadViewModel.ActiveSidePanel`. The view model uses CommunityToolkit.Mvvm's
`ObservableObject`, `[ObservableProperty]`, and `[RelayCommand]`; changing the
active panel raises `PropertyChanged`, so the content control updates without
code-behind navigation.

Implicit data templates map `MovesPanelViewModel` to `MovesPanelView` and
`BoardColorsSettingsViewModel` to `BoardColorsSettingsView`. The active panel is
typed to their shared `SidePanelViewModel` base class rather than `object`. The
Moves panel is selected initially and provides the destination for move history
when game behavior is added. **Game Menu > Moves** selects it; **Settings > Board
Colors** selects the palette panel. The palette panel currently displays the
existing colors as swatches; changing palette values is not yet supported.
Both content columns initially use `*` widths. Completing a splitter drag
saves both column widths; the window constructor restores them on subsequent
launches.

`Views\MainWindowSettings.settings` defines the user-scoped string settings
`ChessGameColumnWidth` and `SidePanelColumnWidth`, both initially empty.
`GridLengthConverter` converts between these strings and WPF column widths.
The settings use `ApplicationSettingsBase.Save()` to persist per-user values
in `user.config`, not in the repository or application installation folder.
The settings class namespace and property names are preserved so existing
saved widths remain compatible.

Open the `.settings` file in Visual Studio's Settings designer to edit the
settings. Its `SettingsSingleFileGenerator` custom tool updates the nested
`MainWindowSettings.Designer.cs`; do not edit that generated file directly.
Commit both files so command-line builds need not run the Visual Studio custom
tool. The project explicitly sets the generated namespace to
`ChessPad.WpfUi.Views` so regenerating retains the existing settings identity.

## Saved window size and location

`MainWindow` enables MahApps.Metro's `SaveWindowPosition`. Its
`GetWindowPlacementSettings()` override returns the same designer-backed
`MainWindowSettings` instance used by the splitter. A small non-generated
partial class implements `IWindowPlacementSettings`, leaving the designer free
to regenerate its accessors.

The user-scoped `Placement` setting stores the native `WINDOWPLACEMENT`
structure: the normal window size and location, plus the display state. MahApps
restores the placement at startup and saves it during normal window shutdown,
including **Game Menu > Exit**. Maximized windows reopen maximized while retaining
their normal restore bounds; minimized windows reopen normally rather than
remaining minimized. The first launch keeps the XAML defaults of 800 by 450.
MahApps also handles placement correction when the monitor layout changes.

`UpgradeSettings` is the boolean required by MahApps' placement-settings
contract. It defaults to `False`, matching MahApps' default and leaving automatic
settings migration between application versions disabled. No separate manual
move, resize, or close handlers compete with MahApps placement persistence.

## Application menu and exit command

A menu above the board provides **Game Menu > Moves**, **Game Menu > Exit**,
and **Settings > Board Colors**. The outer layout grid reserves an
automatic-height row for the menu and gives the remaining space to the
board/splitter/panel layout. Menu text remains at normal UI size rather than
scaling with the board, and the coordinate strips remain part of the board.
The Exit item's icon is a 16-by-16 `PackIconMaterial` with `Kind="Power"`,
provided by the existing MahApps.Metro.IconPacks package. It uses the inherited
foreground rather than a fixed color so it follows the menu's styling.

The Exit item uses WPF's built-in `ApplicationCommands.Close` routed command.
Its explicit command target is the containing `MainWindow`, which owns a
`CommandBinding` for that command. This keeps routing deterministic when the
item is inside the menu's popup. Underscores in `_Game Menu`, `_Moves`,
`E_xit`, `_Settings`, and `_Board Colors` provide keyboard access keys; no
additional keyboard shortcut is advertised.

The window's `OnExitExecuted` handler marks the command as handled and calls
`Application.Current.Shutdown()`. This exits the application through the normal
WPF lifecycle, including `App.OnExit` and disposal of the Autofac container;
it does not terminate the process abruptly.

Menu layout, routed commands, and application shutdown remain in the WPF UI
project. No application-lifetime service or WPF dependency is added to the
UI-independent view models for this presentation-only action.

## MetroWindow and theme resources

`MainWindow` derives from `MahApps.Metro.Controls.MetroWindow`, and its XAML root
is `mah:MetroWindow`. Both declarations must describe the same base type because
they contribute to the same partial class.

The title bar explicitly enables `ShowIconOnTitleBar` and uses a
`MetroWindow.IconTemplate` containing a `PackIconGameIcons` with
`Kind="ChessQueen"`. Its dimensions use template bindings, its margin is 4, and
its foreground uses the dynamic `MahApps.Brushes.IdealForeground` resource to
follow the active theme. This uses the existing MahApps.Metro.IconPacks package;
it customizes the title-bar visual, not the executable's embedded icon.

`App.xaml` merges these MahApps resource dictionaries:

```xml
<ResourceDictionary Source="pack://application:,,,/MahApps.Metro;component/Styles/Controls.xaml" />
<ResourceDictionary Source="pack://application:,,,/MahApps.Metro;component/Styles/Fonts.xaml" />
<ResourceDictionary Source="pack://application:,,,/MahApps.Metro;component/Styles/Themes/Light.Blue.xaml" />
```

These provide the control styles, fonts, and Light Blue theme used by the Metro
window. Board colors are supplied separately through the board's local brush
resources; changing the MahApps theme does not automatically change those values.

[Documentation index](README.md)
