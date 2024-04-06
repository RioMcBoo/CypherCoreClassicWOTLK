// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.DataStorage;
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
            SQLResult result = DB.World.Query("SELECT Id, EnchantmentId, Chance FROM item_random_enchantment_template");

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

        public static ItemRandomProperties GenerateRandomProperties(int item_id)
        {
            var properties = new ItemRandomProperties();

            ItemTemplate itemProto = Global.ObjectMgr.GetItemTemplate(item_id);
            if (itemProto == null)
                return properties;

            var randomSelect = itemProto.GetRandomSelect();
            var randomSuffix = itemProto.GetRandomSuffixGroupID();

            if (randomSelect == 0 && randomSuffix == 0)
                return properties;

            if (randomSelect != 0)
            {
                var tab = RandomEnchantmentData.LookupByKey(randomSelect);
                if (tab == null)
                {
                    Log.outError(LogFilter.Sql, $"Item RandomSelect Id {randomSelect} used but it does not have records in `item_random_enchantment_template` table.");
                    return properties;
                }

                var randomPropertiesId = tab.SelectRandomElementByWeight(x => x.Chance);
                var randomPropertiesEntry = CliDB.ItemRandomPropertiesStorage.LookupByKey(randomPropertiesId.EnchantmentID);
                if (randomPropertiesEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Enchantment Id {randomPropertiesId.EnchantmentID} used but it doesn't have records in 'ItemRandomProperties.db2'.");
                    return properties;
                }

                properties.RandomPropertiesID = randomPropertiesEntry.Id;
            }
            else if (randomSuffix != 0)
            {
                var tab = RandomEnchantmentData.LookupByKey(randomSuffix);
                if (tab == null)
                {
                    Log.outError(LogFilter.Sql, $"Item RandomSuffixGroupID Id {randomSuffix} used but it does not have records in `item_random_enchantment_template` table.");
                    return properties;
                }

                var randomSuffixId = tab.SelectRandomElementByWeight(x => x.Chance);
                var randomSuffixEntry = CliDB.ItemRandomSuffixStorage.LookupByKey(randomSuffixId.EnchantmentID);
                if (randomSuffixEntry == null)
                {
                    Log.outError(LogFilter.Sql, $"Enchantment id Id {randomSuffixId} used but it doesn't have records in 'ItemRandomSuffixEntry.db2'.");
                    return properties;
                }

                properties.RandomPropertiesID = randomSuffixEntry.Id;
                properties.RandomPropertiesSeed = GetRandomPropertyPoints(itemProto.GetItemLevel(), itemProto.GetQuality(), itemProto.GetInventoryType());
            }

            return properties;
        }

        public static Dictionary<int, List<(int EnchantmentID, float Chance)>> RandomEnchantmentData;
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
