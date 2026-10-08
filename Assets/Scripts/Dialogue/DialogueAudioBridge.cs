using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using ChatbotAI.Audio;

namespace ChatbotAI.Dialogue
{
    /// Wires the dialogue brain's text reply to the voice. The voice is picked
    /// from the reply's own script (Devanagari -> the avatar's Hindi voice,
    /// otherwise its English voice), since the LLM already replies in whichever
    /// language the question was asked in.
    ///
    /// Hindi replies arrive a sentence at a time as they're translated; speaking
    /// starts with the first one instead of waiting for the whole reply.
    public class DialogueAudioBridge : MonoBehaviour
    {
        [SerializeField] DialogueController dialogueController;
        [SerializeField] SpeechOutputController speechOutput;

        static readonly Regex DevanagariPattern = new Regex(@"\p{IsDevanagari}");

        bool streaming, streamingHindi;

        void OnEnable()
        {
            dialogueController.OnReply += HandleReply;
            dialogueController.OnReplySentence += HandleSentence;
        }

        System.Collections.IEnumerator Start()
        {
            // Optional engines (Veena, Kokoro v1.1...) load on first use; start that as soon as the voice server is up.
            yield return new WaitUntil(() => speechOutput.EngineReady);
            WarmVoices(dialogueController.ActiveProfile);
            if (AvatarRegistry.Instance) AvatarRegistry.Instance.OnActiveChanged += HandleAvatarChanged;
        }

        void OnDestroy()
        {
            if (AvatarRegistry.Instance) AvatarRegistry.Instance.OnActiveChanged -= HandleAvatarChanged;
        }

        // Another avatar: stop the old one mid-sentence, get the new one's voices loading.
        void HandleAvatarChanged(AvatarProfile profile)
        {
            speechOutput.Stop();
            streaming = false;
            WarmVoices(profile);
        }

        void WarmVoices(AvatarProfile profile)
        {
            if (profile == null) return;
            foreach (string id in new[] { profile.hindiVoice, profile.englishVoice }.Distinct())
                if (VoiceCatalog.TryGet(id, out var voice) && voice.engine != null)
                    speechOutput.WarmVoice(id);
        }

        void OnDisable()
        {
            dialogueController.OnReply -= HandleReply;
            dialogueController.OnReplySentence -= HandleSentence;
        }

        void HandleSentence(string sentence, bool isLast)
        {
            var profile = dialogueController.ActiveProfile;
            if (profile == null) return;
            if (!streaming)
            {
                streaming = true;
                streamingHindi = DevanagariPattern.IsMatch(sentence);
                string voice = streamingHindi ? profile.hindiVoice : profile.englishVoice;
                speechOutput.Speak(Spoken(profile, sentence, streamingHindi), voice, profile.speechSpeed, moreToCome: !isLast);
            }
            else speechOutput.Append(Spoken(profile, sentence, streamingHindi), moreToCome: !isLast);
        }

        void HandleReply(string text)
        {
            // Already spoken sentence by sentence.
            if (streaming)
            {
                streaming = false;
                return;
            }
            var profile = dialogueController.ActiveProfile;
            if (profile == null) return;
            bool hindi = DevanagariPattern.IsMatch(text);
            speechOutput.Speak(Spoken(profile, text, hindi), hindi ? profile.hindiVoice : profile.englishVoice, profile.speechSpeed);
        }

        /// The text with the avatar's pronunciation fixes applied (logged when one kicks in).
        static string Spoken(AvatarProfile profile, string text, bool hindi)
        {
            string spoken = profile.ForSpeech(text, hindi);
            if (spoken != text) Debug.Log($"Pronunciation fixes applied - speaking: \"{spoken}\"");
            return spoken;
        }
    }
}
