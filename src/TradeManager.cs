using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MultiplayerTradeMod
{
    public class TradeManager : MonoBehaviour
    {
        public static TradeManager Instance { get; private set; }

        private readonly List<TradeMachineComponent> activeMachines = new List<TradeMachineComponent>();

        public void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void RegisterMachine(TradeMachineComponent machine)
        {
            if (machine == null)
                return;

            if (!activeMachines.Contains(machine))
                activeMachines.Add(machine);
        }

        public void UnregisterMachine(TradeMachineComponent machine)
        {
            if (machine == null)
                return;

            activeMachines.Remove(machine);
        }

        public void SendTrade(string targetPlayerId, List<CargoItem> items)
        {
            if (items == null || items.Count == 0)
            {
                UIManager.Instance?.ShowNotification("Cannot send trade: no items selected.");
                return;
            }

            var net = MultiplayerServerManager.Instance;
            if (net == null || !net.IsConnected)
            {
                UIManager.Instance?.ShowNotification("Cannot send trade: not connected to multiplayer.");
                return;
            }

            TradeMessage msg = new TradeMessage
            {
                senderId = "LocalPlayerID",
                recipientId = targetPlayerId,
                senderName = "Player " + UnityEngine.Random.Range(10, 99),
                cargo = items,
                sendTime = GameClock.Instance != null ? GameClock.Instance.GetTime() : 0f,
                arrivalDelay = ConfigManager.Config.gameplay.payloadTravelTime
            };

            net.SendTradeMessage(msg);
            ConsumeLocalResources(items);
        }

        private void ConsumeLocalResources(List<CargoItem> items)
        {
            if (items == null)
                return;

            foreach (CargoItem item in items)
            {
                float amountRemaining = item.amount;
                Element element = ElementLoader.FindElementByHash(item.resourceHash);
                if (element == null)
                    continue;

                Tag resourceTag = element.tag;

                foreach (Storage storage in Object.FindObjectsOfType<Storage>())
                {
                    if (amountRemaining <= 0f)
                        break;

                    if (storage == null || !storage.Has(resourceTag))
                        continue;

                    float available = storage.GetMassAvailable(resourceTag);
                    float amountToTake = Mathf.Min(amountRemaining, available);
                    if (amountToTake <= 0f)
                        continue;

                    storage.ConsumeIgnoringDisease(resourceTag, amountToTake);
                    amountRemaining -= amountToTake;

                    Debug.Log("[play.gg][MultiplayerTrade] Deducted " + amountToTake + "kg of " + item.resourceHash + " from " + storage.gameObject.name);
                }

                if (amountRemaining > 0f)
                {
                    Debug.LogWarning("[play.gg][MultiplayerTrade] Could not fully deduct " + item.resourceHash + ". Missing: " + amountRemaining + "kg.");
                }
            }
        }

        public void ReceiveIncomingTrade(TradeMessage trade)
        {
            if (trade.cargo == null)
                trade.cargo = new List<CargoItem>();

            MultiplayerSaveManager.Instance?.AddPendingPayload(trade);
            StartCoroutine(ProcessIncomingPayload(trade));
        }

        public void ResumeIncomingTrade(TradeMessage trade)
        {
            if (trade.cargo == null)
                trade.cargo = new List<CargoItem>();

            StartCoroutine(ProcessIncomingPayload(trade));
        }

        private IEnumerator ProcessIncomingPayload(TradeMessage trade)
        {
            UIManager.Instance?.ShowNotification("Incoming payload from " + trade.senderName + ". ETA: " + trade.arrivalDelay + " cycles.");

            float durationInSeconds = Mathf.Max(0f, trade.arrivalDelay * 600f);
            if (durationInSeconds > 0f)
                yield return new WaitForSeconds(durationInSeconds);

            bool deposited = false;

            if (activeMachines.Count > 0)
            {
                TradeMachineComponent machine = activeMachines[0];
                Storage targetStorage = machine != null ? machine.GetComponent<Storage>() : null;

                if (targetStorage != null)
                {
                    foreach (CargoItem item in trade.cargo)
                    {
                        Element element = ElementLoader.FindElementByHash(item.resourceHash);
                        if (element == null)
                            continue;

                        GameObject resource = element.substance.SpawnResource(machine.transform.position, item.amount,
                            item.temperature, item.diseaseIdx, item.diseaseCount);
                        if (resource != null)
                            targetStorage.Store(resource);
                    }

                    deposited = true;
                    UIManager.Instance?.PlaySound("payload_arrival");
                    UIManager.Instance?.ShowNotification("Received resources from " + trade.senderName + " into Trade Machine.");
                }
            }

            if (!deposited)
            {
                SpawnCargoPod(trade.cargo);
                UIManager.Instance?.PlaySound("payload_arrival");
                UIManager.Instance?.ShowNotification("Payload landed from " + trade.senderName + ". Check your asteroid surface.");
            }

            MultiplayerSaveManager.Instance?.RemovePendingPayload(trade);
        }

        private void SpawnCargoPod(List<CargoItem> items)
        {
            GameObject podPrefab = Assets.GetPrefab("RailGunPayload");
            if (podPrefab == null)
            {
                Debug.LogWarning("[play.gg][MultiplayerTrade] RailGunPayload prefab not found, skipping spawn.");
                return;
            }

            int randomX = UnityEngine.Random.Range(20, Grid.WidthInCells - 20);
            int cell = Grid.PosToCell(new Vector3(randomX, Grid.HeightInCells - 5));

            GameObject spawnedPod = GameUtil.KInstantiate(podPrefab, Grid.CellToPos(cell), Grid.SceneLayer.Ore);
            spawnedPod.SetActive(true);

            Storage storage = spawnedPod.GetComponent<Storage>();
            if (storage != null && items != null)
            {
                foreach (CargoItem item in items)
                {
                    Element element = ElementLoader.FindElementByHash(item.resourceHash);
                    if (element == null)
                        continue;

                    GameObject resource = element.substance.SpawnResource(Vector3.zero, item.amount, item.temperature, 0, 0);
                    if (resource != null)
                        storage.Store(resource);
                }
            }

            Debug.Log("[play.gg][MultiplayerTrade] Dropped cargo pod at cell " + cell + " with " + (items == null ? 0 : items.Count) + " items.");
        }
    }
}
