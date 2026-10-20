# Test projects

[Documentation index](README.md)

## Purpose and project boundaries

ChessPad separates fast view-model unit tests from Windows integration tests.
Both use MSTest 4.2.3 with VSTest and are included in `src\ChessPad.slnx` under
its `Tests` solution folder. The physical layout matches that grouping:

```text
src
|-- ChessPad.slnx
|-- App
|   |-- ChessPad.ViewModels
|   `-- ChessPad.WpfUi
`-- Tests
	|-- ChessPad.ViewModels.Tests
	`-- ChessPad.WpfUi.IntegrationTests
```

| Project | Target | Production reference | Responsibility |
|---|---|---|---|
| `ChessPad.ViewModels.Tests` | `net10.0` | `ChessPad.ViewModels` | Model behavior, factory arguments, reference identity, and collection contracts |
| `ChessPad.WpfUi.IntegrationTests` | `net10.0-windows`, WPF | `ChessPad.WpfUi` | Real container resolution, rendered bindings, lifecycle, and logging output |

The unit project does not reference WPF, Autofac, or a logging backend. It supplies
factory delegates directly. The integration project deliberately references the
actual application rather than recreating its registrations or logging settings.
Neither project is referenced by a production project.

The `MSTest` package supplies the test framework, adapter, and test SDK. Each
project's `Using` item makes `Microsoft.VisualStudio.TestTools.UnitTesting`
available globally. `MSTestSettings.cs` enables method-level parallelization.

## Running tests

Run these commands from the repository root with the .NET 10 SDK available.
The full solution and integration project require Windows with WPF support;
the unit project can run independently without the Windows application.

```powershell
# Fast, UI-independent unit tests.
dotnet test .\src\Tests\ChessPad.ViewModels.Tests\ChessPad.ViewModels.Tests.csproj

# Real Windows application and logging integration tests.
dotnet test .\src\Tests\ChessPad.WpfUi.IntegrationTests\ChessPad.WpfUi.IntegrationTests.csproj

# Both projects through the saved solution.
dotnet test .\src\ChessPad.slnx

# Also verify that Debug logging is excluded in Release.
dotnet test .\src\ChessPad.slnx --configuration Release
```

In Visual Studio, open `src\ChessPad.slnx`, build, and open **Test > Test
Explorer**. Select either project or run both. The current suite has **32 data
cases: 18 unit cases and 14 integration cases**. DataRow entries count as
separate cases; the number of test methods is smaller. A successful build alone
is not a passing test run: check discovered, passed, failed, and skipped counts.

For a focused CLI run, use a fully qualified test filter, for example:

```powershell
dotnet test .\src\Tests\ChessPad.ViewModels.Tests\ChessPad.ViewModels.Tests.csproj --filter "FullyQualifiedName~ChessBoardViewModelTests"
```

Integration lifecycle cases briefly show real windows. They require a Windows
desktop environment capable of displaying WPF and `dotnet` on PATH. Do not
expect the integration project to run on Linux or a non-desktop test agent.
There is no repository CI configuration supplied by these projects.

## Unit project: ChessPad.ViewModels.Tests

The suite creates fresh model instances in each test, with no application
startup, filesystem access, network access, or shared container state.

| File | Data cases | Verified behavior |
|---|---:|---|
| `ChessBoardViewModelTests.cs` | 5 | Complete row-major coordinates; field factory calls and identity; all rank/file labels and indices; label factory calls and identity; rejected collection mutations |
| `ChessPadViewModelTests.cs` | 1 | The application model exposes the exact injected board instance |
| `FieldViewModelTests.cs` | 8 | One case per row verifies stored coordinates and light/dark parity across all 64 fields |
| `RowColumnLabelViewModelTests.cs` | 4 | Rank/file endpoints preserve the supplied index and text |

### Factory contracts and identity

The board constructor receives `FieldViewModel.Factory` and
`RowColumnLabelViewModel.Factory`. Recording delegates capture every argument
and created instance. Tests check that the board retains those exact instances,
not copies or independently reconstructed equivalents. A private `CreateBoard`
helper supplies ordinary construction delegates for cases that do not need call
recording. No mock framework is needed for these delegate contracts.

These are not Autofac tests: they verify what the board does with a supplied
factory. Container-generated factories are exercised separately by integration
tests using the real application registrations.

### Collection and coordinate contracts

The board tests compare all 64 coordinates in row-major order. Label tests
compare every rank (`8` through `1`), file (`A` through `H`), and index (`0`
through `7`). Color cases specify expected patterns rather than merely
repeating the production parity expression.

The collection test accesses the exposed collections through `IList<T>`, checks
read-only flags, and requires add, replacement, and clear attempts to throw
`NotSupportedException`. It also checks state after the rejected operations.
It verifies read-only collection behavior, not a general deep-immutability policy
for every object that might be added to the model in future.

## Integration project: ChessPad.WpfUi.IntegrationTests

### Parent test and executable child host

The project is an executable as well as a VSTest-discoverable test assembly:

- `ApplicationIntegrationTests.cs` contains the parent MSTest fixture.
- `IntegrationTestHost.cs` contains the explicit STA `Main` entry point.
- The project sets `OutputType` to `Exe`, enables WPF, and selects
  `IntegrationTestHost` through `StartupObject`.

For each DataRow, the parent creates a unique temporary directory and starts:

```text
dotnet <test-assembly.dll> <provider> <scenario> <temporary-log-directory>
```

The child selects the requested scenario, performs assertions, and prints
`PASS: <provider> / <scenario>` only on success. Its entry point writes any
failure to standard error and returns exit code 1. The parent requires exit
code 0, the matching PASS marker, and empty standard error. A crash or a process
that exits without running the assertions cannot satisfy that full contract.

A separate process is important because WPF application lifetime and provider
state should not leak between cases or into the Visual Studio test host. The
child can execute real `Application.Run()` and shutdown on its STA thread.
There is no extra host project and no command-line test mode in the shipped app.

### Scenario matrix

Each scenario runs once with Serilog and once with NLog, giving 14 cases.

| Scenario | Action | Required outcome |
|---|---|---|
| `menu-exit` | Open the real menu and execute its routed Close command against the main window | Correct command/target, normal window/application exit, exit code 0, disposed container, expected lifecycle logs |
| `window-close` | Call `Close()` on the displayed main window | Normal exit notifications, exit code 0, disposed container, expected lifecycle logs |
| `startup-failure` | Throw an expected `InvalidOperationException` from an application Startup subscriber | Original exception propagates, exactly one Fatal event describes startup failure, no first-render event |
| `rolling` | Seed historical files and emit a payload crossing the actual size threshold | Size archive and current file, expected filename shape, bounded retention, both emitted events preserved |
| `placement-Normal` | Move and resize the window, close it, reload settings from disk, and open a new window | Normal state and saved position/size restored within 2 WPF units for native pixel rounding |
| `placement-Maximized` | Maximize the resized window before closing and reopening | Maximized state restored with the original normal restore bounds |
| `placement-Minimized` | Minimize the resized window before closing and reopening | Reopens normally with the original normal bounds, not minimized |

Placement scenarios exercise MahApps' real native window-placement behavior and
the production settings class. They keep the application alive explicitly while
closing the first window and showing a second one; settings are reloaded from
disk between those windows rather than relying on cached property values.
The tests do not simulate monitor removal or a DPI change between launches.

The failure scenario explicitly disposes the container in the helper after the
startup exception. It does not assert that normal application exit occurred or
that startup failure automatically disposed the container.

### Composition and rendering checks

Normal lifecycle scenarios wait until dispatcher idle before inspecting the
shown main window. They use the application's actual container and verify:

- A `ChessPadViewModel` data context with 64 fields and eight labels per axis.
- Resolution of field and label delegate factories with runtime arguments.
- The board ItemsControl's ItemsSource is the model's actual Fields collection,
  with 64 bound items.
- All 32 coordinate-label TextBlocks have the model's text and positive rendered
  width and height.

Menu assertions inspect the actual Exit entry, its command, explicit target,
and enabled state. They execute the routed command rather than clicking through
an operating-system UI automation driver.

### Logging assertions

Lifecycle tests resolve `ILogger<T>` through Autofac and emit a structured
probe with `ProbeValue = 42` plus a Debug probe. After the scenario, they parse
the real newline-delimited JSON files and require:

- A startup event with a nonempty application version and a valid session GUID.
- The same session identifier on all events.
- The expected typed logger category and integer probe property.
- Exactly one Debug probe in Debug builds, and none in Release builds.
- For normal exit, one first-render event with nonnegative elapsed milliseconds,
  an exit event with code 0, and no Fatal events.
- For the injected failure, one Fatal event containing the expected exception
  and `FailureReason`, with no first-render event.

Provider schemas are intentionally different. Structured properties are nested
under `Properties` in Serilog output and at the root in NLog output. Severity
text is `Information` for Serilog and `Info` for NLog. Typed logger categories
normalize nested type separators to dots, rather than the plus signs returned
by `Type.FullName`. Helpers account for those actual format conventions without
changing production logs.

After lifecycle cleanup, the helper opens each log file with exclusive access.
This checks that the selected provider released its file handles.

### Real rolling and retention

The rolling case seeds 20 dated JSON fixtures from January 2000. It does not
change the machine clock. Using the production provider factory, it writes a
10 MiB payload event followed by a second event, then disposes the logger factory
and provider before examining output.

Assertions require two current log files, a size-suffixed archive, a date/sequence
filename shape, at least one file reaching 10 MiB, and exactly one copy of each
emitted event. The oldest fixture must be removed and the newest fixture kept.
Serilog must retain 14 total files. NLog permits 14 or 15 because its archive
cleanup can account for the active file differently from historical archives.
These assertions exercise size rolling and retention of daily-named files;
they do not wait for an actual midnight transition.

## Internal access and production behavior

The production WPF project declares:

```xml
<InternalsVisibleTo Include="ChessPad.WpfUi.IntegrationTests" />
```

The SDK generates an assembly-level `InternalsVisibleTo` attribute. It allows
the named integration assembly to use the internal `App.LoggingProvider` enum,
`App(LoggingProvider, string logDirectory)` constructor, and
`App.CreateLoggingProvider` method. The grant follows the assembly name, not
its folder location, and exposes all internal members to that assembly. It does
not make them public or allow direct access to private members.

The host separately uses reflection to inspect App's private `_container` field.
This avoids duplicating registrations, but couples that check to the field name;
if the composition root changes, update the helper deliberately.

The public parameterless App constructor still uses the hardcoded backend
selection and `%LOCALAPPDATA%\ChessPad\Logs`. Tests supply their own directory
and backend without modifying that selector. The integration seam is internal,
not a user-facing configuration switch.

## Isolation, timeout, and cleanup

Each integration case owns a child process and a directory beneath
`%TEMP%\ChessPad.IntegrationTests` with a unique GUID name. The parent deletes
that directory in a finally block after execution. It never uses or deletes the
normal per-user application logs. `IsolatedSettingsProvider` redirects every
property on `MainWindowSettings.Default` to `window-settings.testsettings` inside
that same directory. The settings framework still serializes and deserializes
the real property values, including native placement XML, but tests never read
or overwrite normal per-user window settings. The settings file uses a separate
extension so the log checks only inspect provider-generated JSON log files.

Standard output and error are read asynchronously to avoid filling a redirected
pipe while waiting. A 60-second deadline bounds the child execution. On timeout,
the parent terminates only its own child's process tree, waits for it to exit,
and reports captured diagnostics as a failed test. Cleanup errors also surface;
they are not silently treated as a successful run.

Method-level parallelization is safe because unit cases own their model state
and integration cases isolate WPF/logging state in separate processes. Do not
introduce shared mutable fixtures or a common log directory without revisiting
that assumption.

## Extending the suites

- Put UI-independent model behavior in the unit project and supply dependencies
  through its existing constructor/delegate seams.
- Put real registration, WPF lifecycle, and backend-output checks in the
  integration project; use production configuration rather than a copied setup.
- Use separate data cases for provider-dependent behavior. Keep exact expected
  counts, values, exception types, and reference identities where they define
  the contract.
- For another child scenario, add its DataRows to the parent and dispatch and
  assertions to the host. Preserve failure output, PASS markers, and cleanup.
- Keep XML summaries and parameter/return documentation aligned with the actual
  checks. Update this guide's case counts when adding or removing data cases.
- Run the affected project first; run both Debug and Release when changing
  logging thresholds or conditional compilation.

## Limits and troubleshooting

The suites do not automate multi-size label-center alignment, visual styling,
physical mouse/keyboard interactions, forced runtime termination, native crash
handling, or actual midnight rollover. Use the
[manual verification checklist](development.md#manual-verification) for visual
behavior. Startup failure tests cover a managed lifecycle failure, not every
possible constructor, dispatcher, or native failure path.

| Symptom | What to check |
|---|---|
| No tests discovered | Build the intended test project, verify its MSTest reference, and clear stale Test Explorer filters. Confirm that the saved solution includes both test paths. |
| Child cannot start | Ensure dotnet is on PATH and the executable test assembly and runtime configuration were built. |
| WPF scenario fails or times out | Use a desktop-capable Windows environment; inspect captured standard error for assertion failures, startup errors, or blocked shutdown. |
| Integration code cannot access an internal App member | Check the production InternalsVisibleTo entry and the integration assembly name; private members are not covered by the grant. |
| Reflection cannot find the container | Check whether App's private container field was renamed or composition ownership changed. |
| JSON/property assertion fails | Inspect the captured failure and check the selected backend's schema, category naming, severity text, and structured properties. Do not assume both providers serialize identically. |
| Debug probe differs | Ensure the test and referenced production application use the intended Debug or Release configuration. |
| File cleanup fails | Check child termination and provider disposal; do not delete normal application logs as a workaround. |

For logging ownership and backend settings, see [logging](logging.md). For
container lifetimes, see [dependency injection](dependency-injection.md).

[Documentation index](README.md)
