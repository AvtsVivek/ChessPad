# Logging and diagnostics

[Documentation index](README.md)

## Abstraction and dependency injection

Application logging uses `Microsoft.Extensions.Logging.ILogger<T>`. Both providers
are configured only in `App.xaml.cs`; application event calls use the standard
logging API rather than static provider-specific logging APIs. Autofac registers
the shared `ILoggerFactory` as externally owned and registers `Logger<T>` as
`ILogger<T>`, allowing future UI services to request typed loggers through
constructor injection. `App` explicitly owns and disposes both the factory and
the selected provider. The Serilog provider owns its logger; the NLog provider
has `ShutdownOnDispose` enabled for its dedicated `LogFactory`.

The current view models have no logging calls and receive no new dependencies.
If meaningful operations are added to that library later, it can reference
`Microsoft.Extensions.Logging.Abstractions` without referencing either backend.

## Choosing Serilog or NLog

Change this single hardcoded field in `App.xaml.cs`, rebuild, and rerun:

```csharp
private static readonly LoggingProvider SelectedLoggingProvider = LoggingProvider.Serilog;
```

For NLog, use `LoggingProvider.NLog` instead. The current application selection
is NLog; set the field to Serilog to choose that backend. The switch creates
exactly one provider, not both. It is not a runtime toggle and does not
read environment variables or external configuration. Unsupported enum values
throw rather than silently falling back to a different backend.

Both implementations keep the same `ILogger<T>` registrations and application
event calls. The Microsoft logger factory allows Trace and higher through, then
the selected backend applies the Debug/Release threshold. NLog captures message
templates, event properties, and scopes; Serilog enables `FromLogContext`.
The JSON schemas differ, so consumers must account for the selected provider.

No decorators or interception infrastructure is introduced for the current
lifecycle-only events. Those techniques can be added at future service boundaries
when there are repeated operations worth observing.

Integration tests use an internal `App(LoggingProvider, string logDirectory)`
constructor and the internal provider creation method to select either backend
and isolate file output. Only `ChessPad.WpfUi.IntegrationTests` is granted friend
assembly access. The public parameterless constructor still uses the hardcoded
selector and normal per-user log directory, with no test arguments or environment
variables exposed in the running application. See
[integration tests](testing.md#integration-project-chesspadwpfuiintegrationtests)
and [internal access](testing.md#internal-access-and-production-behavior) for details.

## Events and severity

| Event | Level | Properties |
|---|---|---|
| Application starting | Information | `ApplicationVersion` |
| Main window displayed | Information | `StartupElapsedMilliseconds` |
| Application exiting | Information | `ExitCode` |
| Initialization, startup, or shutdown failure | Critical (Fatal in either backend) | Exception, `FailureReason`, `ExceptionType` |
| Unhandled application exception | Critical (Fatal in either backend) | Exception, `FailureReason`, `ExceptionType` |

Every event includes a generated `SessionId`, allowing concurrent runs to be
distinguished, and typed loggers provide their category as `SourceContext`.
No command-line arguments, game content, or user documents are logged.

## Files, levels, and retention

Both providers write under `%LOCALAPPDATA%\ChessPad\Logs`, not the installation
folder or repository. Each line is a JSON event with timestamp, level, category,
message template, rendered message, structured properties, and exception details.

| Behavior | Serilog | NLog |
|---|---|---|
| Active filename | `chesspad-serilog-YYYYMMDD.json` | `chesspad-nlog-YYYYMMDD.json` |
| Rolling | Daily and at 10 MiB | Date-based filename and at 10 MiB |
| Archive naming | Date with size-roll sequence | Date with sequence suffix, e.g. `chesspad-nlog-20261018_01.json` |
| Retention | 14 files total | 14 matching historical files plus the active file |
| Structured properties | Nested under `Properties` | Top-level JSON attributes |
| Category | `Properties.SourceContext` | `SourceContext` |
| File access | Shared file sink | Opens and closes file per write (`KeepFileOpen=false`) |

NLog renders the date in its filename using `${date:format=yyyyMMdd}`. A new
day selects a new file automatically; `ArchiveEvery` is not needed. Size rolls
add a sequence suffix without repeating the date. NLog's retention cleanup
covers matching daily files and size-roll files, keeping up to 14 historical
files plus the active file. Cleanup occurs during logging, not on a timer when
the application is closed. Existing files with the previous naming scheme are
not renamed or migrated.

Retention counts files, not days. File limits are rolling thresholds, not strict
byte quotas; a single event can take a file beyond the threshold. NLog archive
rotation is not coordinated between separate processes; do not rely on this
configuration for multi-process archive management.

Release builds record Information and above. Debug builds record Debug and above
and additionally write to Debug output. Output is synchronous with no background
logging queue. Provider disposal closes and flushes its logging resources.

Both configurations currently live in code; NLog does not load an `NLog.config`
file. Serilog internal sink failures go through `SelfLog` to `Trace.TraceError`
and standard error. NLog internal warnings and errors go to standard error.
Neither diagnostic path recursively writes through its failing file target.
A GUI app launched without a debugger or console may not have a visible
standard-error stream. Logging cannot guarantee durable output when the disk is
unavailable or the process is forcibly terminated.

## Failure handling

Initialization, startup, and shutdown catches log exceptions and rethrow them;
they do not convert failure into apparent success. The application also subscribes
to `AppDomain.CurrentDomain.UnhandledException` for best-effort fatal diagnostics,
including unhandled dispatcher exceptions that reach runtime termination.
The same exception object already reported by a lifecycle catch is not logged
again by the unhandled-exception handler. A terminating notification disposes
the selected provider to close its files before termination where possible.

No dispatcher exception is marked handled. There is no catch-and-continue policy,
and unobserved task exceptions, native crashes, stack overflows, and forced
process termination are not guaranteed to pass through this hook. Logging setup
itself precedes logger availability, so failures that prevent the logger from
being created cannot be recorded through that logger.

## Checking logging

Launch ChessPad, wait for the board to render, then exit through the menu. The
same session should contain startup, window-displayed, and exit events with exit
code 0. Check that first-render logging occurs only once and that the log file
is readable after shutdown. Repeat with each selector value and verify the
corresponding provider's file. Check Debug and Release builds separately to
confirm that Debug events are excluded in Release.

See [dependency injection and lifecycle](dependency-injection.md) for ownership
and [development](development.md) for the application verification checklist.

[Documentation index](README.md)
