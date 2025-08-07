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
    private static readonly Dictionary<string, SprayData> Sprays = new();
    private static readonly Dictionary<int, SprayInstance> ActiveSprays = new();

    private static readonly SprayPhysics SprayPhysics = new();
    private static readonly SprayRenderer SprayRenderer = new();
    private static readonly SprayBackendService BackendService = new();

    public static void RegisterEvents()
    {
        PlayerEvents.Joined += OnJoined;
        AudioClipStorage.LoadClip(Plugin.Instance.Config!.SpraySoundEffectPath, "spray_sound_effect");
        ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSSSReceived;
        RegisterServerSpecificSettings();
    }

    public static void UnregisterEvents()
    {
        ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnSSSReceived;
    }

    private static void RegisterServerSpecificSettings()
    {
        ServerSpecificSettingBase[] extra =
        [
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
        ];

        ServerSpecificSettingBase[] existing = ServerSpecificSettingsSync.DefinedSettings ?? [];
        ServerSpecificSettingBase[] combined = new ServerSpecificSettingBase[existing.Length + extra.Length];
        existing.CopyTo(combined, 0);
        extra.CopyTo(combined, existing.Length);

        ServerSpecificSettingsSync.DefinedSettings = combined;
        ServerSpecificSettingsSync.UpdateDefinedSettings();
    }

    private static void OnJoined(PlayerJoinedEventArgs ev)
    {
        _ = BackendService.LoadSprayForUser(ev.Player.UserId, Sprays);
    }

    private static void OnSSSReceived(ReferenceHub hub, ServerSpecificSettingBase ev)
    {
        if (!Player.TryGet(hub.networkIdentity, out Player player))
            return;

        if (ev is SSKeybindSetting keybindSetting &&
            keybindSetting.SettingId == Plugin.Instance.Config!.KeybindId &&
            keybindSetting.SyncIsPressed)
        {
            PlaceSpray(player);
            return;
        }

        if (ev is SSButton button && button.SettingId == Plugin.Instance.Config!.KeybindId) HandleSprayRefresh(player);
    }

    private static void HandleSprayRefresh(Player player)
    {
        if (RefreshCooldowns.TryGetValue(player.PlayerId, out int cooldown) && cooldown > Time.time)
            return;

        _ = BackendService.LoadSprayForUser(player.UserId, Sprays);
        player.SendHint(Plugin.Instance.Translation.SpraysRefreshed, 10f);
        RefreshCooldowns[player.PlayerId] = (int)(Time.time + 5f);
        Logger.Debug($"Reloaded spray for player {player.UserId} ({player.PlayerId})");
    }

    private static void PlaceSpray(Player player)
    {
        if (!ValidateSprayPlacement(player))
            return;

        if (!SprayPhysics.TryGetSprayPlacement(player, out SprayPlacement placement))
            return;

        if (!Sprays.TryGetValue(player.UserId, out SprayData sprayData) || sprayData.IsEmpty)
        {
            Logger.Debug($"No spray found for User ID: {player.UserId}");
            player.SendHint(Plugin.Instance.Translation.NoSprayFound, 10f);
            return;
        }

        DestroyExistingSpray(player.PlayerId);

        Timing.RunCoroutine(SprayRenderer.RenderSpray(placement, sprayData, player.PlayerId, ActiveSprays));
        SprayAudioManager.PlaySoundEffect(placement.Position);

        Cooldowns[player.PlayerId] = (int)(Time.time + Plugin.Instance.Config!.CooldownDuration);
        player.SendHitMarker();
        player.SendHint(Plugin.Instance.Translation.AbilityUsed);
    }

    private static bool ValidateSprayPlacement(Player player)
    {
        if (player == null || !player.IsAlive || player.IsDisarmed)
            return false;

        if (Cooldowns.TryGetValue(player.PlayerId, out int cooldown) && cooldown > Time.time)
        {
            float remaining = Mathf.Round((cooldown - Time.time) * 10f) / 10f;
            string message = Plugin.Instance.Translation.AbilityOnCooldown.Replace("{remaining}", $"{remaining}");
            player.SendHint(message);
            return false;
        }

        return true;
    }

    private static void DestroyExistingSpray(int playerId)
    {
        if (!ActiveSprays.TryGetValue(playerId, out SprayInstance sprayInstance))
            return;

        sprayInstance.Destroy();
        ActiveSprays.Remove(playerId);
    }
}

public class SprayData
{
    public SprayData(List<string> frames)
    {
        Frames = frames ?? new List<string>();
    }

    public SprayData(string singleFrame) : this(new List<string> { singleFrame })
    {
    }

    public List<string> Frames { get; }
    public bool IsGif => Frames.Count > 1;
    public bool IsEmpty => Frames.Count == 0 || string.IsNullOrEmpty(Frames[0]);
}

public class SprayPlacement
{
    public SprayPlacement(Vector3 position, Quaternion rotation, Vector3 scale, Transform parent)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
        Parent = parent;
    }

    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public Vector3 Scale { get; }
    public Transform Parent { get; }
}

public class SprayInstance
{
    public SprayInstance(List<TextToy> textToys)
    {
        TextToys = textToys ?? new List<TextToy>();
    }

    public List<TextToy> TextToys { get; }
    public bool IsDestroyed => TextToys.Count == 0 || TextToys[0].IsDestroyed;

    public void Destroy()
    {
        foreach (TextToy textToy in TextToys.Where(t => !t.IsDestroyed)) textToy.Destroy();
        TextToys.Clear();
    }
}

public class SprayPhysics
{
    private const float MaxRaycastDistance = 2.5f;

    private static readonly int IgnoredLayers =
        (1 << 1) | // TransparentFX
        (1 << 8) | // Player
        (1 << 13) | // Hitbox
        (1 << 16) | // InvisibleCollider
        (1 << 17) | // Ragdoll
        (1 << 18) | // CCTV
        (1 << 20) | // Grenade
        (1 << 28) | // Skybox
        (1 << 29); // Fence

    public bool TryGetSprayPlacement(Player player, out SprayPlacement placement)
    {
        placement = null;

        Vector3 origin = player.Camera.position;
        Vector3 direction = player.Camera.forward;
        int layerMask = ~IgnoredLayers;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, MaxRaycastDistance, layerMask))
            return false;

        if (Player.TryGet(hit.transform.gameObject, out _))
            return false;

        Logger.Debug(
            $"Hit layer: {LayerMask.LayerToName(hit.transform.gameObject.layer)} ({hit.transform.gameObject.layer})");

        Vector3 forward = -hit.normal;
        Vector3 basePos = hit.point + hit.normal * 0.01f;
        Quaternion rotation = Quaternion.LookRotation(forward);
        Vector3 scale = new(0.015f, 0.01f, 1f);

        placement = new SprayPlacement(basePos, rotation, scale, hit.transform);
        return true;
    }
}

public class SprayRenderer
{
    private const float LineSpacing = 0.01f;
    private const float GifFrameDuration = 0.2f;
    private const float SprayLifetime = 300f;

    public IEnumerator<float> RenderSpray(SprayPlacement placement, SprayData sprayData, int playerId,
        Dictionary<int, SprayInstance> activeSprays)
    {
        SprayFrameProcessor frameProcessor = new(sprayData);
        string[] firstFrameLines = frameProcessor.GetFirstFrameLines();

        // Create the canvas but with empty text initially
        List<TextToy> textToys = CreateSprayCanvas(placement, firstFrameLines, true);

        SprayInstance sprayInstance = new(textToys);
        activeSprays[playerId] = sprayInstance;

        // Animate the lines appearing one by one - inline the animation here
        for (int i = 0; i < Math.Min(textToys.Count, firstFrameLines.Length); i++)
            if (textToys[i] != null && !textToys[i].IsDestroyed)
            {
                textToys[i].TextFormat = firstFrameLines[i];
                yield return Timing.WaitForOneFrame;
            }

        // Only start GIF animation after line appearance is complete
        if (sprayData.IsGif) Timing.RunCoroutine(AnimateGif(frameProcessor, sprayInstance));
    }

    private IEnumerator<float> AnimateGif(SprayFrameProcessor frameProcessor, SprayInstance sprayInstance)
    {
        int frameIndex = 1;
        List<string[]> normalizedFrames = frameProcessor.GetNormalizedFrames();

        while (!sprayInstance.IsDestroyed)
        {
            string[] currentFrame = normalizedFrames[frameIndex];
            UpdateSprayFrame(sprayInstance.TextToys, currentFrame);

            frameIndex = (frameIndex + 1) % normalizedFrames.Count;
            yield return Timing.WaitForSeconds(GifFrameDuration);
        }

        Logger.Debug("Spray destroyed, stopping GIF playback.");
    }

    private void UpdateSprayFrame(List<TextToy> textToys, string[] frameLines)
    {
        int maxLines = Math.Min(textToys.Count, frameLines.Length);
        for (int i = 0; i < maxLines; i++)
            if (textToys[i] != null && !textToys[i].IsDestroyed)
                textToys[i].TextFormat = frameLines[i];
    }

    private List<TextToy> CreateSprayCanvas(SprayPlacement placement, string[] lines, bool startEmpty = false)
    {
        List<TextToy> textToys = new();
        float totalHeight = (lines.Length - 1) * LineSpacing;
        Vector3 startPos = placement.Position + placement.Rotation * Vector3.up * (totalHeight / 2);

        foreach (string line in lines)
        {
            // Start with empty text if startEmpty is true, otherwise use the line content
            string initialText = startEmpty ? string.Empty : line;
            TextToy textToy = CreateTextToy(startPos, placement.Scale, placement.Rotation, initialText,
                placement.Parent);
            textToys.Add(textToy);
            startPos -= placement.Rotation * Vector3.up * LineSpacing;
        }

        return textToys;
    }

    private IEnumerator<float> MaintainSprayPosition(Transform parent, TextToy textToy)
    {
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

    private TextToy CreateTextToy(Vector3 pos, Vector3 scale, Quaternion rot, string text, Transform parent)
    {
        TextToy textToy = TextToy.Create();
        textToy.Scale = scale;
        textToy.Rotation = rot;
        textToy.DisplaySize = new Vector2(100000, 100000);
        textToy.TextFormat = text;
        textToy.Position = pos;

        Timing.RunCoroutine(MaintainSprayPosition(parent, textToy));
        Timing.CallDelayed(SprayLifetime, textToy.Destroy);

        return textToy;
    }
}

public class SprayFrameProcessor
{
    private readonly SprayData _sprayData;
    private List<string[]> _normalizedFrames;

    public SprayFrameProcessor(SprayData sprayData)
    {
        _sprayData = sprayData;
    }

    public string[] GetFirstFrameLines()
    {
        return SplitFrameIntoLines(_sprayData.Frames[0]);
    }

    public List<string[]> GetNormalizedFrames()
    {
        if (_normalizedFrames != null)
            return _normalizedFrames;

        _normalizedFrames = _sprayData.Frames
            .Select(SplitFrameIntoLines)
            .ToList();

        NormalizeFrameLengths();
        return _normalizedFrames;
    }

    private string[] SplitFrameIntoLines(string frame)
    {
        return frame.Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private void NormalizeFrameLengths()
    {
        if (_normalizedFrames.Count == 0) return;

        int targetLineCount = _normalizedFrames[0].Length;

        for (int i = 0; i < _normalizedFrames.Count; i++)
            if (_normalizedFrames[i].Length < targetLineCount)
            {
                string[] frame = _normalizedFrames[i];
                Array.Resize(ref frame, targetLineCount);

                for (int j = 0; j < targetLineCount; j++) frame[j] ??= string.Empty;

                _normalizedFrames[i] = frame;
            }
    }
}

public static class SprayAudioManager
{
    public static void PlaySoundEffect(Vector3 position)
    {
        string audioPlayerId = "sprayed_audioplayer" + position.GetHashCode();
        string speakerId = "sprayed_speaker" + position.GetHashCode();

        AudioPlayer audioPlayer = AudioPlayer.CreateOrGet(audioPlayerId);
        audioPlayer.AddSpeaker(speakerId, position, 10F, true, 5F, 1000F);
        audioPlayer.DestroyWhenAllClipsPlayed = true;
        audioPlayer.AddClip("spray_sound_effect", 10F);

        Logger.Debug("Playing sound effect at position: " + position);
    }
}

public class SprayBackendService
{
    public async Task LoadSprayForUser(string userId, Dictionary<string, SprayData> sprays)
    {
        try
        {
            Config config = Plugin.Instance.Config!;
            string endpoint = $"{config.BackendURL}/spray?userid={userId}";

            Logger.Debug($"Fetching spray from endpoint: {endpoint}");

            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("Authorization", config.BackendAPIToken);

            HttpResponseMessage response = await client.GetAsync(endpoint);

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                Logger.Debug($"Successfully fetched spray for User ID: {userId}");

                // Parse the JSON response as a list of strings
                List<string> frames = ParseSprayFrames(jsonResponse);
                sprays[userId] = new SprayData(frames);
            }
            else
            {
                Logger.Debug($"Failed to fetch spray for User ID: {userId}. Status: {response.StatusCode}");
                sprays[userId] = new SprayData(string.Empty);
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Exception while fetching spray for User ID {userId}: {ex}");
            sprays[userId] = new SprayData(string.Empty);
        }
    }

    private List<string> ParseSprayFrames(string jsonResponse)
    {
        try
        {
            // Try to parse as JSON array first
            if (jsonResponse.TrimStart().StartsWith("["))
            {
                // Simple JSON array parsing - remove brackets and split by quotes
                string content = jsonResponse.Trim().Trim('[', ']');
                if (string.IsNullOrWhiteSpace(content)) return new List<string> { string.Empty };

                List<string> frames = new();
                string[] parts = content.Split(new[] { "\",\"" }, StringSplitOptions.None);

                for (int i = 0; i < parts.Length; i++)
                {
                    string frame = parts[i].Trim('"');
                    // Unescape JSON string
                    frame = frame.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                    frames.Add(frame);
                }

                return frames;
            }

            // Treat as single string frame
            return new List<string> { jsonResponse };
        }
        catch (Exception ex)
        {
            Logger.Debug($"Failed to parse spray frames JSON: {ex}");
            // Fallback to treating the entire response as a single frame
            return new List<string> { jsonResponse };
        }
    }
}