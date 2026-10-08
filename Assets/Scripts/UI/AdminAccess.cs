using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChatbotAI.UI
{
    /// Who may open the settings menu. The menu button asks for a username and password first, so visitors at a
    /// demo can't change anything. Users are set here in the Inspector (default admin / admin) until staff accounts
    /// are set in the app (menu > Advanced > Staff sign-in, StaffAccounts - hashed, these then no longer apply). After signing in, the menu can be reopened without asking for Stay Signed In minutes after it was
    /// last closed; "Sign out" in the menu ends that at once.
    public class AdminAccess : MonoBehaviour
    {
        [Serializable]
        public class User
        {
            public string username = "admin";
            [Tooltip("Kept as plain text in the scene - use a password only for this app.")]
            public string password = "admin";
        }

        [Tooltip("Ask for a username and password before the settings menu opens. Off = anyone can open it.")]
        [SerializeField] bool requireSignIn = true;
        [SerializeField] List<User> users = new List<User> { new User() };
        [Tooltip("After the menu is closed, reopening it within this many minutes doesn't ask again. 0 = ask every time.")]
        [SerializeField, Range(0f, 60f)] float staySignedInMinutes = 2f;
        [Tooltip("Wrong passwords in a row before sign-in is paused.")]
        [SerializeField] int maxAttempts = 5;
        [SerializeField] float lockoutSeconds = 30f;

        string signedInAs;
        float signedInUntil = -1f;
        int failures;
        float lockedUntil = -1f;

        public bool RequiresSignIn => requireSignIn && (users.Count > 0 || StaffAccounts.Any);

        /// The Inspector's users, as the starting staff list (StaffAccounts copies them when the menu first changes it).
        public IReadOnlyList<User> InspectorUsers => users;
        public string SignedInAs => signedInAs;

        /// The menu may open without asking.
        public bool IsSignedIn => !RequiresSignIn || (signedInAs != null && (menuOpen || Time.realtimeSinceStartup < signedInUntil));

        public float LockedForSeconds => Mathf.Max(0f, lockedUntil - Time.realtimeSinceStartup);

        bool menuOpen;

        /// True if the details match a user. Case-insensitive username, exact password. Staff accounts set in the app's
        /// menu (StaffAccounts) replace the Inspector's users once there are any.
        public bool TrySignIn(string username, string password)
        {
            if (LockedForSeconds > 0f) return false;
            if (StaffAccounts.Any)
            {
                if (StaffAccounts.Check(username, password, out string name))
                {
                    signedInAs = name;
                    failures = 0;
                    return true;
                }
            }
            else
            {
                foreach (var u in users)
                    if (u != null && !string.IsNullOrEmpty(u.username) &&
                        string.Equals(u.username.Trim(), (username ?? "").Trim(), StringComparison.OrdinalIgnoreCase) &&
                        u.password == password)
                    {
                        signedInAs = u.username.Trim();
                        failures = 0;
                        return true;
                    }
            }
            if (++failures >= maxAttempts)
            {
                failures = 0;
                lockedUntil = Time.realtimeSinceStartup + lockoutSeconds;
            }
            return false;
        }

        /// The menu opened / closed (the sign-in lasts while it's open, then Stay Signed In minutes).
        public void MenuOpened() => menuOpen = true;

        public void MenuClosed()
        {
            menuOpen = false;
            signedInUntil = Time.realtimeSinceStartup + staySignedInMinutes * 60f;
        }

        public void SignOut()
        {
            signedInAs = null;
            signedInUntil = -1f;
        }
    }
}
