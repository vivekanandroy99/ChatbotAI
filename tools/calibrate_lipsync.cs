// Play mode: teaches uLipSync voices' sounds. For each voice, plays each sustained-sound sample
// (Assets/Avatar LipSync/Calibration/<voice>_<sound>.wav, made by tools/make_lipsync_calibration.py)
// through a uLipSync and records its MFCCs on voiced frames into the voice's own profile
// (uLipSync-Profile-<voice>.asset: a copy of the sample female profile, silence kept). Sounds: the
// vowels A I U E O, plus consonant groups
//   M = m/b/p (lips pressed), F = f/v (lip to teeth), S = s/sh/ch (teeth together)
// that the mouth has to show for speech to look real. Then scores it: how often each sample is
// recognised as the right sound, against the Kavya profile on the same samples. Which profile a
// voice uses at runtime: LipSyncVoiceProfiles on SpeechOutput/LipSync.
string[] voices = { "indictts_male" };
const string folder = "Assets/Avatar LipSync";
const string sampleProfilePath = "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample-Female.asset";
const string comparePath = "Assets/Avatar LipSync/uLipSync-Profile-veena_kavya.asset";
string[] sounds = { "A", "I", "U", "E", "O", "M", "F", "S" };
string[] consonants = { "M", "F", "S" };

UnityEditor.AssetDatabase.Refresh();
var sample = UnityEditor.AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(sampleProfilePath);
var compare = UnityEditor.AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(comparePath);

var go = new UnityEngine.GameObject("LipSyncCalibrator");
var source = go.AddComponent<UnityEngine.AudioSource>();
source.loop = true;
source.volume = 0.3f;
var lipSync = go.AddComponent<uLipSync.uLipSync>();

// Consonants are quieter than vowels, so they get a lower bar for "this frame is the sound".
bool Voiced(string s) => lipSync.result.rawVolume > 0 &&
    UnityEngine.Mathf.Log10(lipSync.result.rawVolume) > (System.Array.IndexOf(consonants, s) >= 0 ? -2.4f : -2.0f);

uLipSync.Profile MakeProfile(string voice)
{
    string profilePath = $"{folder}/uLipSync-Profile-{voice}.asset";
    var calibrated = UnityEngine.Object.Instantiate(sample);
    calibrated.mfccDataCount = 48;  // average over more frames than the default 16
    // Standardized coefficients + cosine matching scored best for Veena Kavya (70% of vowel frames right vs 63% default, 45% uncalibrated).
    calibrated.useStandardization = true;
    calibrated.compareMethod = uLipSync.CompareMethod.CosineSimilarity;
    foreach (string c in consonants)
        if (calibrated.mfccs.FindIndex(m => m.name == c) < 0) calibrated.AddMfcc(c);
    // Overwritten in place when it exists, so scene references to the profile stay valid.
    var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(profilePath);
    if (existing)
    {
        UnityEditor.EditorUtility.CopySerialized(calibrated, existing);
        calibrated = existing;
        calibrated.name = System.IO.Path.GetFileNameWithoutExtension(profilePath);  // CopySerialized copies the "(Clone)" name too
    }
    else UnityEditor.AssetDatabase.CreateAsset(calibrated, profilePath);
    // Copying skips the profile's own setup of its per-sound buffers.
    foreach (var data in calibrated.mfccs)
    {
        data.Allocate();
        data.UpdateNativeArray();
    }
    calibrated.UpdateMeansAndStandardization();
    return calibrated;
}

async System.Threading.Tasks.Task<string> Score(uLipSync.Profile profile, System.Collections.Generic.Dictionary<string, UnityEngine.AudioClip> clips)
{
    lipSync.profile = profile;
    var parts = new System.Collections.Generic.List<string>();
    int totalRight = 0, total = 0;
    foreach (string s in sounds)
    {
        if (profile.mfccs.FindIndex(m => m.name == s) < 0 || !clips[s]) { parts.Add($"{s} -"); continue; }
        source.clip = clips[s];
        source.Play();
        await System.Threading.Tasks.Task.Delay(300);
        int right = 0, n = 0;
        var heard = new System.Collections.Generic.Dictionary<string, int>();
        float end = UnityEngine.Time.realtimeSinceStartup + 3f;
        while (UnityEngine.Time.realtimeSinceStartup < end)
        {
            await System.Threading.Tasks.Task.Delay(20);
            if (!Voiced(s)) continue;
            n++;
            string p = lipSync.result.phoneme;
            heard[p] = heard.TryGetValue(p, out int c) ? c + 1 : 1;
            if (p == s) right++;
        }
        source.Stop();
        totalRight += right;
        total += n;
        string top = string.Join(" ", System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(heard, kv => kv.Value), 2)
            .Select(kv => $"{kv.Key}:{100 * kv.Value / System.Math.Max(n, 1)}%"));
        parts.Add($"{s} {100 * right / System.Math.Max(n, 1)}% (heard {top})");
    }
    return $"{100 * totalRight / System.Math.Max(total, 1)}% overall | " + string.Join(", ", parts);
}

async void Run()
{
    foreach (string voice in voices)
    {
        var clips = new System.Collections.Generic.Dictionary<string, UnityEngine.AudioClip>();
        foreach (string s in sounds)
            clips[s] = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>($"{folder}/Calibration/{voice}_{s}.wav");
        if (compare) UnityEngine.Debug.Log($"CALIB {voice} with Kavya's profile: " + await Score(compare, clips));

        var calibrated = MakeProfile(voice);
        lipSync.profile = calibrated;
        foreach (string s in sounds)
        {
            if (!clips[s]) continue;
            int index = calibrated.mfccs.FindIndex(m => m.name == s);
            source.clip = clips[s];
            source.Play();
            await System.Threading.Tasks.Task.Delay(300);
            int frames = 0;
            float end = UnityEngine.Time.realtimeSinceStartup + 4f;
            while (UnityEngine.Time.realtimeSinceStartup < end)
            {
                await System.Threading.Tasks.Task.Delay(30);
                if (!Voiced(s)) continue;
                lipSync.RequestCalibration(index);
                frames++;
            }
            source.Stop();
            UnityEngine.Debug.Log($"CALIB {voice} learned {s} from {frames} voiced frames");
        }
        calibrated.Save();
        UnityEngine.Debug.Log($"CALIB {voice} with its own profile: " + await Score(calibrated, clips));
    }
    UnityEngine.Object.Destroy(go);
    UnityEngine.Debug.Log("CALIB_DONE");
}
Run();
return null;

