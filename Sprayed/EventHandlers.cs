using System.Collections.Generic;
using LabApi.Features.Wrappers;
using UserSettings.ServerSpecific;
using UnityEngine;
using MEC;
using Logger = LabApi.Features.Console.Logger;

namespace Sprayed;

public static class EventHandlers
{
    private static readonly Dictionary<int, int> Cooldowns = new();

    public static void RegisterEvents()
    {
        ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSSSReceived;
        
        var extra = new ServerSpecificSettingBase[]
        {
            new SSGroupHeader("CS:GO (Zeitvertreib) Spray"),
            new SSKeybindSetting(
                Plugin.Instance.Config.KeybindId,
                Plugin.Instance.Translation.KeybindSettingLabel,
                KeyCode.None, false, false,
                Plugin.Instance.Translation.KeybindSettingHintDescription)
        };

        var existing = ServerSpecificSettingsSync.DefinedSettings ?? [];

        var combined = new ServerSpecificSettingBase[existing.Length + extra.Length];
        existing.CopyTo(combined, 0);
        extra.CopyTo(combined, existing.Length);

        ServerSpecificSettingsSync.DefinedSettings = combined;
        ServerSpecificSettingsSync.UpdateDefinedSettings();
    }

    public static void UnregisterEvents()
    {
        ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnSSSReceived;
        
    }

    private static void OnSSSReceived(ReferenceHub hub, ServerSpecificSettingBase ev)
    {
        if (!Player.TryGet(hub.networkIdentity, out Player player))
            return;
        
        // Check if the setting is the keybind setting and if it is pressed
        if (ev is SSKeybindSetting keybindSetting &&
            keybindSetting.SettingId == Plugin.Instance.Config.KeybindId &&
            keybindSetting.SyncIsPressed)
            PlaceSpray(player);
    }
    
    private static void PlaceSpray(Player player)
    {
        if (player == null || !player.IsAlive || player.IsDisarmed)
            return;
        
        if (Cooldowns.TryGetValue(player.PlayerId, out int cooldown) && cooldown > Time.time)
        {
            player.SendHint(Plugin.Instance.Translation.AbilityOnCooldown, 5f);
            return;
        }

        Vector3 origin = player.Camera.position;
        Vector3 direction = player.Camera.forward;

        // Ignored layers
        int ignoredLayers =
            (1 << 1)  | // TransparentFX
            (1 << 8)  | // Player
            (1 << 13) | // Hitbox
            (1 << 16) | // InvisibleCollider
            (1 << 17) | // Ragdoll
            (1 << 18) | // CCTV
            (1 << 20) | // Grenade
            (1 << 27) | // Door
            (1 << 28) | // Skybox
            (1 << 29);  // Fence

        int layerMask = ~ignoredLayers;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, 5f, layerMask))
            return;

        Logger.Debug($"Hit layer: {LayerMask.LayerToName(hit.transform.gameObject.layer)} ({hit.transform.gameObject.layer})");

        TextToy spray = TextToy.Create(null, true);
        var text = "<color=#0e012e>████</color><color=#0f012f>█████████</color><color=#10012f>██</color><color=#10012e>██</color><color=#10012d>█</color><color=#10012c>█</color><color=#11012b>█</color><color=#11012a>█</color><color=#120129>█</color><color=#130128>█</color><color=#140126>█</color><color=#150125>█</color><color=#150124>█</color><color=#160123>█</color><color=#170122>█</color><color=#170221>█</color><color=#180220>█</color><color=#18021f>█</color><color=#18021e>█</color>\n<color=#100131>██</color><color=#100132>█</color><color=#110132>█</color><color=#110133>█</color><color=#120134>██</color><color=#130135>█</color><color=#130136>██</color><color=#140137>███</color><color=#150137>█</color><color=#150136>█</color><color=#160136>█</color><color=#170135>█</color><color=#180134>█</color><color=#190132>█</color><color=#1a0131>█</color><color=#1a0130>█</color><color=#1b012e>█</color><color=#1c012c>█</color><color=#1c012b>█</color><color=#1d0229>█</color><color=#1d0227>█</color><color=#1e0226>█</color><color=#1e0224>█</color><color=#1f0222>█</color><color=#1f0221>█</color><color=#1f0220>█</color><color=#1f021f>█</color>\n<color=#120136>█</color><color=#130137>█</color><color=#140138>█</color><color=#15013a>█</color><color=#16013b>█</color><color=#17013d>█</color><color=#18013e>█</color><color=#18013f>█</color><color=#190140>█</color><color=#1a0141>█</color><color=#1b0142>█</color><color=#1c0143>█</color><color=#1d0143>█</color><color=#1f0143>█</color><color=#200142>█</color><color=#220141>█</color><color=#230140>█</color><color=#24013f>█</color><color=#25013d>█</color><color=#26013c>█</color><color=#270239>█</color><color=#270237>█</color><color=#280235>█</color><color=#280233>█</color><color=#280230>█</color><color=#28022e>█</color><color=#28022b>█</color><color=#280329>█</color><color=#270327>█</color><color=#270324>█</color><color=#270323>█</color><color=#270321>█</color>\n<color=#17013e>█</color><color=#180140>█</color><color=#1a0142>█</color><color=#1b0144>█</color><color=#1d0147>█</color><color=#1e0149>█</color><color=#20014b>█</color><color=#21014d>█</color><color=#23014f>█</color><color=#240151>█</color><color=#270152>█</color><color=#290153>█</color><color=#2b0153>█</color><color=#2d0153>█</color><color=#2e0150>█</color><color=#0c0016>█</color><color=#2b0144>█</color><color=#34024f>█</color><color=#35024d>█</color><color=#36024a>█</color><color=#360248>█</color><color=#360245>█</color><color=#360242>█</color><color=#36033f>█</color><color=#35033b>█</color><color=#350338>█</color><color=#340335>█</color><color=#340331>█</color><color=#33032e>█</color><color=#32032b>█</color><color=#310428>█</color><color=#310426>█</color>\n<color=#1d0148>█</color><color=#1f014b>█</color><color=#21014f>█</color><color=#230152>█</color><color=#250155>█</color><color=#270158>█</color><color=#2a015b>█</color><color=#2c015e>█</color><color=#2f0161>█</color><color=#320163>█</color><color=#350164>█</color><color=#390165>█</color><color=#3c0166>█</color><color=#3c0161>█</color><color=#11001b>█</color><color=#000000>█</color><color=#08000c>█</color><color=#3b0253>█</color><color=#47025f>█</color><color=#47025c>█</color><color=#480359>█</color><color=#470356>█</color><color=#470352>█</color><color=#46034e>█</color><color=#45034a>█</color><color=#440445>█</color><color=#430441>█</color><color=#41043c>█</color><color=#400438>█</color><color=#3e0434>█</color><color=#3d042f>█</color><color=#3c042d>█</color>\n<color=#240154>█</color><color=#260158>█</color><color=#29015c>█</color><color=#2c0161>█</color><color=#2f0165>█</color><color=#320169>█</color><color=#35016c>█</color><color=#390170>█</color><color=#3d0173>█</color><color=#420175>█</color><color=#460177>█</color><color=#490178>█</color><color=#39015b>█</color><color=#0b0012>█</color><color=#000000>███</color><color=#050006>█</color><color=#340244>█</color><color=#58036e>█</color><color=#59036b>█</color><color=#590367>█</color><color=#580463>█</color><color=#57045e>█</color><color=#550459>█</color><color=#540454>█</color><color=#52054f>█</color><color=#500549>█</color><color=#4e0544>█</color><color=#4c053e>█</color><color=#4a0539>█</color><color=#490535>█</color>\n<color=#2b0160>█</color><color=#2e0165>█</color><color=#31016a>█</color><color=#35016f>█</color><color=#390174>█</color><color=#3d0178>█</color><color=#42017c>█</color><color=#470180>█</color><color=#4c0183>█</color><color=#4d017f>█</color><color=#360157>█</color><color=#160023>█</color><color=#000000>███████</color><color=#100013>█</color><color=#390244>█</color><color=#5e046b>█</color><color=#680472>█</color><color=#67056d>█</color><color=#650568>█</color><color=#640562>█</color><color=#62055c>█</color><color=#5f0656>█</color><color=#5d0650>█</color><color=#5a0649>█</color><color=#570643>█</color><color=#55063f>█</color>\n<color=#31016a>█</color><color=#350170>█</color><color=#390176>█</color><color=#3d017c>█</color><color=#430181>█</color><color=#480185>█</color><color=#3a0168>█</color><color=#270043>█</color><color=#150024>█</color><color=#030004>█</color><color=#000000>████████████</color><color=#160117>█</color><color=#310234>█</color><color=#4e044f>█</color><color=#6e066a>█</color><color=#6f0668>█</color><color=#6d0761>█</color><color=#6a075a>█</color><color=#670752>█</color><color=#64074b>█</color><color=#610746>█</color>\n<color=#360172>█</color><color=#3b0179>█</color><color=#40017f>█</color><color=#450185>█</color><color=#4c018a>█</color><color=#52018f>█</color><color=#590193>█</color><color=#520183>█</color><color=#400162>█</color><color=#27013a>█</color><color=#07000a>█</color><color=#000000>█████████</color><color=#030003>█</color><color=#250228>█</color><color=#4b034e>█</color><color=#6a056b>█</color><color=#7f077c>█</color><color=#7e0777>█</color><color=#7c0770>█</color><color=#790869>█</color><color=#760861>█</color><color=#720859>█</color><color=#6e0851>█</color><color=#6c094c>█</color>\n<color=#390178>█</color><color=#3f017f>█</color><color=#460185>█</color><color=#4d018b>█</color><color=#540191>█</color><color=#5b0196>█</color><color=#62019a>█</color><color=#68029e>█</color><color=#6e02a1>█</color><color=#7302a4>█</color><color=#7503a2>█</color><color=#51026c>█</color><color=#15001c>█</color><color=#000000>█████</color><color=#0b000c>█</color><color=#4d0354>█</color><color=#83068d>█</color><color=#8e0695>█</color><color=#8d0790>█</color><color=#8c078a>█</color><color=#8a0884>█</color><color=#88087d>█</color><color=#860875>█</color><color=#83096d>█</color><color=#7f0965>█</color><color=#7b095d>█</color><color=#770a54>█</color><color=#750a4f>█</color>\n<color=#3c017b>█</color><color=#430182>█</color><color=#4a0188>█</color><color=#52018f>█</color><color=#5a0194>█</color><color=#62029a>█</color><color=#69029e>█</color><color=#6f02a3>█</color><color=#7502a6>█</color><color=#7b03a9>█</color><color=#8003ac>█</color><color=#8503ad>█</color><color=#8604ab>█</color><color=#450256>█</color><color=#000000>███</color><color=#2d0233>█</color><color=#8a0599>█</color><color=#9706a3>█</color><color=#97079f>█</color><color=#97079a>█</color><color=#960894>█</color><color=#94088e>█</color><color=#920987>█</color><color=#900980>█</color><color=#8d0978>█</color><color=#8a0a6f>█</color><color=#870a67>█</color><color=#830b5e>█</color><color=#7f0b55>█</color><color=#7c0b50>█</color>\n<color=#3d017e>█</color><color=#450184>█</color><color=#4d018b>█</color><color=#560191>█</color><color=#5e0297>█</color><color=#66029c>█</color><color=#6d02a1>█</color><color=#7402a5>█</color><color=#7b03a9>█</color><color=#8103ac>█</color><color=#8603ae>█</color><color=#8b04b0>█</color><color=#8f04b2>█</color><color=#9305b3>█</color><color=#5e0370>█</color><color=#000000>█</color><color=#3b0243>█</color><color=#9c06ad>█</color><color=#9d07aa>█</color><color=#9d07a6>█</color><color=#9d08a1>█</color><color=#9d089c>█</color><color=#9c0996>█</color><color=#9a098f>█</color><color=#980a88>█</color><color=#950a81>█</color><color=#920a79>█</color><color=#8f0b70>█</color><color=#8b0b68>█</color><color=#870c60>█</color><color=#840c58>█</color><color=#810d53>█</color>\n<color=#3c0184>█</color><color=#45018a>█</color><color=#4e018f>█</color><color=#570194>█</color><color=#5f0299>█</color><color=#67029e>█</color><color=#6f02a3>█</color><color=#7603a7>█</color><color=#7d03ab>█</color><color=#8304ae>█</color><color=#8904b0>█</color><color=#8e05b2>█</color><color=#9205b4>█</color><color=#9605b5>█</color><color=#9906b5>█</color><color=#660476>█</color><color=#9c07b0>█</color><color=#9f07af>█</color><color=#a008ac>█</color><color=#a108a7>█</color><color=#a108a2>█</color><color=#a0099d>█</color><color=#9f0a97>█</color><color=#9d0a90>█</color><color=#9b0b89>█</color><color=#980b81>█</color><color=#950c7a>█</color><color=#910c72>█</color><color=#8e0d6b>█</color><color=#8a0d64>█</color><color=#870e5d>█</color><color=#850e5a>█</color>\n<color=#3c018f>█</color><color=#440193>█</color><color=#4c0296>█</color><color=#55029a>█</color><color=#5d039e>█</color><color=#6503a2>█</color><color=#6d04a6>█</color><color=#7505aa>█</color><color=#7b06ad>█</color><color=#8206b0>█</color><color=#8707b3>█</color><color=#8d08b4>█</color><color=#9108b6>█</color><color=#9509b7>█</color><color=#9909b6>█</color><color=#9b09b5>█</color><color=#9d0ab4>█</color><color=#9f0ab1>█</color><color=#a00bad>█</color><color=#a00ba9>█</color><color=#a00ba4>█</color><color=#9f0b9f>█</color><color=#9e0c99>█</color><color=#9c0c93>█</color><color=#9a0c8c>█</color><color=#970d85>█</color><color=#950d7e>█</color><color=#920e77>█</color><color=#900e71>█</color><color=#8e0f6c>█</color><color=#8d0f67>█</color><color=#8c1065>█</color>\n<color=#43029e>█</color><color=#47029f>█</color><color=#4d03a2>█</color><color=#5304a4>█</color><color=#5a06a7>█</color><color=#6208aa>█</color><color=#690aad>█</color><color=#700cb1>█</color><color=#760db3>█</color><color=#7d0fb6>█</color><color=#8210b8>█</color><color=#8810b9>█</color><color=#8c11ba>█</color><color=#9011ba>█</color><color=#9411ba>█</color><color=#9712b9>█</color><color=#9912b7>█</color><color=#9a12b4>█</color><color=#9b12b1>█</color><color=#9c12ad>█</color><color=#9b12a8>█</color><color=#9b12a3>█</color><color=#9a129e>█</color><color=#981298>█</color><color=#971292>█</color><color=#96128c>█</color><color=#951286>█</color><color=#951280>█</color><color=#96127b>█</color><color=#981277>█</color><color=#9b1375>█</color><color=#9d1474>█</color>\n";
        Vector3 forward = -hit.normal;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 up = Vector3.Cross(forward, right).normalized;

        Vector3 basePos = hit.point + hit.normal * 0.01f;
        Quaternion rotation = Quaternion.LookRotation(forward);

        CreateText(basePos, new Vector3(0.05f, 0.07f, 1f), rotation, text, 10f);
        CreateText(basePos + up * 0.005f + right * 0.005f, new Vector3(0.05f, 0.07f, 1f), rotation, text, 10f);

        Cooldowns[player.PlayerId] = (int)(Time.time + Plugin.Instance.Config.CooldownDuration);
        player.SendHint(Plugin.Instance.Translation.AbilityUsed, 3f);
    }
    
    private static void CreateText(Vector3 pos, Vector3 scale, Quaternion rot, string text, float time = 20)
    {
        TextToy textToy = TextToy.Create();
        textToy.Position = pos;
        textToy.Scale = scale;
        textToy.Rotation = rot;
        textToy.DisplaySize = new Vector2(100000, 100000);
        textToy.TextFormat = text;

        Timing.CallDelayed(time, textToy.Destroy);
    }
}