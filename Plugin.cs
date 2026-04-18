using BepInEx;
using Console;
using Photon.Voice.Unity;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FortniteEmoteWheel
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake() =>
            Console.Console.LoadConsole();

        public void Start() =>
            HarmonyPatches.ApplyHarmonyPatches();

        private static AssetBundle assetBundle;
        public static GameObject LoadAsset(string assetName)
        {
            GameObject gameObject = null;

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortniteEmoteWheel.Resources.fn");
            if (stream != null)
            {
                if (assetBundle == null)
                    assetBundle = AssetBundle.LoadFromStream(stream);
                gameObject = Instantiate<GameObject>(assetBundle.LoadAsset<GameObject>(assetName));
            }
            else
                Debug.LogError("Failed to load asset from resource: " + assetName);

            return gameObject;
        }

        public static GameObject audiomgr = null;
        public static void Play2DAudio(AudioClip sound, float volume, bool looping = false)
        {
            if (audiomgr == null)
            {
                audiomgr = new GameObject("2DAudioMgr");
                AudioSource temp = audiomgr.AddComponent<AudioSource>();
                temp.spatialBlend = 0f;
            }
            AudioSource ausrc = audiomgr.GetComponent<AudioSource>();
            ausrc.volume = volume;
            ausrc.loop = looping;
            if (!looping)
                ausrc.PlayOneShot(sound);
            else
            {
                ausrc.clip = sound;
                ausrc.Play();
            }
        }

        public static Dictionary<string, AudioClip> audioPool = new Dictionary<string, AudioClip> { };
        public static AudioClip LoadSoundFromResource(string resourcePath)
        {
            AudioClip sound = null;

            if (!audioPool.ContainsKey(resourcePath))
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortniteEmoteWheel.Resources.fn");
                if (stream != null)
                {
                    if (assetBundle == null)
                        assetBundle = AssetBundle.LoadFromStream(stream);

                    sound = assetBundle.LoadAsset(resourcePath) as AudioClip;
                    audioPool.Add(resourcePath, sound);
                }
                else
                {
                    Debug.LogError("Failed to load sound from resource: " + resourcePath);
                }
            }
            else
                sound = audioPool[resourcePath];

            return sound;
        }

        private static readonly List<GameObject> portedCosmetics = new List<GameObject> { };

        public static void DisableCosmetics()
        {
            try
            {
                var rig = GorillaTagger.Instance.offlineVRRig;
                if (rig == null) return;

                rig.transform.Find("rig/head/gorillaface").gameObject.layer = LayerMask.NameToLayer("Default");
                foreach (GameObject Cosmetic in rig.cosmetics)
                {
                    if (Cosmetic.activeSelf && Cosmetic.transform.parent == rig.mainCamera.transform.Find("HeadCosmetics"))
                    {
                        portedCosmetics.Add(Cosmetic);
                        Cosmetic.transform.SetParent(rig.headMesh.transform, false);
                        Cosmetic.transform.localPosition += new Vector3(0f, 0.1333f, 0.1f);
                    }
                }
            }
            catch { }
        }

        public static void EnableCosmetics()
        {
            var rig = GorillaTagger.Instance.offlineVRRig;
            if (rig == null) return;

            rig.transform.Find("rig/head/gorillaface").gameObject.layer = LayerMask.NameToLayer("MirrorOnly");
            foreach (GameObject Cosmetic in portedCosmetics)
            {
                Cosmetic.transform.SetParent(rig.mainCamera.transform.Find("HeadCosmetics"), false);
                Cosmetic.transform.localPosition -= new Vector3(0f, 0.1333f, 0.1f);
            }
            portedCosmetics.Clear();
        }

        public static GameObject Kyle;
        public static float emoteTime;

        public static Vector3 archivePosition;

        public static void Emote(string emoteName, string emoteSound, float animationTime = -1f, bool looping = false)
        {
            var rig = GorillaTagger.Instance.offlineVRRig;
            if (rig == null) return;

            if (Kyle != null)
                Object.Destroy(Kyle);

            rig.enabled = false;
            DisableCosmetics();

            Play2DAudio(LoadSoundFromResource("play"), 0.5f);

            archivePosition = GorillaTagger.Instance.transform.position;
            GorillaLocomotion.GTPlayer.Instance.GetControllerTransform(false).parent.rotation *= Quaternion.Euler(0f, 180f, 0f);

            Kyle = LoadAsset("Rig");
            Kyle.transform.position = rig.transform.Find("rig/body_pivot").position - new Vector3(0f, 1.15f, 0f);
            Kyle.transform.rotation = rig.transform.Find("rig/body_pivot").rotation;

            Kyle.transform.Find("KyleRobot/RobotKile").gameObject.GetComponent<Renderer>().renderingLayerMask = 0;

            Animator KyleRobot = Kyle.transform.Find("KyleRobot").GetComponent<Animator>();
            KyleRobot.enabled = true;

            AnimationClip Animation = null;
            foreach (AnimationClip Clip in KyleRobot.runtimeAnimatorController.animationClips)
            {
                if (Clip.name == emoteName)
                {
                    Animation = Clip;
                    break;
                }
            }

            if (Animation == null)
            {
                Debug.LogError($"Emote animation '{emoteName}' not found.");
                return;
            }

            Animation.wrapMode = looping ? WrapMode.Loop : WrapMode.Default;
            KyleRobot.Play(Animation.name);

            AudioClip Sound = LoadSoundFromResource(emoteSound);
            Play2DAudio(Sound, 0.5f, looping);

            var rec = GorillaTagger.Instance.myRecorder?.photonRecorder as Recorder;
            if (rec != null)
            {
                rec.SourceType = Recorder.InputSourceType.AudioClip;
                rec.AudioClip = Sound;
                rec.RestartRecording(true);
            }

            emoteTime = Time.time + (animationTime > 0f ? animationTime : Animation.length) + (looping ? 999999999999999f : 0);
        }

        public static Vector3 World2Player(Vector3 world)
        {
            var tagger = GorillaTagger.Instance;
            return world - tagger.playerCollider.transform.position + tagger.transform.position;
        }

        public void Update()
        {
            if (GorillaLocomotion.GTPlayer.Instance == null)
                return;

            var rig = GorillaTagger.Instance.offlineVRRig;
            if (rig == null)
                return;

            if (Classes.Wheel.instance == null)
            {
                GameObject Wheel = Plugin.LoadAsset("Wheel");
                Wheel.transform.SetParent(rig.transform.Find("rig/hand.R"), false);
                Wheel.AddComponent<Classes.Wheel>();
            }

            if (Time.time < emoteTime)
            {
                if (Kyle != null)
                {
                    rig.enabled = false;

                    GorillaTagger.Instance.transform.position =
                        World2Player(Kyle.transform.position + (Kyle.transform.forward * 1.5f) + new Vector3(0f, 1.15f, 0f)) +
                        new Vector3(0f, 0.5f, 0f);

                    rig.leftHandTransform.position = GorillaTagger.Instance.playerCollider.transform.position;
                    rig.rightHandTransform.position = GorillaTagger.Instance.playerCollider.transform.position;

                    GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;

                    Transform spine = Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2");
                    rig.transform.position = spine.position - (spine.right / 2.5f);
                    rig.transform.rotation = Quaternion.Euler(new Vector3(0f, spine.rotation.eulerAngles.y, 0f));

                    rig.leftHand.rigTarget.transform.position =
                        Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/LeftShoulder/LeftUpperArm/LeftArm/LeftHand").position;
                    rig.rightHand.rigTarget.transform.position =
                        Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/RightShoulder/RightUpperArm/RightArm/RightHand").position;

                    rig.leftHand.rigTarget.transform.rotation =
                        Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/LeftShoulder/LeftUpperArm/LeftArm/LeftHand").rotation *
                        Quaternion.Euler(0, 0, 75);
                    rig.rightHand.rigTarget.transform.rotation =
                        Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/RightShoulder/RightUpperArm/RightArm/RightHand").rotation *
                        Quaternion.Euler(180, 0, -75);

                    rig.head.rigTarget.transform.rotation =
                        Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1/Spine2/Neck/Head").rotation *
                        Quaternion.Euler(0f, 0f, 90f);
                }
            }
            else
            {
                if (Kyle != null)
                {
                    rig.enabled = true;
                    EnableCosmetics();

                    Object.Destroy(Kyle);

                    var rec = GorillaTagger.Instance.myRecorder?.photonRecorder as Recorder;
                    if (rec != null)
                    {
                        rec.SourceType = Recorder.InputSourceType.Microphone;
                        rec.AudioClip = null;
                        rec.RestartRecording(true);
                    }

                    GorillaTagger.Instance.transform.position = archivePosition;
                    GorillaLocomotion.GTPlayer.Instance.GetControllerTransform(false).parent.rotation *= Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }
    }
}
