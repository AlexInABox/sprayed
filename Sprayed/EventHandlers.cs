using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using UserSettings.ServerSpecific;
using UnityEngine;
using MEC;
using Logger = LabApi.Features.Console.Logger;

namespace Sprayed;

public static class EventHandlers
{
    private static readonly Dictionary<int, int> Cooldowns = new();
    private static readonly Dictionary<string, string> Sprays = new();

    public static void RegisterEvents()
    {
        LabApi.Events.Handlers.PlayerEvents.Joined += OnJoined;
        
        
        AudioClipStorage.LoadClip(Plugin.Instance.Config.SpraySoundEffectPath, "spray_sound_effect");
        
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

    private static void OnJoined(PlayerJoinedEventArgs ev)
    {
        _ = SetSprayForUserFromBackend(ev.Player.UserId);
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
    
    private static async void PlaceSpray(Player player)
    {
        if (player == null || !player.IsAlive || player.IsDisarmed)
            return;
        
        if (Cooldowns.TryGetValue(player.PlayerId, out int cooldown) && cooldown > Time.time)
        {
            float remaining = Mathf.Round((cooldown - Time.time) * 10f) / 10f;
            string message = Plugin.Instance.Translation.AbilityOnCooldown.Replace("{remaining}", $"{remaining}");
            player.SendHint(message, 3f);
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

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, 2.5f, layerMask))
            return;

        Logger.Debug($"Hit layer: {LayerMask.LayerToName(hit.transform.gameObject.layer)} ({hit.transform.gameObject.layer})");

        // Get spray text from backend
        string sprayText = Sprays[player.UserId];
        
        if (string.IsNullOrEmpty(sprayText))
        {
            player.SendHint("Set a custom spray at \"dev.zeitvertreib.vip\" ^^", 10f);
            return;
        }

        Vector3 forward = -hit.normal;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 up = Vector3.Cross(forward, right).normalized;

        Vector3 basePos = hit.point + hit.normal * 0.01f;
        Quaternion rotation = Quaternion.LookRotation(forward);

        CreateText(basePos, new Vector3(0.05f, 0.068f, 1f), rotation, sprayText, 25f);
        CreateText(basePos + up * 0.005f + right * 0.005f, new Vector3(0.05f, 0.068f, 1f), rotation, sprayText, 25f);
        PlaySoundEffect(basePos);

        Cooldowns[player.PlayerId] = (int)(Time.time + Plugin.Instance.Config.CooldownDuration);
        player.SendHitMarker(); // funny
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

    private static void PlaySoundEffect(Vector3 pos)
    {
        AudioPlayer audioPlayer = AudioPlayer.CreateOrGet($"sprayed_audioplayer" + pos.GetHashCode());
        audioPlayer.AddSpeaker("sprayed_speaker" + pos.GetHashCode(), pos, 10F, true, 5F, 1000F);
        audioPlayer.DestroyWhenAllClipsPlayed = true;
        audioPlayer.AddClip("spray_sound_effect", 10F);
        
        Logger.Debug("Playing sound effect at position: " + pos);
    }

    private static async Task SetSprayForUserFromBackend(string userId)
    {
        try
        {
            var config = Plugin.Instance.Config;
            string endpoint = $"{config.BackendURL}/spray?userid={userId}";

            Logger.Debug($"Fetching spray from endpoint: {endpoint}");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", config.BackendAPIToken);

                var response = await client.GetAsync(endpoint);

                if (response.IsSuccessStatusCode)
                {
                    var sprayText = await response.Content.ReadAsStringAsync();
                    Logger.Debug($"Successfully fetched spray for Steam ID: {userId}");
                    Sprays[userId] = sprayText;
                }
                else
                {
                    Logger.Debug($"Failed to fetch spray for User ID: {userId}. Status: {response.StatusCode}");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Exception while fetching spray for Steam ID {userId}: {ex}");
            return;
        }
    }
}