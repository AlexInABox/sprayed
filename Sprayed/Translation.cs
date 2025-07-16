namespace Sprayed;

public class Translation
{
    public string KeybindSettingLabel { get; set; } = "Place a spray!";

    public string KeybindSettingHintDescription { get; set; } =
        "Press this key to place a spray on the wall you are looking at.";
    
    public string AbilityOnCooldown { get; set; } = "<color=yellow>Your Spray is on cooldown! ({remaining}s)</color>";
    
    public string AbilityUsed { get; set; } = "<color=green>Spray has been placed!</color>";
}