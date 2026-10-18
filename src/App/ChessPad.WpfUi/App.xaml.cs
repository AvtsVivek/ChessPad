using Autofac;
using ChessPad.ViewModels;
using ChessPad.WpfUi.Views;
using Microsoft.Extensions.Logging;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Layouts;
using NLog.Targets;
using Serilog;
using Serilog.Debugging;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Json;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ChessPad.WpfUi
{
    /// <summary>
    /// Owns application composition, logging, main-window startup, and shutdown cleanup.
    /// </summary>
    public partial class App : Application
    {
        internal enum LoggingProvider
        {
            Serilog,
            NLog
        }

        private static readonly LoggingProvider SelectedLoggingProvider = LoggingProvider.NLog;

        private readonly IContainer _container;
        private readonly ILoggerProvider _loggingProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<App> _logger;
        private readonly Stopwatch _startupTimer = Stopwatch.StartNew();
        private object? _reportedFatalException;

        /// <summary>Creates exactly one configured logging backend for the requested output directory.</summary>
        /// <param name="provider">The backend to instantiate.</param>
        /// <param name="logDirectory">The directory in which the backend writes its rolling JSON files.</param>
        /// <returns>A provider owned by the caller, which must dispose it independently of the logger factory.</returns>
        /// <exception cref="InvalidOperationException">The provider value is not supported.</exception>
        /// <remarks>Internal access allows integration tests to select a backend without changing production defaults.</remarks>
        internal static ILoggerProvider CreateLoggingProvider(LoggingProvider provider, string logDirectory)
        {
            return provider switch
            {
                LoggingProvider.Serilog => CreateSerilogProvider(logDirectory),
                LoggingProvider.NLog => CreateNLogProvider(logDirectory),
                _ => throw new InvalidOperationException($"Unsupported logging provider: {provider}")
            };
        }

        /// <summary>Configures Serilog with session enrichment, rolling JSON files, and internal diagnostics.</summary>
        /// <param name="logDirectory">The directory containing daily, size-rolled Serilog log files.</param>
        /// <returns>A provider that owns and disposes its underlying Serilog logger.</returns>
        /// <remarks>
        /// Files roll daily and at 10 MiB, retaining at most 14 files. Debug builds also emit
        /// Debug-level events to the debugger; other builds use Information as the minimum level.
        /// Serilog self-diagnostics are sent to tracing and standard error until logging is disposed.
        /// </remarks>
        private static ILoggerProvider CreateSerilogProvider(string logDirectory)
        {
            SelfLog.Enable(message =>
            {
                Trace.TraceError("Serilog: {0}", message);
                Console.Error.WriteLine(message);
            });

            var logPath = Path.Combine(logDirectory, "chesspad-serilog-.json");
            var configuration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("SessionId", Guid.NewGuid())
                .WriteTo.File(new JsonFormatter(renderMessage: true), logPath,
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 14,
                    shared: true);
#if DEBUG
            configuration.MinimumLevel.Debug().WriteTo.Debug();
#endif
            return new SerilogLoggerProvider(configuration.CreateLogger(), dispose: true);
        }

        /// <summary>Configures a dedicated NLog factory with structured JSON output and session metadata.</summary>
        /// <param name="logDirectory">The directory containing date-stamped NLog files and size archives.</param>
        /// <returns>A provider that shuts down its dedicated NLog factory when disposed.</returns>
        /// <remarks>
        /// Files archive above 10 MiB with a 14-archive retention setting. Debug builds enable
        /// Debug events and a debugger target; other builds start at Info. Message templates,
        /// event properties, and scopes are captured, and internal warnings go to standard error.
        /// </remarks>
        private static ILoggerProvider CreateNLogProvider(string logDirectory)
        {
            NLog.Common.InternalLogger.LogToConsoleError = true;
            NLog.Common.InternalLogger.LogLevel = NLog.LogLevel.Warn;

            var layout = new JsonLayout
            {
                IncludeEventProperties = true,
                IncludeScopeProperties = true,
                Attributes =
                {
                    new JsonAttribute("Timestamp", "${date:format=o}"),
                    new JsonAttribute("Level", "${level}"),
                    new JsonAttribute("SourceContext", "${logger}"),
                    new JsonAttribute("MessageTemplate", "${message:raw=true}"),
                    new JsonAttribute("RenderedMessage", "${message}"),
                    new JsonAttribute("Exception", "${exception:format=tostring}"),
                    new JsonAttribute("SessionId", Guid.NewGuid().ToString())
                }
            };
            var file = new FileTarget("file")
            {
                FileName = Path.Combine(logDirectory, "chesspad-nlog-${date:format=yyyyMMdd}.json"),
                Layout = layout,
                ArchiveAboveSize = 10 * 1024 * 1024,
                ArchiveSuffixFormat = "_{0:00}",
                MaxArchiveFiles = 14,
                KeepFileOpen = false
            };
            var configuration = new LoggingConfiguration();
#if DEBUG
            configuration.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, file);
            configuration.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal,
                new DebuggerTarget("debug") { Layout = layout });
#else
            configuration.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, file);
#endif
            var factory = new NLog.LogFactory { Configuration = configuration };
            return new NLogLoggerProvider(new NLogProviderOptions
            {
                CaptureMessageTemplates = true,
                CaptureMessageProperties = true,
                IncludeScopes = true,
                ShutdownOnDispose = true
            }, factory);
        }

        /// <summary>Initializes the application using the hardcoded backend and the normal per-user log directory.</summary>
        /// <remarks>Logs are written beneath the current user's local application data in ChessPad's Logs directory.</remarks>
        public App() : this(SelectedLoggingProvider, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChessPad", "Logs"))
        {
        }

        /// <summary>Initializes logging and builds the Autofac composition root with explicit logging settings.</summary>
        /// <param name="provider">The single logging backend used for this application instance.</param>
        /// <param name="logDirectory">The log output directory, supplied explicitly for isolated integration tests.</param>
        /// <remarks>
        /// Registers the window, view models, delegate-factory targets, and typed loggers. The
        /// application owns both the container and logging infrastructure. Failures while logging
        /// startup or building the container are logged, release logging resources, and propagate.
        /// WPF resources and the main window are initialized later by the normal startup sequence.
        /// </remarks>
        internal App(LoggingProvider provider, string logDirectory)
        {
            _loggingProvider = CreateLoggingProvider(provider, logDirectory);
            _loggerFactory = LoggerFactory.Create(builder => builder
                .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace)
                .AddProvider(_loggingProvider));
            _logger = _loggerFactory.CreateLogger<App>();
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            try
            {
                _logger.LogInformation("Application starting, version {ApplicationVersion}",
                    typeof(App).Assembly.GetName().Version?.ToString());

                var builder = new ContainerBuilder();
                builder.RegisterInstance(_loggerFactory).As<ILoggerFactory>().ExternallyOwned();
                builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>)).SingleInstance();
                builder.RegisterType<MainWindow>().AsSelf();
                builder.RegisterType<ChessPadViewModel>().AsSelf();
                builder.RegisterType<ChessBoardViewModel>().AsSelf();
                builder.RegisterType<FieldViewModel>().AsSelf();
                builder.RegisterType<RowColumnLabelViewModel>().AsSelf();
                _container = builder.Build();
            }
            catch (Exception exception)
            {
                LogFatalException(exception, "Application initialization failed");
                DisposeLogging();
                throw;
            }
        }

        /// <summary>Raises startup notifications, resolves the injected main window, and displays it.</summary>
        /// <param name="e">The WPF startup event data, including command-line arguments.</param>
        /// <remarks>
        /// Subscribes to the first content-render event to measure startup. Exceptions from startup
        /// subscribers, resolution, or window display are logged as fatal, followed by logging cleanup
        /// and rethrow; failures are not suppressed or converted into successful startup.
        /// </remarks>
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);

                var window = _container.Resolve<MainWindow>();
                window.ContentRendered += OnMainWindowContentRendered;
                MainWindow = window;
                window.Show();
            }
            catch (Exception exception)
            {
                LogFatalException(exception, "Application startup failed");
                DisposeLogging();
                throw;
            }
        }

        /// <summary>Records elapsed startup time after the main window first renders its content.</summary>
        /// <param name="sender">The rendered window, whose handler is detached to prevent repeated startup events.</param>
        /// <param name="e">The content-render event data.</param>
        private void OnMainWindowContentRendered(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                window.ContentRendered -= OnMainWindowContentRendered;
            }

            _startupTimer.Stop();
            _logger.LogInformation("Main window displayed after {StartupElapsedMilliseconds} ms",
                _startupTimer.ElapsedMilliseconds);
        }

        /// <summary>Attempts to record an unhandled exception and releases logging if the runtime is terminating.</summary>
        /// <param name="sender">The application-domain event source.</param>
        /// <param name="e">The exception object and runtime termination indicator.</param>
        /// <remarks>This diagnostic hook does not mark exceptions handled or prevent process termination.</remarks>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogFatalException(e.ExceptionObject, "Unhandled application exception");
            if (e.IsTerminating)
            {
                DisposeLogging();
            }
        }

        /// <summary>Detaches the unhandled-exception hook and disposes application-owned logging resources.</summary>
        /// <remarks>
        /// The provider is disposed separately because the factory does not own the supplied provider
        /// instance. Provider disposal and Serilog self-diagnostic shutdown follow factory disposal
        /// through a finally block; disposal errors are not swallowed.
        /// </remarks>
        private void DisposeLogging()
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            try
            {
                _loggerFactory.Dispose();
            }
            finally
            {
                _loggingProvider.Dispose();
                SelfLog.Disable();
            }
        }

        /// <summary>Logs a critical failure unless the same exception object was the most recently reported failure.</summary>
        /// <param name="exceptionObject">The runtime failure object; an Exception also supplies stack-trace details.</param>
        /// <param name="message">The lifecycle failure description recorded as the structured FailureReason property.</param>
        /// <remarks>Reference-based, atomic tracking avoids duplicate reporting across overlapping failure hooks.</remarks>
        private void LogFatalException(object exceptionObject, string message)
        {
            if (!ReferenceEquals(Interlocked.Exchange(ref _reportedFatalException, exceptionObject), exceptionObject))
            {
                _logger.LogCritical(exceptionObject as Exception,
                    "{FailureReason}: {ExceptionType}", message, exceptionObject.GetType().FullName);
            }
        }

        /// <summary>Disposes the container, raises exit notifications, and records the final application exit code.</summary>
        /// <param name="e">The WPF exit event data containing the application exit code.</param>
        /// <remarks>
        /// Logging remains available while dependencies are disposed. Shutdown failures are logged
        /// and rethrown, and logging cleanup is attempted in the finally block on every exit path.
        /// </remarks>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _container.Dispose();
                base.OnExit(e);
                _logger.LogInformation("Application exiting with code {ExitCode}", e.ApplicationExitCode);
            }
            catch (Exception exception)
            {
                LogFatalException(exception, "Application shutdown failed");
                throw;
            }
            finally
            {
                DisposeLogging();
            }
        }
    }
}
