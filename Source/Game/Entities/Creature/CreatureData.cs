// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using System;
using System.Collections.Generic;
using Game.Networking.Packets;
using Game.Maps;
using Game.DataStorage;

namespace Game.Entities
{
    public class CreatureTemplate
    {
        public int Entry;
        public int[] KillCredit = new int[SharedConst.MaxCreatureKillCredit];
        public List<CreatureModel> Models = new();
        public string Name;
        public string FemaleName;
        public string SubName;
        public string TitleAlt;
        public string IconName;
        public List<int> GossipMenuIds = new();
        public Dictionary<Difficulty, CreatureDifficulty> difficultyStorage = new();
        public Expansion RequiredExpansion;
        public int VignetteID; // @todo Read Vignette.db2
        public int Faction;
        public NPCFlags1 Npcflag;
        public NPCFlags2 Npcflag2;
        public float SpeedWalk;
        public float SpeedRun;
        public float Scale;
        public CreatureEliteType Rank;
        public SpellSchools DmgSchool;
        public uint BaseAttackTime;
        public uint RangeAttackTime;
        public float BaseVariance;
        public float RangeVariance;
        public Class UnitClass;
        public UnitFlags UnitFlags;
        public UnitFlags2 UnitFlags2;
        public UnitFlags3 UnitFlags3;
        public CreatureFamily Family;
        public Class TrainerClass;
        public CreatureType CreatureType;
        public int PetSpellDataId;
        public int[] Resistance = new int[(int)SpellSchools.Max];
        public int[] Spells = new int[SharedConst.MaxCreatureSpells];
        public int VehicleId;
        public string AIName = string.Empty;
        public MovementGeneratorType MovementType;
        public CreatureMovementData Movement = new();
        public float ModExperience;
        public bool Civilian;
        public bool RacialLeader;
        public int MovementId;
        public int WidgetSetID;
        public int WidgetSetUnitConditionID;
        public bool RegenHealth;
        public ulong MechanicImmuneMask;
        public SpellSchoolMask SpellSchoolImmuneMask;
        public CreatureFlagsExtra FlagsExtra;
        public int ScriptID;
        public string StringId;

        public QueryCreatureResponse[] QueryData = new QueryCreatureResponse[(int)Locale.Total];

        public CreatureModel GetModelByIdx(int idx)
        {
            return idx < Models.Count ? Models[idx] : null;
        }

        public CreatureModel GetRandomValidModel()
        {
            if (Models.Empty())
                return null;

            // If only one element, ignore the Probability (even if 0)
            if (Models.Count == 1)
                return Models[0];

            var selectedItr = Models.SelectRandomElementByWeight(model =>
            {
                return model.Probability;
            });

            return selectedItr;
        }

        public CreatureModel GetFirstValidModel()
        {
            foreach (CreatureModel model in Models)
                if (model.CreatureDisplayID != 0)
                    return model;

            return null;
        }

        public CreatureModel GetModelWithDisplayId(int displayId)
        {
            foreach (CreatureModel model in Models)
                if (displayId == model.CreatureDisplayID)
                    return model;

            return null;
        }

        public CreatureModel GetFirstInvisibleModel()
        {
            foreach (CreatureModel model in Models)
            {
                CreatureModelInfo modelInfo = Global.ObjectMgr.GetCreatureModelInfo(model.CreatureDisplayID);
                if (modelInfo != null && modelInfo.IsTrigger)
                    return model;
            }

            return CreatureModel.DefaultInvisibleModel;
        }

        public CreatureModel GetFirstVisibleModel()
        {
            foreach (CreatureModel model in Models)
            {
                CreatureModelInfo modelInfo = Global.ObjectMgr.GetCreatureModelInfo(model.CreatureDisplayID);
                if (modelInfo != null && !modelInfo.IsTrigger)
                    return model;
            }

            return CreatureModel.DefaultVisibleModel;
        }

        public bool IsExotic(CreatureDifficulty creatureDifficulty)
        {
            return creatureDifficulty.TypeFlags.HasFlag(CreatureTypeFlags.TameableExotic);
        }
        public bool IsTameable(bool canTameExotic, CreatureDifficulty creatureDifficulty)
        {
            if (CreatureType != CreatureType.Beast || Family == CreatureFamily.None || !creatureDifficulty.TypeFlags.HasFlag(CreatureTypeFlags.Tameable))
                return false;

            // if can tame exotic then can tame any tameable
            return canTameExotic || !IsExotic(creatureDifficulty);
        }

        public void InitializeQueryData()
        {
            for (var loc = Locale.enUS; loc < Locale.Total; ++loc)
                QueryData[(int)loc] = BuildQueryData(loc, Difficulty.None);
        }

        public QueryCreatureResponse BuildQueryData(Locale locale, Difficulty difficulty)
        {
            CreatureDifficulty creatureDifficulty = GetDifficulty(difficulty);

            var queryTemp = new QueryCreatureResponse();

            queryTemp.CreatureID = Entry;
            queryTemp.Allow = true;

            CreatureStats stats = new();
            stats.Civilian = Civilian;
            stats.Leader = RacialLeader;

            stats.Name[0] = Name;
            stats.NameAlt[0] = FemaleName;

            stats.Flags[0] = (uint)creatureDifficulty.TypeFlags;
            stats.Flags[1] = creatureDifficulty.TypeFlags2;

            stats.CreatureType = CreatureType;
            stats.CreatureFamily = Family;
            stats.Classification = Rank;
            stats.PetSpellDataId = PetSpellDataId;

            for (int i = 0; i < SharedConst.MaxCreatureKillCredit; ++i)
                stats.ProxyCreatureID[i] = KillCredit[i];

            foreach (var model in Models)
            {
                stats.Display.TotalProbability += model.Probability;
                stats.Display.CreatureDisplay.Add(new CreatureXDisplay(model.CreatureDisplayID, model.DisplayScale, model.Probability));
            }

            stats.HpMulti = creatureDifficulty.HealthModifier;
            stats.EnergyMulti = creatureDifficulty.ManaModifier;

            stats.CreatureMovementInfoID = MovementId;
            stats.RequiredExpansion = RequiredExpansion;
            stats.HealthScalingExpansion = creatureDifficulty.HealthScalingExpansion;
            stats.VignetteID = VignetteID;
            stats.Class = UnitClass;
            stats.CreatureDifficultyID = creatureDifficulty.CreatureDifficultyID;
            stats.WidgetSetID = WidgetSetID;
            stats.WidgetSetUnitConditionID = WidgetSetUnitConditionID;

            stats.Title = SubName;
            stats.TitleAlt = TitleAlt;
            stats.CursorName = IconName;

            if (Global.ObjectMgr.GetCreatureQuestItemList(Entry, difficulty) is List<int> items)
                stats.QuestItems.AddRange(items);

            if (locale != Locale.Default)
            {
                CreatureLocale creatureLocale = Global.ObjectMgr.GetCreatureLocale(Entry);
                if (creatureLocale != null)
                {
                    string name = stats.Name[0];
                    string nameAlt = stats.NameAlt[0];

                    ObjectManager.GetLocaleString(creatureLocale.Name, locale, ref name);
                    ObjectManager.GetLocaleString(creatureLocale.NameAlt, locale, ref nameAlt);
                    ObjectManager.GetLocaleString(creatureLocale.Title, locale, ref stats.Title);
                    ObjectManager.GetLocaleString(creatureLocale.TitleAlt, locale, ref stats.TitleAlt);
                }
            }

            queryTemp.Stats = stats;
            return queryTemp;
        }

        public CreatureDifficulty GetDifficulty(Difficulty difficulty)
        {
            var creatureDifficulty = difficultyStorage.LookupByKey(difficulty);
            if (creatureDifficulty != null)
                return creatureDifficulty;

            // If there is no data for the difficulty, try to get data for the fallback difficulty
            var difficultyEntry = CliDB.DifficultyStorage.LookupByKey((int)difficulty);
            if (difficultyEntry != null)
                return GetDifficulty(difficultyEntry.FallbackDifficultyID);

            return new CreatureDifficulty();
        }
    }

    public class CreatureBaseStats
    {
        public int[] BaseHealth = new int[(int)Expansion.Current + 1];
        public int BaseMana;
        public int BaseArmor;
        public int AttackPower;
        public int RangedAttackPower;
        public float[] BaseDamage = new float[(int)Expansion.Current + 1];

        //Helpers
        public int GenerateHealth(CreatureDifficulty difficulty)
        { 
            return (int)Math.Ceiling(BaseHealth[(int)difficulty.GetHealthScalingExpansion()] * difficulty.HealthModifier); 
        }

        public int GenerateMana(CreatureDifficulty difficulty)
        {
            // Mana can be 0.
            if (BaseMana == 0)
                return 0;

            return (int)Math.Ceiling(BaseMana * difficulty.ManaModifier);
        }

        public int GenerateArmor(CreatureDifficulty difficulty)
        { 
            return (int)Math.Ceiling(BaseArmor * difficulty.ArmorModifier); 
        }

        public float GenerateBaseDamage(CreatureDifficulty difficulty) 
        { 
            return BaseDamage[(int)difficulty.GetHealthScalingExpansion()]; 
        }
    }

    public class CreatureLocale
    {
        public StringArray Name = new((int)Locale.Total);
        public StringArray NameAlt = new((int)Locale.Total);
        public StringArray Title = new((int)Locale.Total);
        public StringArray TitleAlt = new((int)Locale.Total);
    }

    public struct EquipmentItem
    {
        public int ItemId;
        public ushort AppearanceModId;
        public ushort ItemVisual;
    }

    public class EquipmentInfo
    {
        public EquipmentItem[] Items = new EquipmentItem[SharedConst.MaxEquipmentItems];
    }

    public class CreatureData : SpawnData
    {
        public int displayid;
        public sbyte equipmentId;
        public float WanderDistance;
        public uint currentwaypoint;
        public int curhealth;
        public int curmana;
        public MovementGeneratorType movementType;
        public NPCFlags1? npcflag;
        public NPCFlags2? npcflag2;
        public UnitFlags? unit_flags;
        public UnitFlags2? unit_flags2;
        public UnitFlags3? unit_flags3;

        public CreatureData() : base(SpawnObjectType.Creature) { }
    }

    public class CreatureMovementData
    {
        public CreatureGroundMovementType Ground;
        public CreatureFlightMovementType Flight;
        public bool Swim;
        public bool Rooted;
        public CreatureChaseMovementType Chase;
        public CreatureRandomMovementType Random;
        public uint InteractionPauseTimer;

        public CreatureMovementData()
        {
            Ground = CreatureGroundMovementType.Run;
            Flight = CreatureFlightMovementType.None;
            Swim = true;
            Rooted = false;
            Chase = CreatureChaseMovementType.Run;
            Random = CreatureRandomMovementType.Walk;
            InteractionPauseTimer = WorldConfig.GetUIntValue(WorldCfg.CreatureStopForPlayer);
        }

        public bool IsGroundAllowed() { return Ground != CreatureGroundMovementType.None; }
        public bool IsSwimAllowed() { return Swim; }
        public bool IsFlightAllowed() { return Flight != CreatureFlightMovementType.None; }
        public bool IsRooted() { return Rooted; }

        public CreatureChaseMovementType GetChase() { return Chase; }
        public CreatureRandomMovementType GetRandom() { return Random; }

        public uint GetInteractionPauseTimer() { return InteractionPauseTimer; }

        public override string ToString()
        {
            return $"Ground: {Ground}, Swim: {Swim}, Flight: {Flight} {(Rooted ? ", Rooted" : "")}, Chase: {Chase}, Random: {Random}, InteractionPauseTimer: {InteractionPauseTimer}";
        }
    }

    public class CreatureModelInfo
    {
        public float BoundingRadius;
        public float CombatReach;
        public Gender gender;
        public int DisplayIdOtherGender;
        public bool IsTrigger;
    }

    public class CreatureModel
    {
        public static CreatureModel DefaultInvisibleModel = new(11686, 1.0f, 1.0f);
        public static CreatureModel DefaultVisibleModel = new(17519, 1.0f, 1.0f);

        public int CreatureDisplayID;
        public float DisplayScale;
        public float Probability;

        public CreatureModel() { }
        public CreatureModel(int creatureDisplayID, float displayScale, float probability)
        {
            CreatureDisplayID = creatureDisplayID;
            DisplayScale = displayScale;
            Probability = probability;
        }
    }

    public class CreatureSummonedData
    {
        public int? CreatureIDVisibleToSummoner;
        public int? GroundMountDisplayID;
        public int? FlyingMountDisplayID;
    }

    public class CreatureAddon
    {
        public int path_id;
        public int mount;
        public UnitStandStateType standState;
        public AnimTier animTier;
        public SheathState sheathState;
        public byte pvpFlags;
        public byte visFlags;
        public int emote;
        public ushort aiAnimKit;
        public ushort movementAnimKit;
        public ushort meleeAnimKit;
        public List<int> auras = new();
        public VisibilityDistanceType visibilityDistanceType;
    }

    public class VendorItem
    {
        public VendorItem() { }
        public VendorItem(int _item, int _maxcount, uint _incrtime, int _ExtendedCost, ItemVendorType _Type)
        {
            item = _item;
            maxcount = _maxcount;
            incrtime = _incrtime;
            ExtendedCost = _ExtendedCost;
            Type = _Type;
        }

        public int item;
        public int maxcount;                                        // 0 for infinity item amount
        public uint incrtime;                                        // time for restore items amount if maxcount != 0
        public int ExtendedCost;
        public ItemVendorType Type;
        public List<int> BonusListIDs = new();
        public int PlayerConditionId;
        public bool IgnoreFiltering;
    }

    public class VendorItemData
    {
        List<VendorItem> m_items = new();

        public VendorItem GetItem(int slot)
        {
            if (slot >= m_items.Count)
                return null;

            return m_items[(int)slot];
        }

        public bool Empty()
        {
            return m_items.Count == 0;
        }

        public int GetItemCount()
        {
            return m_items.Count;
        }

        public void AddItem(VendorItem vItem)
        {
            m_items.Add(vItem);
        }

        public bool RemoveItem(int item_id, ItemVendorType type)
        {
            int i = m_items.RemoveAll(p => p.item == item_id && p.Type == type);
            if (i == 0)
                return false;
            else
                return true;
        }

        public VendorItem FindItemCostPair(int item_id, int extendedCost, ItemVendorType type)
        {
            return m_items.Find(p => p.item == item_id && p.ExtendedCost == extendedCost && p.Type == type);
        }

        public void Clear()
        {
            m_items.Clear();
        }
    }

    public class CreatureDifficulty
    {
        public byte MinLevel;
        public byte MaxLevel;
        public Expansion HealthScalingExpansion;
        public float HealthModifier;
        public float ManaModifier;
        public float ArmorModifier;
        public float DamageModifier;
        public int CreatureDifficultyID;
        public CreatureTypeFlags TypeFlags;
        public uint TypeFlags2;
        public int LootID;
        public int PickPocketLootID;
        public int SkinLootID;
        public int GoldMin;
        public int GoldMax;
        public CreatureStaticFlagsHolder StaticFlags;

        public CreatureDifficulty()
        {
            MinLevel = 1;
            MaxLevel = 1;
            HealthModifier = 1.0f;
            ManaModifier = 1.0f;
            ArmorModifier = 1.0f;
            DamageModifier = 1.0f;
        }

        // Helpers
        public Expansion GetHealthScalingExpansion()
        {
            return HealthScalingExpansion == Expansion.LevelCurrent ? PlayerConst.CurrentExpansion : HealthScalingExpansion;
        }

        public SkillType GetRequiredLootSkill()
        {
            if (TypeFlags.HasFlag(CreatureTypeFlags.SkinWithHerbalism))
                return SkillType.Herbalism;
            else if (TypeFlags.HasFlag(CreatureTypeFlags.SkinWithMining))
                return SkillType.Mining;
            else if (TypeFlags.HasFlag(CreatureTypeFlags.SkinWithEngineering))
                return SkillType.Engineering;
            else
                return SkillType.Skinning; // Default case
        }
    }
}
