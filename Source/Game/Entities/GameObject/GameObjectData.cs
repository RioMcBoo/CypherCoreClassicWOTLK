﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Game.Maps;
using Game.Networking.Packets;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Game.Entities
{
    [StructLayout(LayoutKind.Explicit)]
    public class GameObjectTemplate
    {
        [FieldOffset(0)]
        public int entry;

        [FieldOffset(4)]
        public GameObjectTypes type;

        [FieldOffset(8)]
        public int displayId;

        [FieldOffset(16)]
        public string name;

        [FieldOffset(24)]
        public string IconName;

        [FieldOffset(32)]
        public string castBarCaption;

        [FieldOffset(40)]
        public string unk1;

        [FieldOffset(48)]
        public float size;

        [FieldOffset(52)]
        public int ContentTuningId;

        [FieldOffset(56)]
        public string AIName;

        [FieldOffset(64)]
        public int ScriptId;

        [FieldOffset(72)]
        public string StringId;

        [FieldOffset(80)]
        public door Door;

        [FieldOffset(80)]
        public button Button;

        [FieldOffset(80)]
        public questgiver QuestGiver;

        [FieldOffset(80)]
        public chest Chest;

        [FieldOffset(80)]
        public binder Binder;

        [FieldOffset(80)]
        public generic Generic;

        [FieldOffset(80)]
        public trap Trap;

        [FieldOffset(80)]
        public chair Chair;

        [FieldOffset(80)]
        public spellFocus SpellFocus;

        [FieldOffset(80)]
        public text Text;

        [FieldOffset(80)]
        public goober Goober;

        [FieldOffset(80)]
        public transport Transport;

        [FieldOffset(80)]
        public areadamage AreaDamage;

        [FieldOffset(80)]
        public camera Camera;

        [FieldOffset(80)]
        public moTransport MoTransport;

        [FieldOffset(80)]
        public duelflag DuelFlag;

        [FieldOffset(80)]
        public fishingnode FishingNode;

        [FieldOffset(80)]
        public ritual Ritual;

        [FieldOffset(80)]
        public mailbox MailBox;

        [FieldOffset(80)]
        public guardpost GuardPost;

        [FieldOffset(80)]
        public spellcaster SpellCaster;

        [FieldOffset(80)]
        public meetingstone MeetingStone;

        [FieldOffset(80)]
        public flagstand FlagStand;

        [FieldOffset(80)]
        public fishinghole FishingHole;

        [FieldOffset(80)]
        public flagdrop FlagDrop;

        [FieldOffset(80)]
        public controlzone ControlZone;

        [FieldOffset(80)]
        public auraGenerator AuraGenerator;

        [FieldOffset(80)]
        public dungeonDifficulty DungeonDifficulty;

        [FieldOffset(80)]
        public barberChair BarberChair;

        [FieldOffset(80)]
        public destructiblebuilding DestructibleBuilding;

        [FieldOffset(80)]
        public guildbank GuildBank;

        [FieldOffset(80)]
        public trapDoor TrapDoor;

        [FieldOffset(80)]
        public newflag NewFlag;

        [FieldOffset(80)]
        public newflagdrop NewFlagDrop;

        [FieldOffset(80)]
        public garrisonbuilding GarrisonBuilding;

        [FieldOffset(80)]
        public garrisonplot GarrisonPlot;

        [FieldOffset(80)]
        public clientcreature ClientCreature;

        [FieldOffset(80)]
        public clientitem ClientItem;

        [FieldOffset(80)]
        public capturepoint CapturePoint;

        [FieldOffset(80)]
        public phaseablemo PhaseableMO;

        [FieldOffset(80)]
        public garrisonmonument GarrisonMonument;

        [FieldOffset(80)]
        public garrisonshipment GarrisonShipment;

        [FieldOffset(80)]
        public garrisonmonumentplaque GarrisonMonumentPlaque;

        [FieldOffset(80)]
        public itemforge ItemForge;

        [FieldOffset(80)]
        public uilink UILink;

        [FieldOffset(80)]
        public keystonereceptacle KeystoneReceptacle;

        [FieldOffset(80)]
        public gatheringnode GatheringNode;

        [FieldOffset(80)]
        public challengemodereward ChallengeModeReward;

        [FieldOffset(80)]
        public multi Multi;

        [FieldOffset(80)]
        public siegeableMulti SiegeableMulti;

        [FieldOffset(80)]
        public siegeableMO SiegeableMO;

        [FieldOffset(80)]
        public pvpReward PvpReward;

        [FieldOffset(80)]
        public playerchoicechest PlayerChoiceChest;

        [FieldOffset(80)]
        public legendaryforge LegendaryForge;

        [FieldOffset(80)]
        public garrtalenttree GarrTalentTree;

        [FieldOffset(80)]
        public weeklyrewardchest WeeklyRewardChest;

        [FieldOffset(80)]
        public clientmodel ClientModel;

        [FieldOffset(80)]
        public craftingTable CraftingTable;

        [FieldOffset(80)]
        public raw Raw;

        [FieldOffset(224)]
        public QueryGameObjectResponse QueryData;

        // helpers
        public bool IsDespawnAtAction()
        {
            switch (type)
            {
                case GameObjectTypes.Chest:
                    return Chest.consumable != 0;
                case GameObjectTypes.Goober:
                    return Goober.consumable != 0;
                default:
                    return false;
            }
        }

        public bool IsUsableMounted()
        {
            switch (type)
            {
                case GameObjectTypes.Mailbox:
                    return true;
                case GameObjectTypes.BarberChair:
                    return false;
                case GameObjectTypes.QuestGiver:
                    return QuestGiver.allowMounted != 0;
                case GameObjectTypes.Text:
                    return Text.allowMounted != 0;
                case GameObjectTypes.Goober:
                    return Goober.allowMounted != 0;
                case GameObjectTypes.SpellCaster:
                    return SpellCaster.allowMounted != 0;
                case GameObjectTypes.UILink:
                    return UILink.allowMounted != 0;
                default:
                    return false;
            }
        }

        public int GetConditionID1() => type switch
        {
            GameObjectTypes.Door => Door.conditionID1,
            GameObjectTypes.Button => Button.conditionID1,
            GameObjectTypes.QuestGiver => QuestGiver.conditionID1,
            GameObjectTypes.Chest => Chest.conditionID1,
            GameObjectTypes.Generic => Generic.conditionID1,
            GameObjectTypes.Trap => Trap.conditionID1,
            GameObjectTypes.Chair => Chair.conditionID1,
            GameObjectTypes.SpellFocus => SpellFocus.conditionID1,
            GameObjectTypes.Text => Text.conditionID1,
            GameObjectTypes.Goober => Goober.conditionID1,
            GameObjectTypes.Camera => Camera.conditionID1,
            GameObjectTypes.Ritual => Ritual.conditionID1,
            GameObjectTypes.Mailbox => MailBox.conditionID1,
            GameObjectTypes.SpellCaster => SpellCaster.conditionID1,
            GameObjectTypes.FlagStand => FlagStand.conditionID1,
            GameObjectTypes.AuraGenerator => AuraGenerator.conditionID1,
            GameObjectTypes.GuildBank => GuildBank.conditionID1,
            GameObjectTypes.NewFlag => NewFlag.conditionID1,
            GameObjectTypes.ItemForge => ItemForge.conditionID1,
            GameObjectTypes.GatheringNode => GatheringNode.conditionID1,
            _ => 0,
        };

        public int GetInteractRadiusOverride() => type switch
        {

            GameObjectTypes.Door => Door.InteractRadiusOverride,
            GameObjectTypes.Button => Button.InteractRadiusOverride,
            GameObjectTypes.QuestGiver => QuestGiver.InteractRadiusOverride,
            GameObjectTypes.Chest => Chest.InteractRadiusOverride,
            GameObjectTypes.Binder => Binder.InteractRadiusOverride,
            GameObjectTypes.Generic => Generic.InteractRadiusOverride,
            GameObjectTypes.Trap => Trap.InteractRadiusOverride,
            GameObjectTypes.Chair => Chair.InteractRadiusOverride,
            GameObjectTypes.SpellFocus => SpellFocus.InteractRadiusOverride,
            GameObjectTypes.Text => Text.InteractRadiusOverride,
            GameObjectTypes.Goober => Goober.InteractRadiusOverride,
            GameObjectTypes.Transport => Transport.InteractRadiusOverride,
            GameObjectTypes.AreaDamage => AreaDamage.InteractRadiusOverride,
            GameObjectTypes.Camera => Camera.InteractRadiusOverride,
            GameObjectTypes.MapObjTransport => MoTransport.InteractRadiusOverride,
            GameObjectTypes.DuelArbiter => DuelFlag.InteractRadiusOverride,
            GameObjectTypes.FishingNode => FishingNode.InteractRadiusOverride,
            GameObjectTypes.Ritual => Ritual.InteractRadiusOverride,
            GameObjectTypes.Mailbox => MailBox.InteractRadiusOverride,
            GameObjectTypes.GuardPost => GuardPost.InteractRadiusOverride,
            GameObjectTypes.SpellCaster => SpellCaster.InteractRadiusOverride,
            GameObjectTypes.MeetingStone => MeetingStone.InteractRadiusOverride,
            GameObjectTypes.FlagStand => FlagStand.InteractRadiusOverride,
            GameObjectTypes.FishingHole => FishingHole.InteractRadiusOverride,
            GameObjectTypes.FlagDrop => FlagDrop.InteractRadiusOverride,
            GameObjectTypes.ControlZone => ControlZone.InteractRadiusOverride,
            GameObjectTypes.AuraGenerator => AuraGenerator.InteractRadiusOverride,
            GameObjectTypes.DungeonDifficulty => DungeonDifficulty.InteractRadiusOverride,
            GameObjectTypes.BarberChair => BarberChair.InteractRadiusOverride,
            GameObjectTypes.DestructibleBuilding => DestructibleBuilding.InteractRadiusOverride,
            GameObjectTypes.GuildBank => GuildBank.InteractRadiusOverride,
            GameObjectTypes.TrapDoor => TrapDoor.InteractRadiusOverride,
            GameObjectTypes.NewFlag => NewFlag.InteractRadiusOverride,
            GameObjectTypes.NewFlagDrop => NewFlagDrop.InteractRadiusOverride,
            GameObjectTypes.GarrisonBuilding => GarrisonBuilding.InteractRadiusOverride,
            GameObjectTypes.GarrisonPlot => GarrisonPlot.InteractRadiusOverride,
            GameObjectTypes.CapturePoint => CapturePoint.InteractRadiusOverride,
            GameObjectTypes.PhaseableMo => PhaseableMO.InteractRadiusOverride,
            GameObjectTypes.GarrisonMonument => GarrisonMonument.InteractRadiusOverride,
            GameObjectTypes.GarrisonShipment => GarrisonShipment.InteractRadiusOverride,
            GameObjectTypes.GarrisonMonumentPlaque => GarrisonMonumentPlaque.InteractRadiusOverride,
            GameObjectTypes.ItemForge => ItemForge.InteractRadiusOverride,
            GameObjectTypes.UILink => UILink.InteractRadiusOverride,
            GameObjectTypes.KeystoneReceptacle => KeystoneReceptacle.InteractRadiusOverride,
            GameObjectTypes.GatheringNode => GatheringNode.InteractRadiusOverride,
            GameObjectTypes.ChallengeModeReward => ChallengeModeReward.InteractRadiusOverride,
            GameObjectTypes.SiegeableMo => SiegeableMO.InteractRadiusOverride,
            GameObjectTypes.PvpReward => PvpReward.InteractRadiusOverride,
            GameObjectTypes.PlayerChoiceChest => PlayerChoiceChest.InteractRadiusOverride,
            GameObjectTypes.LegendaryForge => LegendaryForge.InteractRadiusOverride,
            GameObjectTypes.GarrTalentTree => GarrTalentTree.InteractRadiusOverride,
            GameObjectTypes.WeeklyRewardChest => WeeklyRewardChest.InteractRadiusOverride,
            _ => 0
        };

        public int GetRequireLOS() => type switch
        {
            GameObjectTypes.Button => Button.requireLOS,
            GameObjectTypes.QuestGiver => QuestGiver.requireLOS,
            GameObjectTypes.Chest => Chest.requireLOS,
            GameObjectTypes.Trap => Trap.requireLOS,
            GameObjectTypes.Goober => Goober.requireLOS,
            GameObjectTypes.FlagStand => FlagStand.requireLOS,
            GameObjectTypes.NewFlag => NewFlag.requireLOS,
            GameObjectTypes.GatheringNode => GatheringNode.requireLOS,
            _ => 0,
        };

        public int GetLockId() => type switch
        {
            GameObjectTypes.Door => Door.open,
            GameObjectTypes.Button => Button.open,
            GameObjectTypes.QuestGiver => QuestGiver.open,
            GameObjectTypes.Chest => Chest.open,
            GameObjectTypes.Trap => Trap.open,
            GameObjectTypes.Goober => Goober.open,
            GameObjectTypes.AreaDamage => AreaDamage.open,
            GameObjectTypes.Camera => Camera.open,
            GameObjectTypes.FlagStand => FlagStand.open,
            GameObjectTypes.FishingHole => FishingHole.open,
            GameObjectTypes.FlagDrop => FlagDrop.open,
            GameObjectTypes.NewFlag => NewFlag.open,
            GameObjectTypes.NewFlagDrop => NewFlagDrop.open,
            GameObjectTypes.CapturePoint => CapturePoint.open,
            GameObjectTypes.GatheringNode => GatheringNode.open,
            GameObjectTypes.ChallengeModeReward => ChallengeModeReward.open,
            GameObjectTypes.PvpReward => PvpReward.open,
            _ => 0,
        };

        // despawn at targeting of cast?
        public bool GetDespawnPossibility()
        {
            switch (type)
            {
                case GameObjectTypes.Door:
                    return Door.noDamageImmune != 0;
                case GameObjectTypes.Button:
                    return Button.noDamageImmune != 0;
                case GameObjectTypes.QuestGiver:
                    return QuestGiver.noDamageImmune != 0;
                case GameObjectTypes.Goober:
                    return Goober.noDamageImmune != 0;
                case GameObjectTypes.FlagStand:
                    return FlagStand.noDamageImmune != 0;
                case GameObjectTypes.FlagDrop:
                    return FlagDrop.noDamageImmune != 0;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Cannot be used/activated/looted by players under immunity effects (example: Divine Shield)
        /// </summary>
        /// <returns></returns>
        public int GetNoDamageImmune()
        {
            switch (type)
            {
                case GameObjectTypes.Door:
                    return Door.noDamageImmune;
                case GameObjectTypes.Button:
                    return Button.noDamageImmune;
                case GameObjectTypes.QuestGiver:
                    return QuestGiver.noDamageImmune;
                case GameObjectTypes.Chest:
                    return Chest.DamageImmuneOK == 0 ? 1 : 0;
                case GameObjectTypes.Goober:
                    return Goober.noDamageImmune;
                case GameObjectTypes.FlagStand:
                    return FlagStand.noDamageImmune;
                case GameObjectTypes.FlagDrop:
                    return FlagDrop.noDamageImmune;
                default:
                    return 0;
            }
        }

        public int GetNotInCombat() => type switch
        {
            GameObjectTypes.Chest => Chest.notInCombat,
            GameObjectTypes.GatheringNode => GatheringNode.notInCombat,
            _ => 0,
        };

        /// <summary>
        /// despawn at uses amount
        /// </summary>
        /// <returns></returns>
        public int GetCharges()
        {
            switch (type)
            {
                case GameObjectTypes.GuardPost:
                    return GuardPost.charges;
                case GameObjectTypes.SpellCaster:
                    return SpellCaster.charges;
                default:
                    return 0;
            }
        }

        public int GetLinkedGameObjectEntry()
        {
            switch (type)
            {
                case GameObjectTypes.Button:
                    return Button.linkedTrap;
                case GameObjectTypes.Chest:
                    return Chest.linkedTrap;
                case GameObjectTypes.SpellFocus:
                    return SpellFocus.linkedTrap;
                case GameObjectTypes.Goober:
                    return Goober.linkedTrap;
                case GameObjectTypes.GatheringNode:
                    return GatheringNode.linkedTrap;
                default: return 0;
            }
        }

        public Milliseconds GetAutoCloseTime()
        {
            Milliseconds autoCloseTime = Milliseconds.Zero;
            switch (type)
            {
                case GameObjectTypes.Door:
                    autoCloseTime = Door.autoClose;
                    break;
                case GameObjectTypes.Button:
                    autoCloseTime = Button.autoClose;
                    break;
                case GameObjectTypes.Trap:
                    autoCloseTime = Trap.autoClose;
                    break;
                case GameObjectTypes.Goober:
                    autoCloseTime = Goober.autoClose;
                    break;
                case GameObjectTypes.Transport:
                    autoCloseTime = Transport.autoClose;
                    break;
                case GameObjectTypes.AreaDamage:
                    autoCloseTime = AreaDamage.autoClose;
                    break;
                case GameObjectTypes.TrapDoor:
                    autoCloseTime = TrapDoor.autoClose;
                    break;
                default:
                    break;
            }
            return autoCloseTime;   // prior to 3.0.3, conversion was / 0x10000;
        }

        public int GetLootId()
        {
            switch (type)
            {
                case GameObjectTypes.Chest:
                    return Chest.chestLoot;
                case GameObjectTypes.FishingHole:
                    return FishingHole.chestLoot;
                case GameObjectTypes.GatheringNode:
                    return GatheringNode.chestLoot;
                default: return 0;
            }
        }

        public int GetGossipMenuId()
        {
            switch (type)
            {
                case GameObjectTypes.QuestGiver:
                    return QuestGiver.gossipID;
                case GameObjectTypes.Goober:
                    return Goober.gossipID;
                default:
                    return 0;
            }
        }

        public List<int> GetEventScriptSet()
        {
            List<int> eventSet = new();
            switch (type)
            {
                case GameObjectTypes.Chest:
                    eventSet.Add(Chest.triggeredEvent);
                    break;
                case GameObjectTypes.Chair:
                    eventSet.Add(Chair.triggeredEvent);
                    break;
                case GameObjectTypes.Goober:
                    eventSet.Add(Goober.eventID);
                    break;
                case GameObjectTypes.Transport:
                    eventSet.Add(Transport.Reached1stfloor);
                    eventSet.Add(Transport.Reached2ndfloor);
                    eventSet.Add(Transport.Reached3rdfloor);
                    eventSet.Add(Transport.Reached4thfloor);
                    eventSet.Add(Transport.Reached5thfloor);
                    eventSet.Add(Transport.Reached6thfloor);
                    eventSet.Add(Transport.Reached7thfloor);
                    eventSet.Add(Transport.Reached8thfloor);
                    eventSet.Add(Transport.Reached9thfloor);
                    eventSet.Add(Transport.Reached10thfloor);
                    break;
                case GameObjectTypes.Camera:
                    eventSet.Add(Camera.eventID);
                    break;
                case GameObjectTypes.MapObjTransport:
                    eventSet.Add(MoTransport.startEventID);
                    eventSet.Add(MoTransport.stopEventID);
                    break;
                case GameObjectTypes.FlagDrop:
                    eventSet.Add(FlagDrop.eventID);
                    break;
                case GameObjectTypes.ControlZone:
                    eventSet.Add(ControlZone.CaptureEventHorde);
                    eventSet.Add(ControlZone.CaptureEventAlliance);
                    eventSet.Add(ControlZone.ContestedEventHorde);
                    eventSet.Add(ControlZone.ContestedEventAlliance);
                    eventSet.Add(ControlZone.ProgressEventHorde);
                    eventSet.Add(ControlZone.ProgressEventAlliance);
                    eventSet.Add(ControlZone.NeutralEventHorde);
                    eventSet.Add(ControlZone.NeutralEventAlliance);
                    break;
                case GameObjectTypes.DestructibleBuilding:
                    eventSet.Add(DestructibleBuilding.IntactEvent);
                    eventSet.Add(DestructibleBuilding.DamagedEvent);
                    eventSet.Add(DestructibleBuilding.DestroyedEvent);
                    eventSet.Add(DestructibleBuilding.RebuildingEvent);
                    eventSet.Add(DestructibleBuilding.DamageEvent);
                    break;
                case GameObjectTypes.CapturePoint:
                    eventSet.Add(CapturePoint.ContestedEventHorde);
                    eventSet.Add(CapturePoint.CaptureEventHorde);
                    eventSet.Add(CapturePoint.DefendedEventHorde);
                    eventSet.Add(CapturePoint.ContestedEventAlliance);
                    eventSet.Add(CapturePoint.CaptureEventAlliance);
                    eventSet.Add(CapturePoint.DefendedEventAlliance);
                    break;
                case GameObjectTypes.GatheringNode:
                    eventSet.Add(GatheringNode.triggeredEvent);
                    break;
                default:
                    break;
            }

            // Erase invalid value added from unused GameEvents data fields
            eventSet.Remove(0);

            return eventSet;
        }

        public int GetTrivialSkillHigh() => type switch
        {
            GameObjectTypes.Chest => Chest.trivialSkillHigh,
            GameObjectTypes.GatheringNode => GatheringNode.trivialSkillHigh,
            _ => 0,
        };

        public int GetTrivialSkillLow() => type switch
        {
            GameObjectTypes.Chest => Chest.trivialSkillLow,
            GameObjectTypes.GatheringNode => GatheringNode.trivialSkillLow,
            _ => 0,
        };

        // Cooldown preventing goober and traps to cast spell
        public Seconds GetCooldown()
        {
            switch (type)
            {
                case GameObjectTypes.Trap:
                    return Trap.cooldown;
                case GameObjectTypes.Goober:
                    return Goober.cooldown;
                default:
                    return Seconds.Zero;
            }
        }

        public bool IsInfiniteGameObject()
        {
            switch (type)
            {
                case GameObjectTypes.Door:
                    return Door.InfiniteAOI != 0;
                case GameObjectTypes.FlagStand:
                    return FlagStand.InfiniteAOI != 0;
                case GameObjectTypes.FlagDrop:
                    return FlagDrop.InfiniteAOI != 0;
                case GameObjectTypes.DestructibleBuilding:
                    return true;
                case GameObjectTypes.TrapDoor:
                    return TrapDoor.InfiniteAOI != 0;
                case GameObjectTypes.NewFlag:
                    return NewFlag.InfiniteAOI != 0;
                case GameObjectTypes.GarrisonBuilding:
                    return true;
                case GameObjectTypes.PhaseableMo:
                    return true;
                case GameObjectTypes.SiegeableMo:
                    return true;
                case GameObjectTypes.ClientModel:
                    return ClientModel.InfiniteAOI != 0;
                default:
                    return false;
            }
        }

        public bool IsGiganticGameObject()
        {
            switch (type)
            {
                case GameObjectTypes.Door:
                    return Door.GiganticAOI != 0;
                case GameObjectTypes.Button:
                    return Button.GiganticAOI != 0;
                case GameObjectTypes.QuestGiver:
                    return QuestGiver.GiganticAOI != 0;
                case GameObjectTypes.Chest:
                    return Chest.GiganticAOI != 0;
                case GameObjectTypes.Generic:
                    return Generic.GiganticAOI != 0;
                case GameObjectTypes.Trap:
                    return Trap.GiganticAOI != 0;
                case GameObjectTypes.SpellFocus:
                    return SpellFocus.GiganticAOI != 0;
                case GameObjectTypes.Goober:
                    return Goober.GiganticAOI != 0;
                case GameObjectTypes.Transport:
                    return true;
                case GameObjectTypes.SpellCaster:
                    return SpellCaster.GiganticAOI != 0;
                case GameObjectTypes.FlagStand:
                    return FlagStand.GiganticAOI != 0;
                case GameObjectTypes.FlagDrop:
                    return FlagDrop.GiganticAOI != 0;
                case GameObjectTypes.ControlZone:
                    return ControlZone.GiganticAOI != 0;
                case GameObjectTypes.DungeonDifficulty:
                    return DungeonDifficulty.GiganticAOI != 0;
                case GameObjectTypes.TrapDoor:
                    return TrapDoor.GiganticAOI != 0;
                case GameObjectTypes.NewFlag:
                    return NewFlag.GiganticAOI != 0;
                case GameObjectTypes.GarrisonPlot:
                    return true;
                case GameObjectTypes.CapturePoint:
                    return CapturePoint.GiganticAOI != 0;
                case GameObjectTypes.GarrisonShipment:
                    return GarrisonShipment.GiganticAOI != 0;
                case GameObjectTypes.UILink:
                    return UILink.GiganticAOI != 0;
                case GameObjectTypes.GatheringNode:
                    return GatheringNode.GiganticAOI != 0;
                default: return false;
            }
        }

        public bool IsLargeGameObject()
        {
            switch (type)
            {
                case GameObjectTypes.Chest:
                    return Chest.LargeAOI != 0;
                case GameObjectTypes.Generic:
                    return Generic.LargeAOI != 0;
                case GameObjectTypes.DungeonDifficulty:
                    return DungeonDifficulty.LargeAOI != 0;
                case GameObjectTypes.GarrisonShipment:
                    return GarrisonShipment.LargeAOI != 0;
                case GameObjectTypes.ItemForge:
                    return ItemForge.LargeAOI != 0;
                case GameObjectTypes.GatheringNode:
                    return GatheringNode.LargeAOI != 0;
                default: return false;
            }
        }

        public int GetServerOnly() => type switch
        {
            GameObjectTypes.Generic => Generic.serverOnly,
            GameObjectTypes.Trap => Trap.serverOnly,
            GameObjectTypes.SpellFocus => SpellFocus.serverOnly,
            GameObjectTypes.AuraGenerator => AuraGenerator.serverOnly,
            _ => 0,
        };
        
        public int GetSpellFocusType()
        {
            switch (type)
            {
                case GameObjectTypes.SpellFocus:
                    return SpellFocus.spellFocusType;
                case GameObjectTypes.UILink:
                    return UILink.spellFocusType;
                default:
                    return 0;
            }
        }

        public int GetSpellFocusRadius()
        {
            switch (type)
            {
                case GameObjectTypes.SpellFocus:
                    return SpellFocus.radius;
                case GameObjectTypes.UILink:
                    return UILink.radius;
                default:
                    return 0;
            }
        }

        public bool IsDisplayMandatory() => type switch
        {
            GameObjectTypes.SpellFocus or GameObjectTypes.Multi or GameObjectTypes.SiegeableMulti => false,
            _ => true
        };

        public void InitializeQueryData()
        {
            QueryData = new QueryGameObjectResponse();

            QueryData.GameObjectID = entry;
            QueryData.Allow = true;

            GameObjectStats stats = new();
            stats.Type = type;
            stats.DisplayID = displayId;

            stats.Name[0] = name;
            stats.IconName = IconName;
            stats.CastBarCaption = castBarCaption;
            stats.UnkString = unk1;

            stats.Size = size;

            var items = Global.ObjectMgr.GetGameObjectQuestItemList(entry);
            foreach (var item in items)
                stats.QuestItems.Add(item);

            unsafe
            {
                for (int i = 0; i < SharedConst.MaxGOData; i++)
                    stats.Data[i] = Raw.data[i];
            }

            stats.ContentTuningId = ContentTuningId;

            QueryData.Stats = stats;
        }

        #region TypeStructs
        public unsafe struct raw
        {
            public fixed int data[SharedConst.MaxGOData];
        }

        public struct door
        {
            public int startOpen;                               // 0 startOpen, enum { false, true, }; Default: false
            public int open;                                    // 1 open, References: Lock_, NoValue = 0
            public Milliseconds autoClose;                      // 2 autoClose (ms), int, Min value: 0, Max value: 2147483647, Default value: 3000
            public int noDamageImmune;                          // 3 noDamageImmune, enum { false, true, }; Default: false
            public uint openTextID;                              // 4 openTextID, References: BroadcastText, NoValue = 0
            public uint closeTextID;                             // 5 closeTextID, References: BroadcastText, NoValue = 0
            public uint ignoredByPathing;                        // 6 Ignored By Pathing, enum { false, true, }; Default: false
            public int conditionID1;                            // 7 conditionID1, References: PlayerCondition, NoValue = 0
            public uint DoorisOpaque;                            // 8 Door is Opaque (Disable portal on close), enum { false, true, }; Default: true
            public uint GiganticAOI;                             // 9 Gigantic AOI, enum { false, true, }; Default: false
            public uint InfiniteAOI;                             // 10 Infinite AOI, enum { false, true, }; Default: false
            public uint NotLOSBlocking;                          // 11 Not LOS Blocking, enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 12 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint Collisionupdatedelayafteropen;           // 13 Collision update delay(ms) after open, int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct button
        {
            public int startOpen;                               // 0 startOpen, enum { false, true, }; Default: false
            public int open;                                    // 1 open, References: Lock_, NoValue = 0
            public Milliseconds autoClose;                      // 2 autoClose (ms), int, Min value: 0, Max value: 2147483647, Default value: 3000
            public int linkedTrap;                              // 3 linkedTrap, References: GameObjects, NoValue = 0
            public int noDamageImmune;                          // 4 noDamageImmune, enum { false, true, }; Default: false
            public int GiganticAOI;                             // 5 Gigantic AOI, enum { false, true, }; Default: false
            public int openTextID;                              // 6 openTextID, References: BroadcastText, NoValue = 0
            public int closeTextID;                             // 7 closeTextID, References: BroadcastText, NoValue = 0
            public int requireLOS;                              // 8 require LOS, enum { false, true, }; Default: false
            public int conditionID1;                            // 9 conditionID1, References: PlayerCondition, NoValue = 0
            public int InteractRadiusOverride;                  // 10 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct questgiver
        {
            public int open;                                    // 0 open, References: Lock_, NoValue = 0
            public int questGiver;                              // 1 questGiver, References: QuestGiver, NoValue = 0
            public int pageMaterial;                            // 2 pageMaterial, References: PageTextMaterial, NoValue = 0
            public int gossipID;                                // 3 gossipID, References: Gossip, NoValue = 0
            public int customAnim;                              // 4 customAnim, int, Min value: 0, Max value: 4, Default value: 0
            public int noDamageImmune;                          // 5 noDamageImmune, enum { false, true, }; Default: false
            public int openTextID;                              // 6 openTextID, References: BroadcastText, NoValue = 0
            public int requireLOS;                              // 7 require LOS, enum { false, true, }; Default: false
            public int allowMounted;                            // 8 allowMounted, enum { false, true, }; Default: false
            public int GiganticAOI;                             // 9 Gigantic AOI, enum { false, true, }; Default: false
            public int conditionID1;                            // 10 conditionID1, References: PlayerCondition, NoValue = 0
            public int NeverUsableWhileMounted;                 // 11 Never Usable While Mounted, enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 12 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct chest
        {
            public int open;                                    // 0 open, References: Lock_, NoValue = 0
            public int chestLoot;                               // 1 chestLoot (legacy/classic), References: Treasure, NoValue = 0
            public Seconds chestRestockTime;                    // 2 chestRestockTime, int, Min value: 0, Max value: 1800000, Default value: 0
            public int consumable;                              // 3 consumable, enum { false, true, }; Default: false
            public int minRestock;                              // 4 minRestock, int, Min value: 0, Max value: 65535, Default value: 0
            public int maxRestock;                              // 5 maxRestock, int, Min value: 0, Max value: 65535, Default value: 0
            public int triggeredEvent;                          // 6 triggeredEvent, References: GameEvents, NoValue = 0
            public int linkedTrap;                              // 7 linkedTrap, References: GameObjects, NoValue = 0
            public int questID;                                 // 8 questID, References: QuestV2, NoValue = 0
            public int InteractRadiusOverride;                  // 9 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int requireLOS;                              // 10 require LOS, enum { false, true, }; Default: false
            public int leaveLoot;                               // 11 leaveLoot, enum { false, true, }; Default: false
            public int notInCombat;                             // 12 notInCombat, enum { false, true, }; Default: false
            public int logloot;                                 // 13 log loot, enum { false, true, }; Default: false
            public int openTextID;                              // 14 openTextID, References: BroadcastText, NoValue = 0
            public int usegrouplootrules;                       // 15 use group loot rules, enum { false, true, }; Default: false
            public int floatingTooltip;                         // 16 floatingTooltip, enum { false, true, }; Default: false
            public int conditionID1;                            // 17 conditionID1, References: PlayerCondition, NoValue = 0
            public int Unused;                                  // 18 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int xpDifficulty;                            // 19 xpDifficulty, enum { No Exp, Trivial, Very Small, Small, Substandard, Standard, High, Epic, Dungeon, 5, }; Default: No Exp
            public int Unused2;                                 // 20 Unused, int, Min value: 0, Max value: 123, Default value: 0
            public int GroupXP;                                 // 21 Group XP, enum { false, true, }; Default: false
            public int DamageImmuneOK;                          // 22 Damage Immune OK, enum { false, true, }; Default: false
            public int trivialSkillLow;                         // 23 trivialSkillLow, int, Min value: 0, Max value: 65535, Default value: 0
            public int trivialSkillHigh;                        // 24 trivialSkillHigh, int, Min value: 0, Max value: 65535, Default value: 0
            public int DungeonEncounter;                        // 25 Dungeon Encounter, References: DungeonEncounter, NoValue = 0
            public int spell;                                   // 26 spell, References: Spell, NoValue = 0
            public int GiganticAOI;                             // 27 Gigantic AOI, enum { false, true, }; Default: false
            public int LargeAOI;                                // 28 Large AOI, enum { false, true, }; Default: false
            public int SpawnVignette;                           // 29 Spawn Vignette, References: vignette, NoValue = 0
            public int chestPersonalLoot;                       // 30 chest Personal Loot, References: Treasure, NoValue = 0
            public int turnpersonallootsecurityoff;             // 31 turn personal loot security off, enum { false, true, }; Default: false
            public int ChestProperties;                         // 32 Chest Properties, References: ChestProperties, NoValue = 0
            public int chestPushLoot;                           // 33 chest Push Loot, References: Treasure, NoValue = 0
            public int ForceSingleLooter;                       // 34 Force Single Looter, enum { false, true, }; Default: false
        }

        public struct binder
        {
            public int InteractRadiusOverride;                  // 0 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct generic
        {
            public int floatingTooltip;                         // 0 floatingTooltip, enum { false, true, }; Default: false
            public int highlight;                               // 1 highlight, enum { false, true, }; Default: true
            public int serverOnly;                              // 2 serverOnly, enum { false, true, }; Default: false
            public int GiganticAOI;                             // 3 Gigantic AOI, enum { false, true, }; Default: false
            public int floatOnWater;                            // 4 floatOnWater, enum { false, true, }; Default: false
            public int questID;                                 // 5 questID, References: QuestV2, NoValue = 0
            public int conditionID1;                            // 6 conditionID1, References: PlayerCondition, NoValue = 0
            public int LargeAOI;                                // 7 Large AOI, enum { false, true, }; Default: false
            public int UseGarrisonOwnerGuildColors;             // 8 Use Garrison Owner Guild Colors, enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 9 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Allowunfriendlycrossfactionpartymemberstocollaborateonaritual;// 10 Allow unfriendly cross faction party members to collaborate on a ritual, enum { false, true, }; Default: false
        }

        public struct trap
        {
            public int open;                                    // 0 open, References: Lock_, NoValue = 0
            public int Unused;                                  // 1 Unused, int, Min value: 0, Max value: 65535, Default value: 0
            public int radius;                                  // 2 radius, int, Min value: 0, Max value: 100, Default value: 0
            public int spell;                                   // 3 spell, References: Spell, NoValue = 0
            public int charges;                                 // 4 charges, int, Min value: 0, Max value: 65535, Default value: 1
            public Seconds cooldown;                            // 5 cooldown, int, Min value: 0, Max value: 65535, Default value: 0
            public Milliseconds autoClose;                      // 6 autoClose (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public Seconds startDelay;                          // 7 startDelay, int, Min value: 0, Max value: 65535, Default value: 0
            public int serverOnly;                              // 8 serverOnly, enum { false, true, }; Default: false
            public int stealthed;                               // 9 stealthed, enum { false, true, }; Default: false
            public int GiganticAOI;                             // 10 Gigantic AOI, enum { false, true, }; Default: false
            public int stealthAffected;                         // 11 stealthAffected, enum { false, true, }; Default: false
            public int openTextID;                              // 12 openTextID, References: BroadcastText, NoValue = 0
            public int closeTextID;                             // 13 closeTextID, References: BroadcastText, NoValue = 0
            public int IgnoreTotems;                            // 14 Ignore Totems, enum { false, true, }; Default: false
            public int conditionID1;                            // 15 conditionID1, References: PlayerCondition, NoValue = 0
            public int playerCast;                              // 16 playerCast, enum { false, true, }; Default: false
            public int SummonerTriggered;                       // 17 Summoner Triggered, enum { false, true, }; Default: false
            public int requireLOS;                              // 18 require LOS, enum { false, true, }; Default: false
            public int TriggerCondition;                        // 19 Trigger Condition, References: PlayerCondition, NoValue = 0
            public int Checkallunits;                           // 20 Check all units (spawned traps only check players), enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 21 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct chair
        {
            public int chairslots;                              // 0 chairslots, int, Min value: 1, Max value: 5, Default value: 1
            public int chairheight;                             // 1 chairheight, int, Min value: 0, Max value: 2, Default value: 1
            public int onlyCreatorUse;                          // 2 onlyCreatorUse, enum { false, true, }; Default: false
            public int triggeredEvent;                          // 3 triggeredEvent, References: GameEvents, NoValue = 0
            public int conditionID1;                            // 4 conditionID1, References: PlayerCondition, NoValue = 0
            public int InteractRadiusOverride;                  // 5 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct spellFocus
        {
            public int spellFocusType;                          // 0 spellFocusType, References: SpellFocusObject, NoValue = 0
            public int radius;                                  // 1 radius, int, Min value: 0, Max value: 50, Default value: 10
            public int linkedTrap;                              // 2 linkedTrap, References: GameObjects, NoValue = 0
            public int serverOnly;                              // 3 serverOnly, enum { false, true, }; Default: false
            public int questID;                                 // 4 questID, References: QuestV2, NoValue = 0
            public uint GiganticAOI;                             // 5 Gigantic AOI, enum { false, true, }; Default: false
            public uint floatingTooltip;                         // 6 floatingTooltip, enum { false, true, }; Default: false
            public uint floatOnWater;                            // 7 floatOnWater, enum { false, true, }; Default: false
            public int conditionID1;                            // 8 conditionID1, References: PlayerCondition, NoValue = 0
            public int InteractRadiusOverride;                  // 9 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int gossipID;                                // 10 gossipID, References: Gossip, NoValue = 0
            public uint spellFocusType2;                         // 11 spellFocusType 2, References: SpellFocusObject, NoValue = 0
            public uint spellFocusType3;                         // 12 spellFocusType 3, References: SpellFocusObject, NoValue = 0
            public uint spellFocusType4;                         // 13 spellFocusType 4, References: SpellFocusObject, NoValue = 0
            public uint Profession;                              // 14 Profession, enum { First Aid, Blacksmithing, Leatherworking, Alchemy, Herbalism, Cooking, Mining, Tailoring, Engineering, Enchanting, Fishing, Skinning, Jewelcrafting, Inscription, Archaeology, }; Default: Blacksmithing
            public uint Profession2;                             // 15 Profession 2, enum { First Aid, Blacksmithing, Leatherworking, Alchemy, Herbalism, Cooking, Mining, Tailoring, Engineering, Enchanting, Fishing, Skinning, Jewelcrafting, Inscription, Archaeology, }; Default: Blacksmithing
            public uint Profession3;                             // 16 Profession 3, enum { First Aid, Blacksmithing, Leatherworking, Alchemy, Herbalism, Cooking, Mining, Tailoring, Engineering, Enchanting, Fishing, Skinning, Jewelcrafting, Inscription, Archaeology, }; Default: Blacksmithing
        }

        public struct text
        {
            public int pageID;                                  // 0 pageID, References: PageText, NoValue = 0
            public int language;                                // 1 language, References: Languages, NoValue = 0
            public int pageMaterial;                            // 2 pageMaterial, References: PageTextMaterial, NoValue = 0
            public int allowMounted;                            // 3 allowMounted, enum { false, true, }; Default: false
            public int conditionID1;                            // 4 conditionID1, References: PlayerCondition, NoValue = 0
            public int NeverUsableWhileMounted;                 // 5 Never Usable While Mounted, enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 6 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct goober
        {
            public int open;                                    // 0 open, References: Lock_, NoValue = 0
            public int questID;                                 // 1 questID, References: QuestV2, NoValue = 0
            public int eventID;                                 // 2 eventID, References: GameEvents, NoValue = 0
            public Milliseconds autoClose;                      // 3 autoClose (ms), int, Min value: 0, Max value: 2147483647, Default value: 3000
            public int customAnim;                              // 4 customAnim, int, Min value: 0, Max value: 4, Default value: 0
            public int consumable;                              // 5 consumable, enum { false, true, }; Default: false
            public Seconds cooldown;                               // 6 cooldown, int, Min value: 0, Max value: 65535, Default value: 0
            public int pageID;                                  // 7 pageID, References: PageText, NoValue = 0
            public int language;                                // 8 language, References: Languages, NoValue = 0
            public int pageMaterial;                            // 9 pageMaterial, References: PageTextMaterial, NoValue = 0
            public int spell;                                   // 10 spell, References: Spell, NoValue = 0
            public int noDamageImmune;                          // 11 noDamageImmune, enum { false, true, }; Default: false
            public int linkedTrap;                              // 12 linkedTrap, References: GameObjects, NoValue = 0
            public int GiganticAOI;                             // 13 Gigantic AOI, enum { false, true, }; Default: false
            public int openTextID;                              // 14 openTextID, References: BroadcastText, NoValue = 0
            public int closeTextID;                             // 15 closeTextID, References: BroadcastText, NoValue = 0
            public int requireLOS;                              // 16 require LOS, enum { false, true, }; Default: false
            public int allowMounted;                            // 17 allowMounted, enum { false, true, }; Default: false
            public int floatingTooltip;                         // 18 floatingTooltip, enum { false, true, }; Default: false
            public int gossipID;                                // 19 gossipID, References: Gossip, NoValue = 0
            public int AllowMultiInteract;                      // 20 Allow Multi-Interact, enum { false, true, }; Default: false
            public int floatOnWater;                            // 21 floatOnWater, enum { false, true, }; Default: false
            public int conditionID1;                            // 22 conditionID1, References: PlayerCondition, NoValue = 0
            public int playerCast;                              // 23 playerCast, enum { false, true, }; Default: false
            public int SpawnVignette;                           // 24 Spawn Vignette, References: vignette, NoValue = 0
            public int startOpen;                               // 25 startOpen, enum { false, true, }; Default: false
            public int DontPlayOpenAnim;                        // 26 Dont Play Open Anim, enum { false, true, }; Default: false
            public int IgnoreBoundingBox;                       // 27 Ignore Bounding Box, enum { false, true, }; Default: false
            public int NeverUsableWhileMounted;                 // 28 Never Usable While Mounted, enum { false, true, }; Default: false
            public int SortFarZ;                                // 29 Sort Far Z, enum { false, true, }; Default: false
            public int SyncAnimationtoObjectLifetime;           // 30 Sync Animation to Object Lifetime (global track only), enum { false, true, }; Default: false
            public int NoFuzzyHit;                              // 31 No Fuzzy Hit, enum { false, true, }; Default: false
            public int LargeAOI;                                // 32 Large AOI, enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 33 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct transport
        {
            public Milliseconds Timeto2ndfloor;                 // 0 Time to 2nd floor (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint startOpen;                              // 1 startOpen, enum { false, true, }; Default: false
            public Milliseconds autoClose;                      // 2 autoClose (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Reached1stfloor;                         // 3 Reached 1st floor, References: GameEvents, NoValue = 0
            public int Reached2ndfloor;                         // 4 Reached 2nd floor, References: GameEvents, NoValue = 0
            public int SpawnMap;                                // 5 Spawn Map, References: Map, NoValue = -1
            public Milliseconds Timeto3rdfloor;                 // 6 Time to 3rd floor (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Reached3rdfloor;                         // 7 Reached 3rd floor, References: GameEvents, NoValue = 0
            public Milliseconds Timeto4thfloor;                 // 8 Time to 4th floor (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Reached4thfloor;                         // 9 Reached 4th floor, References: GameEvents, NoValue = 0
            public Milliseconds Timeto5thfloor;                 // 10 Time to 5th floor (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Reached5thfloor;                         // 11 Reached 5th floor, References: GameEvents, NoValue = 0
            public Milliseconds Timeto6thfloor;                 // 12 Time to 6th floor (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Reached6thfloor;                         // 13 Reached 6th floor, References: GameEvents, NoValue = 0
            public Milliseconds Timeto7thfloor;                 // 14 Time to 7th floor (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Reached7thfloor;                         // 15 Reached 7th floor, References: GameEvents, NoValue = 0
            public Milliseconds Timeto8thfloor;                 // 16 Time to 8th floor (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Reached8thfloor;                         // 17 Reached 8th floor, References: GameEvents, NoValue = 0
            public Milliseconds Timeto9thfloor;                 // 18 Time to 9th floor (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Reached9thfloor;                         // 19 Reached 9th floor, References: GameEvents, NoValue = 0
            public Milliseconds Timeto10thfloor;                // 20 Time to 10th floor (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Reached10thfloor;                        // 21 Reached 10th floor, References: GameEvents, NoValue = 0
            public int onlychargeheightcheck;                   // 22 only charge height check. (yards), int, Min value: 0, Max value: 65535, Default value: 0
            public int onlychargetimecheck;                     // 23 only charge time check, int, Min value: 0, Max value: 65535, Default value: 0
            public int InteractRadiusOverride;                  // 24 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct areadamage
        {
            public int open;                                    // 0 open, References: Lock_, NoValue = 0
            public uint radius;                                 // 1 radius, int, Min value: 0, Max value: 50, Default value: 3
            public int damageMin;                               // 2 damageMin, int, Min value: 0, Max value: 65535, Default value: 0
            public int damageMax;                               // 3 damageMax, int, Min value: 0, Max value: 65535, Default value: 0
            public uint damageSchool;                           // 4 damageSchool, int, Min value: 0, Max value: 65535, Default value: 0
            public Milliseconds autoClose;                      // 5 autoClose (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int openTextID;                              // 6 openTextID, References: BroadcastText, NoValue = 0
            public int closeTextID;                             // 7 closeTextID, References: BroadcastText, NoValue = 0
            public int InteractRadiusOverride;                  // 8 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct camera
        {
            public int open;                                    // 0 open, References: Lock_, NoValue = 0
            public int _camera;                                 // 1 camera, References: CinematicSequences, NoValue = 0
            public int eventID;                                 // 2 eventID, References: GameEvents, NoValue = 0
            public int openTextID;                              // 3 openTextID, References: BroadcastText, NoValue = 0
            public int conditionID1;                            // 4 conditionID1, References: PlayerCondition, NoValue = 0
            public int InteractRadiusOverride;                  // 5 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct moTransport
        {
            public int taxiPathID;                              // 0 taxiPathID, References: TaxiPath, NoValue = 0
            public int moveSpeed;                               // 1 moveSpeed, int, Min value: 1, Max value: 60, Default value: 1
            public int accelRate;                               // 2 accelRate, int, Min value: 1, Max value: 20, Default value: 1
            public int startEventID;                            // 3 startEventID, References: GameEvents, NoValue = 0
            public int stopEventID;                             // 4 stopEventID, References: GameEvents, NoValue = 0
            public int transportPhysics;                        // 5 transportPhysics, References: TransportPhysics, NoValue = 0
            public int SpawnMap;                                 // 6 Spawn Map, References: Map, NoValue = -1
            public uint worldState1;                             // 7 worldState1, References: WorldState, NoValue = 0
            public uint allowstopping;                           // 8 allow stopping, enum { false, true, }; Default: false
            public uint InitStopped;                             // 9 Init Stopped, enum { false, true, }; Default: false
            public uint TrueInfiniteAOI;                         // 10 True Infinite AOI (programmer only!), enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 11 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint Allowareaexplorationwhileonthistransport;// 12 Allow area exploration while on this transport, enum { false, true, }; Default: false
        }

        public struct duelflag
        {
            public int InteractRadiusOverride;                  // 0 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Willthisduelgountilaplayerdies;          // 1 Will this duel go until a player dies?, enum { false, true, }; Default: false
        }

        public struct fishingnode
        {
            public int InteractRadiusOverride;                  // 0 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct ritual
        {
            public int casters;                                 // 0 casters, int, Min value: 1, Max value: 10, Default value: 1
            public int spell;                                   // 1 spell, References: Spell, NoValue = 0
            public int animSpell;                               // 2 animSpell, References: Spell, NoValue = 0
            public int ritualPersistent;                        // 3 ritualPersistent, enum { false, true, }; Default: false
            public int casterTargetSpell;                       // 4 casterTargetSpell, References: Spell, NoValue = 0
            public int casterTargetSpellTargets;                // 5 casterTargetSpellTargets, int, Min value: 1, Max value: 10, Default value: 1
            public int castersGrouped;                          // 6 castersGrouped, enum { false, true, }; Default: true
            public int ritualNoTargetCheck;                     // 7 ritualNoTargetCheck, enum { false, true, }; Default: true
            public int conditionID1;                            // 8 conditionID1, References: PlayerCondition, NoValue = 0
            public int InteractRadiusOverride;                  // 9 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct mailbox
        {
            public int conditionID1;                            // 0 conditionID1, References: PlayerCondition, NoValue = 0
            public int InteractRadiusOverride;                  // 1 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct guardpost
        {
            public int creatureID;                              // 0 creatureID, References: Creature, NoValue = 0
            public int charges;                                 // 1 charges, int, Min value: 0, Max value: 65535, Default value: 1
            public int Preferonlyifinlineofsight;               // 2 Prefer only if in line of sight (expensive), enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 3 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct spellcaster
        {
            public int spell;                                   // 0 spell, References: Spell, NoValue = 0
            public int charges;                                  // 1 charges, int, Min value: -1, Max value: 65535, Default value: 1
            public int partyOnly;                               // 2 partyOnly, enum { false, true, }; Default: false
            public int allowMounted;                            // 3 allowMounted, enum { false, true, }; Default: false
            public int GiganticAOI;                             // 4 Gigantic AOI, enum { false, true, }; Default: false
            public int conditionID1;                            // 5 conditionID1, References: PlayerCondition, NoValue = 0
            public int playerCast;                              // 6 playerCast, enum { false, true, }; Default: false
            public int NeverUsableWhileMounted;                 // 7 Never Usable While Mounted, enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 8 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct meetingstone
        {
            public uint Unused;                                  // 0 Unused, int, Min value: 0, Max value: 65535, Default value: 1
            public uint Unused2;                                 // 1 Unused, int, Min value: 1, Max value: 65535, Default value: 60
            public int areaID;                                  // 2 areaID, References: AreaTable, NoValue = 0
            public int InteractRadiusOverride;                  // 3 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Preventmeetingstonefromtargetinganunfriendlypartymemberoutsideofinstances;// 4 Prevent meeting stone from targeting an unfriendly party member outside of instances, enum { false, true, }; Default: false
        }

        public struct flagstand
        {
            public int open;                                    // 0 open, References: Lock_, NoValue = 0
            public int pickupSpell;                             // 1 pickupSpell, References: Spell, NoValue = 0
            public int radius;                                  // 2 radius, int, Min value: 0, Max value: 50, Default value: 0
            public int returnAura;                              // 3 returnAura, References: Spell, NoValue = 0
            public int returnSpell;                             // 4 returnSpell, References: Spell, NoValue = 0
            public int noDamageImmune;                          // 5 noDamageImmune, enum { false, true, }; Default: false
            public int openTextID;                              // 6 openTextID, References: BroadcastText, NoValue = 0
            public int requireLOS;                              // 7 require LOS, enum { false, true, }; Default: true
            public int conditionID1;                            // 8 conditionID1, References: PlayerCondition, NoValue = 0
            public int playerCast;                              // 9 playerCast, enum { false, true, }; Default: false
            public int GiganticAOI;                             // 10 Gigantic AOI, enum { false, true, }; Default: false
            public int InfiniteAOI;                             // 11 Infinite AOI, enum { false, true, }; Default: false
            public uint cooldown;                                // 12 cooldown, int, Min value: 0, Max value: 2147483647, Default value: 3000
            public int InteractRadiusOverride;                  // 13 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct fishinghole
        {
            public int radius;                                  // 0 radius, int, Min value: 0, Max value: 50, Default value: 0
            public int chestLoot;                               // 1 chestLoot (legacy/classic), References: Treasure, NoValue = 0
            public int minRestock;                              // 2 minRestock, int, Min value: 0, Max value: 65535, Default value: 0
            public int maxRestock;                              // 3 maxRestock, int, Min value: 0, Max value: 65535, Default value: 0
            public int open;                                    // 4 open, References: Lock_, NoValue = 0
            public int InteractRadiusOverride;                  // 5 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct flagdrop
        {
            public int open;                                    // 0 open, References: Lock_, NoValue = 0
            public int eventID;                                 // 1 eventID, References: GameEvents, NoValue = 0
            public uint pickupSpell;                             // 2 pickupSpell, References: Spell, NoValue = 0
            public int noDamageImmune;                          // 3 noDamageImmune, enum { false, true, }; Default: false
            public uint openTextID;                              // 4 openTextID, References: BroadcastText, NoValue = 0
            public uint playerCast;                              // 5 playerCast, enum { false, true, }; Default: false
            public uint ExpireDuration;                          // 6 Expire Duration, int, Min value: 0, Max value: 60000, Default value: 10000
            public uint GiganticAOI;                             // 7 Gigantic AOI, enum { false, true, }; Default: false
            public uint InfiniteAOI;                             // 8 Infinite AOI, enum { false, true, }; Default: false
            public uint cooldown;                                // 9 cooldown, int, Min value: 0, Max value: 2147483647, Default value: 3000
            public int InteractRadiusOverride;                  // 10 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct controlzone
        {
            public int radius;                                  // 0 radius, int, Min value: 0, Max value: 100, Default value: 10
            public int spell;                                   // 1 spell, References: Spell, NoValue = 0
            public int worldState1;                             // 2 worldState1, References: WorldState, NoValue = 0
            public int worldstate2;                             // 3 worldstate2, References: WorldState, NoValue = 0
            public int CaptureEventHorde;                       // 4 Capture Event (Horde), References: GameEvents, NoValue = 0
            public int CaptureEventAlliance;                    // 5 Capture Event (Alliance), References: GameEvents, NoValue = 0
            public int ContestedEventHorde;                     // 6 Contested Event (Horde), References: GameEvents, NoValue = 0
            public int ContestedEventAlliance;                  // 7 Contested Event (Alliance), References: GameEvents, NoValue = 0
            public int ProgressEventHorde;                      // 8 Progress Event (Horde), References: GameEvents, NoValue = 0
            public int ProgressEventAlliance;                   // 9 Progress Event (Alliance), References: GameEvents, NoValue = 0
            public int NeutralEventHorde;                       // 10 Neutral Event (Horde), References: GameEvents, NoValue = 0
            public int NeutralEventAlliance;                    // 11 Neutral Event (Alliance), References: GameEvents, NoValue = 0
            public int neutralPercent;                          // 12 neutralPercent, int, Min value: 0, Max value: 100, Default value: 0
            public int worldstate3;                             // 13 worldstate3, References: WorldState, NoValue = 0
            public int minSuperiority;                          // 14 minSuperiority, int, Min value: 1, Max value: 65535, Default value: 1
            public int maxSuperiority;                          // 15 maxSuperiority, int, Min value: 1, Max value: 65535, Default value: 1
            public uint minTime;                                 // 16 minTime, int, Min value: 1, Max value: 65535, Default value: 1
            public uint maxTime;                                 // 17 maxTime, int, Min value: 1, Max value: 65535, Default value: 1
            public int GiganticAOI;                             // 18 Gigantic AOI, enum { false, true, }; Default: false
            public int highlight;                               // 19 highlight, enum { false, true, }; Default: true
            public int startingValue;                           // 20 startingValue, int, Min value: 0, Max value: 100, Default value: 50
            public int unidirectional;                          // 21 unidirectional, enum { false, true, }; Default: false
            public uint killbonustime;                           // 22 kill bonus time %, int, Min value: 0, Max value: 100, Default value: 0
            public uint speedWorldState1;                        // 23 speedWorldState1, References: WorldState, NoValue = 0
            public uint speedWorldState2;                        // 24 speedWorldState2, References: WorldState, NoValue = 0
            public uint UncontestedTime;                         // 25 Uncontested Time, int, Min value: 0, Max value: 65535, Default value: 0
            public uint FrequentHeartbeat;                       // 26 Frequent Heartbeat, enum { false, true, }; Default: false
            public uint EnablingWorldStateExpression;            // 27 Enabling World State Expression, References: WorldStateExpression, NoValue = 0
            public int InteractRadiusOverride;                  // 28 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct auraGenerator
        {
            public int startOpen;                               // 0 startOpen, enum { false, true, }; Default: true
            public int radius;                                  // 1 radius, int, Min value: 0, Max value: 100, Default value: 10
            public int auraID1;                                 // 2 auraID1, References: Spell, NoValue = 0
            public int conditionID1;                            // 3 conditionID1, References: PlayerCondition, NoValue = 0
            public int auraID2;                                 // 4 auraID2, References: Spell, NoValue = 0
            public int conditionID2;                            // 5 conditionID2, References: PlayerCondition, NoValue = 0
            public int serverOnly;                              // 6 serverOnly, enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 7 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct dungeonDifficulty
        {
            public uint InstanceType;                            // 0 Instance Type, enum { Not Instanced, Party Dungeon, Raid Dungeon, PVP Battlefield, Arena Battlefield, Scenario, WoWLabs }; Default: Party Dungeon
            public uint DifficultyNormal;                        // 1 Difficulty Normal, References: animationdata, NoValue = 0
            public uint DifficultyHeroic;                        // 2 Difficulty Heroic, References: animationdata, NoValue = 0
            public uint DifficultyEpic;                          // 3 Difficulty Epic, References: animationdata, NoValue = 0
            public uint DifficultyLegendary;                     // 4 Difficulty Legendary, References: animationdata, NoValue = 0
            public uint HeroicAttachment;                        // 5 Heroic Attachment, References: gameobjectdisplayinfo, NoValue = 0
            public uint ChallengeAttachment;                     // 6 Challenge Attachment, References: gameobjectdisplayinfo, NoValue = 0
            public uint DifficultyAnimations;                    // 7 Difficulty Animations, References: GameObjectDiffAnim, NoValue = 0
            public uint LargeAOI;                                // 8 Large AOI, enum { false, true, }; Default: false
            public uint GiganticAOI;                             // 9 Gigantic AOI, enum { false, true, }; Default: false
            public uint Legacy;                                  // 10 Legacy, enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 11 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct barberChair
        {
            public int chairheight;                             // 0 chairheight, int, Min value: 0, Max value: 2, Default value: 1
            public int HeightOffset;                             // 1 Height Offset (inches), int, Min value: -100, Max value: 100, Default value: 0
            public int SitAnimKit;                              // 2 Sit Anim Kit, References: AnimKit, NoValue = 0
            public int InteractRadiusOverride;                  // 3 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint CustomizationScope;                      // 4 Customization Scope, int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint Preventteleportingtheplayeroutofthebarbershopchair;// 5 Prevent teleporting the player out of the barbershop chair, enum { false, true, }; Default: false
        }

        public struct destructiblebuilding
        {
            public int Unused;                                   // 0 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public uint CreditProxyCreature;                     // 1 Credit Proxy Creature, References: Creature, NoValue = 0
            public uint HealthRec;                               // 2 Health Rec, References: DestructibleHitpoint, NoValue = 0
            public int IntactEvent;                             // 3 Intact Event, References: GameEvents, NoValue = 0
            public uint PVPEnabling;                             // 4 PVP Enabling, enum { false, true, }; Default: false
            public uint InteriorVisible;                         // 5 Interior Visible, enum { false, true, }; Default: false
            public uint InteriorLight;                           // 6 Interior Light, enum { false, true, }; Default: false
            public int Unused1;                                  // 7 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int Unused2;                                  // 8 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int DamagedEvent;                            // 9 Damaged Event, References: GameEvents, NoValue = 0
            public int Unused3;                                  // 10 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int Unused4;                                  // 11 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int Unused5;                                  // 12 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int Unused6;                                  // 13 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int DestroyedEvent;                          // 14 Destroyed Event, References: GameEvents, NoValue = 0
            public int Unused7;                                  // 15 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public uint RebuildingTime;                          // 16 Rebuilding: Time (secs), int, Min value: 0, Max value: 65535, Default value: 0
            public int Unused8;                                  // 17 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int DestructibleModelRec;                    // 18 Destructible Model Rec, References: DestructibleModelData, NoValue = 0
            public int RebuildingEvent;                         // 19 Rebuilding: Event, References: GameEvents, NoValue = 0
            public int Unused9;                                  // 20 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int Unused10;                                 // 21 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int DamageEvent;                             // 22 Damage Event, References: GameEvents, NoValue = 0
            public uint Displaymouseoverasanameplate;            // 23 Display mouseover as a nameplate, enum { false, true, }; Default: false
            public int Thexoffsetofthedestructiblenameplateifitisenabled;// 24 The x offset (in hundredths) of the destructible nameplate, if it is enabled, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int Theyoffsetofthedestructiblenameplateifitisenabled;// 25 The y offset (in hundredths) of the destructible nameplate, if it is enabled, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int Thezoffsetofthedestructiblenameplateifitisenabled;// 26 The z offset (in hundredths) of the destructible nameplate, if it is enabled, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int InteractRadiusOverride;                  // 27 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct guildbank
        {
            public int conditionID1;                            // 0 conditionID1, References: PlayerCondition, NoValue = 0
            public int InteractRadiusOverride;                  // 1 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct trapDoor
        {
            public uint AutoLink;                                // 0 Auto Link, enum { false, true, }; Default: false
            public uint startOpen;                               // 1 startOpen, enum { false, true, }; Default: false
            public Milliseconds autoClose;                       // 2 autoClose (ms), int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint BlocksPathsDown;                         // 3 Blocks Paths Down, enum { false, true, }; Default: false
            public int PathBlockerBump;                          // 4 Path Blocker Bump (ft), int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public uint GiganticAOI;                             // 5 Gigantic AOI, enum { false, true, }; Default: false
            public uint InfiniteAOI;                             // 6 Infinite AOI, enum { false, true, }; Default: false
            public uint DoorisOpaque;                            // 7 Door is Opaque (Disable portal on close), enum { false, true, }; Default: false
            public int InteractRadiusOverride;                   // 8 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct newflag
        {
            public int open;                                    // 0 open, References: Lock_, NoValue = 0
            public int pickupSpell;                             // 1 pickupSpell, References: Spell, NoValue = 0
            public int openTextID;                              // 2 openTextID, References: BroadcastText, NoValue = 0
            public int requireLOS;                              // 3 require LOS, enum { false, true, }; Default: true
            public int conditionID1;                            // 4 conditionID1, References: PlayerCondition, NoValue = 0
            public int GiganticAOI;                             // 5 Gigantic AOI, enum { false, true, }; Default: false
            public int InfiniteAOI;                             // 6 Infinite AOI, enum { false, true, }; Default: false
            public Milliseconds ExpireDuration;                 // 7 Expire Duration, int, Min value: 0, Max value: 3600000, Default value: 10000
            public Seconds RespawnTime;                         // 8 Respawn Time, int, Min value: 0, Max value: 3600000, Default value: 20000
            public int FlagDrop;                                // 9 Flag Drop, References: GameObjects, NoValue = 0
            public int ExclusiveCategory;                       // 10 Exclusive Category (BGs Only), int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int worldState1;                             // 11 worldState1, References: WorldState, NoValue = 0
            public int ReturnonDefenderInteract;                // 12 Return on Defender Interact, enum { false, true, }; Default: false
            public int SpawnVignette;                           // 13 Spawn Vignette, References: vignette, NoValue = 0
            public int InteractRadiusOverride;                  // 14 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct newflagdrop
        {
            public int open;                                    // 0 open, References: Lock_, NoValue = 0
            public int SpawnVignette;                           // 1 Spawn Vignette, References: vignette, NoValue = 0
            public int InteractRadiusOverride;                  // 2 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct garrisonbuilding
        {
            public int SpawnMap;                                 // 0 Spawn Map, References: Map, NoValue = -1
            public int InteractRadiusOverride;                  // 1 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct garrisonplot
        {
            public int PlotInstance;                            // 0 Plot Instance, References: GarrPlotInstance, NoValue = 0
            public int SpawnMap;                                 // 1 Spawn Map, References: Map, NoValue = -1
            public int InteractRadiusOverride;                  // 2 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct clientcreature
        {
            public int CreatureDisplayInfo;                     // 0 Creature Display Info, References: CreatureDisplayInfo, NoValue = 0
            public int AnimKit;                                 // 1 Anim Kit, References: AnimKit, NoValue = 0
            public int creatureID;                              // 2 creatureID, References: Creature, NoValue = 0
        }

        public struct clientitem
        {
            public uint Item;                                    // 0 Item, References: Item, NoValue = 0
        }

        public struct capturepoint
        {
            public Milliseconds CaptureTime;                    // 0 Capture Time (ms), int, Min value: 0, Max value: 2147483647, Default value: 60000
            public int GiganticAOI;                             // 1 Gigantic AOI, enum { false, true, }; Default: false
            public int highlight;                               // 2 highlight, enum { false, true, }; Default: true
            public int open;                                    // 3 open, References: Lock_, NoValue = 0
            public int AssaultBroadcastHorde;                   // 4 Assault Broadcast (Horde), References: BroadcastText, NoValue = 0
            public int CaptureBroadcastHorde;                   // 5 Capture Broadcast (Horde), References: BroadcastText, NoValue = 0
            public int DefendedBroadcastHorde;                  // 6 Defended Broadcast (Horde), References: BroadcastText, NoValue = 0
            public int AssaultBroadcastAlliance;                // 7 Assault Broadcast (Alliance), References: BroadcastText, NoValue = 0
            public int CaptureBroadcastAlliance;                // 8 Capture Broadcast (Alliance), References: BroadcastText, NoValue = 0
            public int DefendedBroadcastAlliance;               // 9 Defended Broadcast (Alliance), References: BroadcastText, NoValue = 0
            public int worldState1;                             // 10 worldState1, References: WorldState, NoValue = 0
            public int ContestedEventHorde;                     // 11 Contested Event (Horde), References: GameEvents, NoValue = 0
            public int CaptureEventHorde;                       // 12 Capture Event (Horde), References: GameEvents, NoValue = 0
            public int DefendedEventHorde;                      // 13 Defended Event (Horde), References: GameEvents, NoValue = 0
            public int ContestedEventAlliance;                  // 14 Contested Event (Alliance), References: GameEvents, NoValue = 0
            public int CaptureEventAlliance;                    // 15 Capture Event (Alliance), References: GameEvents, NoValue = 0
            public int DefendedEventAlliance;                   // 16 Defended Event (Alliance), References: GameEvents, NoValue = 0
            public int SpellVisual1;                            // 17 Spell Visual 1, References: SpellVisual, NoValue = 0
            public int SpellVisual2;                            // 18 Spell Visual 2, References: SpellVisual, NoValue = 0
            public int SpellVisual3;                            // 19 Spell Visual 3, References: SpellVisual, NoValue = 0
            public int SpellVisual4;                            // 20 Spell Visual 4, References: SpellVisual, NoValue = 0
            public int SpellVisual5;                            // 21 Spell Visual 5, References: SpellVisual, NoValue = 0
            public int SpawnVignette;                           // 22 Spawn Vignette, References: vignette, NoValue = 0
            public int InteractRadiusOverride;                  // 23 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct phaseablemo
        {
            public int SpawnMap;                                 // 0 Spawn Map, References: Map, NoValue = -1
            public int AreaNameSet;                              // 1 Area Name Set (Index), int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public uint DoodadSetA;                              // 2 Doodad Set A, int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint DoodadSetB;                              // 3 Doodad Set B, int, Min value: 0, Max value: 2147483647, Default value: 0
            public int InteractRadiusOverride;                  // 4 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct garrisonmonument
        {
            public uint TrophyTypeID;                            // 0 Trophy Type ID, References: TrophyType, NoValue = 0
            public uint TrophyInstanceID;                        // 1 Trophy Instance ID, References: TrophyInstance, NoValue = 0
            public int InteractRadiusOverride;                  // 2 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct garrisonshipment
        {
            public uint ShipmentContainer;                       // 0 Shipment Container, References: CharShipmentContainer, NoValue = 0
            public uint GiganticAOI;                             // 1 Gigantic AOI, enum { false, true, }; Default: false
            public uint LargeAOI;                                // 2 Large AOI, enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 3 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct garrisonmonumentplaque
        {
            public uint TrophyInstanceID;                        // 0 Trophy Instance ID, References: TrophyInstance, NoValue = 0
            public int InteractRadiusOverride;                  // 1 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct itemforge
        {
            public int conditionID1;                            // 0 conditionID1, References: PlayerCondition, NoValue = 0
            public int LargeAOI;                                // 1 Large AOI, enum { false, true, }; Default: false
            public int IgnoreBoundingBox;                       // 2 Ignore Bounding Box, enum { false, true, }; Default: false
            public int CameraMode;                              // 3 Camera Mode, References: CameraMode, NoValue = 0
            public int FadeRegionRadius;                        // 4 Fade Region Radius, int, Min value: 0, Max value: 2147483647, Default value: 0
            public int ForgeType;                               // 5 Forge Type, enum { Artifact Forge, Relic Forge, Heart Forge, Soulbind Forge, Anima Reservoir, }; Default: Relic Forge
            public int InteractRadiusOverride;                  // 6 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int GarrTalentTreeID;                        // 7 GarrTalentTree ID, References: GarrTalentTree, NoValue = 0
        }

        public struct uilink
        {
            public uint UILinkType;                              // 0 UI Link Type, enum { Adventure Journal, Obliterum Forge, Scrapping Machine, Item Interaction }; Default: Adventure Journal
            public uint allowMounted;                            // 1 allowMounted, enum { false, true, }; Default: false
            public uint GiganticAOI;                             // 2 Gigantic AOI, enum { false, true, }; Default: false
            public int spellFocusType;                          // 3 spellFocusType, References: SpellFocusObject, NoValue = 0
            public int radius;                                  // 4 radius, int, Min value: 0, Max value: 50, Default value: 10
            public int InteractRadiusOverride;                  // 5 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint ItemInteractionID;                       // 6 Item Interaction ID, References: UiItemInteraction, NoValue = 0
        }

        public struct keystonereceptacle
        {
            public int InteractRadiusOverride;                  // 0 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct gatheringnode
        {
            public int open;                                    // 0 open, References: Lock_, NoValue = 0
            public int chestLoot;                               // 1 chestLoot (legacy/classic), References: Treasure, NoValue = 0
            public int Unused;                                  // 2 Unused, int, Min value: 0, Max value: 65535, Default value: 0
            public int notInCombat;                             // 3 notInCombat, enum { false, true, }; Default: false
            public int trivialSkillLow;                         // 4 trivialSkillLow, int, Min value: 0, Max value: 65535, Default value: 0
            public int trivialSkillHigh;                        // 5 trivialSkillHigh, int, Min value: 0, Max value: 65535, Default value: 0
            public Seconds ObjectDespawnDelay;                  // 6 Object Despawn Delay, int, Min value: 0, Max value: 600, Default value: 15
            public int triggeredEvent;                          // 7 triggeredEvent, References: GameEvents, NoValue = 0
            public int requireLOS;                              // 8 require LOS, enum { false, true, }; Default: false
            public int openTextID;                              // 9 openTextID, References: BroadcastText, NoValue = 0
            public int floatingTooltip;                         // 10 floatingTooltip, enum { false, true, }; Default: false
            public int conditionID1;                            // 11 conditionID1, References: PlayerCondition, NoValue = 0
            public int Unused2;                                 // 12 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int xpDifficulty;                            // 13 xpDifficulty, enum { No Exp, Trivial, Very Small, Small, Substandard, Standard, High, Epic, Dungeon, 5, }; Default: No Exp
            public int spell;                                   // 14 spell, References: Spell, NoValue = 0
            public int GiganticAOI;                             // 15 Gigantic AOI, enum { false, true, }; Default: false
            public int LargeAOI;                                // 16 Large AOI, enum { false, true, }; Default: false
            public int SpawnVignette;                           // 17 Spawn Vignette, References: vignette, NoValue = 0
            public int MaxNumberofLoots;                        // 18 Max Number of Loots, int, Min value: 1, Max value: 40, Default value: 10
            public int logloot;                                 // 19 log loot, enum { false, true, }; Default: false
            public int linkedTrap;                              // 20 linkedTrap, References: GameObjects, NoValue = 0
            public int PlayOpenAnimationonOpening;              // 21 Play Open Animation on Opening, enum { false, true, }; Default: false
            public int turnpersonallootsecurityoff;             // 22 turn personal loot security off, enum { false, true, }; Default: false
            public int ClearObjectVignetteonOpening;            // 23 Clear Object Vignette on Opening, enum { false, true, }; Default: false
            public int InteractRadiusOverride;                  // 24 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public int Overrideminimaptrackingicon;             // 25 Override minimap tracking icon, References: UiTextureAtlasMember, NoValue = 0
        }

        public struct challengemodereward
        {
            public int Unused;                                   // 0 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public uint WhenAvailable;                           // 1 When Available, References: GameObjectDisplayInfo, NoValue = 0
            public int open;                                    // 2 open, References: Lock_, NoValue = 0
            public uint openTextID;                              // 3 openTextID, References: BroadcastText, NoValue = 0
            public int InteractRadiusOverride;                  // 4 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct multi
        {
            public uint MultiProperties;                         // 0 Multi Properties, References: MultiProperties, NoValue = 0
        }

        public struct siegeableMulti
        {
            public uint MultiProperties;                         // 0 Multi Properties, References: MultiProperties, NoValue = 0
            public uint InitialDamage;                           // 1 Initial Damage, enum { None, Raw, Ratio, }; Default: None
        }

        public struct siegeableMO
        {
            public uint SiegeableProperties;                     // 0 Siegeable Properties, References: SiegeableProperties, NoValue = 0
            public uint DoodadSetA;                              // 1 Doodad Set A, int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint DoodadSetB;                              // 2 Doodad Set B, int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint DoodadSetC;                              // 3 Doodad Set C, int, Min value: 0, Max value: 2147483647, Default value: 0
            public int SpawnMap;                                 // 4 Spawn Map, References: Map, NoValue = -1
            public int AreaNameSet;                              // 5 Area Name Set (Index), int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public int InteractRadiusOverride;                  // 6 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct pvpReward
        {
            public int Unused;                                   // 0 Unused, int, Min value: -2147483648, Max value: 2147483647, Default value: 0
            public uint WhenAvailable;                           // 1 When Available, References: GameObjectDisplayInfo, NoValue = 0
            public int open;                                    // 2 open, References: Lock_, NoValue = 0
            public uint openTextID;                              // 3 openTextID, References: BroadcastText, NoValue = 0
            public int InteractRadiusOverride;                  // 4 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct playerchoicechest
        {
            public uint spell;                                   // 0 spell, References: Spell, NoValue = 0
            public uint WhenAvailable;                           // 1 When Available, References: GameObjectDisplayInfo, NoValue = 0
            public uint GiganticAOI;                             // 2 Gigantic AOI, enum { false, true, }; Default: false
            public uint PlayerChoice;                            // 3 Player Choice, References: PlayerChoice, NoValue = 0
            public uint MawPowerFilter;                          // 4 Maw Power Filter, References: MawPowerFilter, NoValue = 0
            public uint Script;                                  // 5 Script, References: SpellScript, NoValue = 0
            public uint SpellVisual1;                            // 6 Spell Visual 1, References: SpellVisual, NoValue = 0
            public int InteractRadiusOverride;                  // 7 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint Dontupdateplayerinteractability;         // 8 Don't update player interactability, enum { false, true, }; Default: false
        }

        public struct legendaryforge
        {
            public uint PlayerChoice;                            // 0 Player Choice, References: PlayerChoice, NoValue = 0
            public uint CustomItemBonusFilter;                   // 1 Custom Item Bonus Filter, References: CustomItemBonusFilter, NoValue = 0
            public int InteractRadiusOverride;                  // 2 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct garrtalenttree
        {
            public uint UiMapID;                                 // 0 Ui Map ID, References: UiMap, NoValue = 0
            public uint GarrTalentTreeID;                        // 1 GarrTalentTree ID, References: GarrTalentTree, NoValue = 0
            public int InteractRadiusOverride;                  // 2 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct weeklyrewardchest
        {
            public uint WhenAvailable;                           // 0 When Available, References: GameObjectDisplayInfo, NoValue = 0
            public uint open;                                    // 1 open, References: Lock_, NoValue = 0
            public int InteractRadiusOverride;                  // 2 Interact Radius Override (in hundredths), int, Min value: 0, Max value: 2147483647, Default value: 0
            public uint ExpansionLevel;                          // 3 Expansion Level, int, Min value: 0, Max value: 2147483647, Default value: 0
        }

        public struct clientmodel
        {
            public uint LargeAOI;                                // 0 Large AOI, enum { false, true, }; Default: false
            public uint GiganticAOI;                             // 1 Gigantic AOI, enum { false, true, }; Default: false
            public uint InfiniteAOI;                             // 2 Infinite AOI, enum { false, true, }; Default: false
            public uint TrueInfiniteAOI;                         // 3 True Infinite AOI (programmer only!), enum { false, true, }; Default: false
        }

        public struct craftingTable
        {
            public uint Profession;                              // 0 Profession, enum { First Aid, Blacksmithing, Leatherworking, Alchemy, Herbalism, Cooking, Mining, Tailoring, Engineering, Enchanting, Fishing, Skinning, Jewelcrafting, Inscription, Archaeology, }; Default: Blacksmithing
        }

        public struct perksProgramChest
        {
            public uint Script;                                  // 0 Script, References: SpellScript, NoValue = 0
            public uint autoClose;                               // 1 autoClose (ms), int, Min value: 0, Max value: 2147483647, Default value: 3000
        }
        #endregion
    }

    // From `gameobject_template_addon`, `gameobject_overrides`
    public class GameObjectOverride
    {
        public int Faction;
        public GameObjectFlags Flags;
    }

    public class GameObjectTemplateAddon : GameObjectOverride
    {
        public uint Mingold;
        public uint Maxgold;
        public int[] ArtKits = new int[5];
        public int WorldEffectID;
        public int AIAnimKitID;
    }

    public class GameObjectLocale
    {
        public StringArray Name = new((int)Locale.Total);
        public StringArray CastBarCaption = new((int)Locale.Total);
        public StringArray Unk1 = new((int)Locale.Total);
    }

    public class GameObjectAddon
    {
        public Quaternion ParentRotation;
        public InvisibilityType invisibilityType;
        public uint invisibilityValue;
        public int WorldEffectID;
        public int AIAnimKitID;
    }

    public class GameObjectData : SpawnData
    {
        public Quaternion rotation;
        public int animprogress;
        public GameObjectState goState;
        public int artKit;

        public GameObjectData() : base(SpawnObjectType.GameObject) { }
    }
}
