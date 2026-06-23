using System;
using Godot;
using INISOnline.Theme;

namespace INISOnline.App;

/// <summary>A simple modal list of actions (e.g. the in-game gear menu). Each item closes the menu first.</summary>
public partial class MenuOverlay : Overlay
{
    private readonly string _title;
    private readonly (string Label, Action Action)[] _items;

    public MenuOverlay(string title, params (string Label, Action Action)[] items)
    {
        _title = title;
        _items = items;
    }

    protected override void Build()
    {
        Body.AddChild(Ui.Heading(_title));
        foreach (var (label, action) in _items)
        {
            var button = Ui.MenuButton(label);
            var captured = action;
            button.Pressed += () => { Close(); captured(); };
            Body.AddChild(button);
        }
        AddCloseButton("Resume");
    }
}
