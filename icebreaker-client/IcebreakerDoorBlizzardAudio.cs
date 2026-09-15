using System;
using System.Collections.Generic;
using System.IO;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using ZLinq;

namespace Manimal.Icebreaker
{
    // 1.1.5's twelve HandlerPlaySoundAdvanced components decoded from level707.
    // SPT lacks their fade fields and the imported doors lack the retail trigger
    // graph. Adapt synchronized DoorState / our hatch driver to SPT BetterAudio.
    // The authored sound, anchors, gain, fades, rolloff and occlusion mode survive.
    internal sealed class IcebreakerDoorBlizzardAudio : MonoBehaviour
    {
        private const string ResourcePrefix = "Manimal.Icebreaker.DoorBlizzard.";
        private static IcebreakerDoorBlizzardAudio _live;
        private readonly List<Emitter> _emitters = new List<Emitter>();
        private AudioClip _clip;
        private float _nextBind;
        private bool _missingLogged;
        private float _createdAt;

        internal static void Restore()
        {
            if (!FikaBridge.CanRender || _live != null) return;
            var scene = SceneManager.GetSceneByName("Icebreaker_Sound");
            if (!scene.IsValid() || !scene.isLoaded) return;
            var host = new GameObject("Icebreaker_DoorBlizzardAudio");
            SceneManager.MoveGameObjectToScene(host, scene);
            var driver = host.AddComponent<IcebreakerDoorBlizzardAudio>();
            _live = driver;
            try { driver.Initialize(); }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[DoorBlizzard] restore failed: {e.Message}");
                Destroy(host);
            }
        }

        private void Initialize()
        {
            var assembly = typeof(IcebreakerDoorBlizzardAudio).Assembly;
            JObject data;
            using (var stream = assembly.GetManifestResourceStream(ResourcePrefix + "manifest.json"))
            using (var reader = new StreamReader(stream ?? throw new InvalidDataException("Missing door audio manifest")))
                data = JObject.Parse(reader.ReadToEnd());
            if (data.Value<int>("formatVersion") != 1) throw new InvalidDataException("Unknown door audio format");
            int channels = data.Value<int>("channels"), frequency = data.Value<int>("frequency");
            using (var stream = assembly.GetManifestResourceStream(ResourcePrefix + "blizzard.pcm"))
            using (var reader = new BinaryReader(stream ?? throw new InvalidDataException("Missing door audio samples")))
            {
                if (channels < 1 || channels > 2 || frequency < 8000 || frequency > 96000 ||
                    stream.Length == 0 || stream.Length % (2 * channels) != 0)
                    throw new InvalidDataException("Invalid PCM16 audio dimensions");
                var samples = new float[checked((int)(stream.Length / 2))];
                // Decode interleaved little-endian PCM16; this is binary decoding, not a collection query.
                for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f;
                _clip = AudioClip.Create(data.Value<string>("clip"), samples.Length / channels, channels, frequency, false);
                if (!_clip.SetData(samples, 0)) throw new InvalidDataException("AudioClip rejected door samples");
            }
            foreach (var row in (JArray)data["doors"])
                _emitters.Add(new Emitter
                {
                    Hatch = row.Value<string>("kind") == "hatch",
                    Path = row.Value<string>("doorPath"), DoorPosition = Vector(row["doorPosition"]),
                    Position = Vector(row["position"]), MixerName = row.Value<string>("mixer"),
                    Volume = row.Value<float>("volume"), FadeIn = row.Value<float>("fadeIn"),
                    FadeOut = row.Value<float>("fadeOut"), RollOff = row.Value<int>("rollOff")
                });
            _createdAt = Time.unscaledTime;
            BindDoors();
            Plugin.Log.LogInfo($"[DoorBlizzard] restored {_emitters.Count} retail wind emitters (11 doors + frozen hatch)");
        }

        private void BindDoors()
        {
            var doors = UnityEngine.Object.FindObjectsOfType<Door>(true).AsValueEnumerable()
                .Where(d => d != null && d.gameObject.scene.name.StartsWith("Icebreaker", StringComparison.Ordinal))
                .Select(d => new DoorLocation { Door = d, Path = PathOf(d.transform) }).ToArray();
            foreach (var emitter in _emitters)
            {
                if (emitter.Hatch || emitter.Door != null) continue;
                // Reused sibling names make paths alone ambiguous. Both path and
                // the authored pivot must agree; never claim an unrelated nearby door.
                var matches = doors.AsValueEnumerable().Where(d => d.Path == emitter.Path &&
                    (d.Door.transform.position - emitter.DoorPosition).sqrMagnitude < 0.25f).ToArray();
                if (matches.Length == 1) emitter.Door = matches[0].Door;
            }
            _nextBind = Time.unscaledTime + 2f;
        }

        private void Update()
        {
            try
            {
                bool unbound = _emitters.AsValueEnumerable().Any(e => !e.Hatch && e.Door == null);
                if (unbound && Time.unscaledTime >= _nextBind) BindDoors();
                if (unbound && !_missingLogged && Time.unscaledTime - _createdAt > 10f)
                {
                    _missingLogged = true;
                    Plugin.Log.LogWarning("[DoorBlizzard] unmatched door anchors: " + string.Join(", ",
                        _emitters.AsValueEnumerable().Where(e => !e.Hatch && e.Door == null).Select(e => e.Path).ToArray()));
                }
                var player = Singleton<GameWorld>.Instance?.MainPlayer;
                var audio = MonoBehaviourSingleton<BetterAudio>.Instance;
                foreach (var emitter in _emitters)
                {
                    bool open = emitter.Hatch
                        ? Blowtorch.HatchMeltDriver.Instance != null && Blowtorch.HatchMeltDriver.Instance.HatchOpened
                        : emitter.Door != null && emitter.Door.DoorState == EDoorState.Open;
                    bool nearby = player != null && (player.Position - emitter.Position).sqrMagnitude <=
                        (emitter.RollOff + 2f) * (emitter.RollOff + 2f);
                    if (!Plugin.DoorBlizzardSounds.Value || !nearby || audio == null)
                    {
                        emitter.Release();
                        continue;
                    }
                    emitter.Tick(audio, _clip, open, Time.deltaTime);
                }
            }
            catch (Exception e)
            {
                // A failed pooled source must not throw every frame or keep a stray loop playing.
                Plugin.Log.LogWarning($"[DoorBlizzard] playback stopped: {e.Message}");
                enabled = false;
            }
        }

        private void OnDisable()
        {
            foreach (var emitter in _emitters) emitter.Release();
        }

        private void OnDestroy()
        {
            OnDisable();
            if (_clip != null) Destroy(_clip);
            if (_live == this) _live = null;
        }

        private static Vector3 Vector(JToken value) => new Vector3((float)value[0], (float)value[1], (float)value[2]);
        private static string PathOf(Transform transform)
        {
            string path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
        private struct DoorLocation { internal Door Door; internal string Path; }

        private sealed class Emitter
        {
            internal bool Hatch;
            internal string Path, MixerName;
            internal Door Door;
            internal Vector3 DoorPosition, Position;
            internal float Volume, FadeIn, FadeOut;
            internal int RollOff;
            private BetterSource _source;
            private AudioMixerGroup _mixer;
            private float _gain, _retryAt;

            internal void Tick(BetterAudio audio, AudioClip clip, bool open, float dt)
            {
                if (_source != null && (_source.source1 == null || !_source.source1.isPlaying)) Release();
                if (_source == null)
                {
                    if (!open || Time.unscaledTime < _retryAt) return;
                    _retryAt = Time.unscaledTime + 0.5f;
                    if (_mixer == null && audio.Master != null)
                        _mixer = audio.Master.FindMatchingGroups(MixerName).AsValueEnumerable().FirstOrDefault(g => g.name == MixerName);
                    // Verified against SPT 4.1.5: group 18 = LightOcclusion,
                    // test 4 = ContinuousPropagated. Let native propagation handle walls/portals.
                    if (!audio.TryPlayAtPoint(out _source, Position, clip, BetterAudio.AudioSourceGroupType.LightOcclusion,
                        RollOff, Volume, EOcclusionTest.ContinuousPropagated, _mixer,
                        spatialize: true, oneShot: false, autoReleaseSource: false, enabledHighPassFilter: false)) return;
                    _source.OnReleased += Released;
                    _source.Loop = true;
                    _source.SetPitch(1f);
                    _source.SetBaseVolume(Volume);
                    _gain = 0f;
                }
                float duration = open ? FadeIn : FadeOut;
                _gain = Mathf.MoveTowards(_gain, open ? 1f : 0f, duration <= 0f ? 1f : dt / duration);
                _source.FadeFactor = _gain;
                _source.UpdateSourceVolume();
                if (!open && _gain <= 0f) Release();
            }

            private void Released(BetterSource source)
            {
                source.OnReleased -= Released;
                source.FadeFactor = 1f;
                if (_source == source) { _source = null; _gain = 0f; }
            }

            internal void Release()
            {
                var source = _source;
                _source = null;
                _gain = 0f;
                if (source == null) return;
                source.OnReleased -= Released;
                source.Loop = false;
                source.FadeFactor = 1f;
                source.Release();
            }
        }
    }
}
