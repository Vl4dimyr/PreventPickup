using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using RoR2;
using R2API.Utils;

namespace PreventPickup
{
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("de.userstorm.preventpickup", "PreventPickup", "{VERSION}")]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)]
    public class PreventPickupPlugin : BaseUnityPlugin
    {
        private static On.RoR2.GenericPickupController.orig_GrantItem GrantItem;
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

        private CharacterMaster GetCharacterMasterByInvetory(Inventory inventory)
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
                list.Add(ItemIndex.ScrapWhite);
            }

            return list[new Random().Next(list.Count)];
        }

        private ItemIndex GetScrapItemIndex(ItemIndex itemIndex)
        {
            ItemDef definition = ItemCatalog.GetItemDef(itemIndex);

            switch (definition.tier)
            {
                case ItemTier.Tier1:
                    return ItemIndex.ScrapWhite;
                case ItemTier.Tier2:
                    return ItemIndex.ScrapGreen;
                case ItemTier.Tier3:
                    return ItemIndex.ScrapRed;
                case ItemTier.Lunar:
                    return ItemIndex.ScrapGreen;
                case ItemTier.Boss:
                    return ItemIndex.ScrapYellow;
            }

            return ItemIndex.ScrapWhite;
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

        private void GiveRandomItem(CharacterBody body, Inventory inventory, ItemIndex itemIndex)
        {
            float chance = GetChance(itemIndex);

            if (chance - new Random().NextDouble() > 0f)
            {
                genericPickupController.pickupIndex = PickupCatalog.FindPickupIndex(GetRandomItemIndex(itemIndex));

                SendChatMessage("Lucky!");

                GrantItem.Invoke(genericPickupController, body, inventory);

                return;
            }

            if (chance > 0f)
            {
                SendChatMessage("No luck this time!");
            }

            GiveScrapItem(body, inventory, itemIndex);
        }

        private void GiveScrapItem(CharacterBody body, Inventory inventory, ItemIndex itemIndex)
        {
            genericPickupController.pickupIndex = PickupCatalog.FindPickupIndex(GetScrapItemIndex(itemIndex));

            GrantItem.Invoke(genericPickupController, body, inventory);
        }

        private void OnGrantItem(
            On.RoR2.GenericPickupController.orig_GrantItem orig,
            GenericPickupController self,
            CharacterBody body,
            Inventory inventory
        )
        {
            GrantItem = orig;
            genericPickupController = self;

            ItemIndex itemIndex = PickupCatalog.GetPickupDef(self.pickupIndex).itemIndex;

            if (PreventPickupConfigEntries.ContainsKey(itemIndex) && PreventPickupConfigEntries[itemIndex].Value)
            {
                GiveRandomItem(body, inventory, itemIndex);

                return;
            }

            orig.Invoke(self, body, inventory);
        }

        private void OnGiveItem(On.RoR2.Inventory.orig_GiveItem orig, Inventory self, ItemIndex itemIndex, int count)
        {
            if (PreventPickupConfigEntries.ContainsKey(itemIndex) && PreventPickupConfigEntries[itemIndex].Value)
            {
                CharacterMaster characterMasterByInvetory = GetCharacterMasterByInvetory(self);

                if (characterMasterByInvetory == null)
                {
                    return;
                }

                GiveRandomItem(characterMasterByInvetory.GetBody(), self, itemIndex);

                return;
            }

            orig.Invoke(self, itemIndex, count);
        }

        private string GetChanceDescription (string from, string to)
        {
            string info = "(0.00 to 1.00, 0.00 == 0%, 1.0 == 100%)";

            return $"Chance to get a random {to} item when a {from} item pick up is prevented. {info}";
        }

        public void Start()
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

            for (int i = 0; i < (int)ItemIndex.Count; i++)
            {
                ItemIndex itemIndex = (ItemIndex)i;
                ItemDef definition = ItemCatalog.GetItemDef(itemIndex);

                if (
                    itemIndex.Equals(ItemIndex.ArtifactKey) ||
                    itemIndex.Equals(ItemIndex.CaptainDefenseMatrix) ||
                    itemIndex.Equals(ItemIndex.ScrapWhite) ||
                    itemIndex.Equals(ItemIndex.ScrapGreen) ||
                    itemIndex.Equals(ItemIndex.ScrapRed) ||
                    itemIndex.Equals(ItemIndex.ScrapYellow) ||
                    definition.tier.Equals(ItemTier.NoTier)
                )
                {
                    continue;
                }

                string displayName = Language.GetString(definition.nameToken);

                PreventPickupConfigEntries[itemIndex] = Config.Bind(
                    "PreventPickup",
                    itemIndex.ToString(),
                    false,
                    $"Item index: {(int)itemIndex} | Name: {displayName} | Tier: {definition.tier}"
                );
            }
        }

        public void Awake()
        {
            On.RoR2.GenericPickupController.GrantItem += OnGrantItem;
            On.RoR2.Inventory.GiveItem += OnGiveItem;
        }

        public void OnDestroy()
        {
            On.RoR2.GenericPickupController.GrantItem -= OnGrantItem;
            On.RoR2.Inventory.GiveItem -= OnGiveItem;
        }
    }
}
