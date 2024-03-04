// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
using System;
using System.Collections.Generic;

namespace Game.Entities
{
    public static class ItemEnchantmentManager
    { 
        public static void LoadRandomEnchantmentsTable()
        {
            uint oldMSTime = Time.GetMSTime();

            RandomEnchantmentData.Clear();

            //                                         0              1      2
            using var result = DB.World.Query("SELECT Id, EnchantmentId, Chance FROM item_random_enchantment_template");

            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.Player, "Loaded 0 Random item bonus list definitions. DB table `item_random_enchantment_template` is empty.");
                return;
            }
            uint count = 0;

            do
            {
                int id = result.Read<int>(0);
                int enchantmentId = result.Read<int>(1);
                float chance = result.Read<float>(2);

                if (CliDB.ItemRandomPropertiesStorage.LookupByKey(enchantmentId) != null && CliDB.ItemRandomSuffixStorage.LookupByKey(enchantmentId) != null)
                {
                    Log.outError(LogFilter.Sql, $"ItemRandomProperties / ItemRandomSuffix Id {enchantmentId} used in `item_random_enchantment_template` by id {id} " +
                        "doesn't have exist in its corresponding db2 file.");
                    continue;
                }

                if (chance < 0.000001f || chance > 100.0f)
                {
                    Log.outError(LogFilter.Sql, $"Enchantment Id {enchantmentId} used in `item_random_enchantment_template` by id {id} has invalid chance {chance}");
                    continue;
                }

                if (!RandomEnchantmentData.ContainsKey(id))
                    RandomEnchantmentData[id] = new();

                RandomEnchantmentData[id].Add((enchantmentId, chance));

                ++count;
            } while (result.NextRow());

            Log.outInfo(LogFilter.Player, $"Loaded {count} Random item enchantment definitions in {Time.GetMSTimeDiffToNow(oldMSTime)} ms.");
        }

        public static int GetRandomPropertyPoints(int itemLevel, ItemQuality quality, InventoryType inventoryType)
        {
            int propIndex;

            switch (inventoryType)
            {
                // Items of that type don`t have points
                case InventoryType.NonEquip:
                case InventoryType.Bag:
                case InventoryType.Tabard:
                case InventoryType.Ammo:
                case InventoryType.Quiver:
                case InventoryType.Relic:
                    return 0;

                // Select point coefficient
                case InventoryType.Head:
                case InventoryType.Body:
                case InventoryType.Chest:
                case InventoryType.Legs:
                case InventoryType.Weapon2Hand:
                case InventoryType.Robe:
                    propIndex = 0;
                    break;
                case InventoryType.Shoulders:
                case InventoryType.Waist:
                case InventoryType.Feet:
                case InventoryType.Hands:
                case InventoryType.Trinket:
                    propIndex = 1;
                    break;
                case InventoryType.Neck:
                case InventoryType.Wrists:
                case InventoryType.Finger:
                case InventoryType.Shield:
                case InventoryType.Cloak:
                case InventoryType.Holdable:
                    propIndex = 2;
                    break;
                case InventoryType.Weapon:
                case InventoryType.WeaponMainhand:
                case InventoryType.WeaponOffhand:
                    propIndex = 3;
                    break;
                case InventoryType.Ranged:
                case InventoryType.Thrown:
                case InventoryType.RangedRight:
                    propIndex = 4;
                    break;
                default:
                    return 0;
            }

            RandPropPointsRecord randPropPointsEntry = CliDB.RandPropPointsStorage.LookupByKey(itemLevel);
            if (randPropPointsEntry == null)
                return 0;

            switch (quality)
            {
                case ItemQuality.Uncommon:
                    return randPropPointsEntry.Good[propIndex];
                case ItemQuality.Rare:
                case ItemQuality.Heirloom:
                    return randPropPointsEntry.Superior[propIndex];
                case ItemQuality.Epic:
                case ItemQuality.Legendary:
                case ItemQuality.Artifact:
                    return randPropPointsEntry.Epic[propIndex];
            }

            return 0;
        }

        public static Dictionary<int, List<(int EnchantmentID, float Chance)>> RandomEnchantmentData;
    }

    public static ItemRandomProperties GenerateRandomProperties(int item_id)
    {
        //ItemTemplate itemProto = Global.ObjectMgr.GetItemTemplate(item_id);
        //if (itemProto == null)
        //    return 0;

        //// item must have one from this field values not null if it can have random enchantments
        //if (itemProto.RandomBonusListTemplateId == 0)
        //    return 0;

        //var tab = _storage.LookupByKey(itemProto.RandomBonusListTemplateId);
        //if (tab == null)
        //{
        //    Log.outError(LogFilter.Sql, $"Item RandomBonusListTemplateId id {itemProto.RandomBonusListTemplateId} used in `item_template_addon` but it does not have records in `item_random_bonus_list_template` table.");
        //    return 0;
        //}
        ////todo fix me this is ulgy
        //return tab.BonusListIDs.SelectRandomElementByWeight(x => (float)tab.Chances[tab.BonusListIDs.IndexOf(x)]);
        return new();
    }

    public struct EnchStoreItem
    {
        public EnchStoreItem() { }
        public EnchStoreItem(ItemRandomEnchantmentId itemRandomEnchantmentId, float chance)
        {
            itemRandomEnchantmentId = itemRandomEnchantmentId;
            Chance = chance;
        }
        public EnchStoreItem(ItemRandomEnchantmentType type, int id, float chance)
        {
            itemRandomEnchantmentId.Type = type;
            itemRandomEnchantmentId.Id = id;
            Chance = chance;
        }
        public ItemRandomEnchantmentId itemRandomEnchantmentId;
        public float Chance;
    }

    public struct ItemRandomEnchantmentId
    {
        public ItemRandomEnchantmentId() { }
        public ItemRandomEnchantmentId(ItemRandomEnchantmentType type, int id)
        {
            Type = type;
            Id = id;
        }

        public ItemRandomEnchantmentType Type;
        public int Id;
    };

    class EnchantmentStore
    {
        private Dictionary<ItemRandomEnchantmentType, Dictionary<uint, List<EnchStoreItem>>> _data;
        private ItemRandomEnchantmentType Check(ItemRandomEnchantmentType type)
        {
            // random bonus lists use RandomSuffix field in Item-sparse.db2
            //ASSERT(Type != ItemRandomEnchantmentType.BonusList, "Random bonus lists do not have their own storage, use Suffix for them");
            if (type == ItemRandomEnchantmentType.BonusList)
                return ItemRandomEnchantmentType.Suffix;
            return type;
        }

        public EnchantmentStore()
        {
            _data = new()
            {
                { ItemRandomEnchantmentType.Property, new Dictionary<uint,List<EnchStoreItem>>() },
                { ItemRandomEnchantmentType.Suffix, new Dictionary<uint,List<EnchStoreItem>>() },
                //{ ItemRandomEnchantmentType.BonusList, new Dictionary<uint,EnchStoreItem>() }
            };
        }

        public Dictionary<uint, List<EnchStoreItem>> this[ItemRandomEnchantmentType type]
        {
            get => _data[Check(type)];
            set => _data[Check(type)] = value;
        }
    }

    public enum ItemRandomEnchantmentType : byte
    {
        Property = 0,
        Suffix = 1,
        BonusList = 2
    }

    public struct ItemRandomProperties
    {
        public ItemRandomProperties(int randomPropertiesId = 0, int randomPropertiesSeed = 0)
        {
            RandomPropertiesID = randomPropertiesId;
            RandomPropertiesSeed = randomPropertiesSeed;
        }

        public int RandomPropertiesID;
        public int RandomPropertiesSeed;
    }
}
