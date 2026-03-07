using TUNING;
using UnityEngine;
using System.Collections.Generic;

namespace MultiplayerTradeMod
{
    // Temporarily disabled: requires custom kanim assets to register properly.
    // Re-enable by implementing IBuildingConfig once kanim assets are included.
    public class TradeMachineConfig
    {
        public const string ID = "InterplanetaryTradeMachine";
        public const string DisplayName = "Interplanetary Trade Machine";
        public const string Description = "Sends physical resources to another colony in the multiverse.";
        public const string Effect = "Connects to a remote server. Requires a multiplayer connection.";

        public BuildingDef CreateBuildingDef()
        {
            int width = 3;
            int height = 3;
            string anim = "storage_locker_kanim"; // Use a real base-game anim to avoid null crashes
            int hitpoints = 100;
            float construction_time = 120f;
            float[] tieR4 = BUILDINGS.CONSTRUCTION_MASS_KG.TIER4; // 400kg
            string[] all_METALS = MATERIALS.REFINED_METALS;
            float melting_point = 1600f;
            BuildLocationRule build_location_rule = BuildLocationRule.OnFloor;
            EffectorValues noise = NOISE_POLLUTION.NOISY.TIER5;
            
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef(ID, width, height, anim, hitpoints, construction_time, tieR4, all_METALS, melting_point, build_location_rule, BUILDINGS.DECOR.PENALTY.TIER1, noise, 0.2f);
            
            buildingDef.RequiresPowerInput = true;
            buildingDef.EnergyConsumptionWhenActive = 240f;
            buildingDef.ExhaustKilowattsWhenActive = 0.5f;
            buildingDef.SelfHeatKilowattsWhenActive = 2f;
            buildingDef.AudioCategory = "Metal";
            buildingDef.ViewMode = OverlayModes.Power.ID;
            
            return buildingDef;
        }

        public void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery);
            
            Storage storage = go.AddOrGet<Storage>();
            storage.capacityKg = 20000f; // 20t buffer
            storage.showInUI = true;
            storage.showDescriptor = true;
            storage.allowItemRemoval = false;
            storage.fetchCategory = Storage.FetchCategory.GeneralStorage;

            Prioritizable.AddRef(go);
            go.AddOrGet<TreeFilterable>();

            go.AddOrGet<StorageLocker>();
            go.AddOrGet<TradeMachineComponent>();
        }

        public void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<LogicOperationalController>();
            go.AddOrGetDef<PoweredActiveController.Def>();
        }
    }
}
