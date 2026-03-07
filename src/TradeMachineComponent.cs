using KSerialization;
using UnityEngine;

namespace MultiplayerTradeMod
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class TradeMachineComponent : KMonoBehaviour, ISidescreenButtonControl
    {
        private Storage storage;
        private Operational operational;

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            storage = GetComponent<Storage>();
            operational = GetComponent<Operational>();
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            TradeManager.Instance?.RegisterMachine(this);
        }

        protected override void OnCleanUp()
        {
            base.OnCleanUp();
            TradeManager.Instance?.UnregisterMachine(this);
        }

        public string SidescreenButtonText => "LAUNCH RESOURCES";
        public string SidescreenButtonTooltip => "Sends resources in this machine's storage to the connected player's colony.";
        public int ButtonSideScreenSortOrder() => 20;
        public int HorizontalGroupID() => -1;

        public void SetButtonTextOverride(ButtonMenuTextOverride textOverride) { }

        public bool SidescreenEnabled() => true;

        public bool SidescreenButtonInteractable()
        {
            return operational != null && operational.IsOperational &&
                   MultiplayerServerManager.Instance != null && MultiplayerServerManager.Instance.IsConnected &&
                   storage != null && storage.MassStored() > 0f;
        }

        public void OnSidescreenButtonPressed()
        {
            if (!SidescreenButtonInteractable())
                return;

            var items = storage.items;
            if (items == null || items.Count == 0)
                return;

            var cargoList = new System.Collections.Generic.List<CargoItem>();

            for (int i = items.Count - 1; i >= 0; i--)
            {
                GameObject itemObj = items[i];
                if (itemObj == null)
                    continue;

                PrimaryElement element = itemObj.GetComponent<PrimaryElement>();
                if (element == null)
                    continue;

                cargoList.Add(new CargoItem
                {
                    resourceHash = element.ElementID,
                    amount = element.Mass,
                    temperature = element.Temperature,
                    diseaseIdx = element.DiseaseIdx,
                    diseaseCount = element.DiseaseCount
                });

                Util.KDestroyGameObject(itemObj);
            }

            if (cargoList.Count == 0)
                return;

            TradeMessage msg = new TradeMessage
            {
                senderId = "Trader",
                recipientId = "Remote",
                senderName = "Multiplayer Partner",
                cargo = cargoList,
                sendTime = GameClock.Instance != null ? GameClock.Instance.GetTime() : 0f,
                arrivalDelay = 0.1f
            };

            MultiplayerServerManager.Instance?.SendTradeMessage(msg);
            UIManager.Instance?.ShowNotification("Resources launched to multiplayer server.");
        }
    }
}
