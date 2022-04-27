using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using RoR2;
using R2API.Utils;

namespace PreventPickup
{
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("de.userstorm.preventpickup", "PreventPickup", "{VERSION}")]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)]
    public class PreventPickupPlugin : BaseUnityPlugin
    {
        private static On.RoR2.GenericPickupController.orig_AttemptGrant AttemptGrant;
        private static GenericPickupController genericPickupController;

        public static ConfigEntry<float> WhiteRandomItemChance { get; set; }
        public static ConfigEntry<float> GreenRandomItemChance { get; set; }
        public static ConfigEntry<float> RedRandomItemChance { get; set; }
        public static ConfigEntry<float> LunarRandomItemChance { get; set; }
        public static ConfigEntry<float> BossRandomItemChance { get; set; }

        public static Dictionary<ItemIndex, ConfigEntry<bool>> PreventPickupConfigEntries =
            new Dictionary<ItemIndex, ConfigEntry<bool>>();

        private void SendChatMessage(string message)
        {
            Chat.SendBroadcastChat(
                new Chat.SimpleChatMessage
                {
                    baseToken = "<color=#e5eefc>{0}</color>",
                    paramTokens = new string[] { message }
                }
            );
        }

        private CharacterMaster GetCharacterMasterByInventory(Inventory inventory)
        {
            PlayerCharacterMasterController playerCharacterMasterController =
                PlayerCharacterMasterController.instances.FirstOrDefault((PlayerCharacterMasterController p) =>
                    p.master.inventory == inventory
                );

            return (playerCharacterMasterController != null) ? playerCharacterMasterController.master : null;
        }

        private ItemIndex GetRandomItemIndex(ItemIndex itemIndex)
        {
            ItemDef definition = ItemCatalog.GetItemDef(itemIndex);

            List<ItemIndex> list = new List<ItemIndex>();

            switch (definition.tier)
            {
                case ItemTier.Tier1:
                    list = ItemCatalog.tier1ItemList;
                    break;
                case ItemTier.Tier2:
                    list = ItemCatalog.tier2ItemList;
                    break;
                case ItemTier.Tier3:
                    list = ItemCatalog.tier3ItemList;
                    break;
                case ItemTier.Lunar:
                    list = ItemCatalog.lunarItemList;
                    break;
                case ItemTier.Boss:
                    list = ItemCatalog.allItems.ToList().FindAll(
                        iIndex => ItemCatalog.GetItemDef(iIndex).tier.Equals(ItemTier.Boss)
                    );
                    break;
            }

            list = list.FindAll(
                iIndex => PreventPickupConfigEntries.ContainsKey(iIndex) && !PreventPickupConfigEntries[iIndex].Value
            );

            if (list.Count() == 0)
            {
                list.Add(RoR2Content.Items.ScrapWhite.itemIndex);
            }

            return list[new Random().Next(list.Count)];
        }

        private ItemIndex GetScrapItemIndex(ItemIndex itemIndex)
        {
            ItemDef definition = ItemCatalog.GetItemDef(itemIndex);

            switch (definition.tier)
            {
               case ItemTier.Tier1:
                   return RoR2Content.Items.ScrapWhite.itemIndex;
               case ItemTier.Tier2:
                   return RoR2Content.Items.ScrapGreen.itemIndex;
               case ItemTier.Tier3:
                   return RoR2Content.Items.ScrapRed.itemIndex;
               case ItemTier.Lunar:
                   return RoR2Content.Items.ScrapGreen.itemIndex;
               case ItemTier.Boss:
                   return RoR2Content.Items.ScrapYellow.itemIndex;
            }

            return RoR2Content.Items.ScrapWhite.itemIndex;
        }

        private float GetChance(ItemIndex itemIndex)
        {
            ItemDef definition = ItemCatalog.GetItemDef(itemIndex);

            switch (definition.tier)
            {
                case ItemTier.Tier1:
                    return WhiteRandomItemChance.Value;
                case ItemTier.Tier2:
                    return GreenRandomItemChance.Value;
                case ItemTier.Tier3:
                    return RedRandomItemChance.Value;
                case ItemTier.Lunar:
                    return LunarRandomItemChance.Value;
                case ItemTier.Boss:
                    return BossRandomItemChance.Value;
            }

            return 0f;
        }

        private void GiveRandomItem(CharacterBody body, ItemIndex itemIndex)
        {
            float chance = GetChance(itemIndex);

            if (chance - new Random().NextDouble() > 0f)
            {
                genericPickupController.pickupIndex = PickupCatalog.FindPickupIndex(GetRandomItemIndex(itemIndex));

                SendChatMessage("Lucky!");

                AttemptGrant.Invoke(genericPickupController, body);

                return;
            }

            if (chance > 0f)
            {
                SendChatMessage("No luck this time!");
            }

            GiveScrapItem(body, itemIndex);
        }

        private void GiveScrapItem(CharacterBody body, ItemIndex itemIndex)
        {
            genericPickupController.pickupIndex = PickupCatalog.FindPickupIndex(GetScrapItemIndex(itemIndex));

            AttemptGrant.Invoke(genericPickupController, body);
        }

        private void OnAttemptGrant(
            On.RoR2.GenericPickupController.orig_AttemptGrant orig,
            GenericPickupController self,
            CharacterBody body
        )
        {
            AttemptGrant = orig;
            genericPickupController = self;

            ItemIndex itemIndex = PickupCatalog.GetPickupDef(self.pickupIndex).itemIndex;

            if (PreventPickupConfigEntries.ContainsKey(itemIndex) && PreventPickupConfigEntries[itemIndex].Value)
            {
                GiveRandomItem(body, itemIndex);

                return;
            }

            orig.Invoke(self, body);
        }

        private void OnGiveItem(On.RoR2.Inventory.orig_GiveItem_ItemDef_int orig, Inventory self, ItemDef itemDef, int count)
        {
            ItemIndex itemIndex = itemDef.itemIndex;

            if (PreventPickupConfigEntries.ContainsKey(itemIndex) && PreventPickupConfigEntries[itemIndex].Value)
            {
                CharacterMaster characterMasterByInventory = GetCharacterMasterByInventory(self);

                if (characterMasterByInventory == null)
                {
                    return;
                }

                GiveRandomItem(characterMasterByInventory.GetBody(), itemIndex);

                return;
            }

            orig.Invoke(self, itemDef, count);
        }

        private void OnItemCatalogInit(On.RoR2.ItemCatalog.orig_Init orig)
        {
            orig.Invoke();

            InitConfig();
        }

        private string GetChanceDescription (string from, string to)
        {
            string info = "(0.00 to 1.00, 0.00 == 0%, 1.0 == 100%)";

            return $"Chance to get a random {to} item when a {from} item pick up is prevented. {info}";
        }

        private void InitConfig()
        {
            WhiteRandomItemChance = Config.Bind(
                "Balance",
                "WhiteRandomItemChance",
                0.05f,
                GetChanceDescription("white", "white")
            );

            GreenRandomItemChance = Config.Bind(
                "Balance",
                "GreenRandomItemChance",
                0.05f,
                GetChanceDescription("green", "green")
            );

            RedRandomItemChance = Config.Bind(
                "Balance",
                "RedRandomItemChance",
                0.05f,
                GetChanceDescription("red", "red")
            );

            LunarRandomItemChance = Config.Bind(
                "Balance",
                "LunarRandomItemChance",
                0.05f,
                GetChanceDescription("lunar", "GREEN")
            );

            BossRandomItemChance = Config.Bind(
                "Balance",
                "BossRandomItemChance",
                0.05f,
                GetChanceDescription("boss", "boss")
            );

            if (RiskOfOptionsMod.enabled)
            {
                RiskOfOptionsMod.Init(
                    "This mod shows the current item counts in the item selection dialog of the Artifact of Command and the Scrapper."
                );
                RiskOfOptionsMod.AddStepSliderOption(WhiteRandomItemChance, 0, 1, 0.01f);
                RiskOfOptionsMod.AddStepSliderOption(GreenRandomItemChance, 0, 1, 0.01f);
                RiskOfOptionsMod.AddStepSliderOption(RedRandomItemChance, 0, 1, 0.01f);
                RiskOfOptionsMod.AddStepSliderOption(LunarRandomItemChance, 0, 1, 0.01f);
                RiskOfOptionsMod.AddStepSliderOption(BossRandomItemChance, 0, 1, 0.01f);
            }

            foreach (ItemIndex itemIndex in ItemCatalog.allItems)
            {
                ItemDef definition = ItemCatalog.GetItemDef(itemIndex);

                if (
                    definition.Equals(RoR2Content.Items.ArtifactKey) ||
                    definition.Equals(RoR2Content.Items.CaptainDefenseMatrix) ||
                    definition.Equals(RoR2Content.Items.ScrapWhite) ||
                    definition.Equals(RoR2Content.Items.ScrapGreen) ||
                    definition.Equals(RoR2Content.Items.ScrapRed) ||
                    definition.Equals(RoR2Content.Items.ScrapYellow) ||
                    definition.tier.Equals(ItemTier.NoTier)
                )
                {
                    continue;
                }

                string displayName = Language.GetString(definition.nameToken);

                PreventPickupConfigEntries[itemIndex] = Config.Bind(
                    "PreventPickup",
                    definition.name,
                    false,
                    $"Item index: {(int)itemIndex} | Name: {displayName} | Tier: {definition.tier}"
                );

                if (RiskOfOptionsMod.enabled)
                {
                    RiskOfOptionsMod.AddCheckboxOption(PreventPickupConfigEntries[itemIndex]);
                }
            }
        }

        public void Awake()
        {
            On.RoR2.GenericPickupController.AttemptGrant += OnAttemptGrant;
            On.RoR2.Inventory.GiveItem_ItemDef_int += OnGiveItem;
            On.RoR2.ItemCatalog.Init += OnItemCatalogInit;
        }

        public void OnDestroy()
        {
            On.RoR2.GenericPickupController.AttemptGrant -= OnAttemptGrant;
            On.RoR2.Inventory.GiveItem_ItemDef_int -= OnGiveItem;
            On.RoR2.ItemCatalog.Init -= OnItemCatalogInit;
        }
    }
}
