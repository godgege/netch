using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Netch.Interfaces;
using Netch.Models;
using Netch.Models.Modes;
using WindowsJobAPI;

namespace Netch;

public class NetchAppContext
{
    /// <summary>
    ///     Global static accessor for use in reflection-created instances (e.g. IServerUtil implementations).
    ///     Must be set at application startup.
    /// </summary>
    public static NetchAppContext? Current { get; set; }

    public static IServerEditorService? ServerEditor { get; set; }

    public Setting Settings { get; set; } = new();

    public JobObject Job { get; } = new();

    public List<Mode> Modes { get; } = new();

    public string NetchDir { get; init; } = string.Empty;

    public string NetchExecutable { get; init; } = string.Empty;

    public static JsonSerializerOptions NewCustomJsonSerializerOptions() => new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
