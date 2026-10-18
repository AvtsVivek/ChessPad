# ChessPad documentation

ChessPad is a WPF application intended to record and replay chess moves. It
currently displays an empty, labeled 8-by-8 board with a Metro window, an Exit
menu, dependency-injected view models, and selectable logging providers. Pieces,
legal moves, recording, replay, and board inversion are not implemented yet.

## Guides

| Guide | Contents |
|---|---|
| [Architecture](architecture.md) | Project layout, dependencies, technology versions, and responsibility boundaries |
| [View models and coordinates](view-models.md) | Composition, fields, row-major ordering, ranks, files, and label models |
| [Dependency injection and lifecycle](dependency-injection.md) | Autofac registrations, delegate factories, lifetimes, startup, and shutdown |
| [Logging and diagnostics](logging.md) | Selecting Serilog or NLog, events, JSON output, rolling, retention, and failure handling |
| [WPF presentation](wpf-presentation.md) | Data binding, board layout, colors, menu commands, title-bar icon, and theme resources |
| [Development and troubleshooting](development.md) | Build and MSTest commands, unit and integration tests, process isolation, verification checklists, and common issues |

Start with architecture for an overview. For a specific task, go directly to
the relevant guide; implementation details have one primary home rather than
being repeated across the pages.

[Return to the project README](../README.md)
