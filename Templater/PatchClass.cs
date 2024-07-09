using ACE.Database;
using ACE.Entity.Adapter;

namespace Templater;

[HarmonyPatch]
public class PatchClass
{
    #region Settings
    const int RETRIES = 10;

    public static Settings Settings = new();
    static string settingsPath => Path.Combine(Mod.ModPath, "Settings.json");
    private FileInfo settingsInfo = new(settingsPath);

    private JsonSerializerOptions _serializeOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private void SaveSettings()
    {
        string jsonString = JsonSerializer.Serialize(Settings, _serializeOptions);

        if (!settingsInfo.RetryWrite(jsonString, RETRIES))
        {
            ModManager.Log($"Failed to save settings to {settingsPath}...", ModManager.LogLevel.Warn);
            Mod.State = ModState.Error;
        }
    }

    private void LoadSettings()
    {
        if (!settingsInfo.Exists)
        {
            ModManager.Log($"Creating {settingsInfo}...");
            SaveSettings();
        }
        else
            ModManager.Log($"Loading settings from {settingsPath}...");

        if (!settingsInfo.RetryRead(out string jsonString, RETRIES))
        {
            Mod.State = ModState.Error;
            return;
        }

        try
        {
            Settings = JsonSerializer.Deserialize<Settings>(jsonString, _serializeOptions);
        }
        catch (Exception)
        {
            ModManager.Log($"Failed to deserialize Settings: {settingsPath}", ModManager.LogLevel.Warn);
            Mod.State = ModState.Error;
            return;
        }
    }
    #endregion

    #region Start/Shutdown
    public void Start()
    {
        //Need to decide on async use
        Mod.State = ModState.Loading;
        LoadSettings();

        if (Mod.State == ModState.Error)
        {
            ModManager.DisableModByPath(Mod.ModPath);
            return;
        }

        Mod.State = ModState.Running;
    }

    public void Shutdown()
    {
        //if (Mod.State == ModState.Running)
        // Shut down enabled mod...

        //If the mod is making changes that need to be saved use this and only manually edit settings when the patch is not active.
        //SaveSettings();

        if (Mod.State == ModState.Error)
            ModManager.Log($"Improper shutdown: {Mod.ModPath}", ModManager.LogLevel.Error);
    }
    #endregion

    [CommandHandler("template", AccessLevel.Developer, CommandHandlerFlag.None, 3, "Generates templates", "<wcid> <# templates to export> <start of range>")]
    public static void HandleCILoot(ISession session, params string[] parameters)
    {
        var player = session?.Player;

        if (parameters.Length != 3)
        {
            player?.SendMessage("Invalid usage: \"<wcid> <# templates to export> <start of range>\".");
            ModManager.Log("Invalid usage: \"<wcid> <# templates to export> <start of range>\".");
            return;
        }

        if (!uint.TryParse(parameters[0], out var wcid) || DatabaseManager.World.GetCachedWeenie(wcid) is not Weenie weenie)
        {
            player?.SendMessage("Invalid weenie.");
            ModManager.Log("Invalid weenie.");
            return;
        }

        if (!uint.TryParse(parameters[1], out var numTemplates) || numTemplates > 10000)
        {
            player?.SendMessage("Invalid number of templates.");
            ModManager.Log("Invalid number of templates.");
            return;
        }

        if (!uint.TryParse(parameters[2], out var start))
        {
            player?.SendMessage("Invalid number of templates.");
            ModManager.Log("Invalid number of templates.");
            return;
        }

        //Make copy to be changed
        var dbWeenie = weenie.ConvertFromEntityWeenie();

        //Depending on what you want to change an Entity weenie might be easier to work with
        //var clone = ACE.Database.Adapter.WeenieConverter.ConvertToEntityWeenie(dbWeenie);

        //Set starting wcid
        dbWeenie.ClassId = start - 1;

        for (var i = 0; i < numTemplates; i++)
        {
            dbWeenie.ClassId++;
            dbWeenie.ClassName += i;

            if (Settings.ExportSql)
                dbWeenie.SaveSql();
            if (Settings.ExportJson)
                dbWeenie.SaveJson(Path.Combine(Mod.ModPath, $"{dbWeenie.ClassId} {dbWeenie.ClassName}.json"));
        }

        player?.SendMessage($"Exported {numTemplates} of WCID={wcid} starting at {start}.");
        ModManager.Log($"Exported {numTemplates} of WCID={wcid} starting at {start}.");
    }
}
