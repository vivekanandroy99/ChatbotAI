using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ChatbotAI.Dialogue
{
    /// Searches an avatar's document knowledge base via the local sidecar's
    /// /retrieve endpoint (TTSServer/knowledge.py, localhost only).
    public static class KnowledgeClient
    {
        [Serializable]
        public class Passage
        {
            public string text;
            public string source;
            public float score;
        }

        [Serializable]
        public class Result
        {
            public float best_score;
            // Best single sentence - runs higher than passage scores for everything, so
            // it only backs up borderline questions.
            public float best_sentence_score;
            public Passage[] chunks;
        }

        [Serializable]
        class Request
        {
            public string avatar;
            public string text;
            public int top_k;
            public bool rerank;  // reorder the closest passages with the reranker (if the server has it loaded)
            public string rank_text;  // the question as searched for ranking only (AvatarProfile.SearchText); gate scores use text
        }

        [Serializable]
        class VocabularyResult
        {
            public string[] words;
        }

        [Serializable]
        class RestoreEnglishRequest
        {
            public string english;
            public string hinglish;
            public bool names_only;
        }

        [Serializable]
        class TextResult
        {
            public string text;
        }

        /// Swaps English words the translator wrote in Devanagari (अल्टकोर, सेल्स टीम)
        /// back to their English spelling from the source answer. Returns the input
        /// unchanged if the sidecar isn't reachable.
        /// namesOnly: only names (capitalised words not starting a sentence) may come back - for
        /// conversation, where everyday English words collide with Hindi verbs (कहूँगा -> "cooking").
        public static async Task<string> RestoreEnglish(string baseUrl, string english, string hinglish, bool namesOnly = false)
        {
            string json = JsonUtility.ToJson(new RestoreEnglishRequest { english = english, hinglish = hinglish, names_only = namesOnly });
            using var req = new UnityWebRequest(baseUrl + "/restore_english", "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            if (req.result != UnityWebRequest.Result.Success) return hinglish;
            return JsonUtility.FromJson<TextResult>(req.downloadHandler.text)?.text ?? hinglish;
        }

        /// Distinctive names/terms from the avatar's documents. Null if the sidecar isn't reachable.
        public static Task<string[]> GetVocabulary(string baseUrl, string knowledgeFolder) =>
            GetWords($"{baseUrl}/vocabulary?avatar={UnityWebRequest.EscapeURL(knowledgeFolder)}");

        /// The brands/products the documents are about (e.g. Altscape, Altcore). Null if the sidecar isn't reachable.
        public static Task<string[]> GetTopics(string baseUrl, string knowledgeFolder) =>
            GetWords($"{baseUrl}/topics?avatar={UnityWebRequest.EscapeURL(knowledgeFolder)}");

        static async Task<string[]> GetWords(string url)
        {
            using var req = UnityWebRequest.Get(url);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            if (req.result != UnityWebRequest.Result.Success) return null;
            return JsonUtility.FromJson<VocabularyResult>(req.downloadHandler.text)?.words;
        }

        /// Returns null if the sidecar isn't reachable (e.g. still starting up).
        public static async Task<Result> Retrieve(string baseUrl, string knowledgeFolder, string text, int topK, bool rerank = true,
                                                string rankText = null)
        {
            string json = JsonUtility.ToJson(new Request { avatar = knowledgeFolder, text = text, top_k = topK, rerank = rerank, rank_text = rankText ?? "" });

            using var req = new UnityWebRequest(baseUrl + "/retrieve", "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"KnowledgeClient: retrieve failed - {req.error} {req.downloadHandler?.text}");
                return null;
            }
            return JsonUtility.FromJson<Result>(req.downloadHandler.text);
        }
    }
}
