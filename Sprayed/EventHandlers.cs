using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using MEC;
using TMPro;
using UnityEngine;
using UserSettings.ServerSpecific;
using Logger = LabApi.Features.Console.Logger;

namespace Sprayed;

public static class EventHandlers
{
    private static readonly Dictionary<int, int> Cooldowns = new();
    private static readonly Dictionary<int, int> RefreshCooldowns = new();
    private static readonly Dictionary<string, string> Sprays = new();
    private static readonly Dictionary<int, List<TextToy>> ActiveSprays = new();

    public static void RegisterEvents()
    {
        PlayerEvents.Joined += OnJoined;


        AudioClipStorage.LoadClip(Plugin.Instance.Config!.SpraySoundEffectPath, "spray_sound_effect");

        ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSSSReceived;

        ServerSpecificSettingBase[] extra = new ServerSpecificSettingBase[]
        {
            new SSGroupHeader(Plugin.Instance.Translation.SprayGroupHeader),
            new SSKeybindSetting(
                Plugin.Instance.Config!.KeybindId,
                Plugin.Instance.Translation.KeybindSettingLabel,
                KeyCode.None, false, false,
                Plugin.Instance.Translation.KeybindSettingHintDescription),
            new SSButton(Plugin.Instance.Config!.KeybindId, null, Plugin.Instance.Translation.ReloadSprayButtonLabel),
            new SSTextArea(
                Plugin.Instance.Config!.KeybindId,
                "<link=https://dev.zeitvertreib.vip/dashboard><align=center><color=#8A2BE2><size=110%><u>Klicke hier um dein eigenes Spray festzulegen!</u></size></color></align></link>",
                SSTextArea.FoldoutMode.NotCollapsable,
                null,
                TextAlignmentOptions.Center
            )
        };

        ServerSpecificSettingBase[] existing = ServerSpecificSettingsSync.DefinedSettings ?? [];

        ServerSpecificSettingBase[] combined = new ServerSpecificSettingBase[existing.Length + extra.Length];
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
            keybindSetting.SettingId == Plugin.Instance.Config!.KeybindId &&
            keybindSetting.SyncIsPressed)
        {
            PlaceSpray(player);
            return;
        }

        if (ev is SSButton button && button.SettingId == Plugin.Instance.Config!.KeybindId)
        {
            if (RefreshCooldowns.TryGetValue(player.PlayerId, out int cooldown) && cooldown > Time.time) return;
            // Reload spray for the player
            _ = SetSprayForUserFromBackend(player.UserId);
            player.SendHint(Plugin.Instance.Translation.SpraysRefreshed, 10f);
            RefreshCooldowns[player.PlayerId] = (int)(Time.time + 5f); // 5 seconds cooldown for refreshing sprays
            Logger.Debug($"Reloaded spray for player {player.UserId} ({player.PlayerId})");
        }
    }

    private static void PlaceSpray(Player player)
    {
        if (player == null || !player.IsAlive || player.IsDisarmed)
            return;

        if (Cooldowns.TryGetValue(player.PlayerId, out int cooldown) && cooldown > Time.time)
        {
            float remaining = Mathf.Round((cooldown - Time.time) * 10f) / 10f;
            string message = Plugin.Instance.Translation.AbilityOnCooldown.Replace("{remaining}", $"{remaining}");
            player.SendHint(message);
            return;
        }

        Vector3 origin = player.Camera.position;
        Vector3 direction = player.Camera.forward;

        // Ignored layers
        int ignoredLayers =
            (1 << 1) | // TransparentFX
            (1 << 8) | // Player
            (1 << 13) | // Hitbox
            (1 << 16) | // InvisibleCollider
            (1 << 17) | // Ragdoll
            (1 << 18) | // CCTV
            (1 << 20) | // Grenade
            (1 << 28) | // Skybox
            (1 << 29); // Fence

        int layerMask = ~ignoredLayers;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, 2.5f, layerMask))
            return;

        if (Player.TryGet(hit.transform.gameObject, out _)) return;

        Logger.Debug(
            $"Hit layer: {LayerMask.LayerToName(hit.transform.gameObject.layer)} ({hit.transform.gameObject.layer})");

        if (string.IsNullOrEmpty(Sprays[player.UserId]))
        {
            player.SendHint(Plugin.Instance.Translation.NoSprayFound, 10f);
            return;
        }

        if (ActiveSprays.TryGetValue(player.PlayerId, out List<TextToy> sprays) && sprays?.Count > 0)
        {
            sprays.ForEach(s =>
            {
                if (!s.IsDestroyed) s.Destroy();
            });
            ActiveSprays.Remove(player.PlayerId);
        }

        // Get spray text from backend
        string sprayText = Sprays[player.UserId];

        Vector3 forward = -hit.normal;

        Vector3 basePos = hit.point + hit.normal * 0.01f;
        Quaternion rotation = Quaternion.LookRotation(forward);
        Vector3 realScale = new(0.015f, 0.01f, 1f);

        Timing.RunCoroutine(SpawnSpray(basePos, rotation, realScale, sprayText, hit.transform, player.PlayerId));

        PlaySoundEffect(basePos);

        Cooldowns[player.PlayerId] = (int)(Time.time + Plugin.Instance.Config!.CooldownDuration);
        player.SendHitMarker(); // funny
        player.SendHint(Plugin.Instance.Translation.AbilityUsed);
    }

    private static IEnumerator<float> SpawnSpray(Vector3 basePos, Quaternion rotation, Vector3 scale, string sprayText,
        Transform parent, int playerId)
    {
        string[] lines = sprayText.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();

        float lineSpacing = 0.01f; // Manual line spacing - adjust this value as needed

        // Calculate total height of the spray
        float totalHeight = (lines.Length - 1) * lineSpacing;

        // Start at half the total height above the hit point to center the spray
        Vector3 pos = basePos + rotation * Vector3.up * (totalHeight / 2);

        ActiveSprays[playerId] = new List<TextToy>();
        ActiveSprays[playerId].Add(CreateText(pos, scale, rotation, lines[0], parent));
        foreach (string _ in lines)
        {
            ActiveSprays[playerId].Add(CreateText(pos, scale, rotation, "", parent));

            // Move down for next line
            pos -= rotation * Vector3.up * lineSpacing;
        }

        for (int i = 0; i < ActiveSprays[playerId].Count; i++)
        {
            TextToy textToy = ActiveSprays[playerId][i];
            textToy.TextFormat = i < lines.Length ? lines[i] : string.Empty; // Clear remaining lines
            yield return Timing.WaitForOneFrame;
        }
    }

    private static TextToy CreateText(Vector3 pos, Vector3 scale, Quaternion rot, string text, Transform parent)
    {
        TextToy textToy = TextToy.Create();
        textToy.Scale = scale;
        textToy.Rotation = rot;
        textToy.DisplaySize = new Vector2(100000, 100000);
        textToy.TextFormat = text;
        textToy.Position = pos;


        Timing.RunCoroutine(SprayLifeTime(parent, textToy));
        Timing.CallDelayed(300f, textToy.Destroy);

        return textToy;
    }

    private static IEnumerator<float> SprayLifeTime(Transform parent, TextToy textToy)
    {
        // Cache the local offset
        Vector3 localOffset = Quaternion.Inverse(parent.rotation) * (textToy.Position - parent.position);
        Quaternion localRotation = Quaternion.Inverse(parent.rotation) * textToy.Rotation;

        while (!textToy.IsDestroyed)
        {
            Vector3 newPos = parent.position + parent.rotation * localOffset;
            Quaternion newRot = parent.rotation * localRotation;

            if (textToy.Position != newPos)
                textToy.Position = newPos;

            if (textToy.Rotation != newRot)
                textToy.Rotation = newRot;

            yield return Timing.WaitForOneFrame;
        }
    }


    private static void PlaySoundEffect(Vector3 pos)
    {
        AudioPlayer audioPlayer = AudioPlayer.CreateOrGet("sprayed_audioplayer" + pos.GetHashCode());
        audioPlayer.AddSpeaker("sprayed_speaker" + pos.GetHashCode(), pos, 10F, true, 5F, 1000F);
        audioPlayer.DestroyWhenAllClipsPlayed = true;
        audioPlayer.AddClip("spray_sound_effect", 10F);

        Logger.Debug("Playing sound effect at position: " + pos);
    }

    private static async Task SetSprayForUserFromBackend(string userId)
    {
        try
        {
            Config config = Plugin.Instance.Config!;
            string endpoint = $"{config.BackendURL}/spray?userid={userId}";

            Logger.Debug($"Fetching spray from endpoint: {endpoint}");

            using (HttpClient client = new())
            {
                client.DefaultRequestHeaders.Add("Authorization", config.BackendAPIToken);

                HttpResponseMessage response = await client.GetAsync(endpoint);

                if (response.IsSuccessStatusCode)
                {
                    string sprayText = await response.Content.ReadAsStringAsync();
                    Logger.Debug($"Successfully fetched spray for Steam ID: {userId}");
                    Sprays[userId] = sprayText;
                }
                else
                {
                    Logger.Debug($"Failed to fetch spray for User ID: {userId}. Status: {response.StatusCode}");
                    Sprays[userId] = string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Exception while fetching spray for Steam ID {userId}: {ex}");
        }
    }
}