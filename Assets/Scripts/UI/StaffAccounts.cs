using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace ChatbotAI.UI
{
    /// Staff sign-ins managed in the app (menu > Advanced > Staff sign-in): add people, change passwords, remove them.
    /// Passwords are never stored - only a salted PBKDF2 hash (PlayerPrefs "staff/accounts"). Until the menu changes
    /// anything, AdminAccess's Inspector users apply (default admin / admin); the first change copies them in here.
    /// The list travels with builds (DefaultSettings carries "staff/"), so a password set before building is the kiosk's.
    public static class StaffAccounts
    {
        const string Key = "staff/accounts";
        const int Iterations = 20000;

        [Serializable]
        public class Account
        {
            public string username, salt, hash;
        }

        [Serializable]
        class Store
        {
            public List<Account> accounts = new List<Account>();
        }

        static Store Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            if (json.Length == 0) return new Store();
            try { return JsonUtility.FromJson<Store>(json) ?? new Store(); }
            catch (ArgumentException) { return new Store(); }
        }

        static void Save(Store s)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(s));
            PlayerPrefs.Save();
        }

        /// The app manages the accounts (else the Inspector's users apply).
        public static bool Any => Load().accounts.Count > 0;

        public static List<string> Usernames => Load().accounts.Select(a => a.username).ToList();

        public static bool Check(string username, string password, out string name)
        {
            name = null;
            var a = Load().accounts.FirstOrDefault(x => string.Equals(x.username, (username ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            if (a == null || !SlowEquals(Hash(password ?? "", Convert.FromBase64String(a.salt)), Convert.FromBase64String(a.hash))) return false;
            name = a.username;
            return true;
        }

        /// First change from the menu: the Inspector's users become the app's list (hashed).
        public static void TakeOver(IEnumerable<AdminAccess.User> inspectorUsers)
        {
            if (Any) return;
            var s = new Store();
            foreach (var u in inspectorUsers)
                if (u != null && !string.IsNullOrWhiteSpace(u.username)) s.accounts.Add(Make(u.username.Trim(), u.password ?? ""));
            Save(s);
        }

        /// Adds a person, or changes their password. False with a reason if it can't.
        public static bool Set(string username, string password, out string problem)
        {
            username = (username ?? "").Trim();
            problem = username.Length == 0 ? "Type a name."
                    : (password ?? "").Length < 4 ? "Use at least 4 characters for the password."
                    : null;
            if (problem != null) return false;
            var s = Load();
            s.accounts.RemoveAll(a => string.Equals(a.username, username, StringComparison.OrdinalIgnoreCase));
            s.accounts.Add(Make(username, password));
            s.accounts.Sort((x, y) => string.Compare(x.username, y.username, StringComparison.OrdinalIgnoreCase));
            Save(s);
            return true;
        }

        /// Removes a person (never the last one - someone must be able to sign in).
        public static bool Remove(string username)
        {
            var s = Load();
            if (s.accounts.Count <= 1) return false;
            int removed = s.accounts.RemoveAll(a => string.Equals(a.username, username, StringComparison.OrdinalIgnoreCase));
            Save(s);
            return removed > 0;
        }

        static Account Make(string username, string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            return new Account { username = username, salt = Convert.ToBase64String(salt), hash = Convert.ToBase64String(Hash(password, salt)) };
        }

        static byte[] Hash(string password, byte[] salt)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations)) return kdf.GetBytes(32);
        }

        static bool SlowEquals(byte[] a, byte[] b)
        {
            int diff = a.Length ^ b.Length;
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
