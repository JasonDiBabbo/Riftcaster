using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Riftcaster.Contracts;
using Riftcaster.Core.Settings;

namespace Riftcaster.Server;

/// <summary>
/// Supplies the default log level from the Settings page's log detail, so changing it takes effect
/// at once: the logging system watches its configuration and reloads when it changes.
/// </summary>
/// <remarks>
/// It sits after appsettings.json and before the environment variables and command line, so a saved
/// choice beats the files but not an explicit launch option such as <c>--Logging:LogLevel:Default Debug</c>.
/// Only the default level is set: the levels appsettings.json gives particular categories (such as
/// Microsoft.AspNetCore, which stays at Warning) are left as they are.
/// </remarks>
internal sealed class LogDetailSource : IConfigurationSource
{
    private readonly LogDetailProvider _provider = new();

    /// <summary>
    /// Adds the source to the configuration, before the environment variables and the command line.
    /// </summary>
    /// <param name="configuration">The app's configuration.</param>
    /// <returns>The source, to <see cref="Attach"/> once the settings exist.</returns>
    /// <remarks>
    /// Call it before adding anything else: inserting into the list rebuilds the providers already
    /// in it, which would give a provider that subscribes to something a second subscription.
    /// </remarks>
    public static LogDetailSource AddTo(IConfigurationManager configuration)
    {
        var source = new LogDetailSource();
        var sources = configuration.Sources;

        // The last of them: the web host's own settings come first in the list, ahead of appsettings.json.
        var all = sources.ToList();
        var index = all.FindLastIndex(s => s is EnvironmentVariablesConfigurationSource);
        if (index < 0)
        {
            index = all.FindLastIndex(s => s is CommandLineConfigurationSource);
        }

        if (index < 0)
        {
            sources.Add(source);
        }
        else
        {
            sources.Insert(index, source);
        }

        return source;
    }

    /// <summary>
    /// Starts following the settings: applies the saved log detail now, and every change after.
    /// </summary>
    /// <param name="settings">The settings to follow.</param>
    public void Attach(AppSettingsService settings) => _provider.Attach(settings);

    /// <summary>
    /// The log level that stands for a log detail.
    /// </summary>
    /// <param name="detail">The log detail.</param>
    public static LogLevel LevelOf(LogDetail detail) => detail switch
    {
        LogDetail.WarningsOnly => LogLevel.Warning,
        LogDetail.Detailed => LogLevel.Debug,
        _ => LogLevel.Information,
    };

    /// <inheritdoc/>
    public IConfigurationProvider Build(IConfigurationBuilder builder) => _provider;
}

/// <summary>
/// The configuration <see cref="LogDetailSource"/> supplies: the default log level.
/// </summary>
internal sealed class LogDetailProvider : ConfigurationProvider
{
    /// <summary>
    /// The configuration key for the default log level.
    /// </summary>
    public const string Key = "Logging:LogLevel:Default";

    private readonly Lock _lock = new();

    private AppSettingsService? _settings;

    /// <summary>
    /// Follows the settings, reloading the configuration whenever they change.
    /// </summary>
    /// <param name="settings">The settings to follow.</param>
    public void Attach(AppSettingsService settings)
    {
        lock (_lock)
        {
            _settings = settings;
        }

        // Lives as long as the app, like the settings, so the handler never needs removing.
        settings.Changed += Reload;
        Reload();
    }

    /// <inheritdoc/>
    public override void Load()
    {
        // Reading the settings and publishing them is one step. The settings raise Changed on whatever
        // thread changed them, so two reloads can overlap: without the lock, the one that read the older
        // value could publish it last, and the level would stay behind the settings until the next change.
        lock (_lock)
        {
            var settings = _settings;
            Data = settings is null
                ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    [Key] = LogDetailSource.LevelOf(settings.Settings.Logging.Detail).ToString(),
                };
        }
    }

    private void Reload()
    {
        Load();
        OnReload(); // The logging system watches its configuration, and applies the new level
    }
}
