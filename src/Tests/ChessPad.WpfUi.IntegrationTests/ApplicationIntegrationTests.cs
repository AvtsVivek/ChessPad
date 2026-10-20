using System.Diagnostics;
using System.IO;

namespace ChessPad.WpfUi.IntegrationTests;

/// <summary>Coordinates isolated, real-application integration scenarios for both supported logging providers.</summary>
/// <remarks>
/// VSTest executes these parent tests, which launch IntegrationTestHost in separate processes.
/// Each case owns its temporary logs and settings and enforces a bounded execution time, allowing
/// method-level parallelization without sharing WPF application instances or provider state.
/// Normal user logs and settings are not touched. The cases verify routed commands and native
/// window placement, not physical mouse or keyboard input.
/// </remarks>
[TestClass]
public sealed class ApplicationIntegrationTests
{
    /// <summary>Runs a real application or logging scenario in a fresh child process and requires successful verification.</summary>
    /// <param name="provider">The backend name parsed by the child: Serilog or NLog.</param>
    /// <param name="scenario">A lifecycle, rolling, or placement-Normal/Maximized/Minimized scenario.</param>
    /// <returns>A task completing after the child exits, its diagnostics are checked, and temporary output is removed.</returns>
    /// <remarks>
    /// Starts this assembly through dotnet so each case has its own STA/WPF application and logging
    /// state. A unique temporary directory protects normal application logs and settings and permits parallel cases.
    /// The child must exit with code zero, print its matching PASS marker, and produce no standard error.
    /// After 60 seconds, only that child's process tree is terminated and captured diagnostics are reported.
    /// Windows desktop support and dotnet on PATH are required for the lifecycle cases.
    /// </remarks>
    [TestMethod]
    [DataRow("Serilog", "menu-exit")]
    [DataRow("NLog", "menu-exit")]
    [DataRow("Serilog", "window-close")]
    [DataRow("NLog", "window-close")]
    [DataRow("Serilog", "startup-failure")]
    [DataRow("NLog", "startup-failure")]
    [DataRow("Serilog", "rolling")]
    [DataRow("NLog", "rolling")]
    [DataRow("Serilog", "placement-Normal")]
    [DataRow("NLog", "placement-Normal")]
    [DataRow("Serilog", "placement-Maximized")]
    [DataRow("NLog", "placement-Maximized")]
    [DataRow("Serilog", "placement-Minimized")]
    [DataRow("NLog", "placement-Minimized")]
    public async Task IsolatedApplication_CompletesExpectedScenario(string provider, string scenario)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ChessPad.IntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            start.ArgumentList.Add(typeof(ApplicationIntegrationTests).Assembly.Location);
            start.ArgumentList.Add(provider);
            start.ArgumentList.Add(scenario);
            start.ArgumentList.Add(directory);
            using var process = new Process { StartInfo = start };
            Assert.IsTrue(process.Start());
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                Assert.Fail($"Integration process timed out: {provider} / {scenario}\n{await output}\n{await errors}");
            }
            string stdout = await output;
            string stderr = await errors;
            Assert.AreEqual(0, process.ExitCode, $"{provider} / {scenario}\n{stdout}\n{stderr}");
            StringAssert.Contains(stdout, $"PASS: {provider} / {scenario}");
            Assert.AreEqual(string.Empty, stderr, "Unexpected logging or process diagnostics.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
