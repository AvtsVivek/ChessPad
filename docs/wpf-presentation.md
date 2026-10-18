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

The board's items source is:

```xml
<ItemsControl ItemsSource="{Binding ChessBoard.Fields}">
```

WPF reads `ChessBoard` from the window's `ChessPadViewModel`, then reads `Fields`
from the board. An 8-row, 8-column `UniformGrid` arranges the 64 items in their
collection order.

Because the view models live in a separate assembly, the window declares:

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
preserving its aspect ratio. The container is 448 by 448 units, includes an
8-unit border, and sits inside the viewbox with a margin of 10. A three-by-three
layout grid reserves a 24-unit strip on each edge for labels, leaving the field
area at 384 by 384 units. Each label strip uses an eight-cell `UniformGrid`, so
label centers align with field centers while resizing.

An implicit `RowColumnLabelViewModel` data template renders centered text using
the existing light-field brush against the dark board frame. Field templates
continue to use their existing background trigger; no label overlays a field.

The left and right strips bind to `ChessBoard.RowLabels`; the top and bottom
strips bind to `ChessBoard.ColumnLabels`. See
[view models and coordinates](view-models.md#rank-and-file-labels) for label
ordering and the relationship between display positions and chess notation.
See [change notifications](view-models.md#change-notifications) before adding
state that changes after the board is displayed.

## Application menu and exit command

A menu above the board provides **Game Menu > Exit**. The outer layout grid
reserves an automatic-height row for the menu and gives the remaining space to
the board's `Viewbox`. Menu text therefore stays at normal UI size rather than
scaling with the board, and the coordinate strips remain part of the board.
The Exit item's icon is a 16-by-16 `PackIconMaterial` with `Kind="Power"`,
provided by the existing MahApps.Metro.IconPacks package. It uses the inherited
foreground rather than a fixed color so it follows the menu's styling.

The Exit item uses WPF's built-in `ApplicationCommands.Close` routed command.
Its explicit command target is the containing `MainWindow`, which owns a
`CommandBinding` for that command. This keeps routing deterministic when the
item is inside the menu's popup. The underscores in `_Game Menu` and `E_xit`
provide keyboard access keys; no additional keyboard shortcut is advertised.

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
