# Architecture

[Documentation index](README.md)

## Project layout

The solution is `src\ChessPad.slnx` and contains four projects:

- `src\App\ChessPad.WpfUi\ChessPad.WpfUi.csproj`: the WPF executable.
- `src\App\ChessPad.ViewModels\ChessPad.ViewModels.csproj`: the UI-independent
  view-model library, referenced by the WPF project.
- `src\Tests\ChessPad.ViewModels.Tests\ChessPad.ViewModels.Tests.csproj`: MSTest unit
  tests referencing only the view-model library.
- `src\Tests\ChessPad.WpfUi.IntegrationTests\ChessPad.WpfUi.IntegrationTests.csproj`:
  Windows MSTest integration tests referencing the WPF executable.

Solution Explorer and the physical layout both group production projects under
`App` and test projects under `Tests`. Both directories are inside `src`, beside
`ChessPad.slnx`. Test project references point to the sibling `App` tree.

```text
ChessPad (solution)
|-- App
|   |-- ChessPad.ViewModels
|   `-- ChessPad.WpfUi
`-- Tests
    |-- ChessPad.ViewModels.Tests
    `-- ChessPad.WpfUi.IntegrationTests
```

The physical directory layout is:

```text
src
|-- ChessPad.slnx
|-- App
|   |-- ChessPad.ViewModels
|   |   |-- ChessPad.ViewModels.csproj
|   |   |-- ChessPadViewModel.cs
|   |   |-- ChessBoardViewModel.cs
|   |   |-- FieldViewModel.cs
|   |   `-- RowColumnLabelViewModel.cs
|   `-- ChessPad.WpfUi
|       |-- ChessPad.WpfUi.csproj
|       |-- App.xaml
|       |-- App.xaml.cs
|       `-- Views
|           |-- BoardColors.cs
|           |-- MainWindow.xaml
|           `-- MainWindow.xaml.cs
`-- Tests
	|-- ChessPad.ViewModels.Tests
	|   |-- ChessPad.ViewModels.Tests.csproj
	|   |-- MSTestSettings.cs
	|   |-- ChessPadViewModelTests.cs
	|   |-- ChessBoardViewModelTests.cs
	|   |-- FieldViewModelTests.cs
	|   `-- RowColumnLabelViewModelTests.cs
	`-- ChessPad.WpfUi.IntegrationTests
		|-- ChessPad.WpfUi.IntegrationTests.csproj
		|-- MSTestSettings.cs
		|-- ApplicationIntegrationTests.cs
		`-- IntegrationTestHost.cs
```

- `App.xaml` defines application-wide resources, including MahApps styles.
- `App.xaml.cs` is the composition root: it initializes logging, registers
  dependencies, builds the container, resolves the main window, and owns cleanup.
- `Views` contains the WPF presentation and its code-behind.
- `ChessPad.ViewModels` contains the objects exposed through data binding, using
  the namespace `ChessPad.ViewModels`.
- `BoardColors` centralizes WPF color values referenced by the board's brushes.
  It remains in `ChessPad.WpfUi.Views` because it is a presentation helper, not
  a view model, and depends on `System.Windows.Media.Color`.

The names `Views` and `ViewModels` are plural because they group multiple types.
Individual type names remain singular. With only a few view models, there is
no redundant `ViewModels` subfolder inside the view-model project.

## Dependency direction

```text
ChessPad.WpfUi  ->  ChessPad.ViewModels
```

The view-model library does not reference WPF, Autofac, Serilog, NLog, or the UI
project. Constructor injection supplies its dependencies without requiring it
to know how the application container works.

Presentation types such as brushes, colors, and window commands remain in the
UI project. The field exposes `IsLight`; the view chooses the actual color.

## Technologies

The UI application targets `net10.0-windows` with WPF enabled. The view-model
library targets `net10.0` without Windows or WPF dependencies. Both projects
have nullable reference types and implicit usings enabled.

The UI project references Autofac, MahApps, and the logging providers below.
The CommunityToolkit.Mvvm reference belongs to the view-model library.

| Package | Version | Role in this implementation |
|---|---|---|
| Autofac | 9.3.4 | Constructor injection, registrations, and delegate factories |
| MahApps.Metro | 2.4.11 | `MetroWindow`, control styles, fonts, and theme resources |
| CommunityToolkit.Mvvm | 8.4.2 | Available for MVVM features; the current board view models do not use its observable types or commands |
| MahApps.Metro.IconPacks | 6.2.1 | Exit menu and chess queen title-bar icons |
| Serilog.Extensions.Logging | 10.0.0 | Connects `ILogger<T>` and `ILoggerFactory` to Serilog |
| Serilog.Sinks.File | 7.0.0 | Daily and size-based rolling JSON log files |
| Serilog.Sinks.Debug | 3.0.0 | Visual Studio Debug output in Debug builds |
| NLog.Extensions.Logging | 6.2.2 | Alternative provider, including NLog file and debugger targets |
| MSTest | 4.2.3 | VSTest unit tests (`net10.0`) and WPF integration tests (`net10.0-windows`) |

## Responsibility boundaries

- `ChessPadViewModel`: application-level composition.
- `ChessBoardViewModel`: board structure, field ordering, and coordinate labels.
- `FieldViewModel`: one field's coordinates and computed light/dark state.
- `RowColumnLabelViewModel`: one coordinate label's index and text.
- Views: WPF templates, layout, theme resources, and presentation-only commands.
- `App`: DI configuration, application lifecycle, and logging infrastructure.

See [view models](view-models.md) for the object graph,
[dependency injection](dependency-injection.md) for construction and ownership,
and [WPF presentation](wpf-presentation.md) for rendering.

[Documentation index](README.md)
