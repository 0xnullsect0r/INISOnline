using Godot;

namespace INISOnline.Audio;

/// <summary>
/// User settings (audio volumes, video, gameplay) persisted to <c>user://settings.cfg</c> and
/// applied live to the audio buses and window. Centralized so any screen reads/writes the same
/// values; <see cref="Apply"/> pushes them to the engine.
/// </summary>
public static class Settings
{
    private const string Path = "user://settings.cfg";

    public static float Master = 0.9f;
    public static float Music = 0.7f;
    public static float Sfx = 0.85f;
    public static float Ui = 0.85f;
    public static bool Fullscreen = true;
    public static float AnimationSpeed = 1.0f;
    public static bool ConfirmMoves;

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) { Apply(); return; }
        Master = (float)cfg.GetValue("audio", "master", Master);
        Music = (float)cfg.GetValue("audio", "music", Music);
        Sfx = (float)cfg.GetValue("audio", "sfx", Sfx);
        Ui = (float)cfg.GetValue("audio", "ui", Ui);
        Fullscreen = (bool)cfg.GetValue("video", "fullscreen", Fullscreen);
        AnimationSpeed = (float)cfg.GetValue("video", "animation_speed", AnimationSpeed);
        ConfirmMoves = (bool)cfg.GetValue("gameplay", "confirm_moves", ConfirmMoves);
        Apply();
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("audio", "master", Master);
        cfg.SetValue("audio", "music", Music);
        cfg.SetValue("audio", "sfx", Sfx);
        cfg.SetValue("audio", "ui", Ui);
        cfg.SetValue("video", "fullscreen", Fullscreen);
        cfg.SetValue("video", "animation_speed", AnimationSpeed);
        cfg.SetValue("gameplay", "confirm_moves", ConfirmMoves);
        cfg.Save(Path);
    }

    /// <summary>Pushes the current settings to the audio buses and the window mode.</summary>
    public static void Apply()
    {
        SetBus("Master", Master);
        SetBus("Music", Music);
        SetBus("SFX", Sfx);
        SetBus("UI", Ui);
        if (DisplayServer.GetName() != "headless")
            DisplayServer.WindowSetMode(Fullscreen
                ? DisplayServer.WindowMode.Fullscreen
                : DisplayServer.WindowMode.Windowed);
    }

    private static void SetBus(string name, float linear)
    {
        var idx = AudioServer.GetBusIndex(name);
        if (idx < 0) return;
        AudioServer.SetBusVolumeDb(idx, linear <= 0.0001f ? -80f : Mathf.LinearToDb(linear));
        AudioServer.SetBusMute(idx, linear <= 0.0001f);
    }
}
