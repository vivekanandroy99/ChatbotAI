using System;
using UnityEngine;

namespace ChatbotAI.Avatar
{
    /// How the camera frames a character, as set in the app's menu (Camera): distance, where it aims, a sideways
    /// shift, turn around the character and tilt. The character asset's framing is the starting point; changes are
    /// kept per character and per screen shape (wide / tall) in PlayerPrefs, so the asset is never edited.
    [Serializable]
    public struct CameraFraming
    {
        [Tooltip("Metres from the aim point to the camera.")] public float distance;
        [Tooltip("How far below the eyes the camera aims (higher = the face sits higher on screen).")] public float aimBelowEyes;
        [Tooltip("Moves the character sideways on screen, metres (positive = to the right).")] public float side;
        [Tooltip("Degrees the camera goes round the character (positive = towards the character's left).")] public float turn;
        [Tooltip("Degrees the camera looks down on the character (negative = looks up).")] public float tilt;
        [Tooltip("Lens: the camera's vertical field of view in degrees (small = telephoto, flatter; large = wide). 0 = the camera's own.")]
        public float fieldOfView;
        [Tooltip("Gently re-centre on the face as the character moves, instead of a fixed camera.")]
        public bool follow;

        /// A ready-made camera shot (menu > Camera > Shots): distance and height for tall and wide screens (tall screens
        /// stand further back - the panel covers the bottom), plus angle, tilt and lens.
        public readonly struct Shot
        {
            public readonly string name, description;
            public readonly float tallDistance, tallAim, wideDistance, wideAim, side, turn, tilt, lens;
            public readonly bool follow;

            public Shot(string name, string description, float tallDistance, float tallAim, float wideDistance, float wideAim,
                        float turn, float tilt, float side = 0f, float lens = 0f, bool follow = false)
            {
                this.name = name;
                this.description = description;
                this.tallDistance = tallDistance;
                this.tallAim = tallAim;
                this.wideDistance = wideDistance;
                this.wideAim = wideAim;
                this.turn = turn;
                this.tilt = tilt;
                this.side = side;
                this.lens = lens;
                this.follow = follow;
            }

            public CameraFraming For(bool tall) => new CameraFraming
            {
                distance = tall ? tallDistance : wideDistance,
                aimBelowEyes = tall ? tallAim : wideAim,
                side = side, turn = turn, tilt = tilt, fieldOfView = lens, follow = follow,
            };
        }

        /// Lens 0 = the camera's own (30 degrees). "Relaxed" is also the characters' default angle (AvatarCharacter).
        public static readonly Shot[] Shots =
        {
            new Shot("Relaxed", "Waist up, turned a little, camera slightly above the eyes", 3.3f, 0.5f, 2.2f, 0.25f, 10f, 4f),
            new Shot("Straight on", "Waist up, square to the camera", 3.4f, 0.55f, 2.2f, 0.27f, 0f, 0f),
            new Shot("Chest up", "A medium close-up - face and hands both read", 2.8f, 0.4f, 1.8f, 0.18f, 10f, 3f),
            new Shot("Close-up", "Head and shoulders - the face does the talking", 2.3f, 0.25f, 1.5f, 0.1f, 6f, 3f),
            new Shot("Close-up, angled", "Head and shoulders, turned - a portrait look", 2.3f, 0.25f, 1.5f, 0.1f, 25f, 3f, 0.03f),
            new Shot("Close-up, low", "Head and shoulders from just below - warm and confident", 2.3f, 0.2f, 1.5f, 0.08f, 5f, -6f),
            new Shot("Close-up, cinematic", "Long lens on the face, soft background; follows gently", 3.6f, 0.25f, 2.4f, 0.1f, 12f, 2f, 0f, 18f, true),
            new Shot("Face", "An extreme close-up - just the face", 1.4f, 0.05f, 0.95f, 0f, 8f, 2f),
            new Shot("Three-quarter", "Turned to one side, like a magazine portrait", 3.4f, 0.5f, 2.2f, 0.25f, 28f, 4f, 0.06f),
            new Shot("Low angle", "Just below eye level - confident and approachable", 3.4f, 0.45f, 2.2f, 0.22f, 6f, -7f),
            new Shot("Across the desk", "From a little above, like a visitor at a reception desk", 3.5f, 0.5f, 2.3f, 0.25f, 0f, 12f),
            new Shot("Cinematic", "Long lens, flatter and softer; follows the character gently", 5.0f, 0.5f, 3.3f, 0.25f, 14f, 2f, 0f, 20f, true),
            new Shot("Full body", "Head to toe, standing on the stage", 5.6f, 0.9f, 4.0f, 0.75f, 8f, 4f),
        };

        /// The shot this framing matches (all values within a hair), or -1 once it's been fine-tuned.
        public static int MatchingShot(CameraFraming f, bool tall)
        {
            for (int i = 0; i < Shots.Length; i++)
            {
                var s = Shots[i].For(tall);
                if (Mathf.Abs(s.distance - f.distance) < 0.02f && Mathf.Abs(s.aimBelowEyes - f.aimBelowEyes) < 0.01f &&
                    Mathf.Abs(s.side - f.side) < 0.01f && Mathf.Abs(s.turn - f.turn) < 0.5f && Mathf.Abs(s.tilt - f.tilt) < 0.5f &&
                    Mathf.Abs(s.fieldOfView - f.fieldOfView) < 0.5f && s.follow == f.follow)
                    return i;
            }
            return -1;
        }

        /// Framings to start from (older menu): tall screens stand further back - the panel covers the bottom.
        public static readonly string[] PresetNames = { "Face", "Shoulders", "Waist up", "Full body" };

        public static CameraFraming Preset(int index, bool tall, CameraFraming current)
        {
            var (distance, aim) = index switch
            {
                0 => tall ? (1.5f, 0.1f) : (1.0f, 0.02f),
                1 => tall ? (2.3f, 0.3f) : (1.5f, 0.12f),
                3 => tall ? (5.5f, 0.95f) : (4.0f, 0.75f),
                _ => tall ? (3.4f, 0.65f) : (2.2f, 0.3f),
            };
            current.distance = distance;
            current.aimBelowEyes = aim;
            return current;
        }

        public static CameraFraming Defaults(AvatarCharacter c, bool tall) => new CameraFraming
        {
            distance = tall ? c.portraitCameraDistance : c.cameraDistance,
            aimBelowEyes = tall ? c.portraitAimBelowEyes : c.aimBelowEyes,
            turn = c.cameraAngle,
            tilt = c.cameraTilt,
        };

        static string Key(AvatarCharacter c, bool tall) => $"camera/{c.name}/{(tall ? "tall" : "wide")}";

        /// The saved framing for this character and screen shape, else the character's own.
        public static CameraFraming Load(AvatarCharacter c, bool tall)
        {
            string json = PlayerPrefs.GetString(Key(c, tall), "");
            if (json.Length > 0)
                try { return JsonUtility.FromJson<CameraFraming>(json); }
                catch (ArgumentException) { }
            return Defaults(c, tall);
        }

        public static bool IsSaved(AvatarCharacter c, bool tall) => PlayerPrefs.HasKey(Key(c, tall));

        public static void Save(AvatarCharacter c, bool tall, CameraFraming f)
        {
            PlayerPrefs.SetString(Key(c, tall), JsonUtility.ToJson(f));
            PlayerPrefs.Save();
        }

        public static void Forget(AvatarCharacter c, bool tall)
        {
            PlayerPrefs.DeleteKey(Key(c, tall));
            PlayerPrefs.Save();
        }
    }
}
