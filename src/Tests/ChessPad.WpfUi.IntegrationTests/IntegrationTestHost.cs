using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Autofac;
using ChessPad.ViewModels;
using ChessPad.WpfUi.Views;
using Microsoft.Extensions.Logging;

namespace ChessPad.WpfUi.IntegrationTests;

internal static class IntegrationTestHost
{
    private const string ExpectedStartupFailure = "Expected integration startup failure";

    /// <summary>Executes one integration scenario on an STA thread in the dedicated child process.</summary>
    /// <param name="args">Three positional arguments: backend name, scenario name, and isolated log directory.</param>
    /// <returns>Zero after all assertions pass; one after writing an assertion or execution failure to standard error.</returns>
    /// <remarks>
    /// Prints a provider/scenario-specific PASS marker only on success. The process boundary isolates
    /// WPF's application singleton and provider-global diagnostics from VSTest and other test cases.
    /// </remarks>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var provider = Enum.Parse<App.LoggingProvider>(args[0]);
            string scenario = args[1];
            string directory = args[2];
            Directory.CreateDirectory(directory);
            if (scenario == "rolling")
            {
                VerifyRolling(provider, directory);
            }
            else
            {
                VerifyApplication(provider, scenario, directory);
            }
            Console.WriteLine($"PASS: {provider} / {scenario}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    /// <summary>Exercises the real application lifecycle and verifies its composition, structured logs, and cleanup.</summary>
    /// <param name="provider">The logging backend used by the application instance.</param>
    /// <param name="scenario">The lifecycle path: menu-exit, window-close, or startup-failure.</param>
    /// <param name="directory">The isolated directory to which the application writes its real log files.</param>
    /// <remarks>
    /// Normal scenarios wait for an idle dispatcher, inspect the rendered window, then trigger a routed
    /// Exit command or close the window. They require exit notifications and container disposal. The
    /// failure scenario injects a Startup subscriber exception and requires propagation plus one fatal
    /// log event without a first-render event; its container is disposed explicitly by this helper.
    /// All paths verify session/category metadata, structured probe values, build-specific Debug
    /// filtering, and exclusive log-file access after logging cleanup. Run only in a fresh STA process.
    /// </remarks>
    private static void VerifyApplication(App.LoggingProvider provider, string scenario, string directory)
    {
        var app = new App(provider, directory);
        app.InitializeComponent();
        // Inspect the actual composition root rather than duplicating its registrations.
        var container = (IContainer)typeof(App)
            .GetField("_container", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app)!;
        var logger = container.Resolve<ILogger<IntegrationTestHostCategory>>();
        logger.LogInformation("Integration probe {ProbeValue}", 42);
        logger.LogDebug("Integration debug probe");
        bool exited = false;
        bool closed = false;
        app.Exit += (_, _) => exited = true;

        if (scenario == "startup-failure")
        {
            app.Startup += (_, _) => throw new InvalidOperationException(ExpectedStartupFailure);
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => app.Run());
            Assert.AreEqual(ExpectedStartupFailure, exception.Message);
            container.Dispose();
        }
        else
        {
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var window = Assert.IsInstanceOfType<MainWindow>(app.MainWindow);
                window.Closed += (_, _) => closed = true;
                Assert.IsTrue(window.IsVisible);
                VerifyCompositionAndBinding(window, container);
                if (scenario == "menu-exit")
                {
                    var menu = Descendants(window).OfType<Menu>().Single();
                    var gameMenu = Assert.IsInstanceOfType<MenuItem>(menu.Items[0]);
                    gameMenu.IsSubmenuOpen = true;
                    window.UpdateLayout();
                    var exit = Assert.IsInstanceOfType<MenuItem>(gameMenu.Items[0]);
                    Assert.AreEqual("E_xit", exit.Header);
                    Assert.AreSame(ApplicationCommands.Close, exit.Command);
                    Assert.AreSame(window, exit.CommandTarget);
                    Assert.IsTrue(exit.IsEnabled);
                    Assert.IsTrue(ApplicationCommands.Close.CanExecute(null, exit.CommandTarget));
                    ApplicationCommands.Close.Execute(null, exit.CommandTarget);
                }
                else if (scenario == "window-close")
                {
                    window.Close();
                }
                else
                {
                    throw new InvalidOperationException($"Unknown lifecycle scenario: {scenario}");
                }
            }));
            Assert.AreEqual(0, app.Run());
            Assert.IsTrue(closed);
            Assert.IsTrue(exited);
            Assert.ThrowsExactly<ObjectDisposedException>(() => container.Resolve<ILoggerFactory>());
        }

        var events = ReadEvents(directory);
        var startup = events.Single(e => Message(e).StartsWith("Application starting", StringComparison.Ordinal));
        Assert.AreEqual(provider == App.LoggingProvider.Serilog ? "Information" : "Info", Level(startup));
        Assert.IsFalse(string.IsNullOrWhiteSpace(Properties(startup).GetProperty("ApplicationVersion").GetString()));
        string session = Properties(startup).GetProperty("SessionId").GetString()!;
        Assert.IsTrue(Guid.TryParse(session, out _));
        foreach (var entry in events)
        {
            Assert.AreEqual(session, Properties(entry).GetProperty("SessionId").GetString());
        }
        var probe = events.Single(e => Message(e) == "Integration probe {ProbeValue}");
        Assert.AreEqual(42, Properties(probe).GetProperty("ProbeValue").GetInt32());
        Assert.AreEqual(typeof(IntegrationTestHostCategory).FullName!.Replace('+', '.'),
            Properties(probe).GetProperty("SourceContext").GetString());
#if DEBUG
        Assert.AreEqual(1, events.Count(e => Message(e) == "Integration debug probe"));
#else
        Assert.AreEqual(0, events.Count(e => Message(e) == "Integration debug probe"));
#endif
        var failures = events.Where(e => Level(e) == "Fatal").ToArray();
        if (scenario == "startup-failure")
        {
            Assert.HasCount(1, failures);
            Assert.AreEqual("Application startup failed", Properties(failures[0]).GetProperty("FailureReason").GetString());
            StringAssert.Contains(failures[0].GetProperty("Exception").GetString()!, ExpectedStartupFailure);
            Assert.AreEqual(0, events.Count(e => Message(e).StartsWith("Main window displayed", StringComparison.Ordinal)));
        }
        else
        {
            Assert.IsEmpty(failures);
            var rendered = events.Single(e => Message(e).StartsWith("Main window displayed", StringComparison.Ordinal));
            Assert.IsTrue(Properties(rendered).GetProperty("StartupElapsedMilliseconds").GetInt64() >= 0);
            var exit = events.Single(e => Message(e) == "Application exiting with code {ExitCode}");
            Assert.AreEqual(0, Properties(exit).GetProperty("ExitCode").GetInt32());
        }
        // Exclusive access after shutdown proves the selected provider released its files.
        foreach (string path in Directory.GetFiles(directory, "*.json"))
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.IsTrue(stream.Length > 0);
        }
    }

    /// <summary>Verifies the real window's injected board, delegate-factory resolution, and rendered board bindings.</summary>
    /// <param name="window">The displayed main window after layout and content rendering.</param>
    /// <param name="container">The application's actual Autofac container, not a duplicate test registration set.</param>
    /// <remarks>
    /// Checks 64 bound fields, eight labels per axis, runtime factory arguments, and 32 nonempty
    /// label visuals with text matching their view models. This checks binding and visibility,
    /// not precise geometry, multiple window sizes, or physical mouse/keyboard interaction.
    /// </remarks>
    private static void VerifyCompositionAndBinding(MainWindow window, IContainer container)
    {
        var pad = Assert.IsInstanceOfType<ChessPadViewModel>(window.DataContext);
        var board = pad.ChessBoard;
        Assert.HasCount(64, board.Fields);
        Assert.HasCount(8, board.RowLabels);
        Assert.HasCount(8, board.ColumnLabels);
        var field = container.Resolve<FieldViewModel.Factory>()(2, 5);
        Assert.AreEqual(2, field.Row);
        Assert.AreEqual(5, field.Column);
        var label = container.Resolve<RowColumnLabelViewModel.Factory>()(3, "D");
        Assert.AreEqual(3, label.Index);
        Assert.AreEqual("D", label.Label);
        var fields = Descendants(window).OfType<ItemsControl>().Single(control =>
            BindingOperations.GetBinding(control, ItemsControl.ItemsSourceProperty)?.Path.Path == "ChessBoard.Fields");
        Assert.AreSame(board.Fields, fields.ItemsSource);
        Assert.HasCount(64, fields.Items);
        var texts = Descendants(window).OfType<TextBlock>()
            .Where(text => text.DataContext is RowColumnLabelViewModel).ToArray();
        Assert.HasCount(32, texts);
        foreach (var text in texts)
        {
            Assert.AreEqual(((RowColumnLabelViewModel)text.DataContext).Label, text.Text);
            Assert.IsTrue(text.ActualWidth > 0 && text.ActualHeight > 0);
        }
    }

    /// <summary>Verifies real size-based file rolling, dated filenames, retention bounds, and event preservation.</summary>
    /// <param name="provider">The backend whose production provider configuration is exercised.</param>
    /// <param name="directory">An isolated directory in which historical fixtures and current logs may be written or deleted.</param>
    /// <remarks>
    /// Seeds 20 January 2000 files without changing the system clock, then writes a 10 MiB payload
    /// and a following event. Requires two current files, a size archive, valid date/sequence names,
    /// removal of the oldest fixture, and preservation of the newest fixture and both emitted events.
    /// Serilog must retain 14 files; NLog permits 14 or 15 depending on active-file archive accounting.
    /// Providers and the logger factory are disposed before files are inspected.
    /// </remarks>
    private static void VerifyRolling(App.LoggingProvider provider, string directory)
    {
        string prefix = provider == App.LoggingProvider.Serilog ? "chesspad-serilog-" : "chesspad-nlog-";
        var oldDate = new DateTime(2000, 1, 1);
        for (int day = 0; day < 20; day++)
        {
            File.WriteAllText(Path.Combine(directory, $"{prefix}{oldDate.AddDays(day):yyyyMMdd}.json"), "{}\n");
        }
        using (var backend = App.CreateLoggingProvider(provider, directory))
        using (var factory = LoggerFactory.Create(builder => builder.AddProvider(backend)))
        {
            var logger = factory.CreateLogger("RollingVerification");
            logger.LogInformation("Size threshold payload {Payload}", new string('x', 10 * 1024 * 1024));
            logger.LogInformation("After size threshold");
        }
        var files = Directory.GetFiles(directory, $"{prefix}*.json");
        if (provider == App.LoggingProvider.Serilog)
        {
            Assert.HasCount(14, files);
        }
        else
        {
            // NLog may count the active file during archive cleanup.
            Assert.IsTrue(files.Length is >= 14 and <= 15, $"Unexpected retained file count: {files.Length}");
        }
        Assert.IsFalse(File.Exists(Path.Combine(directory, $"{prefix}20000101.json")));
        Assert.IsTrue(File.Exists(Path.Combine(directory, $"{prefix}20000120.json")));
        var currentFiles = files.Where(path => !Path.GetFileName(path).StartsWith(prefix + "2000", StringComparison.Ordinal)).ToArray();
        Assert.HasCount(2, currentFiles);
        foreach (string path in currentFiles)
        {
            Assert.MatchesRegex($"^{prefix}[0-9]{{8}}(_[0-9]+)?\\.json$", Path.GetFileName(path));
        }
        Assert.IsTrue(currentFiles.Any(path => Path.GetFileNameWithoutExtension(path).Contains('_')));
        Assert.IsTrue(currentFiles.Any(path => new FileInfo(path).Length >= 10 * 1024 * 1024));
        var events = currentFiles.SelectMany(ReadFile).ToArray();
        Assert.AreEqual(1, events.Count(e => Message(e) == "After size threshold"));
        Assert.AreEqual(1, events.Count(e => Message(e) == "Size threshold payload {Payload}"));
    }

    /// <summary>Loads events from all JSON log files directly beneath the supplied directory.</summary>
    /// <param name="directory">The completed scenario's isolated log directory.</param>
    /// <returns>Detached JSON event values in filesystem enumeration order, not necessarily timestamp order.</returns>
    /// <remarks>Filesystem and JSON errors propagate so corrupt or missing output is not silently ignored.</remarks>
    private static JsonElement[] ReadEvents(string directory) =>
        Directory.GetFiles(directory, "*.json").SelectMany(ReadFile).ToArray();

    /// <summary>Parses a newline-delimited JSON log file one event at a time.</summary>
    /// <param name="path">The log file to read; each line must contain a complete JSON value.</param>
    /// <returns>A lazy sequence of cloned root values that remain usable after their parsing documents are disposed.</returns>
    /// <remarks>File access and parse failures propagate when the sequence is enumerated.</remarks>
    private static IEnumerable<JsonElement> ReadFile(string path)
    {
        foreach (string line in File.ReadLines(path))
        {
            using var document = JsonDocument.Parse(line);
            yield return document.RootElement.Clone();
        }
    }

    /// <summary>Selects the provider-specific location of structured event properties.</summary>
    /// <param name="entry">A complete JSON event from either configured backend.</param>
    /// <returns>Serilog's nested Properties object when present; otherwise the NLog root event.</returns>
    private static JsonElement Properties(JsonElement entry) =>
        entry.TryGetProperty("Properties", out var properties) ? properties : entry;

    /// <summary>Reads an event's original message template rather than its rendered text.</summary>
    /// <param name="entry">A JSON event containing the shared MessageTemplate field.</param>
    /// <returns>The template used to identify an expected event independently of its property values.</returns>
    /// <remarks>A missing field or incompatible JSON type propagates as a test failure.</remarks>
    private static string Message(JsonElement entry) => entry.GetProperty("MessageTemplate").GetString()!;

    /// <summary>Reads the backend's severity text without normalizing provider-specific names.</summary>
    /// <param name="entry">A JSON event containing the shared Level field.</param>
    /// <returns>The severity label, such as Information for Serilog or Info for NLog.</returns>
    /// <remarks>A missing field or incompatible JSON type propagates as a test failure.</remarks>
    private static string Level(JsonElement entry) => entry.GetProperty("Level").GetString()!;

    /// <summary>Enumerates a WPF object's visual descendants recursively in depth-first order.</summary>
    /// <param name="parent">The visual whose children are inspected on its owning dispatcher thread.</param>
    /// <returns>Each visual descendant, excluding the supplied parent and excluding separate visual trees such as popups.</returns>
    /// <remarks>Uses the visual tree rather than the logical tree to inspect instantiated templates and rendered controls.</remarks>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class IntegrationTestHostCategory;
}
