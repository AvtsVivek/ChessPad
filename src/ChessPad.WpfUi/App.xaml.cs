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
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private enum LoggingProvider
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

        private static ILoggerProvider CreateLoggingProvider()
        {
            return SelectedLoggingProvider switch
            {
                LoggingProvider.Serilog => CreateSerilogProvider(),
                LoggingProvider.NLog => CreateNLogProvider(),
                _ => throw new InvalidOperationException($"Unsupported logging provider: {SelectedLoggingProvider}")
            };
        }

        private static ILoggerProvider CreateSerilogProvider()
        {
            SelfLog.Enable(message =>
            {
                Trace.TraceError("Serilog: {0}", message);
                Console.Error.WriteLine(message);
            });

            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ChessPad", "Logs", "chesspad-serilog-.json");
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

        private static ILoggerProvider CreateNLogProvider()
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
                FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ChessPad", "Logs", "chesspad-nlog-${date:format=yyyyMMdd}.json"),
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

        public App()
        {
            _loggingProvider = CreateLoggingProvider();
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

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogFatalException(e.ExceptionObject, "Unhandled application exception");
            if (e.IsTerminating)
            {
                DisposeLogging();
            }
        }

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

        private void LogFatalException(object exceptionObject, string message)
        {
            if (!ReferenceEquals(Interlocked.Exchange(ref _reportedFatalException, exceptionObject), exceptionObject))
            {
                _logger.LogCritical(exceptionObject as Exception,
                    "{FailureReason}: {ExceptionType}", message, exceptionObject.GetType().FullName);
            }
        }

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
