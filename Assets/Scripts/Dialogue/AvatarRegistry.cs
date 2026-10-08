using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChatbotAI.Avatar;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// Holds every AvatarProfile the app knows about and which one is active (switched from the app's menu).
    /// Drop new AvatarProfile assets under a Resources/Avatars folder and they are picked up automatically - no code
    /// changes needed to add/remove one. Bots made in the app (CustomBots: New bot / Duplicate) come after them.
    public class AvatarRegistry : MonoBehaviour
    {
        public static AvatarRegistry Instance { get; private set; }

        [Tooltip("Leave empty to auto-load every AvatarProfile under Resources/Avatars.")]
        [SerializeField] List<AvatarProfile> profiles = new List<AvatarProfile>();
        [SerializeField] int activeIndex = 0;

        public IReadOnlyList<AvatarProfile> Profiles => profiles;

        public AvatarProfile Active =>
            profiles.Count == 0 ? null : profiles[Mathf.Clamp(activeIndex, 0, profiles.Count - 1)];

        // Runtime copy -> the asset it was made from (a made bot: an untouched copy of its starting values).
        readonly Dictionary<AvatarProfile, AvatarProfile> originals = new Dictionary<AvatarProfile, AvatarProfile>();
        readonly List<AvatarProfile> assets = new List<AvatarProfile>();
        readonly List<AvatarCharacter> characters = new List<AvatarCharacter>();

        /// The 3D characters the bots can appear as (every character a built-in bot uses).
        public IReadOnlyList<AvatarCharacter> Characters => characters;

        void Awake()
        {
            Instance = this;
            if (profiles.Count == 0)
                profiles = Resources.LoadAll<AvatarProfile>("Avatars").ToList();
            assets.AddRange(profiles.Where(p => p));
            foreach (var p in assets)
                if (p.character && !characters.Contains(p.character)) characters.Add(p.character);
            AvatarSettingsStore.FindCharacter = name => characters.FirstOrDefault(c => c.name == name);

            // The app runs on copies, so settings changed in its menu never edit the assets (in the Editor they
            // would otherwise stick); those changes are kept in AvatarSettingsStore instead.
            profiles = assets.Select(p =>
            {
                var copy = Instantiate(p);
                copy.name = p.name;
                originals[copy] = p;
                AvatarSettingsStore.Load(copy);
                return copy;
            }).ToList();

            foreach (var d in CustomBots.LoadAll())
            {
                if (profiles.Any(p => p.avatarId == d.id)) continue;
                var bot = Build(d);
                originals[bot] = Build(d);
                AvatarSettingsStore.Load(bot);
                profiles.Add(bot);
            }
        }

        /// The asset a runtime profile was copied from.
        public AvatarProfile OriginalOf(AvatarProfile profile) =>
            profile && originals.TryGetValue(profile, out var original) ? original : profile;

        /// Another avatar became active (persona, knowledge, voices and 3D character all follow it).
        public event System.Action<AvatarProfile> OnActiveChanged;

        /// A bot was made or deleted.
        public event System.Action OnProfilesChanged;

        public void SetActive(string avatarId)
        {
            int idx = profiles.FindIndex(p => p.avatarId == avatarId);
            if (idx >= 0) SetActive(idx);
            else Debug.LogWarning($"AvatarRegistry: no avatar with id '{avatarId}'");
        }

        public void SetActive(AvatarProfile profile)
        {
            int idx = profiles.IndexOf(profile);
            if (idx < 0) idx = profiles.FindIndex(p => OriginalOf(p) == profile);
            if (idx >= 0) SetActive(idx);
            else Debug.LogWarning($"AvatarRegistry: {(profile ? profile.name : "null")} isn't one of the registered avatars");
        }

        public void SetActive(int index)
        {
            var before = Active;
            activeIndex = Mathf.Clamp(index, 0, profiles.Count - 1);
            if (Active != before) OnActiveChanged?.Invoke(Active);
        }

        // ---------------- Bots made in the app ----------------

        /// A made bot as a profile: the built-in bot it started from, with its own id, name, look and mode. Its
        /// documents and taught answers are its own (topic names are found in its documents).
        AvatarProfile Build(CustomBots.Definition d)
        {
            var from = assets.FirstOrDefault(a => a.avatarId == d.basedOn) ?? assets.First();
            var p = Instantiate(from);
            p.name = d.id;
            p.avatarId = d.id;
            p.displayName = d.displayName;
            p.mode = d.mode;
            p.character = characters.FirstOrDefault(c => c.name == d.character) ?? from.character;
            if (!string.IsNullOrEmpty(from.displayName)) p.personaPrompt = from.personaPrompt.Replace(from.displayName, d.displayName);
            p.topicNames = new List<string>();
            p.knowledgeDocuments = new List<Object>();
            return p;
        }

        AvatarProfile Add(CustomBots.Definition d)
        {
            CustomBots.Save(d);
            var bot = Build(d);
            originals[bot] = Build(d);
            profiles.Add(bot);
            OnProfilesChanged?.Invoke();
            return bot;
        }

        /// A new bot appearing as `look`: starts from the built-in bot with that look (its voices suit it).
        public AvatarProfile CreateBot(string displayName, AvatarCharacter look, AvatarProfile.ConversationMode mode)
        {
            var from = assets.FirstOrDefault(a => a.character == look && a.mode == mode) ??
                       assets.FirstOrDefault(a => a.character == look) ?? assets.First();
            return Add(new CustomBots.Definition
            {
                id = CustomBots.NewId(displayName), displayName = displayName, basedOn = from.avatarId,
                character = look ? look.name : "", mode = mode, created = System.DateTime.Now.ToString("s"),
            });
        }

        /// A copy of a bot with everything as it is now (voices, personality, settings, look) and its documents.
        public AvatarProfile Duplicate(AvatarProfile source, string displayName)
        {
            var sourceAsset = OriginalOf(source);
            string basedOn = assets.Contains(sourceAsset) ? sourceAsset.avatarId
                           : CustomBots.LoadAll().FirstOrDefault(d => d.id == source.avatarId)?.basedOn ?? assets.First().avatarId;
            var bot = Add(new CustomBots.Definition
            {
                id = CustomBots.NewId(displayName), displayName = displayName, basedOn = basedOn,
                character = source.character ? source.character.name : "", mode = source.mode, created = System.DateTime.Now.ToString("s"),
            });

            // Every setting that differs from where the copy starts is copied over and kept as the copy's own.
            var changed = new List<string>();
            foreach (var f in typeof(AvatarProfile).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.Name == nameof(AvatarProfile.avatarId) || f.Name == nameof(AvatarProfile.displayName)) continue;
                var t = f.FieldType;
                if (!(t == typeof(string) || t == typeof(float) || t == typeof(int) || t == typeof(bool) || t.IsEnum)) continue;
                object value = f.GetValue(source);
                if (f.Name == nameof(AvatarProfile.personaPrompt) && value is string persona && !string.IsNullOrEmpty(source.displayName))
                    value = persona.Replace(source.displayName, displayName);
                if (Equals(value, f.GetValue(bot))) continue;
                f.SetValue(bot, value);
                changed.Add(f.Name);
            }
            if (changed.Count > 0) AvatarSettingsStore.Save(bot, changed.ToArray());

            // Its documents and taught answers.
            if (Directory.Exists(source.KnowledgeFolderPath))
            {
                Directory.CreateDirectory(bot.KnowledgeFolderPath);
                foreach (string file in Directory.GetFiles(source.KnowledgeFolderPath))
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith(".") || name.EndsWith(".meta")) continue;
                    File.Copy(file, Path.Combine(bot.KnowledgeFolderPath, name), overwrite: true);
                }
                // The document search's index too (keyed by file name, size and date - copies keep them), so the copy
                // answers at once instead of reading every document again.
                string index = Path.Combine(source.KnowledgeFolderPath, ".index");
                if (Directory.Exists(index))
                {
                    string target = Path.Combine(bot.KnowledgeFolderPath, ".index");
                    Directory.CreateDirectory(target);
                    foreach (string file in Directory.GetFiles(index))
                        File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
                }
            }
            return bot;
        }

        /// A bot from an export (BotPackage): a new bot with those settings (its documents are copied in by the caller).
        public AvatarProfile Import(string displayName, string basedOn, string character, AvatarProfile.ConversationMode mode,
                                    List<string> settingNames, List<string> settingValues)
        {
            string name = displayName;
            for (int n = 2; profiles.Any(p => p.displayName == name); n++) name = $"{displayName} {n}";
            if (!assets.Any(a => a.avatarId == basedOn)) basedOn = assets.First().avatarId;
            var bot = Add(new CustomBots.Definition
            {
                id = CustomBots.NewId(name), displayName = name, basedOn = basedOn, character = character, mode = mode,
                created = System.DateTime.Now.ToString("s"),
            });
            // Its settings - without the name (kept as above) or the look (in the definition).
            var names = new List<string>();
            var values = new List<string>();
            for (int i = 0; i < settingNames.Count && i < settingValues.Count; i++)
            {
                if (settingNames[i] == nameof(AvatarProfile.displayName) || settingNames[i] == nameof(AvatarProfile.character)) continue;
                names.Add(settingNames[i]);
                values.Add(settingNames[i] == nameof(AvatarProfile.personaPrompt) ? settingValues[i].Replace(displayName, name) : settingValues[i]);
            }
            AvatarSettingsStore.Apply(bot, names, values);
            return bot;
        }

        /// Deletes a bot made in the app (the built-in ones can't be). Its file and documents go to the Recycle Bin.
        public bool Delete(AvatarProfile bot)
        {
            if (!CustomBots.IsCustom(bot)) return false;
            if (Active == bot) SetActive(0);
            var activeNow = Active;
            CustomBots.Delete(bot);
            profiles.Remove(bot);
            originals.Remove(bot);
            activeIndex = Mathf.Max(0, profiles.IndexOf(activeNow));
            OnProfilesChanged?.Invoke();
            return true;
        }
    }
}
