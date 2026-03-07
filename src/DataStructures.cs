using System.Collections.Generic;

namespace MultiplayerTradeMod
{
    public enum NetworkMessageType
    {
        PLAYER_CONNECT,
        PLAYER_DISCONNECT,
        CHAT_MESSAGE,
        TRADE_REQUEST,
        TRADE_CONFIRM,
        PLAYER_STATUS,
        WORLD_SYNC
    }

    [System.Serializable]
    public struct CargoItem
    {
        public SimHashes resourceHash;
        public float amount;
        public float temperature;
        public byte diseaseIdx;
        public int diseaseCount;
    }

    [System.Serializable]
    public struct TradeMessage
    {
        public string senderId;
        public string recipientId;
        public string senderName;
        public List<CargoItem> cargo;
        public float sendTime;
        public float arrivalDelay;
    }

    [System.Serializable]
    public class PlayerInfo
    {
        public string id;
        public string name;
        public bool connected;
        public int currentCycle;
    }
}
