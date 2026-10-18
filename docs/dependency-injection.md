# Dependency injection and lifecycle

[Documentation index](README.md)

## Registrations

`App.xaml.cs` is the composition root. It registers the window and all four
view-model types with Autofac:

```csharp
var builder = new ContainerBuilder();
builder.RegisterType<MainWindow>().AsSelf();
builder.RegisterType<ChessPadViewModel>().AsSelf();
builder.RegisterType<ChessBoardViewModel>().AsSelf();
builder.RegisterType<FieldViewModel>().AsSelf();
builder.RegisterType<RowColumnLabelViewModel>().AsSelf();
_container = builder.Build();
```

This excerpt shows the presentation registrations; the application also registers
logging as described in [logging and diagnostics](logging.md).

`AsSelf()` exposes each registration by its concrete type. Interfaces are not
required for this object graph. The view models do not reference Autofac or
resolve dependencies from the container themselves.

## Why fields use a delegate factory

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

Autofac automatically provides the custom delegate factory from the
`FieldViewModel` registration. There is no separate factory class or explicit
factory registration in the current implementation. The label factory works
the same way, supplying the runtime `Index` and `Label` values.

## Parameter names matter

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
The label delegate similarly matches `Index` and `Label`.

## Resolution and lifetimes

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

The window and view-model registrations use Autofac's default instance-per-
dependency lifetime: separate resolutions create separate instances. Each field
factory call creates a new field instance, even if the same coordinates are
supplied again. Resolving another `ChessPadViewModel` creates another board and
another set of fields; the app does not register a global singleton board.
Logging is shared separately: the logger factory is an application-owned
instance, and each closed `ILogger<T>` registration is a singleton.

## Startup, ownership, and shutdown

1. The `App` constructor initializes the selected logging provider, creates an
   `ILoggerFactory`, logs application startup, and builds the Autofac container
   with logging registered.
2. Application resources are loaded from `App.xaml` during WPF initialization.
3. `OnStartup` calls its base implementation and resolves `MainWindow`.
4. The resolved window is assigned to the application's `MainWindow` property.
5. Calling `Show()` displays it. The first `ContentRendered` event records
   startup duration and detaches its handler to avoid repeated startup events.
6. On application exit, `OnExit` disposes the container, calls the base
   implementation, and logs the final exit code. Logging is disposed in a
   `finally` block after container cleanup so dependencies can log during disposal.

`App.xaml` intentionally has no `StartupUri`. A `StartupUri` would ask WPF to
create a window separately from the constructor-injected startup path.

The container is owned by `App` and disposed at application exit. The current
view models hold no disposable resources. If resource-owning dependencies are
added later, choose registrations and scopes deliberately rather than assuming
that the visual containment hierarchy creates Autofac lifetime scopes.

See [logging](logging.md#failure-handling) for fatal diagnostics and provider
cleanup, and [the Exit command](wpf-presentation.md#application-menu-and-exit-command)
for the menu-triggered shutdown path.

[Documentation index](README.md)
