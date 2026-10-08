using System.Collections.Generic;
using UnityEngine;

namespace ChatbotAI.Dialogue
{
    /// Written replies for greetings, thanks, goodbyes and "who are you". The LLM
    /// only decides which kind of small talk it is: left to write the reply itself,
    /// it answered "what is Altcore?" as small talk and invented a fact. Hindi lines
    /// avoid gendered first-person verbs, so they suit any avatar voice.
    public static class SmallTalk
    {
        public static readonly string[] Kinds = { "GREETING", "HOW_ARE_YOU", "THANKS", "GOODBYE", "WHO_ARE_YOU", "COMPLIMENT" };

        static readonly Dictionary<string, (string[] english, string[] hindi)> Lines = new Dictionary<string, (string[], string[])>
        {
            ["GREETING"] = (new[]
            {
                "Hello, and welcome! I'm {name}. Ask me anything about {topics}.",
                "Hi there! I'm {name} - what would you like to know about {topics}?",
                "Hey, great to have you here! I'm {name}, your guide to {topics}.",
            }, new[]
            {
                "नमस्ते, आपका स्वागत है! मैं {name} हूँ। {topics} के बारे में कुछ भी पूछिए।",
                "Hello! मैं {name} हूँ - बताइए, {topics} के बारे में क्या जानना है?",
            }),
            ["HOW_ARE_YOU"] = (new[]
            {
                "I'm doing great, thanks for asking! What would you like to know about {topics}?",
                "All good here, thank you! How can I help you with {topics} today?",
            }, new[]
            {
                "मैं बढ़िया हूँ, पूछने के लिए thanks! बताइए, {topics} के बारे में क्या जानना है?",
                "सब बढ़िया है, thank you! आज {topics} के बारे में क्या जानना चाहेंगे?",
            }),
            ["THANKS"] = (new[]
            {
                "You're very welcome! Anything else you'd like to know?",
                "Happy to help! Is there anything else about {topics} I can tell you?",
                "My pleasure! Ask away if anything else comes to mind.",
            }, new[]
            {
                "अरे, कोई बात नहीं! और कुछ जानना हो तो ज़रूर पूछिए।",
                "Welcome! {topics} के बारे में और कुछ पूछना हो तो बताइए।",
            }),
            ["GOODBYE"] = (new[]
            {
                "Thanks for stopping by! Have a wonderful day.",
                "Goodbye, and take care! Come back anytime you have questions about {topics}.",
            }, new[]
            {
                "आने के लिए thanks! आपका दिन अच्छा रहे।",
                "Bye, अपना ध्यान रखिए! {topics} के बारे में कभी भी पूछने आइए।",
            }),
            ["WHO_ARE_YOU"] = (new[]
            {
                "I'm {name}, your guide to {topics}. Ask me what they do, what they offer, or how to get in touch.",
                "I'm {name}! I'm here to answer your questions about {topics} - their products, services and more.",
            }, new[]
            {
                "मैं {name} हूँ, और {topics} से जुड़े आपके हर सवाल का जवाब देना मेरा काम है।",
                "मैं {name} हूँ! {topics} के products, services या contact details के बारे में कुछ भी पूछिए।",
            }),
            ["COMPLIMENT"] = (new[]
            {
                "Aw, thank you - that's very kind! What else would you like to know?",
                "You're too kind! Anything else I can help with?",
            }, new[]
            {
                "अरे, thank you! आपने तो दिल खुश कर दिया। और कुछ जानना है?",
                "बहुत-बहुत thanks! बताइए, और क्या जानना है?",
            }),
        };

        public static string Pick(string kind, AvatarProfile profile, bool hindi)
        {
            if (!Lines.TryGetValue(kind, out var lines)) lines = Lines["GREETING"];
            var pool = hindi ? lines.hindi : lines.english;
            string name = string.IsNullOrWhiteSpace(profile.displayName) ? "your assistant" : profile.displayName.Trim();
            return pool[Random.Range(0, pool.Length)]
                .Replace("{name}", name)
                .Replace("{topics}", profile.TopicsPhrase(hindi));
        }
    }
}
