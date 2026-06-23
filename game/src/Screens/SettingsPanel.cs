using System;
using Godot;
using INISOnline.App;
using INISOnline.Audio;
using INISOnline.Theme;

namespace INISOnline.Screens;

/// <summary>The settings modal: Audio / Video / Gameplay tabs, applied and persisted live.</summary>
public partial class SettingsPanel : Overlay
{
    protected override void Build()
    {
        Body.AddChild(Ui.Heading("Settings"));

        var tabs = new TabContainer { CustomMinimumSize = new Vector2(440, 280) };
        Body.AddChild(tabs);

        tabs.AddChild(AudioTab());
        tabs.AddChild(VideoTab());
        tabs.AddChild(GameplayTab());

        AddCloseButton();
    }

    private Control AudioTab()
    {
        var v = Tab("Audio");
        v.AddChild(Slider("Master", () => Settings.Master, x => Settings.Master = x));
        v.AddChild(Slider("Music", () => Settings.Music, x => Settings.Music = x));
        v.AddChild(Slider("SFX", () => Settings.Sfx, x => Settings.Sfx = x));
        v.AddChild(Slider("UI", () => Settings.Ui, x => Settings.Ui = x));
        return v;
    }

    private Control VideoTab()
    {
        var v = Tab("Video");
        v.AddChild(Toggle("Fullscreen", () => Settings.Fullscreen, x => Settings.Fullscreen = x));
        v.AddChild(Slider("Animation speed", () => Settings.AnimationSpeed, x => Settings.AnimationSpeed = x, 0.5f, 2f));
        return v;
    }

    private Control GameplayTab()
    {
        var v = Tab("Gameplay");
        v.AddChild(Toggle("Confirm before committing a move", () => Settings.ConfirmMoves, x => Settings.ConfirmMoves = x));
        return v;
    }

    private static VBoxContainer Tab(string name)
    {
        var v = new VBoxContainer { Name = name };
        v.AddThemeConstantOverride("separation", 14);
        return v;
    }

    private static Control Slider(string label, Func<float> get, Action<float> set, float min = 0f, float max = 1f)
    {
        var row = new VBoxContainer();
        row.AddChild(Ui.Body(label));
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = 0.05, Value = get(), CustomMinimumSize = new Vector2(380, 0) };
        slider.ValueChanged += value =>
        {
            set((float)value);
            Settings.Apply();
            Settings.Save();
        };
        row.AddChild(slider);
        return row;
    }

    private static Control Toggle(string label, Func<bool> get, Action<bool> set)
    {
        var check = new CheckButton { Text = label, ButtonPressed = get() };
        check.Toggled += value =>
        {
            set(value);
            Settings.Apply();
            Settings.Save();
        };
        return check;
    }
}
