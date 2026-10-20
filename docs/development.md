# Development and troubleshooting

[Documentation index](README.md)

## Building and running

Use Windows with the .NET 10 SDK and WPF build support, or Visual Studio with
.NET desktop development support. From the repository root:

```powershell
dotnet build .\src\ChessPad.slnx
dotnet run --project .\src\App\ChessPad.WpfUi\ChessPad.WpfUi.csproj
```

The view-model library can also be built independently without WPF:

```powershell
dotnet build .\src\App\ChessPad.ViewModels\ChessPad.ViewModels.csproj
```

## Running unit tests

See [Test projects](testing.md) for the detailed case inventory, project
configuration, integration-host design, internal access, and troubleshooting.
The commands and scope below provide a quick development reference.

`src\Tests\ChessPad.ViewModels.Tests\ChessPad.ViewModels.Tests.csproj` uses MSTest
4.2.3 with VSTest and targets `net10.0`. It references only `ChessPad.ViewModels`;
no WPF startup, Autofac container, logging provider, network, or filesystem work
is involved. Tests supply factory delegates directly and can run in parallel.

From the repository root, run the test project or the whole solution:

```powershell
dotnet test .\src\Tests\ChessPad.ViewModels.Tests\ChessPad.ViewModels.Tests.csproj
dotnet test .\src\ChessPad.slnx
```

In Visual Studio, open `src\ChessPad.slnx` and run `ChessPad.ViewModels.Tests`
from Test Explorer. The initial suite contains 18 cases, including data-driven
cases, covering:

- All 64 coordinate pairs in row-major order.
- The expected checkerboard colors across all eight rows.
- Rank labels 8 through 1 and file labels A through H, with indices 0 through 7.
- Field and label factory arguments and preservation of returned instances.
- Read-only collection behavior, including rejected mutation attempts.
- Preservation of the board instance injected into `ChessPadViewModel`.
- Preservation of the index and text passed into label records.

## Running integration tests

`src\Tests\ChessPad.WpfUi.IntegrationTests\ChessPad.WpfUi.IntegrationTests.csproj`
uses MSTest 4.2.3 and VSTest with `net10.0-windows` and WPF enabled. Run it in
Visual Studio Test Explorer or from the repository root:

```powershell
dotnet test .\src\Tests\ChessPad.WpfUi.IntegrationTests\ChessPad.WpfUi.IntegrationTests.csproj
dotnet test .\src\ChessPad.slnx
dotnet test .\src\ChessPad.slnx --configuration Release
```

The 14 integration cases exercise seven scenarios for each of Serilog and
NLog: menu Exit, standard window close, startup failure, file rolling with
retention, and window placement after closing in normal, maximized, or minimized
state. Placement checks reload settings from disk and verify a new window's
position, size, and state. The lifecycle scenarios also verify the real Autofac registrations,
typed logger resolution, board binding, 32 rendered coordinate labels, first
render logging, structured properties, and session/category metadata. Successful
shutdown must dispose the container and release the log files. Startup failure
must log the exception and propagate it. Debug probes must appear only in Debug
builds. Rolling checks cross the real 10 MiB threshold and seed 20 historical
files to verify retention limits, removal of the oldest files, and preservation
of recent files.

The suite contains 32 cases in total: 18 unit cases and 14 integration cases.

### Process and filesystem isolation

Each integration case starts a fresh `dotnet` child process running the test
assembly's explicit STA entry point, `IntegrationTestHost`. This allows real
`Application.Run()` and shutdown without creating a WPF application inside the
Visual Studio test host. The same assembly remains discoverable by VSTest; no
additional host project or runtime test mode is added to the shipped app.

Each child receives an explicit provider and a uniquely named directory beneath
`%TEMP%\ChessPad.IntegrationTests`. The directory is deleted after the child
exits. Normal `%LOCALAPPDATA%\ChessPad\Logs` files are never used or deleted.
A test-only settings provider also redirects the production window settings to
that directory, preserving serialization without touching normal user settings.
A 60-second timeout terminates only that case's child process tree and reports
its captured standard output and error. Assertions in the child produce a
nonzero process result; the parent requires both success and a completion marker.

These are Windows desktop integration tests. They require the .NET SDK/`dotnet`
on PATH and a Windows environment capable of showing WPF windows; windows may
briefly appear. Run them in a desktop-capable agent, not a headless Linux runner.
The fixed-date retention fixtures do not change the machine clock. Forced
runtime termination and native crash scenarios are not tested; the startup
failure case verifies the managed failure path without deliberately crashing
Windows or invoking crash dialogs.

## Manual verification

1. Start the application and confirm that the Metro window opens without a
   dependency-resolution or resource-loading exception.
2. Confirm that the board displays 8 rows and 8 columns.
3. Check that colors alternate horizontally and vertically, with a light field
   at the top-left.
4. Confirm A through H appear left to right along both horizontal edges and
   8 through 1 appear top to bottom along both vertical edges.
5. Resize the window and confirm that the board remains square and all labels
   remain centered against their corresponding fields, while the menu stays above
   the board at normal UI size.
6. Open **Game Menu**, select **Exit**, and confirm that the application exits
   normally. Restart and also check the window's standard close button.
7. Follow the [logging checks](logging.md#checking-logging) for each provider and
   verify startup, first-render, and exit events in the corresponding files.

## Automated verification guidance

Check the actual object graph and observable results, not only whether individual
constructors compile:

- Resolve the application view model through the registered container.
- Confirm that `ChessBoard.Fields` contains exactly 64 entries.
- Check every entry's row, column, and parity against its row-major index.
- Confirm that field and label factory calls produce distinct object instances.
- Check that each label collection contains exactly eight entries, with indices
  0 through 7, rank text `87654321`, and file text `ABCDEFGH`.
- Confirm that `MainWindow.DataContext` is a `ChessPadViewModel`, that
  `ChessBoardView.DataContext` is its existing `ChessBoard` instance, and that the
  board `ItemsControl.ItemsSource` refers to that board's `Fields` collection.
- Verify all 32 rendered labels across the four edges, including their text and
  center alignment with fields at multiple window sizes.

The unit suite verifies view-model behavior and direct factory use. The separate
integration suite verifies container resolution, rendered label text, menu
command routing, shutdown, and actual logging output. Multi-size center
alignment, visual styling, and actual keyboard/mouse interaction remain manual
checks; command tests execute the routed command rather than automating clicks.

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

See [dependency injection](dependency-injection.md) for lifetimes and factories,
[view models](view-models.md#change-notifications) for notification requirements,
and [logging](logging.md) for provider-independent event calls.

## Troubleshooting

| Symptom | What to check |
|---|---|
| Autofac cannot resolve a window or view model | Confirm that its concrete type and all constructor dependencies are registered in `App.xaml.cs`. |
| Autofac cannot supply the field coordinates | Confirm that the board receives `FieldViewModel.Factory` and that its parameters exactly match the record constructor's `Row` and `Column` names. |
| The window opens but the board is empty | Check `MainWindow.DataContext`, the control's `DataContext="{Binding ChessBoard}"`, its internal `Fields` binding, and the field count. Inspect WPF binding diagnostics for path errors. |
| XAML reports an unknown field type | Check the `viewmodels` namespace, the public `FieldViewModel` declaration, and any C# compilation errors. |
| The Metro window has missing resources or unexpected styling | Check the MahApps package reference and all three merged resource dictionaries in `App.xaml`. |
| XAML-generated code reports conflicting base classes | Ensure the XAML root is `mah:MetroWindow` and the code-behind derives from `MetroWindow`. |
| Later changes to state do not appear in the UI | Check property and collection notifications; the current view models are not observable. |

For missing log files or unexpected logging levels, see
[logging output and internal diagnostics](logging.md#files-levels-and-retention).

[Documentation index](README.md)
