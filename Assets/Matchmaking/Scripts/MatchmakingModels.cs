using System;

namespace Sid2026.Matchmaking
{
    [Serializable]
    public sealed class UserProfileData
    {
        public string uid;
        public string displayName;
        public string email;
        public long createdAt;
    }

    [Serializable]
    public sealed class PresenceData
    {
        public string uid;
        public string displayName;
        public string state;
        public long lastChanged;

        public bool IsOnline => string.Equals(state, "online", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public sealed class FriendRequestData
    {
        public string senderUid;
        public string senderName;
        public long sentAt;
    }

    [Serializable]
    public sealed class FriendData
    {
        public string uid;
        public string displayName;
        public long friendsSince;
        public bool isOnline;
    }

    [Serializable]
    public sealed class QueuePlayerData
    {
        public string uid;
        public string displayName;
        public long joinedAt;
    }

    [Serializable]
    public sealed class MatchData
    {
        public string matchId;
        public string player1Uid;
        public string player1Name;
        public string player2Uid;
        public string player2Name;
        public string status;
        public long createdAt;

        public string OpponentUid(string currentUid)
        {
            return currentUid == player1Uid ? player2Uid : player1Uid;
        }

        public string OpponentName(string currentUid)
        {
            return currentUid == player1Uid ? player2Name : player1Name;
        }
    }
}
