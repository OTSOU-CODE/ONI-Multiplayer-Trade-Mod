using ONI_MP.DebugTools;
using Steamworks;
using System;
using System.Collections.Generic;

namespace ONI_MP.Networking
{
    public static class SteamLobby
    {
        public static readonly int LOBBY_SIZE_MAX = 16;
        public static readonly int LOBBY_SIZE_MIN = 2;
        public static readonly int LOBBY_SIZE_DEFAULT = 4;
        private static Callback<LobbyCreated_t> _lobbyCreated;
        private static Callback<GameLobbyJoinRequested_t> _lobbyJoinRequested;
        private static Callback<LobbyEnter_t> _lobbyEntered;
        private static Callback<LobbyChatUpdate_t> _lobbyChatUpdate;
        
        public static readonly List<CSteamID> LobbyMembers = new List<CSteamID>();
        public static CSteamID CurrentLobby { get; private set; } = CSteamID.Nil;
        public static bool InLobby => CurrentLobby.IsValid();
        public static string CurrentLobbyCode { get; private set; } = "";

        private static CallResult<LobbyMatchList_t> _lobbyListCallResult;
        private static Action<List<LobbyListEntry>> _onLobbyListReceived;

        private static event System.Action _onLobbyCreatedSuccess = null;
        private static event Action<CSteamID> _onLobbyJoined = null;

        public static event Action<CSteamID> OnLobbyMembersRefreshed;

        public static void Initialize()
        {
            if (!SteamManager.Initialized) return;
            try
            {
                _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
                _lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
                _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
                _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
                DebugConsole.Log("[SteamLobby] Callbacks registered.");
            }
            catch (Exception ex)
            {
                DebugConsole.LogError($"[SteamLobby] Init failed: {ex.Message}");
            }
        }

        public static void CreateLobby(ELobbyType lobbyType = ELobbyType.k_ELobbyTypePublic, System.Action onSuccess = null)
        {
            if (!SteamManager.Initialized) return;
            if (InLobby) return;
            _onLobbyCreatedSuccess = onSuccess;
            SteamMatchmaking.CreateLobby(lobbyType, LOBBY_SIZE_MAX);
        }

        public static void LeaveLobby()
        {
            if (InLobby)
            {
                SteamMatchmaking.LeaveLobby(CurrentLobby);
                CurrentLobby = CSteamID.Nil;
            }
        }

        private static void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult == EResult.k_EResultOK)
            {
                CurrentLobby = new CSteamID(callback.m_ulSteamIDLobby);
                SteamMatchmaking.SetLobbyData(CurrentLobby, "name", SteamFriends.GetPersonaName() + "'s Trade Lobby");
                SteamMatchmaking.SetLobbyData(CurrentLobby, "host", SteamUser.GetSteamID().ToString());
                SteamMatchmaking.SetLobbyData(CurrentLobby, "hostname", SteamFriends.GetPersonaName());
                SteamMatchmaking.SetLobbyData(CurrentLobby, "game_id", "oni_multiplayer_trade");
                DebugConsole.Log($"[SteamLobby] Lobby created: {CurrentLobby}");
                
                MultiplayerTradeMod.MultiplayerServerManager.Instance?.StartServer();

                _onLobbyCreatedSuccess?.Invoke();
                _onLobbyCreatedSuccess = null;
            }
        }

        private static void OnLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            JoinLobby(callback.m_steamIDLobby);
        }

        private static void OnLobbyEntered(LobbyEnter_t callback)
        {
            CurrentLobby = new CSteamID(callback.m_ulSteamIDLobby);
            
            string hostStr = SteamMatchmaking.GetLobbyData(CurrentLobby, "host");
            if (hostStr != SteamUser.GetSteamID().ToString())
            {
                MultiplayerTradeMod.MultiplayerServerManager.Instance?.JoinServer();
            }

            DebugConsole.Log($"[SteamLobby] Entered lobby: {CurrentLobby}");
            _onLobbyJoined?.Invoke(CurrentLobby);
            RefreshLobbyMembers();
        }

        private static void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            RefreshLobbyMembers();
        }

        public static void JoinLobby(CSteamID lobbyId, Action<CSteamID> onJoinedLobby = null, string password = null)
        {
            if (!SteamManager.Initialized) return;
            if (InLobby) LeaveLobby();
            _onLobbyJoined = onJoinedLobby;
            SteamMatchmaking.JoinLobby(lobbyId);
        }

        public static List<CSteamID> GetAllLobbyMembers()
        {
            List<CSteamID> members = new List<CSteamID>();
            if (!InLobby) return members;
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);
            for (int i = 0; i < memberCount; i++)
            {
                members.Add(SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobby, i));
            }
            return members;
        }

        private static void RefreshLobbyMembers()
        {
            LobbyMembers.Clear();
            if (!InLobby) return;
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobby, i);
                LobbyMembers.Add(member);
                OnLobbyMembersRefreshed?.Invoke(member);
            }
        }

        public static void RequestLobbyList(Action<List<LobbyListEntry>> onComplete)
        {
            if (!SteamManager.Initialized)
            {
                onComplete?.Invoke(new List<LobbyListEntry>());
                return;
            }
            _onLobbyListReceived = onComplete;
            SteamMatchmaking.AddRequestLobbyListStringFilter("game_id", "oni_multiplayer_trade", ELobbyComparison.k_ELobbyComparisonEqual);
            var handle = SteamMatchmaking.RequestLobbyList();
            _lobbyListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbyListReceived);
            _lobbyListCallResult.Set(handle);
        }

        private static void OnLobbyListReceived(LobbyMatchList_t result, bool bIOFailure)
        {
            var lobbies = new List<LobbyListEntry>();
            if (bIOFailure)
            {
                _onLobbyListReceived?.Invoke(lobbies);
                return;
            }
            for (int i = 0; i < result.m_nLobbiesMatching; i++)
            {
                CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
                if (!lobbyId.IsValid()) continue;

                string hostStr = SteamMatchmaking.GetLobbyData(lobbyId, "host");
                ulong.TryParse(hostStr, out ulong hostId);
                
                lobbies.Add(new LobbyListEntry
                {
                    LobbyId = lobbyId,
                    HostSteamId = new CSteamID(hostId),
                    LobbyName = SteamMatchmaking.GetLobbyData(lobbyId, "name"),
                    HostName = SteamMatchmaking.GetLobbyData(lobbyId, "hostname"),
                    PlayerCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId),
                    MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId)
                });
            }
            _onLobbyListReceived?.Invoke(lobbies);
            _onLobbyListReceived = null;
        }
    }
}
