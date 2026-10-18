# ChessPad: architecture and dependency injection

This guide describes the current WPF application, its view-model composition,
and how Autofac connects the objects used to display the chessboard.

ChessPad aims to record and replay chess moves. The implementation described
here currently renders an empty 8-by-8 board; the view models below do not yet
implement pieces, legal moves, recording, or replay.

## Contents

- [Project layout](#project-layout)
- [Technologies](#technologies)
- [View-model composition](#view-model-composition)
- [Field coordinates and ordering](#field-coordinates-and-ordering)
- [Dependency injection with Autofac](#dependency-injection-with-autofac)
- [Startup, ownership, and shutdown](#startup-ownership-and-shutdown)
- [WPF bindings and rendering](#wpf-bindings-and-rendering)
- [MetroWindow and theme resources](#metrowindow-and-theme-resources)
- [Building and checking the application](#building-and-checking-the-application)
- [Extending the design](#extending-the-design)
- [Troubleshooting](#troubleshooting)

## Project layout

The solution is `src\ChessPad.slnx` and contains two projects:

- `src\ChessPad.WpfUi\ChessPad.WpfUi.csproj`: the WPF executable.
- `src\ChessPad.ViewModels\ChessPad.ViewModels.csproj`: the UI-independent
  view-model library, referenced by the WPF project.

```text
src
|-- ChessPad.slnx
|-- ChessPad.ViewModels
|   |-- ChessPad.ViewModels.csproj
|   |-- ChessPadViewModel.cs
|   |-- ChessBoardViewModel.cs
|   |-- FieldViewModel.cs
|   `-- RowColumnLabelViewModel.cs
`-- ChessPad.WpfUi
	|-- ChessPad.WpfUi.csproj
	|-- App.xaml
	|-- App.xaml.cs
	`-- Views
		|-- BoardColors.cs
		|-- MainWindow.xaml
		`-- MainWindow.xaml.cs
```

- `App.xaml` defines application-wide resources, including MahApps styles.
- `App.xaml.cs` is the composition root: it registers dependencies, builds the
  container, resolves the main window, and disposes the container on exit.
- `Views` contains the WPF presentation and its code-behind.
- `ChessPad.ViewModels` contains the objects exposed through data binding, using
  the namespace `ChessPad.ViewModels`.
- `BoardColors` centralizes WPF color values referenced by the board's brushes.
  It remains in `ChessPad.WpfUi.Views` because it is a presentation helper, not
  a view model, and depends on `System.Windows.Media.Color`.

The names `Views` and `ViewModels` are plural because they group multiple types.
Individual type names remain singular. The WPF project references the view-model
library; the library does not reference WPF, Autofac, or the UI project.

## Technologies

The UI application targets `net10.0-windows` with WPF enabled. The view-model
library targets `net10.0` without Windows or WPF dependencies. Both projects
have nullable reference types and implicit usings enabled.

The UI project references Autofac and the MahApps packages below. The
CommunityToolkit.Mvvm reference belongs to the view-model library.

The solution currently uses:

| Package | Version | Role in this implementation |
|---|---|---|
| Autofac | 9.3.4 | Constructor injection, registrations, and delegate factories |
| MahApps.Metro | 2.4.11 | `MetroWindow`, control styles, fonts, and theme resources |
| CommunityToolkit.Mvvm | 8.4.2 | Available for MVVM features; the current board view models do not use its observable types or commands |
| MahApps.Metro.IconPacks | 6.2.1 | Available for icons; the current board template does not use icons |

## View-model composition

The runtime object graph is:

```text
MainWindow
`-- DataContext: ChessPadViewModel
	`-- ChessBoard: ChessBoardViewModel
		|-- Fields: IReadOnlyList<FieldViewModel> (64 fields)
		|-- RowLabels: IReadOnlyList<RowColumnLabelViewModel> (8 ranks)
		`-- ColumnLabels: IReadOnlyList<RowColumnLabelViewModel> (8 files)
```

### ChessPadViewModel: application-level composition

`ChessPadViewModel` receives a `ChessBoardViewModel` through its constructor and
exposes that same instance through its get-only `ChessBoard` property:

```csharp
public sealed class ChessPadViewModel
{
	public ChessBoardViewModel ChessBoard { get; }

	public ChessPadViewModel(ChessBoardViewModel chessBoard)
	{
		ChessBoard = chessBoard;
	}
}
```

This is composition: the application view model **has a** chessboard view model;
it does not inherit from one. It neither creates fields nor calculates their
coordinates. Keeping board-specific construction in `ChessBoardViewModel`
leaves the application-level view model focused on composition.

### ChessBoardViewModel: board structure

`ChessBoardViewModel` receives `FieldViewModel.Factory` and
`RowColumnLabelViewModel.Factory` delegates. Its constructor:

1. Creates eight rank labels and eight file labels using the label factory.
2. Exposes those collections through read-only `RowLabels` and `ColumnLabels`.
3. Allocates a list with capacity for 64 fields.
4. Iterates over rows 0 through 7 and columns 0 through 7 within each row.
5. Calls the field factory once for each coordinate pair.
6. Exposes a read-only wrapper through `Fields`.

Its field collection is:

```csharp
public IReadOnlyList<FieldViewModel> Fields { get; }
```

The board owns a flat, row-major collection rather than separate collections of
rows and columns. `AsReadOnly()` prevents callers from modifying the collection
through the exposed wrapper. The board does not subsequently modify the backing
list.

### FieldViewModel: one board location

Each `FieldViewModel` is a sealed record with two positional properties:

```csharp
public sealed record FieldViewModel(int Row, int Column)
{
	// Autofac matches delegate parameters to constructor parameters by name.
	public delegate FieldViewModel Factory(int Row, int Column);

	public bool IsLight => (Row + Column) % 2 == 0;
}
```

`Row` and `Column` are generated init-only record properties. `IsLight` is a
computed property, not independently stored state. Records also provide value
equality: different field instances with the same coordinates compare equal.

The board generates only coordinates in the range 0 through 7. The record itself
has no range validation, so callers outside the board constructor must not assume
that constructing a field automatically validates its coordinates.

## Field coordinates and ordering

The board has 8 rows and 8 columns, for a total of 64 fields.

| Collection index | Row | Column | IsLight |
|---|---|---|---|
| 0 | 0 | 0 | `true` |
| 1 | 0 | 1 | `false` |
| 7 | 0 | 7 | `false` |
| 8 | 1 | 0 | `false` |
| 9 | 1 | 1 | `true` |
| 63 | 7 | 7 | `true` |

The relationship between an index and a coordinate is:

```text
index  = row * 8 + column
row    = index / 8       (integer division)
column = index % 8
```

The view lays out these items from left to right, then top to bottom. Therefore,
`(0, 0)` appears at the top-left and `(7, 7)` at the bottom-right.

### Rank and file labels

Chess rows are called **ranks** and use numbers 1 through 8. Columns are called
**files** and use letters A through H (traditionally lowercase in notation).
The board displays White's perspective: A through H from left to right and
8 through 1 from top to bottom. Thus `(0, 0)` corresponds to A8 and `(7, 7)`
corresponds to H1. Board inversion is not implemented.

`ChessBoardViewModel` exposes two read-only collections of eight
`RowColumnLabelViewModel` records:

- `RowLabels`: `8, 7, 6, 5, 4, 3, 2, 1` for the left and right edges.
- `ColumnLabels`: `A, B, C, D, E, F, G, H` for the top and bottom edges.

Each label has an `Index` (0 through 7, in visual order) and a `Label` string.
These are presentation coordinates, not additional board fields. The same row
collection is rendered on both vertical edges, and the same column collection
on both horizontal edges: 16 label objects produce 32 visible labels.

The board receives `RowColumnLabelViewModel.Factory` through constructor
injection. Autofac supplies the delegate from the label type's registration.
Its `Index` and `Label` parameters match the record constructor names exactly,
just as the field factory's parameters do. The view models remain independent
of WPF and carry no pixel positions or brushes.

The parity expression `(Row + Column) % 2 == 0` gives alternating colors across
both axes. The current presentation intentionally starts with a light field at
the top-left. Labels do not change the field ordering or colors.

## Dependency injection with Autofac

### Registrations

`App.xaml.cs` registers the window and all four view-model types:

```csharp
var builder = new ContainerBuilder();
builder.RegisterType<MainWindow>().AsSelf();
builder.RegisterType<ChessPadViewModel>().AsSelf();
builder.RegisterType<ChessBoardViewModel>().AsSelf();
builder.RegisterType<FieldViewModel>().AsSelf();
builder.RegisterType<RowColumnLabelViewModel>().AsSelf();
_container = builder.Build();
```

`AsSelf()` exposes each registration by its concrete type. Interfaces are not
required for this object graph. The view models do not reference Autofac or
resolve dependencies from the container themselves.

### Why fields use a delegate factory

The window and the application-level view models have dependencies Autofac can
resolve by type. Fields also need two runtime values: their row and column.
Those values differ for every field, so they are supplied when each field is
created rather than registered as global integer dependencies.

The board's constructor accepts:

```csharp
public ChessBoardViewModel(
    FieldViewModel.Factory createField,
    RowColumnLabelViewModel.Factory createLabel)
```

It creates each field with:

```csharp
fields.Add(createField(row, column));
```

Autofac automatically provides this custom delegate factory from the
`FieldViewModel` registration. There is no separate factory class or explicit
factory registration in the current implementation.

### Parameter names matter

Autofac custom delegate factories match delegate arguments to constructor
parameters **by name**. The record constructor and delegate therefore both use
`Row` and `Column`, with matching spelling and capitalization:

```csharp
FieldViewModel(int Row, int Column)
FieldViewModel Factory(int Row, int Column)
```

This is particularly important because both arguments are `int`. A generic
`Func<int, int, FieldViewModel>` is not a drop-in replacement for this named
parameter mapping. If the record's constructor parameter names change, update
the delegate parameter names at the same time.

The capitalized delegate parameters deliberately mirror the positional record
parameters, even though ordinary method parameters typically use camel case.

### Resolution and lifetimes

When Autofac resolves `MainWindow`, it follows this chain:

```text
Resolve<MainWindow>()
  -> resolve ChessPadViewModel
	 -> resolve ChessBoardViewModel
		-> provide FieldViewModel.Factory
		-> provide RowColumnLabelViewModel.Factory
		-> create 8 rank labels and 8 file labels
		-> create 64 fields with coordinates
  -> initialize window and assign its DataContext
```

No registration specifies a shared lifetime. Autofac's default is instance per
dependency: separate resolutions create separate instances. Each field factory
call creates a new field instance, even if the same coordinates are supplied
again. Resolving another `ChessPadViewModel` creates another board and another
set of fields; the app does not register a global singleton board.

## Startup, ownership, and shutdown

1. The `App` constructor builds the Autofac container.
2. Application resources are loaded from `App.xaml` during WPF initialization.
3. `OnStartup` calls its base implementation and resolves `MainWindow`.
4. The resolved window is assigned to the application's `MainWindow` property.
5. Calling `Show()` displays it.
6. On application exit, `OnExit` disposes the container and calls the base
   implementation.

`App.xaml` intentionally has no `StartupUri`. A `StartupUri` would ask WPF to
create a window separately from the constructor-injected startup path.

The container is owned by `App` and disposed at application exit. The current
view models hold no disposable resources. If resource-owning dependencies are
added later, choose registrations and scopes deliberately rather than assuming
that the visual containment hierarchy creates Autofac lifetime scopes.

## WPF bindings and rendering

### The window's DataContext

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
created by Autofac.

### Binding to the composed board

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
`ChessPad.ViewModels`; Autofac registrations and the runtime object graph remain
unchanged. Local board brushes use the `views:BoardColors` XAML prefix instead.

For each item, the data template declares:

```xml
<DataTemplate DataType="{x:Type viewmodels:FieldViewModel}">
```

This identifies the type displayed by the template. It does **not** instantiate
a `FieldViewModel` or bypass DI. Each template receives an existing item from
`Fields` as its data context.

### Colors and scaling

Each field template uses a `Border` with a dark background by default. A
`DataTrigger` switches the background to the light brush when `IsLight` is true.
Window, frame, light-field, and dark-field brushes reference `BoardColors`.

A `Viewbox` with `Stretch="Uniform"` scales the fixed-size board container while
preserving its aspect ratio. The container is 448 by 448 units, includes an
8-unit border, and sits inside the viewbox with a margin of 10. A three-by-three
layout grid reserves a 24-unit strip on each edge for labels, leaving the field
area at its previous 384 by 384 units. Each label strip uses an eight-cell
`UniformGrid`, so label centers align with field centers while resizing.

An implicit `RowColumnLabelViewModel` data template renders centered text using
the existing light-field brush against the dark board frame. Field templates
continue to use their existing background trigger; no label overlays a field.

### Change notifications

The current view models expose constructor-initialized, get-only properties and
a read-only collection. They do not implement `INotifyPropertyChanged`, and the
collection is not observable. This is sufficient for the current board, which
is built before it is bound and does not change afterward.

If future features replace the board, modify the collection, or update field
state after display, add the corresponding property or collection notifications.
Having CommunityToolkit.Mvvm referenced does not automatically make these plain
classes observable.

## MetroWindow and theme resources

`MainWindow` derives from `MahApps.Metro.Controls.MetroWindow`, and its XAML root
is `mah:MetroWindow`. Both declarations must describe the same base type because
they contribute to the same partial class.

`App.xaml` merges these MahApps resource dictionaries:

```xml
<ResourceDictionary Source="pack://application:,,,/MahApps.Metro;component/Styles/Controls.xaml" />
<ResourceDictionary Source="pack://application:,,,/MahApps.Metro;component/Styles/Fonts.xaml" />
<ResourceDictionary Source="pack://application:,,,/MahApps.Metro;component/Styles/Themes/Light.Blue.xaml" />
```

These provide the control styles, fonts, and Light Blue theme used by the Metro
window. Board colors are supplied separately through the board's local brush
resources; changing the MahApps theme does not automatically change those values.

## Building and checking the application

Use Windows with the .NET 10 SDK and WPF build support, or Visual Studio with
.NET desktop development support. From the repository root:

```powershell
dotnet build .\src\ChessPad.slnx
dotnet run --project .\src\ChessPad.WpfUi\ChessPad.WpfUi.csproj
```

The view-model library can also be built independently without WPF:

```powershell
dotnet build .\src\ChessPad.ViewModels\ChessPad.ViewModels.csproj
```

A useful manual check is:

1. Start the application and confirm that the Metro window opens without a
   dependency-resolution or resource-loading exception.
2. Confirm that the board displays 8 rows and 8 columns.
3. Check that colors alternate horizontally and vertically, with a light field
   at the top-left.
4. Confirm A through H appear left to right along both horizontal edges and
   8 through 1 appear top to bottom along both vertical edges.
5. Resize the window and confirm that the board remains square and all labels
   remain centered against their corresponding fields.
6. Close the window and confirm that the application exits normally.

For automated verification of this design, check the actual object graph and
observable results, not only whether individual constructors compile:

- Resolve the application view model through the registered container.
- Confirm that `ChessBoard.Fields` contains exactly 64 entries.
- Check every entry's row, column, and parity against its row-major index.
- Confirm that field and label factory calls produce distinct object instances.
- Check that each label collection contains exactly eight entries, with indices
  0 through 7, rank text `87654321`, and file text `ABCDEFGH`.
- Confirm that `MainWindow.DataContext` is a `ChessPadViewModel` and that the
  board `ItemsControl.ItemsSource` refers to its `ChessBoard.Fields` collection.
- Verify all 32 rendered labels across the four edges, including their text and
  center alignment with fields at multiple window sizes.

These are verification guidelines, not a claim that a persistent automated test
suite is included with this documentation.

## Extending the design

Keep the existing responsibility boundaries when adding features:

- Put application-level composition in `ChessPadViewModel`.
- Put board-level state and behavior in `ChessBoardViewModel`.
- Put per-field presentation state in `FieldViewModel`.
- Keep WPF templates, brushes, and layout in the view layer.
- Register new services in the composition root and inject them where needed.

For example, if fields later need an injected service, Autofac can resolve that
service while the delegate continues to supply the runtime coordinate values.
Keep the delegate's parameter names aligned with the corresponding constructor
parameters and register any additional dependencies.

Avoid injecting the container into view models to call `Resolve` from them.
Constructor injection and the field factory already make their dependencies
explicit without coupling board logic to Autofac.

Do not change these registrations to singleton lifetimes merely to share an
instance accidentally. Decide first whether multiple windows should share one
board or own independent boards, then choose lifetimes and scopes accordingly.

## Troubleshooting

| Symptom | What to check |
|---|---|
| Autofac cannot resolve a window or view model | Confirm that its concrete type and all constructor dependencies are registered in `App.xaml.cs`. |
| Autofac cannot supply the field coordinates | Confirm that the board receives `FieldViewModel.Factory` and that its parameters exactly match the record constructor's `Row` and `Column` names. |
| The window opens but the board is empty | Check `MainWindow.DataContext`, the `ChessBoard.Fields` binding path, and the field count. Inspect WPF binding diagnostics for path errors. |
| XAML reports an unknown field type | Check the `viewmodels` namespace, the public `FieldViewModel` declaration, and any C# compilation errors. |
| The Metro window has missing resources or unexpected styling | Check the MahApps package reference and all three merged resource dictionaries in `App.xaml`. |
| XAML-generated code reports conflicting base classes | Ensure the XAML root is `mah:MetroWindow` and the code-behind derives from `MetroWindow`. |
| Later changes to state do not appear in the UI | Check property and collection notifications; the current view models are not observable. |

[Return to the README](../README.md)
