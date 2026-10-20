using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Text.Json;
using ChessPad.WpfUi.Views;

namespace ChessPad.WpfUi.IntegrationTests;

/// <summary>Persists serialized application settings inside one test's temporary directory.</summary>
internal sealed class IsolatedSettingsProvider : SettingsProvider
{
    private readonly string _path;

    /// <summary>Initializes a provider that never reads or writes normal per-user settings.</summary>
    /// <param name="directory">The temporary directory owned by the parent test.</param>
    private IsolatedSettingsProvider(string directory)
    {
        _path = Path.Combine(directory, "window-settings.testsettings");
        Initialize(nameof(IsolatedSettingsProvider), new NameValueCollection());
    }

    /// <summary>Gets or sets the application name required by the settings-provider contract.</summary>
    public override string ApplicationName { get; set; } = "ChessPad.IntegrationTests";

    /// <summary>Redirects the real designer-backed settings to isolated on-disk storage.</summary>
    /// <param name="directory">The temporary directory owned by the parent test.</param>
    internal static void Install(string directory)
    {
        var settings = MainWindowSettings.Default;
        var provider = new IsolatedSettingsProvider(directory);
        settings.Providers.Add(provider);
        foreach (SettingsProperty property in settings.Properties)
        {
            property.Provider = provider;
        }
        settings.Reload();
    }

    /// <summary>Loads serialized values, allowing the settings framework to perform normal deserialization.</summary>
    /// <param name="context">The application settings context.</param>
    /// <param name="collection">The properties requested by the framework.</param>
    /// <returns>Stored values, or the declared defaults for properties not yet saved.</returns>
    public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection collection)
    {
        var stored = File.Exists(_path)
            ? JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(_path))
                ?? throw new InvalidDataException("The isolated settings file contains no settings object.")
            : new Dictionary<string, string?>();
        var values = new SettingsPropertyValueCollection();
        foreach (SettingsProperty property in collection)
        {
            values.Add(new SettingsPropertyValue(property)
            {
                SerializedValue = stored.TryGetValue(property.Name, out var value) ? value : property.DefaultValue
            });
        }
        return values;
    }

    /// <summary>Saves the framework's serialized strings, including native placement XML, to disk.</summary>
    /// <param name="context">The application settings context.</param>
    /// <param name="collection">The values being persisted.</param>
    public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
    {
        var stored = new Dictionary<string, string?>();
        foreach (SettingsPropertyValue value in collection)
        {
            stored.Add(value.Name, (string?)value.SerializedValue);
        }
        File.WriteAllText(_path, JsonSerializer.Serialize(stored));
    }
}
