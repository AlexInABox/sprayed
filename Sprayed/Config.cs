using System.ComponentModel;

namespace Sprayed;

public class Config
{
    public bool Debug { get; set; } = false;

    [Description("The cooldown between sprays in seconds.")]
    public float CooldownDuration { get; set; } = 15f;
    public int KeybindId { get; set; } = 206;
}