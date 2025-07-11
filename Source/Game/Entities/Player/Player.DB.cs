﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Framework.Database;
using Game.Arenas;
using Game.BattleGrounds;
using Game.Cache;
using Game.DataStorage;
using Game.Groups;
using Game.Guilds;
using Game.Mails;
using Game.Maps;
using Game.Networking.Packets;
using Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Entities
{
    public partial class Player
    {
        void _LoadInventory(SQLResult result, Seconds timeDiff)
        {
            if (!result.IsEmpty())
            {
                var zoneId = GetZoneId();
                Dictionary<ObjectGuid, Bag> bagMap = new();                               // fast guid lookup for bags
                Dictionary<ObjectGuid, Item> invalidBagMap = new();                       // fast guid lookup for bags
                Queue<Item> problematicItems = new();
                SQLTransaction trans = new();

                // Prevent items from being added to the queue while loading
                m_itemUpdateQueueBlocked = true;
                do
                {
                    Item item = _LoadItem(trans, zoneId, timeDiff, result.GetFields());
                    if (item != null)
                    {
                        long bagLowGuid = result.Read<long>(46);
                        ObjectGuid bagGuid = bagLowGuid != 0 ? ObjectGuid.Create(HighGuid.Item, bagLowGuid) : ObjectGuid.Empty;
                        byte slot = result.Read<byte>(47);

                        GetSession().GetCollectionMgr().CheckHeirloomUpgrades(item);
                        GetSession().GetCollectionMgr().AddItemAppearance(item);

                        InventoryResult err = InventoryResult.Ok;

                        // Item is not in bag
                        if (bagGuid.IsEmpty())
                        {
                            item.SetContainer(null);
                            item.InventorySlot = slot;

                            ItemPos itemPos = new(slot);
                            List<ItemPosCount> dest;

                            if (itemPos.IsInventoryPos)
                            {
                                err = CanStoreItem(itemPos, out dest, item);
                                if (err == InventoryResult.Ok)
                                    item = StoreItem(dest, item, true);
                            }
                            else if (itemPos.IsEquipmentPos)
                            {
                                err = CanEquipItem(slot, out dest, item, false, false);
                                if (err == InventoryResult.Ok)
                                    QuickEquipItem(dest, item);
                            }
                            else if (itemPos.IsBankPos)
                            {
                                err = CanBankItem(itemPos, out dest, item, false, false);
                                if (err == InventoryResult.Ok)
                                    item = BankItem(dest, item, true);
                            }

                            // Remember bags that may contain items in them
                            if (err == InventoryResult.Ok)
                            {
                                if (item.InventoryPosition.IsBagSlotPos)
                                {
                                    Bag pBag = item.ToBag();
                                    if (pBag != null)
                                        bagMap.Add(item.GetGUID(), pBag);
                                }
                            }
                            else if (item.InventoryPosition.IsBagSlotPos)
                            {
                                if (item.IsBag())
                                    invalidBagMap.Add(item.GetGUID(), item);
                            }
                        }
                        else
                        {
                            item.InventorySlot = ItemSlot.Null;
                            // Item is in the bag, find the bag
                            var bag = bagMap.LookupByKey(bagGuid);
                            if (bag != null)
                            {
                                err = CanStoreItem(new(slot, bag.InventorySlot), out var dest, item);
                                if (err == InventoryResult.Ok)
                                    item = StoreItem(dest, item, true);
                            }
                            else if (invalidBagMap.ContainsKey(bagGuid))
                            {
                                var invalidBag = invalidBagMap.LookupByKey(bagGuid);
                                if (problematicItems.Contains(invalidBag))
                                    err = InventoryResult.InternalBagError;
                            }
                            else
                            {
                                Log.outError(LogFilter.Player,
                                    $"LoadInventory: player (GUID: {GetGUID()}, name: '{GetName()}') has item (GUID: {item.GetGUID()}, " +
                                    $"entry: {item.GetEntry()}) which doesnt have a valid bag (Bag GUID: {bagGuid}, slot: {slot}). Possible cheat?");
                                item.DeleteFromInventoryDB(trans);
                                continue;
                            }
                        }

                        // Item's state may have changed after storing
                        if (err == InventoryResult.Ok)
                            item.SetState(ItemUpdateState.Unchanged, this);
                        else
                        {
                            Log.outError(LogFilter.Player, 
                                $"LoadInventory: player (GUID: {GetGUID()}, name: '{GetName()}') has item (GUID: {item.GetGUID()}, " +
                                $"entry: {item.GetEntry()}) which can't be loaded into inventory (Bag GUID: {bagGuid}, slot: {slot}) by reason {err}. " +
                                $"Item will be sent by mail.");
                            item.DeleteFromInventoryDB(trans);
                            problematicItems.Enqueue(item);
                        }
                    }
                } while (result.NextRow());

                m_itemUpdateQueueBlocked = false;

                // Send problematic items by mail
                while (problematicItems.Count != 0)
                {
                    string subject = Global.ObjectMgr.GetCypherString(CypherStrings.NotEquippedItem);
                    MailDraft draft = new(subject, "There were problems with equipping item(s).");
                    for (int i = 0; problematicItems.Count != 0 && i < SharedConst.MaxMailItems; ++i)
                    {
                        draft.AddItem(problematicItems.Dequeue());
                    }
                    draft.SendMailTo(trans, this, new MailSender(this, MailStationery.Gm), MailCheckFlags.Copied);
                }

                DB.Characters.CommitTransaction(trans);
            }

            _ApplyAllItemMods();
        }

        Item _LoadItem(SQLTransaction trans, int zoneId, Seconds timeDiff, SQLFields fields)
        {
            Item item = null;
            long itemGuid = fields.Read<long>(0);
            int itemEntry = fields.Read<int>(1);
            ItemTemplate proto = Global.ObjectMgr.GetItemTemplate(itemEntry);
            if (proto != null)
            {
                bool remove = false;
                item = Bag.NewItemOrBag(proto);
                if (item.LoadFromDB(itemGuid, GetGUID(), fields, itemEntry))
                {
                    PreparedStatement stmt;

                    // Do not allow to have item limited to another map/zone in alive state
                    if (IsAlive() && item.IsLimitedToAnotherMapOrZone(GetMapId(), zoneId))
                    {
                        Log.outDebug(LogFilter.Player, "LoadInventory: player (GUID: {0}, name: '{1}', map: {2}) has item (GUID: {3}, entry: {4}) limited to another map ({5}). Deleting item.",
                            GetGUID().ToString(), GetName(), GetMapId(), item.GetGUID().ToString(), item.GetEntry(), zoneId);
                        remove = true;
                    }
                    // "Conjured items disappear if you are logged out for more than 15 minutes"
                    else if (timeDiff > 15 * Time.Minute && proto.HasFlag(ItemFlags.Conjured))
                    {
                        Log.outDebug(LogFilter.Player, "LoadInventory: player (GUID: {0}, name: {1}, diff: {2}) has conjured item (GUID: {3}, entry: {4}) with expired lifetime (15 minutes). Deleting item.",
                            GetGUID().ToString(), GetName(), timeDiff, item.GetGUID().ToString(), item.GetEntry());
                        remove = true;
                    }
                    if (item.IsRefundable())
                    {
                        if (item.IsRefundExpired())
                        {
                            Log.outDebug(LogFilter.Player, "LoadInventory: player (GUID: {0}, name: {1}) has item (GUID: {2}, entry: {3}) with expired refund time ({4}). Deleting refund data and removing " +
                                "efundable flag.", GetGUID().ToString(), GetName(), item.GetGUID().ToString(), item.GetEntry(), item.GetPlayedTime());

                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_REFUND_INSTANCE);
                            stmt.SetString(0, item.GetGUID().ToString());
                            trans.Append(stmt);

                            item.RemoveItemFlag(ItemFieldFlags.Refundable);
                        }
                        else
                        {
                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_ITEM_REFUNDS);
                            stmt.SetInt64(0, item.GetGUID().GetCounter());
                            stmt.SetInt64(1, GetGUID().GetCounter());
                            SQLResult result = DB.Characters.Query(stmt);
                            if (!result.IsEmpty())
                            {
                                item.SetRefundRecipient(GetGUID());
                                item.SetPaidMoney(result.Read<long>(0));
                                item.SetPaidExtendedCost(result.Read<ushort>(1));
                                AddRefundReference(item.GetGUID());
                            }
                            else
                            {
                                Log.outDebug(LogFilter.Player, "LoadInventory: player (GUID: {0}, name: {1}) has item (GUID: {2}, entry: {3}) with refundable flags, but without data in item_refund_instance. Removing flag.",
                                    GetGUID().ToString(), GetName(), item.GetGUID().ToString(), item.GetEntry());
                                item.RemoveItemFlag(ItemFieldFlags.Refundable);
                            }
                        }
                    }
                    else if (item.IsBOPTradeable())
                    {
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_ITEM_BOP_TRADE);
                        stmt.SetString(0, item.GetGUID().ToString());
                        SQLResult result = DB.Characters.Query(stmt);
                        if (!result.IsEmpty())
                        {
                            string strGUID = result.Read<string>(0);
                            var GUIDlist = new StringArray(strGUID, ' ');
                            List<ObjectGuid> looters = new();
                            for (var i = 0; i < GUIDlist.Length; ++i)
                            {
                                if (long.TryParse(GUIDlist[i], out long guid))
                                    looters.Add(ObjectGuid.Create(HighGuid.Item, guid));
                            }

                            if (looters.Count > 1 && item.GetTemplate().GetMaxStackSize() == 1 && item.IsSoulBound())
                            {
                                item.SetSoulboundTradeable(looters);
                                AddTradeableItem(item);
                            }
                            else
                                item.ClearSoulboundTradeable(this);
                        }
                        else
                        {
                            Log.outDebug(LogFilter.ServerLoading, "LoadInventory: player ({0}, name: {1}) has item ({2}, entry: {3}) with ITEM_FLAG_BOP_TRADEABLE flag, " +
                                "but without data in item_soulbound_trade_data. Removing flag.", GetGUID().ToString(), GetName(), item.GetGUID().ToString(), item.GetEntry());
                            item.RemoveItemFlag(ItemFieldFlags.BopTradeable);
                        }
                    }
                    else if (proto.GetHolidayID() != 0)
                    {
                        remove = true;
                        var events = Global.GameEventMgr.GetEventMap();
                        var activeEventsList = Global.GameEventMgr.GetActiveEventList();
                        foreach (var id in activeEventsList)
                        {
                            if (events[id].holiday_id == proto.GetHolidayID())
                            {
                                remove = false;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    Log.outError(LogFilter.Player, 
                        $"LoadInventory: player (GUID: {GetGUID()}, name: {GetName()}) has broken item " +
                        $"(GUID: {itemGuid}, entry: {itemEntry}) in inventory. Deleting item." );
                    remove = true;
                }
                // Remove item from inventory if necessary
                if (remove)
                {
                    Item.DeleteFromInventoryDB(trans, itemGuid);
                    item.FSetState(ItemUpdateState.Removed);
                    item.SaveToDB(trans);                           // it also deletes item object!
                    item = null;
                }
            }
            else
            {
                Log.outError(LogFilter.Player, 
                    $"LoadInventory: player (GUID: {GetGUID()}, name: {GetName()}) has unknown item " +
                    $"(entry: {itemEntry}) in inventory. Deleting item.");

                Item.DeleteFromInventoryDB(trans, itemGuid);
                Item.DeleteFromDB(trans, itemGuid);
            }
            return item;
        }

        void _LoadSkills(SQLResult result)
        {
            Race race = GetRace();
            Dictionary<SkillType, int> loadedSkillValues = new();
            List<SkillType> loadedProfessionsWithoutSlot = new(); // fixup old characters
            if (!result.IsEmpty())
            {
                do
                {
                    if (mSkillStatus.Count >= SkillConst.MaxPlayerSkills)                      // client limit
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player::_LoadSkills: Player '{GetName()}' ({GetGUID()}) has more than {SkillConst.MaxPlayerSkills} skills.");
                        break;
                    }

                    var skill = (SkillType)result.Read<ushort>(0);
                    var value = result.Read<ushort>(1);
                    var max = result.Read<ushort>(2);
                    var professionSlot = result.Read<sbyte>(3);

                    SkillRaceClassInfoRecord rcEntry = Global.DB2Mgr.GetSkillRaceClassInfo(skill, race, GetClass());
                    if (rcEntry == null)
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player::_LoadSkills: Player '{GetName()}' ({GetGUID()}, Race: {race}, Class: {GetClass()}) " +
                            $"has forbidden skill {skill} for his race/class combination");

                        mSkillStatus.Add(skill, new SkillStatusData(mSkillStatus.Count, SkillState.Deleted));
                        continue;
                    }

                    // set fixed skill ranges
                    switch (Global.SpellMgr.GetSkillRangeType(rcEntry))
                    {
                        case SkillRangeType.Language:
                            value = max = 300;
                            break;
                        case SkillRangeType.Mono:
                            value = max = 1;
                            break;
                        case SkillRangeType.Level:
                            max = GetMaxSkillValueForLevel();
                            break;
                        default:
                            break;
                    }

                    if (!mSkillStatus.ContainsKey(skill))
                        mSkillStatus.Add(skill, new SkillStatusData(mSkillStatus.Count, SkillState.Unchanged));

                    var skillStatusData = mSkillStatus[skill];
                    int step = 0;

                    SkillLineRecord skillLine = CliDB.SkillLineStorage.LookupByKey(rcEntry.SkillID);
                    if (skillLine != null)
                    {
                        if (skillLine.CategoryID == SkillCategory.Secondary)
                            step = max / 75;

                        if (skillLine.CategoryID == SkillCategory.Profession)
                        {
                            step = max / 75;

                            if (skillLine.ParentSkillLineID != 0 && skillLine.ParentTierIndex != 0)
                            {
                                if (professionSlot != -1)
                                    SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ProfessionSkillLine, professionSlot), (int)skill);
                                else
                                    loadedProfessionsWithoutSlot.Add(skill);
                            }
                        }
                    }

                    SetSkillLineId(skillStatusData.Pos, skill);
                    SetSkillStep(skillStatusData.Pos, step);
                    SetSkillRank(skillStatusData.Pos, value);
                    SetSkillStartingRank(skillStatusData.Pos, 1);
                    SetSkillMaxRank(skillStatusData.Pos, max);
                    SetSkillTempBonus(skillStatusData.Pos, 0);
                    SetSkillPermBonus(skillStatusData.Pos, 0);

                    loadedSkillValues[skill] = value;
                }
                while (result.NextRow());
            }

            // Learn skill rewarded spells after all skills have been loaded to prevent learning a skill
            // from them before its loaded with proper value from DB
            foreach (var (skillId, skillValue) in loadedSkillValues)
            {
                LearnSkillRewardedSpells(skillId, skillValue, race);

                // enable parent skill line if missing
                var skillEntry = CliDB.SkillLineStorage.LookupByKey(skillId);
                if (skillEntry.ParentSkillLineID != 0 && skillEntry.ParentTierIndex > 0 && GetSkillStep(skillEntry.ParentSkillLineID) < skillEntry.ParentTierIndex)
                {
                    var rcEntry = Global.DB2Mgr.GetSkillRaceClassInfo(skillEntry.ParentSkillLineID, GetRace(), GetClass());
                    if (rcEntry != null)
                    {
                        var tier = Global.ObjectMgr.GetSkillTier(rcEntry.SkillTierID);
                        if (tier != null)
                            SetSkill(skillEntry.ParentSkillLineID, skillEntry.ParentTierIndex, Math.Max((int)GetPureSkillValue(skillEntry.ParentSkillLineID), 1), tier.GetValueForTierIndex(skillEntry.ParentTierIndex - 1));
                    }
                }

                var childSkillLines = Global.DB2Mgr.GetSkillLinesForParentSkill(skillId);
                if (childSkillLines != null)
                {
                    foreach (var childSkillLine in childSkillLines)
                    {
                        if (mSkillStatus.Count >= SkillConst.MaxPlayerSkills)
                            break;

                        if (!mSkillStatus.ContainsKey(childSkillLine.Id))
                        {
                            int pos = mSkillStatus.Count;
                            SetSkillLineId(pos, childSkillLine.Id);
                            SetSkillStartingRank(pos, 1);
                            mSkillStatus.Add(childSkillLine.Id, new SkillStatusData(pos, SkillState.Unchanged));
                        }
                    }
                }
            }

            foreach (var skill in loadedProfessionsWithoutSlot)
            {
                int emptyProfessionSlot = FindEmptyProfessionSlotFor(skill);
                if (emptyProfessionSlot != -1)
                {
                    SetUpdateFieldValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ProfessionSkillLine, emptyProfessionSlot), (int)skill);
                    mSkillStatus[skill].State = SkillState.Changed;
                }
            }

            if (HasSkill(SkillType.FistWeapons))
                SetSkill(SkillType.FistWeapons, 0, GetSkillValue(SkillType.Unarmed), GetMaxSkillValueForLevel());
        }

        void _LoadSpells(SQLResult result, SQLResult favoritesResult)
        {
            if (!result.IsEmpty())
            {
                do
                {
                    SpellBook.Add(result.Read<int>(0), result.Read<bool>(1), false, false, result.Read<bool>(2), true);
                }
                while (result.NextRow());
            }

            if (!favoritesResult.IsEmpty())
            {
                do
                {
                    var spell = SpellBook[favoritesResult.Read<int>(0)];
                    if (spell != null)
                        spell.Favorite = true;
                } while (favoritesResult.NextRow());
            }
        }

        void _LoadAuras(SQLResult auraResult, SQLResult effectResult, Seconds timediff)
        {
            Log.outDebug(LogFilter.Player, $"Loading auras for player {GetGUID()}");

            ObjectGuid casterGuid = new();
            ObjectGuid itemGuid = new();
            Dictionary<AuraKey, AuraLoadEffectInfo> effectInfo = new();
            if (!effectResult.IsEmpty())
            {
                do
                {
                    uint effectIndex = effectResult.Read<byte>(4);
                    if (effectIndex < SpellConst.MaxEffects)
                    {
                        casterGuid.SetRawValue(effectResult.Read<byte[]>(0));
                        itemGuid.SetRawValue(effectResult.Read<byte[]>(1));

                        AuraKey key = new(casterGuid, itemGuid, effectResult.Read<int>(2), effectResult.Read<uint>(3));
                        if (!effectInfo.ContainsKey(key))
                            effectInfo[key] = new AuraLoadEffectInfo();

                        AuraLoadEffectInfo info = effectInfo[key];
                        info.Amounts[effectIndex] = effectResult.Read<int>(5);
                        info.BaseAmounts[effectIndex] = effectResult.Read<int>(6);
                    }
                }
                while (effectResult.NextRow());
            }

            if (!auraResult.IsEmpty())
            {
                do
                {
                    casterGuid.SetRawValue(auraResult.Read<byte[]>(0));
                    itemGuid.SetRawValue(auraResult.Read<byte[]>(1));
                    AuraKey key = new(casterGuid, itemGuid, auraResult.Read<int>(2), auraResult.Read<uint>(3));
                    uint recalculateMask = auraResult.Read<uint>(4);
                    Difficulty difficulty = (Difficulty)auraResult.Read<byte>(5);
                    byte stackCount = auraResult.Read<byte>(6);
                    Milliseconds maxDuration = (Milliseconds)auraResult.Read<int>(7);
                    Milliseconds remainTime = (Milliseconds)auraResult.Read<int>(8);
                    byte remainCharges = auraResult.Read<byte>(9);
                    int castItemId = auraResult.Read<int>(10);
                    int castItemLevel = auraResult.Read<int>(11);

                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(key.SpellId, difficulty);
                    if (spellInfo == null)
                    {
                        Log.outError(LogFilter.Player, $"Unknown aura (spellid {key.SpellId}), ignore.");
                        continue;
                    }

                    if (difficulty != Difficulty.None && !CliDB.DifficultyStorage.ContainsKey(difficulty))
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player._LoadAuras: Player '{GetName()}' ({GetGUID()}) " +
                            $"has an invalid aura difficulty {difficulty} (SpellID: {key.SpellId}), ignoring.");
                        continue;
                    }

                    // negative effects should continue counting down after logout
                    if (remainTime != -1 && (!spellInfo.IsPositive() || spellInfo.HasAttribute(SpellAttr4.AuraExpiresOffline)))
                    {
                        /// if (remainTime <= timediff) // Overflow may occur here
                        if (new Seconds(remainTime / Time.MillisecondsInSecond) <= timediff)
                            continue;

                        remainTime -= timediff;
                    }

                    // prevent wrong values of remaincharges
                    if (spellInfo.ProcCharges != 0)
                    {
                        // we have no control over the order of applying auras and modifiers allow auras
                        // to have more charges than value in SpellInfo
                        if (remainCharges <= 0)
                            remainCharges = (byte)spellInfo.ProcCharges;
                    }
                    else
                        remainCharges = 0;

                    AuraLoadEffectInfo info = effectInfo[key];
                    ObjectGuid castId = ObjectGuid.Create(HighGuid.Cast, SpellCastSource.Normal, GetMapId(), spellInfo.Id, GetMap().GenerateLowGuid(HighGuid.Cast));

                    AuraCreateInfo createInfo = new(castId, spellInfo, difficulty, key.EffectMask, this);
                    createInfo.SetCasterGUID(casterGuid);
                    createInfo.SetBaseAmount(info.BaseAmounts);
                    createInfo.SetCastItem(itemGuid, castItemId, castItemLevel);

                    Aura aura = Aura.TryCreate(createInfo);
                    if (aura != null)
                    {
                        if (!aura.CanBeSaved())
                        {
                            aura.Remove();
                            continue;
                        }

                        aura.SetLoadedState(maxDuration, remainTime, remainCharges, stackCount, recalculateMask, info.Amounts);
                        aura.ApplyForTargets();
                        Log.outInfo(LogFilter.Player, $"Added aura spellid {spellInfo.Id}, effectmask {key.EffectMask}");
                    }
                }
                while (auraResult.NextRow());
            }
        }

        bool _LoadHomeBind(SQLResult result)
        {
            PlayerInfo info = Global.ObjectMgr.GetPlayerInfo(GetRace(), GetClass());
            if (info == null)
            {
                Log.outError(LogFilter.Player, 
                    $"Player (Name {GetName()}) has incorrect race/class ({GetRace()}/{GetClass()}) pair. Can't be loaded.");
                return false;
            }

            bool ok = false;
            if (!result.IsEmpty())
            {
                homebind.WorldRelocate(result.Read<int>(0), result.Read<float>(2), result.Read<float>(3), result.Read<float>(4), result.Read<float>(5));
                homebindAreaId = result.Read<int>(1);

                var map = CliDB.MapStorage.LookupByKey(homebind.GetMapId());

                // accept saved data only for valid position (and non instanceable), and accessable
                if (GridDefines.IsValidMapCoord(homebind) &&
                    !map.Instanceable && GetSession().GetExpansion() >= map.Expansion)
                    ok = true;
                else
                {
                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PLAYER_HOMEBIND);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    DB.Characters.Execute(stmt);
                }
            }

            void saveHomebindToDb()
            {
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_PLAYER_HOMEBIND);
                stmt.SetInt64(0, GetGUID().GetCounter());
                stmt.SetInt32(1, homebind.GetMapId());
                stmt.SetInt32(2, homebindAreaId);
                stmt.SetFloat(3, homebind.GetPositionX());
                stmt.SetFloat(4, homebind.GetPositionY());
                stmt.SetFloat(5, homebind.GetPositionZ());
                stmt.SetFloat(6, homebind.GetOrientation());
                DB.Characters.Execute(stmt);
            };

            if (!ok && HasAtLoginFlag(AtLoginFlags.FirstLogin))
            {
                var createPosition = m_createMode == PlayerCreateMode.NPE && info.createPositionNPE.HasValue ? info.createPositionNPE.Value : info.createPosition;

                if (!createPosition.TransportGuid.HasValue)
                {
                    homebind.WorldRelocate(createPosition.Loc);
                    homebindAreaId = Global.TerrainMgr.GetAreaId(PhasingHandler.EmptyPhaseShift, homebind);

                    saveHomebindToDb();
                    ok = true;
                }
            }

            if (!ok)
            {
                WorldSafeLocsEntry loc = Global.ObjectMgr.GetDefaultGraveyard(GetTeam());
                if (loc == null && GetRace() == Race.PandarenNeutral)
                    loc = Global.ObjectMgr.GetWorldSafeLoc(3295); // The Wandering Isle, Starting Area GY

                Cypher.Assert(loc != null, 
                    $"Missing fallback graveyard location for faction {GetBatttleGroundTeamId()}");

                homebind.WorldRelocate(loc.Loc);
                homebindAreaId = Global.TerrainMgr.GetAreaId(PhasingHandler.EmptyPhaseShift, loc.Loc);

                saveHomebindToDb();
            }

            Log.outDebug(LogFilter.Player,
                $"Setting player home position - mapid: {homebind.GetMapId()}, areaid: {homebindAreaId}, {homebind}");

            return true;
        }

        void _LoadCurrency(SQLResult result)
        {
            if (result.IsEmpty())
                return;

            do
            {
                ushort currencyID = result.Read<ushort>(0);

                var currency = CliDB.CurrencyTypesStorage.LookupByKey(currencyID);
                if (currency == null)
                    continue;

                PlayerCurrency cur = new();
                cur.state = PlayerCurrencyState.Unchanged;
                cur.Quantity = result.Read<int>(1);
                cur.WeeklyQuantity = result.Read<int>(2);
                cur.TrackedQuantity = result.Read<int>(3);
                cur.IncreasedCapQuantity = result.Read<int>(4);
                cur.EarnedQuantity = result.Read<int>(5);
                cur.Flags = (CurrencyDbFlags)result.Read<byte>(6);

                _currencyStorage.Add(currencyID, cur);
            } while (result.NextRow());
        }

        void LoadActions(SQLResult result)
        {
            _LoadActions(result);

            SendActionButtons(ActionsButtonsUpdateReason.AfterSpecSwap);
        }

        void _LoadActions(SQLResult result)
        {
            m_actionButtons.Clear();
            if (!result.IsEmpty())
            {
                do
                {
                    byte button = result.Read<byte>(0);
                    int action = result.Read<int>(1);
                    ActionButtonType type = (ActionButtonType)result.Read<byte>(2);

                    ActionButton ab = AddActionButton(button, action, type);
                    if (ab != null)
                        ab.State = ActionButtonUpdateState.UnChanged;
                    else
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player::_LoadActions: Player '{GetName()}' ({GetGUID()}) has an invalid action button " +
                            $"(Button: {button}, Action: {action}, Type: {type}). It will be deleted at next save. " +
                            $"This can be due to a player changing their talents.");

                        // Will deleted in DB at next save (it can create data until save but marked as deleted)
                        m_actionButtons[button] = new ActionButton();
                        m_actionButtons[button].State = ActionButtonUpdateState.Deleted;
                    }
                } while (result.NextRow());
            }
        }

        void _LoadQuestStatus(SQLResult result)
        {
            ushort slot = 0;

            RealmTime lastDailyReset = Global.WorldMgr.GetNextDailyQuestsResetTime() - (Days)1;
            RealmTime lastWeeklyReset = Global.WorldMgr.GetNextWeeklyQuestsResetTime() -(Days)30;

            if (!result.IsEmpty())
            {
                do
                {
                    int questId = result.Read<int>(0);
                    // used to be new, no delete?
                    Quest quest = Global.ObjectMgr.GetQuestTemplate(questId);
                    if (quest == null)
                        continue;

                    // find or create
                    QuestStatusData questStatusData = new();

                    QuestStatus qstatus = (QuestStatus)result.Read<byte>(1);
                    if (qstatus < QuestStatus.Max)
                        questStatusData.Status = qstatus;
                    else
                    {
                        questStatusData.Status = QuestStatus.Incomplete;
                        Log.outError(LogFilter.Player, 
                            $"Player._LoadQuestStatus: Player '{GetName()}' ({GetGUID()}) " +
                            $"has invalid quest {questId} status ({qstatus}), " +
                            $"replaced by QUEST_STATUS_INCOMPLETE(3).");
                    }

                    questStatusData.Explored = result.Read<byte>(2) > 0;

                    questStatusData.AcceptTime = (ServerTime)(UnixTime64)result.Read<long>(3);
                    if (quest.HasFlag(QuestFlagsEx.RemoveOnPeriodicReset))
                    {
                        if ((quest.IsDaily() && (RealmTime)questStatusData.AcceptTime < lastDailyReset)
                            || (quest.IsWeekly() && (RealmTime)questStatusData.AcceptTime < lastWeeklyReset))
                        {
                            questStatusData.Status = QuestStatus.None;
                            m_QuestStatusSave[questId] = QuestSaveType.Delete;
                            SendPacket(new QuestForceRemoved(questId));
                        }
                    }

                    ServerTime endTime = (ServerTime)(UnixTime64)result.Read<long>(4);
                    if (quest.LimitTime != TimeSpan.Zero && !GetQuestRewardStatus(questId))
                    {
                        AddTimedQuest(questId);

                        if (endTime <= LoopTime.ServerTime)
                            questStatusData.Timer = (Milliseconds)1;
                        else
                            questStatusData.Timer = endTime - LoopTime.ServerTime;
                    }
                    else
                        endTime = ServerTime.Zero;

                    // add to quest log
                    if (slot < SharedConst.MaxQuestLogSize && questStatusData.Status != QuestStatus.None)
                    {
                        questStatusData.Slot = slot;

                        foreach (QuestObjective obj in quest.Objectives)
                            m_questObjectiveStatus.Add((obj.Type, obj.ObjectID), new QuestObjectiveStatusData() { QuestStatusPair = (questId, questStatusData), ObjectiveId = obj.Id });

                        SetQuestSlot(slot, questId);
                        SetQuestSlotEndTime(slot, endTime);

                        if (questStatusData.Status == QuestStatus.Complete)
                            SetQuestSlotState(slot, QuestSlotStateMask.Complete);
                        else if (questStatusData.Status == QuestStatus.Failed)
                            SetQuestSlotState(slot, QuestSlotStateMask.Fail);

                            if (quest.HasFlag(QuestFlagsEx.RecastAcceptSpellOnLogin) && quest.HasFlag(QuestFlags.PlayerCastAccept) && quest.SourceSpellID > 0)
                                CastSpell(this, quest.SourceSpellID, new CastSpellExtraArgs(TriggerCastFlags.FullMask));

                        ++slot;
                    }

                    m_QuestStatus[questId] = questStatusData;
                    Log.outDebug(LogFilter.ServerLoading, 
                        $"Quest status is {questStatusData.Status} for quest {questId} for player (GUID: {GetGUID()})");

                }
                while (result.NextRow());
            }

            // clear quest log tail
            for (ushort i = slot; i < SharedConst.MaxQuestLogSize; ++i)
                SetQuestSlot(i, 0);
        }

        void _LoadQuestStatusObjectives(SQLResult result)
        {
            if (!result.IsEmpty())
            {
                do
                {
                    int questID = result.Read<int>(0);

                    Quest quest = Global.ObjectMgr.GetQuestTemplate(questID);

                    var questStatusData = m_QuestStatus.LookupByKey(questID);
                    if (questStatusData != null && questStatusData.Slot < SharedConst.MaxQuestLogSize && quest != null)
                    {
                        byte storageIndex = result.Read<byte>(1);

                        var objective = quest.Objectives.FirstOrDefault(objective => objective.StorageIndex == storageIndex);
                        if (objective != null)
                        {
                            int data = result.Read<int>(2);
                            if (!objective.IsStoringFlag())
                                SetQuestSlotCounter(questStatusData.Slot, storageIndex, (ushort)data);
                            else if (data != 0)
                                SetQuestSlotState(questStatusData.Slot, QuestSlotStateMask.None.SetSlot(storageIndex));
                        }
                        else
                        {
                            Log.outError(LogFilter.Player,
                                $"Player {GetName()} ({GetGUID()}) has quest {questID} out of range objective index {storageIndex}.");
                        }
                    }
                    else
                    {
                        Log.outError(LogFilter.Player,
                            $"Player {GetName()} ({GetGUID()}) does not have quest {questID} but has objective data for it.");
                    }
                }
                while (result.NextRow());
            }
        }

        void _LoadQuestStatusRewarded(SQLResult result)
        {
            if (!result.IsEmpty())
            {
                do
                {
                    int quest_id = result.Read<int>(0);
                    // used to be new, no delete?
                    Quest quest = Global.ObjectMgr.GetQuestTemplate(quest_id);
                    if (quest != null)
                    {
                        // learn rewarded spell if unknown
                        LearnQuestRewardedSpells(quest);

                        // set rewarded title if any
                        if (quest.RewardTitleId != 0)
                        {
                            CharTitlesRecord titleEntry = CliDB.CharTitlesStorage.LookupByKey(quest.RewardTitleId);
                            if (titleEntry != null)
                                SetTitle(titleEntry);
                        }

                        if (quest.RewardSkillPoints != 0)
                            m_questRewardedTalentPoints += quest.RewardSkillPoints;


                        // Skip loading special quests - they are also added to rewarded quests but only once and remain there forever
                        // instead add them separately from load daily/weekly/monthly/seasonal
                        if (!quest.IsDailyOrWeekly() && !quest.IsMonthly() && !quest.IsSeasonal())
                        {
                            uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(quest_id);
                            if (questBit != 0)
                                SetQuestCompletedBit(questBit, true);
                        }

                        for (uint i = 0; i < quest.GetRewChoiceItemsCount(); ++i)
                            GetSession().GetCollectionMgr().AddItemAppearance(quest.RewardChoiceItemId[i]);

                        for (uint i = 0; i < quest.GetRewItemsCount(); ++i)
                            GetSession().GetCollectionMgr().AddItemAppearance(quest.RewardItemId[i]);

                        var questPackageItems = Global.DB2Mgr.GetQuestPackageItems(quest.PackageID);
                        if (questPackageItems != null)
                        {
                            foreach (QuestPackageItemRecord questPackageItem in questPackageItems)
                            {
                                ItemTemplate rewardProto = Global.ObjectMgr.GetItemTemplate(questPackageItem.ItemID);
                                if (rewardProto != null)
                                    if (rewardProto.ItemSpecClassMask.HasClass(GetClass()))
                                        GetSession().GetCollectionMgr().AddItemAppearance(questPackageItem.ItemID);
                            }
                        }

                        if (quest.CanIncreaseRewardedQuestCounters())
                            m_RewardedQuests.Add(quest_id);
                    }
                }
                while (result.NextRow());
            }
        }

        void _LoadDailyQuestStatus(SQLResult result)
        {
            m_DFQuests.Clear();

            //QueryResult* result = CharacterDatabase.PQuery("SELECT quest, time FROM character_queststatus_daily WHERE guid = '{0}'");
            if (!result.IsEmpty())
            {
                do
                {
                    int quest_id = result.Read<int>(0);
                    Quest qQuest = Global.ObjectMgr.GetQuestTemplate(quest_id);
                    if (qQuest != null)
                    {
                        if (qQuest.IsDFQuest())
                        {
                            m_DFQuests.Add(qQuest.Id);
                            m_lastDailyQuestTime = (ServerTime)(UnixTime64)result.Read<long>(1);
                            continue;
                        }
                    }

                    // save _any_ from daily quest times (it must be after last reset anyway)
                    m_lastDailyQuestTime = (ServerTime)(UnixTime64)result.Read<long>(1);

                    Quest quest = Global.ObjectMgr.GetQuestTemplate(quest_id);
                    if (quest == null)
                        continue;

                    AddDynamicUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.DailyQuestsCompleted), quest_id);
                    uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(quest_id);
                    if (questBit != 0)
                        SetQuestCompletedBit(questBit, true);

                    Log.outDebug(LogFilter.Player, $"Daily quest ({quest_id}) cooldown for player (GUID: {GetGUID()})");
                }
                while (result.NextRow());
            }

            m_DailyQuestChanged = false;
        }

        void _LoadWeeklyQuestStatus(SQLResult result)
        {
            m_weeklyquests.Clear();

            if (!result.IsEmpty())
            {
                do
                {
                    int quest_id = result.Read<int>(0);
                    Quest quest = Global.ObjectMgr.GetQuestTemplate(quest_id);
                    if (quest == null)
                        continue;

                    m_weeklyquests.Add(quest_id);
                    uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(quest_id);
                    if (questBit != 0)
                        SetQuestCompletedBit(questBit, true);

                    Log.outDebug(LogFilter.Player, $"Weekly quest {quest_id} cooldown for player (GUID: {GetGUID()})");
                }
                while (result.NextRow());
            }

            m_WeeklyQuestChanged = false;
        }

        void _LoadSeasonalQuestStatus(SQLResult result)
        {
            m_seasonalquests.Clear();

            if (!result.IsEmpty())
            {
                do
                {
                    int quest_id = result.Read<int>(0);
                    int event_id = result.Read<int>(1);
                    RealmTime completedTime = (RealmTime)(UnixTime64)result.Read<long>(2);
                    Quest quest = Global.ObjectMgr.GetQuestTemplate(quest_id);
                    if (quest == null)
                        continue;

                    if (!m_seasonalquests.ContainsKey(event_id))
                        m_seasonalquests[event_id] = new();

                    m_seasonalquests[event_id][quest_id] = completedTime;

                    uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(quest_id);
                    if (questBit != 0)
                        SetQuestCompletedBit(questBit, true);

                    Log.outDebug(LogFilter.Player, $"Seasonal quest {quest_id} cooldown for player (GUID: {GetGUID()})");
                }
                while (result.NextRow());
            }

            m_SeasonalQuestChanged = false;
        }

        void _LoadMonthlyQuestStatus(SQLResult result)
        {
            m_monthlyquests.Clear();

            if (!result.IsEmpty())
            {
                do
                {
                    int quest_id = result.Read<int>(0);
                    Quest quest = Global.ObjectMgr.GetQuestTemplate(quest_id);
                    if (quest == null)
                        continue;

                    m_monthlyquests.Add(quest_id);
                    uint questBit = Global.DB2Mgr.GetQuestUniqueBitFlag(quest_id);
                    if (questBit != 0)
                        SetQuestCompletedBit(questBit, true);

                    Log.outDebug(LogFilter.Player, $"Monthly quest {quest_id} cooldown for player (GUID: {GetGUID()})");
                }
                while (result.NextRow());
            }

            m_MonthlyQuestChanged = false;
        }

        void _LoadTalents(SQLResult result)
        {
            // "SELECT talentId, talentRank, talentGroup FROM character_talent WHERE guid = ?"
            if (!result.IsEmpty())
            {
                do
                {   if (CliDB.TalentStorage.LookupByKey(result.Read<int>(0)) is TalentRecord talent)
                    AddTalent(talent, result.Read<byte>(1), result.Read<byte>(2), false);
                }
                while (result.NextRow());
            }
        }

        void _LoadTraits(SQLResult configsResult, SQLResult entriesResult)
        {
            MultiMap<int, TraitEntryPacket> traitEntriesByConfig = new();
            if (!entriesResult.IsEmpty())
            {
                //                    0            1,                2     3             4
                // SELECT traitConfigId, traitNodeId, traitNodeEntryId, rank, grantedRanks FROM character_trait_entry WHERE guid = ?
                do
                {
                    TraitEntryPacket traitEntry = new();
                    traitEntry.TraitNodeID = entriesResult.Read<int>(1);
                    traitEntry.TraitNodeEntryID = entriesResult.Read<int>(2);
                    traitEntry.Rank = entriesResult.Read<int>(3);
                    traitEntry.GrantedRanks = entriesResult.Read<int>(4);

                    if (!TraitMgr.IsValidEntry(traitEntry))
                        continue;

                    traitEntriesByConfig.Add(entriesResult.Read<int>(0), traitEntry);

                } while (entriesResult.NextRow());
            }

            if (!configsResult.IsEmpty())
            {
                //                    0     1                    2                  3                4            5              6      7
                // SELECT traitConfigId, type, chrSpecializationId, combatConfigFlags, localIdentifier, skillLineId, traitSystemId, `name` FROM character_trait_config WHERE guid = ?
                do
                {
                    TraitConfigPacket traitConfig = new();
                    traitConfig.ID = configsResult.Read<int>(0);
                    traitConfig.Type = (TraitConfigType)configsResult.Read<int>(1);
                    switch (traitConfig.Type)
                    {
                        case TraitConfigType.Combat:
                            traitConfig.ChrSpecializationID = (ChrSpecialization)configsResult.Read<int>(2);
                            traitConfig.CombatConfigFlags = (TraitCombatConfigFlags)configsResult.Read<int>(3);
                            traitConfig.LocalIdentifier = configsResult.Read<int>(4);
                            break;
                        case TraitConfigType.Profession:
                            traitConfig.SkillLineID = (SkillType)configsResult.Read<int>(5);
                            break;
                        case TraitConfigType.Generic:
                            traitConfig.TraitSystemID = configsResult.Read<int>(6);
                            break;
                        default:
                            break;
                    }

                    traitConfig.Name = configsResult.Read<string>(7);

                    foreach (var traitEntry in traitEntriesByConfig[traitConfig.ID])
                        traitConfig.Entries.Add(traitEntry);

                    if (TraitMgr.ValidateConfig(traitConfig, this) != TalentLearnResult.LearnOk)
                    {
                        traitConfig.Entries.Clear();
                        foreach (TraitEntry grantedEntry in TraitMgr.GetGrantedTraitEntriesForConfig(traitConfig, this))
                            traitConfig.Entries.Add(new TraitEntryPacket(grantedEntry));
                    }

                    AddTraitConfig(traitConfig);

                } while (configsResult.NextRow());
            }

            bool hasConfigForSpec(int specId)
            {
                return m_activePlayerData.TraitConfigs.FindIndexIf(traitConfig =>
                {
                    return traitConfig.Type == (int)TraitConfigType.Combat
                        && traitConfig.ChrSpecializationID == specId
                        && (traitConfig.CombatConfigFlags & (int)TraitCombatConfigFlags.ActiveForSpec) != 0;
                }) >= 0;
            }

            int findFreeLocalIdentifier(int specId)
            {
                int index = 1;
                while (m_activePlayerData.TraitConfigs.FindIndexIf(traitConfig =>
                {
                    return traitConfig.Type == (int)TraitConfigType.Combat
                        && traitConfig.ChrSpecializationID == specId
                        && traitConfig.LocalIdentifier == index;
                }) >= 0)
                    ++index;

                return index;
            }

            for (int i = 0; i < PlayerConst.MaxSpecializations - 1 /*initial spec doesnt get a config*/; ++i)
            {
                var spec = Global.DB2Mgr.GetChrSpecializationByIndex(GetClass(), i);
                if (spec != null)
                {
                    if (hasConfigForSpec((int)spec.Id))
                        continue;

                    TraitConfigPacket traitConfig = new();
                    traitConfig.Type = TraitConfigType.Combat;
                    traitConfig.ChrSpecializationID = spec.Id;
                    traitConfig.CombatConfigFlags = TraitCombatConfigFlags.ActiveForSpec;
                    traitConfig.LocalIdentifier = findFreeLocalIdentifier((int)spec.Id);
                    traitConfig.Name = spec.Name[GetSession().GetSessionDbcLocale()];

                    CreateTraitConfig(traitConfig);
                }
            }

            int activeConfig = m_activePlayerData.TraitConfigs.FindIndexIf(traitConfig =>
            {
                return traitConfig.Type == (int)TraitConfigType.Combat
                    && traitConfig.ChrSpecializationID == (int)GetPrimarySpecialization()
                    && (traitConfig.CombatConfigFlags & (int)TraitCombatConfigFlags.ActiveForSpec) != 0;
            });

            if (activeConfig >= 0)
                SetActiveCombatTraitConfigID(m_activePlayerData.TraitConfigs[activeConfig].ID);

            foreach (TraitConfig traitConfig in m_activePlayerData.TraitConfigs)
            {
                switch ((TraitConfigType)(int)traitConfig.Type)
                {
                    case TraitConfigType.Combat:
                        if (traitConfig.ID != m_activePlayerData.ActiveCombatTraitConfigID)
                            continue;
                        break;
                    case TraitConfigType.Profession:
                        if (!HasSkill(traitConfig.SkillLineID))
                            continue;
                        break;
                    default:
                        break;
                }

                ApplyTraitConfig(traitConfig.ID, true);
            }
        }

        void _LoadGlyphs(SQLResult result)
        {
            // SELECT talentGroup, glyphSlot, glyphId from character_glyphs WHERE guid = ?
            if (result.IsEmpty())
                return;

            do
            {
                byte talentGroupId = result.Read<byte>(0);
                if (talentGroupId >= PlayerConst.MaxTalentSpecs)
                    continue;

                byte glyphSlot = result.Read<byte>(1);
                if (glyphSlot >= PlayerConst.MaxGlyphSlotIndex)
                    continue;

                int glyphId = result.Read<ushort>(2);
                if (CliDB.GlyphPropertiesStorage.LookupByKey(glyphId) == null)
                    continue;

                SetGlyph(glyphSlot, glyphId);

            } while (result.NextRow());
        }

        void _LoadGlyphAuras()
        {
            foreach (var glyphId in GetGlyphs(GetActiveTalentGroup()))
            {
                if (glyphId==0)
                    continue;
                var glyphProperty = CliDB.GlyphPropertiesStorage.LookupByKey(glyphId);
                Cypher.Assert(glyphProperty != null);

                CastSpell(this, glyphProperty.SpellID, true);
            }

            /*
            for (byte i = 0; i < PlayerConst.MaxGlyphSlotIndex; ++i)
            {
                if (GetGlyph(i) is int glyph && glyph != 0)
                {
                    CliDB.GlyphPropertiesStorage.TryGetValue(glyph, out GlyphPropertiesRecord gp);
                    if (gp != null)
                    {
                        CliDB.GlyphSlotStorage.TryGetValue(GetGlyphSlot(i), out GlyphSlotRecord gs);
                        if (gs != null)
                        {
                            if (gp.GlyphSlotFlags == gs.Type)
                            {
                                CastSpell(this, gp.SpellID, true);
                                    continue;
                            }
                            else
                                Log.outError(LogFilter.Player, "Player::_LoadGlyphAuras: Player '{0}' ({1}) has glyph with typeflags {2} in slot with typeflags {3}, removing.", GetName(), GetGUID().ToString(), gp.GlyphSlotFlags, gs.Type);
                        }
                        else
                            Log.outError(LogFilter.Player, "Player::_LoadGlyphAuras: Player '{0}' ({1}) has not existing glyph slot entry {2} on index {3}", GetName(), GetGUID().ToString(), GetGlyphSlot(i), i);
                    }
                    else
                        Log.outError(LogFilter.Player, "Player::_LoadGlyphAuras: Player '{0}' ({1}) has not existing glyph entry {2} on index {3}", GetName(), GetGUID().ToString(), glyph, i);

                    // On any error remove glyph
                    SetGlyph(i, 0);
                }
            }
            */
        }

        public void LoadCorpse(SQLResult result)
        {
            if (IsAlive() || HasAtLoginFlag(AtLoginFlags.Resurrect))
                SpawnCorpseBones(false);

            if (!IsAlive())
            {
                if (HasAtLoginFlag(AtLoginFlags.Resurrect))
                    ResurrectPlayer(0.5f);
                else if (!result.IsEmpty())
                {
                    _corpseLocation = new WorldLocation(result.Read<ushort>(0), result.Read<float>(1), result.Read<float>(2), result.Read<float>(3), result.Read<float>(4));
                    if (!CliDB.MapStorage.LookupByKey(_corpseLocation.GetMapId()).Instanceable)
                        SetPlayerLocalFlag(PlayerLocalFlags.ReleaseTimer);
                    else
                        RemovePlayerLocalFlag(PlayerLocalFlags.ReleaseTimer);
                }
            }

            RemoveAtLoginFlag(AtLoginFlags.Resurrect);
        }

        void _LoadVoidStorage(SQLResult result)
        {
            if (result.IsEmpty())
                return;

            do
            {   //           0      1           2     3             4                   5                   6                   7
                // "SELECT itemId, itemEntry, slot, creatorGuid, fixedScalingLevel, randomPropertiesId, randomPropertiesSeed, context FROM character_void_storage WHERE playerGuid = ?"
                var itemId = result.Read<long>(0);
                var itemEntry = result.Read<int>(1);
                var slot = result.Read<byte>(2);
                var creatorGuid = result.Read<long>(3) != 0 ? ObjectGuid.Create(HighGuid.Player, result.Read<long>(3)) : ObjectGuid.Empty;
                var fixedScalingLevel = result.Read<int>(4);
                var randomProperties = new ItemRandomProperties(
                    result.Read<int>(5), result.Read<int>(6)
                    );
                var context = (ItemContext)result.Read<byte>(7);
                
                if (itemId == 0)
                {
                    Log.outError(LogFilter.Player, 
                        $"Player._LoadVoidStorage - Player (GUID: {GetGUID()}, name: {GetName()}) " +
                        $"has an item with an invalid id (item id: item id: {itemId}, entry: {itemEntry}).");
                    continue;
                }

                if (Global.ObjectMgr.GetItemTemplate(itemEntry) == null)
                {
                    Log.outError(LogFilter.Player, 
                        $"Player._LoadVoidStorage - Player (GUID: {GetGUID()}, name: {GetName()}) " +
                        $"has an item with an invalid entry (item id: item id: {itemId}, entry: {itemEntry}).");
                    continue;
                }

                if (slot >= SharedConst.VoidStorageMaxSlot)
                {
                    Log.outError(LogFilter.Player, 
                        $"Player._LoadVoidStorage - Player (GUID: {GetGUID()}, name: {GetName()}) " +
                        $"has an item with an invalid slot (item id: item id: {itemId}, entry: {itemEntry}, slot: {slot}).");
                    continue;
                }

                _voidStorageItems[slot] = new VoidStorageItem(itemId, itemEntry, creatorGuid, fixedScalingLevel, randomProperties, context);

                BonusData bonus = new(new ItemInstance(_voidStorageItems[slot]));
                GetSession().GetCollectionMgr().AddItemAppearance(itemEntry, bonus.AppearanceModID);
            }
            while (result.NextRow());
        }

        public void _LoadMail(SQLResult mailsResult, SQLResult mailItemsResult)
        {
            Dictionary<long, Mail> mailById = new();

            if (!mailsResult.IsEmpty())
            {
                do
                {
                    Mail m = new();

                    m.messageID = mailsResult.Read<long>(0);
                    m.messageType = (MailMessageType)mailsResult.Read<byte>(1);
                    m.sender = mailsResult.Read<long>(2);
                    m.receiver = mailsResult.Read<long>(3);
                    m.subject = mailsResult.Read<string>(4);
                    m.body = mailsResult.Read<string>(5);
                    m.expire_time = (ServerTime)(UnixTime64)mailsResult.Read<long>(6);
                    m.deliver_time = (ServerTime)(UnixTime64)mailsResult.Read<long>(7);
                    m.money = mailsResult.Read<long>(8);
                    m.COD = mailsResult.Read<long>(9);
                    m.checkMask = (MailCheckFlags)mailsResult.Read<byte>(10);
                    m.stationery = (MailStationery)mailsResult.Read<byte>(11);
                    m.mailTemplateId = mailsResult.Read<ushort>(12);

                    if (m.mailTemplateId != 0 && !CliDB.MailTemplateStorage.ContainsKey(m.mailTemplateId))
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player:_LoadMail - Mail ({m.messageID}) " +
                            $"have not existed MailTemplateId ({m.mailTemplateId}), remove at load");

                        m.mailTemplateId = 0;
                    }

                    m.state = MailState.Unchanged;

                    m_mail.Add(m);
                    mailById[m.messageID] = m;
                }
                while (mailsResult.NextRow());
            }

            if (!mailItemsResult.IsEmpty())
            {
                do
                {
                    var mailId = mailItemsResult.Read<long>(47);
                    _LoadMailedItem(GetGUID(), this, mailId, mailById[mailId], mailItemsResult.GetFields());
                }
                while (mailItemsResult.NextRow());
            }

            UpdateNextMailTimeAndUnreads();
        }

        static Item _LoadMailedItem(ObjectGuid playerGuid, Player player, long mailId, Mail mail, SQLFields fields)
        {
            var itemGuid = fields.Read<long>(0);
            var itemEntry = fields.Read<int>(1);

            ItemTemplate proto = Global.ObjectMgr.GetItemTemplate(itemEntry);
            if (proto == null)
            {
                Log.outError(LogFilter.Player, 
                    $"Player {(player != null ? player.GetName() : "<unknown>")} ({playerGuid}) " +
                    $"has unknown item in mailed items (GUID: {itemGuid} template: {itemEntry}) in mail ({mailId}), deleted.");

                SQLTransaction trans = new();

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_INVALID_MAIL_ITEM);
                stmt.SetInt64(0, itemGuid);
                trans.Append(stmt);

                Item.DeleteFromDB(trans, itemGuid);

                DB.Characters.CommitTransaction(trans);
                return null;
            }

            Item item = Bag.NewItemOrBag(proto);
            ObjectGuid ownerGuid = fields.Read<long>(47) != 0 ? ObjectGuid.Create(HighGuid.Player, fields.Read<long>(47)) : ObjectGuid.Empty;
            if (!item.LoadFromDB(itemGuid, ownerGuid, fields, itemEntry))
            {
                Log.outError(LogFilter.Player, 
                    $"Player._LoadMailedItems: Item (GUID: {itemGuid}) in mail ({mailId}) doesn't exist, deleted from mail.");

                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_ITEM);
                stmt.SetInt64(0, itemGuid);
                DB.Characters.Execute(stmt);

                item.FSetState(ItemUpdateState.Removed);

                item.SaveToDB(null);                               // it also deletes item object !
                return null;
            }

            if (mail != null)
                mail.AddItem(itemGuid, itemEntry);

            if (player != null)
                player.AddMItem(item);

            return item;
        }

        void _LoadDeclinedNames(SQLResult result)
        {
            if (result.IsEmpty())
                return;
            
            _declinedname = new DeclinedName();
            
            for (byte i = 0; i < SharedConst.MaxDeclinedNameCases; ++i)
                _declinedname.Name[i] = result.Read<string>(i);
        }

        void _LoadArenaTeamInfo(SQLResult result)
        {
            // arenateamid, played_week, played_season, personal_rating
            ushort[] personalRatingCache = [0, 0, 0];

            if (!result.IsEmpty())
            {
                do
                {
                    int arenaTeamId = result.Read<int>(0);

                    ArenaTeam arenaTeam = Global.ArenaTeamMgr.GetArenaTeamById(arenaTeamId);
                    if (arenaTeam == null)
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player:_LoadArenaTeamInfo: couldn't load arenateam {arenaTeamId}");
                        continue;
                    }

                    byte arenaSlot = arenaTeam.GetSlot();

                    personalRatingCache[arenaSlot] = result.Read<ushort>(4);

                    SetArenaTeamInfoField(arenaSlot, ArenaTeamInfoType.Id, arenaTeamId);
                    SetArenaTeamInfoField(arenaSlot, ArenaTeamInfoType.Type, arenaTeam.GetArenaType());
                    SetArenaTeamInfoField(arenaSlot, ArenaTeamInfoType.Member, arenaTeam.GetCaptain() == GetGUID() ? 0 : 1);
                    SetArenaTeamInfoField(arenaSlot, ArenaTeamInfoType.GamesWeek, result.Read<ushort>(1));
                    SetArenaTeamInfoField(arenaSlot, ArenaTeamInfoType.GamesSeason, result.Read<ushort>(2));
                    SetArenaTeamInfoField(arenaSlot, ArenaTeamInfoType.WinsSeason, result.Read<ushort>(3));
                }
                while (result.NextRow());
            }

            for (byte slot = 0; slot <= 2; ++slot)
            {
                SetArenaTeamInfoField(slot, ArenaTeamInfoType.PersonalRating, personalRatingCache[slot]);
            }
        }

        void _LoadStoredAuraTeleportLocations(SQLResult result)
        {
            //                                                       0      1      2          3          4          5
            //QueryResult* result = CharacterDatabase.PQuery("SELECT Spell, MapId, PositionX, PositionY, PositionZ, Orientation FROM character_spell_location WHERE Guid = ?", GetGUIDLow());

            m_storedAuraTeleportLocations.Clear();
            if (!result.IsEmpty())
            {
                do
                {
                    int spellId = result.Read<int>(0);

                    if (!Global.SpellMgr.HasSpellInfo(spellId, Difficulty.None))
                    {
                        Log.outError(LogFilter.Spells, 
                            $"Player._LoadStoredAuraTeleportLocations: " +
                            $"Player {GetName()} ({GetGUID()}) spell (ID: {spellId}) does not exist");
                        continue;
                    }

                    WorldLocation location = new(result.Read<int>(1), result.Read<float>(2), result.Read<float>(3), result.Read<float>(4), result.Read<float>(5));
                    if (!GridDefines.IsValidMapCoord(location))
                    {
                        Log.outError(LogFilter.Spells, 
                            $"Player._LoadStoredAuraTeleportLocations: Player {GetName()} ({GetGUID()}) spell (ID: {spellId}) " +
                            $"has invalid position on map {location.GetMapId()}, {location}.");
                        continue;
                    }

                    StoredAuraTeleportLocation storedLocation = new();
                    storedLocation.Loc = location;
                    storedLocation.CurrentState = StoredAuraTeleportLocation.State.Unchanged;

                    m_storedAuraTeleportLocations[spellId] = storedLocation;
                }
                while (result.NextRow());
            }
        }

        void _LoadGroup(SQLResult result)
        {
            if (!result.IsEmpty())
            {
                Group group = Global.GroupMgr.GetGroupByDbStoreId(result.Read<int>(0));
                if (group != null)
                {
                    if (group.IsLeader(GetGUID()))
                        SetPlayerFlag(PlayerFlags.GroupLeader);

                    byte subgroup = group.GetMemberGroup(GetGUID());
                    SetGroup(group, subgroup);
                    SetPartyType(group.GetGroupCategory(), GroupType.Normal);
                    ResetGroupUpdateSequenceIfNeeded(group);

                    // the group leader may change the instance difficulty while the player is offline
                    SetDungeonDifficultyID(group.GetDungeonDifficultyID());
                    SetRaidDifficultyID(group.GetRaidDifficultyID());
                    SetLegacyRaidDifficultyID(group.GetLegacyRaidDifficultyID());
                }
            }

            if (GetGroup() == null || !GetGroup().IsLeader(GetGUID()))
                RemovePlayerFlag(PlayerFlags.GroupLeader);
        }

        void _LoadInstanceTimeRestrictions(SQLResult result)
        {
            if (result.IsEmpty())
                return;

            do
            {
                _instanceResetTimes.Add(result.Read<int>(0), (ServerTime)(UnixTime64)result.Read<long>(1));
            } while (result.NextRow());
        }

        void _LoadEquipmentSets(SQLResult result)
        {
            if (result.IsEmpty())
                return;

            do
            {
                EquipmentSetInfo eqSet = new();
                eqSet.Data.Guid = result.Read<long>(0);
                eqSet.Data.Type = EquipmentSetInfo.EquipmentSetType.Equipment;
                eqSet.Data.SetID = result.Read<byte>(1);
                eqSet.Data.SetName = result.Read<string>(2);
                eqSet.Data.SetIcon = result.Read<string>(3);
                eqSet.Data.IgnoreMask = result.Read<uint>(4);
                eqSet.Data.AssignedSpecIndex = result.Read<int>(5);
                eqSet.state = EquipmentSetUpdateState.Unchanged;

                for (int i = EquipmentSlot.Start; i < EquipmentSlot.End; ++i)
                {
                    long guid = result.Read<long>(6 + i);
                    if (guid != 0)
                        eqSet.Data.Pieces[i] = ObjectGuid.Create(HighGuid.Item, guid);
                }

                if (eqSet.Data.SetID >= ItemConst.MaxEquipmentSetIndex)   // client limit
                    continue;

                _equipmentSets[eqSet.Data.Guid] = eqSet;
            }
            while (result.NextRow());
        }

        void _LoadTransmogOutfits(SQLResult result)
        {
            //             0         1     2         3            4            5            6            7            8            9
            //SELECT setguid, setindex, name, iconname, ignore_mask, appearance0, appearance1, appearance2, appearance3, appearance4,
            //             10           11           12           13           14            15            16            17            18            19            20            21
            //    appearance5, appearance6, appearance7, appearance8, appearance9, appearance10, appearance11, appearance12, appearance13, appearance14, appearance15, appearance16,
            //              22            23               24              25
            //    appearance17, appearance18, mainHandEnchant, offHandEnchant FROM character_transmog_outfits WHERE guid = ? ORDER BY setindex
            if (result.IsEmpty())
                return;

            do
            {
                EquipmentSetInfo eqSet = new();

                eqSet.Data.Guid = result.Read<long>(0);
                eqSet.Data.Type = EquipmentSetInfo.EquipmentSetType.Transmog;
                eqSet.Data.SetID = result.Read<byte>(1);
                eqSet.Data.SetName = result.Read<string>(2);
                eqSet.Data.SetIcon = result.Read<string>(3);
                eqSet.Data.IgnoreMask = result.Read<uint>(4);
                eqSet.state = EquipmentSetUpdateState.Unchanged;

                for (int i = EquipmentSlot.Start; i < EquipmentSlot.End; ++i)
                    eqSet.Data.Appearances[i] = result.Read<int>(5 + i);

                for (int i = 0; i < eqSet.Data.Enchants.Length; ++i)
                    eqSet.Data.Enchants[i] = result.Read<int>(24 + i);

                if (eqSet.Data.SetID >= ItemConst.MaxEquipmentSetIndex)   // client limit
                    continue;

                _equipmentSets[eqSet.Data.Guid] = eqSet;
            } while (result.NextRow());
        }

        void _LoadCUFProfiles(SQLResult result)
        {
            if (result.IsEmpty())
                return;

            do
            {
                byte id = result.Read<byte>(0);
                string name = result.Read<string>(1);
                ushort frameHeight = result.Read<ushort>(2);
                ushort frameWidth = result.Read<ushort>(3);
                byte sortBy = result.Read<byte>(4);
                byte healthText = result.Read<byte>(5);
                uint boolOptions = result.Read<uint>(6);
                byte topPoint = result.Read<byte>(7);
                byte bottomPoint = result.Read<byte>(8);
                byte leftPoint = result.Read<byte>(9);
                ushort topOffset = result.Read<ushort>(10);
                ushort bottomOffset = result.Read<ushort>(11);
                ushort leftOffset = result.Read<ushort>(12);

                if (id > PlayerConst.MaxCUFProfiles)
                {
                    Log.outError(LogFilter.Player, 
                        $"Player._LoadCUFProfiles - Player (GUID: {GetGUID()}, name: {GetName()}) " +
                        $"has an CUF profile with invalid id (id: {id}), max is {PlayerConst.MaxCUFProfiles}.");
                    continue;
                }

                _CUFProfiles[id] = new CUFProfile(name, frameHeight, frameWidth, sortBy, healthText, boolOptions, topPoint, bottomPoint, leftPoint, topOffset, bottomOffset, leftOffset);
            }
            while (result.NextRow());
        }

        void _LoadRandomBGStatus(SQLResult result)
        {
            if (!result.IsEmpty())
                m_IsBGRandomWinner = true;
        }

        void _LoadBGData(SQLResult result)
        {
            if (result.IsEmpty())
                return;

            // Expecting only one row
            //         0           1     2      3      4      5      6          7          8        9           10
            // SELECT instanceId, team, joinX, joinY, joinZ, joinO, joinMapId, taxiStart, taxiEnd, mountSpell, queueTypeId FROM character_Battleground_data WHERE guid = ?
            m_bgData.bgInstanceID = result.Read<int>(0);
            m_bgData.bgTeam = (Team)result.Read<ushort>(1);
            m_bgData.joinPos = new WorldLocation(result.Read<ushort>(6), result.Read<float>(2), result.Read<float>(3), result.Read<float>(4), result.Read<float>(5));
            m_bgData.taxiPath[0] = result.Read<int>(7);
            m_bgData.taxiPath[1] = result.Read<int>(8);
            m_bgData.mountSpell = result.Read<int>(9);
            m_bgData.queueId = BattlegroundQueueTypeId.FromPacked(result.Read<long>(10));
        }

        void _LoadPetStable(int summonedPetNumber, SQLResult result)
        {
            if (result.IsEmpty())
                return;

            m_petStable = new();

            //         0      1        2      3    4           5     6     7        8          9       10      11        12              13       14              15
            // SELECT id, entry, modelid, level, exp, Reactstate, slot, name, renamed, curhealth, curmana, abdata, savetime, CreatedBySpell, PetType, specialization FROM character_pet WHERE owner = ?

            do
            {
                PetStable.PetInfo petInfo = new();
                petInfo.PetNumber = result.Read<int>(0);
                petInfo.CreatureId = result.Read<int>(1);
                petInfo.DisplayId = result.Read<int>(2);
                petInfo.Level = result.Read<byte>(3);
                petInfo.Experience = result.Read<int>(4);
                petInfo.ReactState = (ReactStates)result.Read<byte>(5);
                PetSaveMode slot = (PetSaveMode)result.Read<short>(6);
                petInfo.Name = result.Read<string>(7);
                petInfo.WasRenamed = result.Read<bool>(8);
                petInfo.Health = result.Read<int>(9);
                petInfo.Mana = result.Read<int>(10);
                petInfo.ActionBar = result.Read<string>(11);
                petInfo.LastSaveTime = (ServerTime)(UnixTime)result.Read<int>(12);
                petInfo.CreatedBySpellId = result.Read<int>(13);
                petInfo.Type = (PetType)result.Read<byte>(14);
                petInfo.SpecializationId = (ChrSpecialization)result.Read<ushort>(15);
                if (slot >= PetSaveMode.FirstActiveSlot && slot < PetSaveMode.LastActiveSlot)
                {
                    m_petStable.ActivePets[(int)slot] = petInfo;

                    if (m_petStable.ActivePets[(int)slot].Type == PetType.Hunter)
                        AddPetToUpdateFields(m_petStable.ActivePets[(int)slot], slot, PetStableFlags.Active);
                }
                else if (slot >= PetSaveMode.FirstStableSlot && slot < PetSaveMode.LastStableSlot)
                {
                    m_petStable.StabledPets[slot - PetSaveMode.FirstStableSlot] = petInfo;

                    if (m_petStable.StabledPets[slot - PetSaveMode.FirstStableSlot].Type == PetType.Hunter)
                        AddPetToUpdateFields(m_petStable.StabledPets[slot - PetSaveMode.FirstStableSlot], slot, PetStableFlags.Inactive);
                }
                else if (slot == PetSaveMode.NotInSlot)
                    m_petStable.UnslottedPets.Add(petInfo);

            } while (result.NextRow());

            if (Pet.GetLoadPetInfo(m_petStable, 0, summonedPetNumber, null).Item1 != null)
                m_temporaryUnsummonedPetNumber = summonedPetNumber;
        }

        void _SaveInventory(SQLTransaction trans)
        {
            PreparedStatement stmt;
            // force items in buyback slots to new state
            // and remove those that aren't already
            for (var i = InventorySlots.BuyBackStart; i < InventorySlots.BuyBackEnd; ++i)
            {
                Item item = m_items[i];
                if (item == null)
                    continue;

                ItemTemplate itemTemplate = item.GetTemplate();

                if (item.GetState() == ItemUpdateState.New)
                {
                    if (itemTemplate != null)
                    {
                        if (itemTemplate.HasFlag(ItemFlags.HasLoot))
                            Global.LootItemStorage.RemoveStoredLootForContainer(item.GetGUID().GetCounter());
                    }

                    continue;
                }

                item.DeleteFromInventoryDB(trans);
                item.DeleteFromDB(trans);
                m_items[i].FSetState(ItemUpdateState.New);

                if (itemTemplate != null)
                {
                    if (itemTemplate.HasFlag(ItemFlags.HasLoot))
                        Global.LootItemStorage.RemoveStoredLootForContainer(item.GetGUID().GetCounter());
                }
            }

            // Updated played time for refundable items. We don't do this in Player.Update because there's simply no need for it,
            // the client auto counts down in real time after having received the initial played time on the first
            // SMSG_ITEM_REFUND_INFO_RESPONSE packet.
            // Item.UpdatePlayedTime is only called when needed, which is in DB saves, and item refund info requests.
            foreach (var guid in m_refundableItems)
            {
                Item item = GetItemByGuid(guid);
                if (item != null)
                {
                    item.UpdatePlayedTime(this);
                    continue;
                }
                else
                {
                    Log.outError(LogFilter.Player, 
                        $"Can't find item guid {guid} but is in refundable storage for player {GetGUID()} ! Removing.");

                    m_refundableItems.Remove(guid);
                }
            }

            // update enchantment durations
            foreach (var enchant in m_enchantDuration)
                enchant.item.SetEnchantmentDuration(enchant.slot, enchant.leftduration, this);

            // if no changes
            if (ItemUpdateQueue.Count == 0)
                return;

            for (var i = 0; i < ItemUpdateQueue.Count; ++i)
            {
                Item item = ItemUpdateQueue[i];
                if (item == null)
                    continue;

                Bag container = item.GetContainer();
                if (item.GetState() != ItemUpdateState.Removed)
                {
                    Item test = GetItemByPos(item.InventoryPosition);
                    if (test == null)
                    {
                        long bagTestGUID = 0;
                        Item test2 = GetItemByPos(item.InventoryBagSlot);
                        if (test2 != null)
                            bagTestGUID = test2.GetGUID().GetCounter();

                        Log.outError(LogFilter.Player, $"Player(GUID: {GetGUID()} Name: {GetName()}).SaveInventory - the bag({item.InventoryBagSlot}) and " +
                            $"slot({item.InventorySlot}) values for the item with guid {item.GetGUID()} (state {item.GetState()}) are incorrect, " +
                            "the player doesn't have an item at that position!");

                        // according to the test that was just performed nothing should be in this slot, delete
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_INVENTORY_BY_BAG_SLOT);
                        stmt.SetInt64(0, bagTestGUID);
                        stmt.SetUInt8(1, item.InventorySlot);
                        stmt.SetInt64(2, GetGUID().GetCounter());
                        trans.Append(stmt);

                        RemoveTradeableItem(item);
                        RemoveEnchantmentDurationsReferences(item);
                        RemoveItemDurations(item);

                        // also THIS item should be somewhere else, cheat attempt
                        item.FSetState(ItemUpdateState.Removed); // we are IN updateQueue right now, can't use SetState which modifies the queue
                        DeleteRefundReference(item.GetGUID());
                    }
                    else if (test != item)
                    {
                        Log.outError(LogFilter.Player, $"Player(GUID: {GetGUID()} Name: {GetName()}).SaveInventory - the bag({item.InventoryBagSlot}) and " +
                            $"slot({item.InventorySlot}) values for the item with guid {item.GetGUID()} are incorrect, " +
                            $"the item with guid {test.GetGUID()} is there instead!");
                        
                        // save all changes to the item...
                        if (item.GetState() != ItemUpdateState.New) // only for existing items, no dupes
                            item.SaveToDB(trans);
                        // ...but do not save position in inventory
                        continue;
                    }
                }

                switch (item.GetState())
                {
                    case ItemUpdateState.New:
                    case ItemUpdateState.Changed:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.REP_INVENTORY_ITEM);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetInt64(1, container != null ? container.GetGUID().GetCounter() : 0);
                        stmt.SetUInt8(2, item.InventorySlot);
                        stmt.SetInt64(3, item.GetGUID().GetCounter());
                        trans.Append(stmt);
                        break;
                    case ItemUpdateState.Removed:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_INVENTORY_BY_ITEM);
                        stmt.SetInt64(0, item.GetGUID().GetCounter());
                        trans.Append(stmt);
                        break;
                    case ItemUpdateState.Unchanged:
                        break;
                }

                item.SaveToDB(trans);                                   // item have unchanged inventory record and can be save standalone
            }
            ItemUpdateQueue.Clear();
        }

        void _SaveSkills(SQLTransaction trans)
        {
            PreparedStatement stmt;// = null;

            SkillInfo skillInfoField = m_activePlayerData.Skill;

            foreach (var pair in mSkillStatus.ToList())
            {
                if (pair.Value.State == SkillState.Unchanged)
                    continue;

                ushort value = skillInfoField.SkillRank[pair.Value.Pos];
                ushort max = skillInfoField.SkillMaxRank[pair.Value.Pos];
                sbyte professionSlot = (sbyte)GetProfessionSlotFor(pair.Key);

                switch (pair.Value.State)
                {
                    case SkillState.New:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_SKILLS);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetUInt16(1, (ushort)pair.Key);
                        stmt.SetUInt16(2, value);
                        stmt.SetUInt16(3, max);
                        stmt.SetInt8(4, professionSlot);
                        trans.Append(stmt);
                        break;
                    case SkillState.Changed:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHAR_SKILLS);
                        stmt.SetUInt16(0, value);
                        stmt.SetUInt16(1, max);
                        stmt.SetInt8(2, professionSlot);
                        stmt.SetInt64(3, GetGUID().GetCounter());
                        stmt.SetUInt16(4, (ushort)pair.Key);
                        trans.Append(stmt);
                        break;
                    case SkillState.Deleted:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_SKILL_BY_SKILL);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetUInt16(1, (ushort)pair.Key);
                        trans.Append(stmt);
                        break;
                    default:
                        break;
                }
                pair.Value.State = SkillState.Unchanged;
            }
        }

        void _SaveSpells(SQLTransaction trans)
        {
            PreparedStatement stmt;
            List<int> removeList = new();

            foreach (var (id, spell) in SpellBook.Spells)
            {
                if (spell.State == PlayerSpellState.Removed || spell.State == PlayerSpellState.Changed)
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_SPELL_BY_SPELL);
                    stmt.SetInt32(0, id);
                    stmt.SetInt64(1, GetGUID().GetCounter());
                    trans.Append(stmt);
                }

                if (spell.State == PlayerSpellState.New || spell.State == PlayerSpellState.Changed)
                {
                    // add only changed/new not dependent spells
                    if (!spell.Dependent)
                    {
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_SPELL);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetInt32(1, id);
                        stmt.SetBool(2, spell.Active);
                        stmt.SetBool(3, spell.Disabled);
                        trans.Append(stmt);
                    }

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_SPELL_FAVORITE);
                    stmt.SetInt32(0, id);
                    stmt.SetInt64(1, GetGUID().GetCounter());
                    trans.Append(stmt);

                    if (spell.Favorite)
                    {
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_SPELL_FAVORITE);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetInt32(1, id);
                        trans.Append(stmt);
                    }
                }

                if (spell.State == PlayerSpellState.Removed)
                {
                    removeList.Add(id);
                    continue;
                }

                if (spell.State != PlayerSpellState.Temporary)
                    spell.State = PlayerSpellState.Unchanged;
            }

            foreach (var id in removeList)
                SpellBook.Remove(id);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_TRADE_SKILL_SPELLS);
            stmt.SetInt64(0, GetGUID().GetCounter());
            trans.Append(stmt);

            foreach(var skill in SpellBook.TradeSkillSpells.Keys)
            {
                foreach (var spell in SpellBook.TradeSkillSpells[skill])
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHARACTER_TRADE_SKILL_SPELL);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    stmt.SetInt16(1, (short)skill);
                    stmt.SetInt32(2, spell);
                    trans.Append(stmt);
                }
            }            
        }

        void _SaveAuras(SQLTransaction trans)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_AURA_EFFECT);
            stmt.SetInt64(0, GetGUID().GetCounter());
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_AURA);
            stmt.SetInt64(0, GetGUID().GetCounter());
            trans.Append(stmt);

            byte index;
            foreach (var pair in GetOwnedAuras())
            {
                Aura aura = pair.Value;
                if (!aura.CanBeSaved())
                    continue;

                uint recalculateMask;
                AuraKey key = aura.GenerateKey(out recalculateMask);

                index = 0;
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_AURA);
                stmt.SetInt64(index++, GetGUID().GetCounter());
                stmt.SetBytes(index++, key.Caster.GetRawValue());
                stmt.SetBytes(index++, key.Item.GetRawValue());
                stmt.SetInt32(index++, key.SpellId);
                stmt.SetUInt32(index++, key.EffectMask);
                stmt.SetUInt32(index++, recalculateMask);
                stmt.SetUInt8(index++, (byte)aura.GetCastDifficulty());
                stmt.SetUInt8(index++, aura.GetStackAmount());
                stmt.SetInt32(index++, aura.GetMaxDuration());
                stmt.SetInt32(index++, aura.GetDuration());
                stmt.SetUInt8(index++, aura.GetCharges());
                stmt.SetInt32(index++, aura.GetCastItemId());
                stmt.SetInt32(index, aura.GetCastItemLevel());
                trans.Append(stmt);

                foreach (AuraEffect effect in aura.GetAuraEffects())
                {
                    if (effect != null)
                    {
                        index = 0;
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_AURA_EFFECT);
                        stmt.SetInt64(index++, GetGUID().GetCounter());
                        stmt.SetBytes(index++, key.Caster.GetRawValue());
                        stmt.SetBytes(index++, key.Item.GetRawValue());
                        stmt.SetInt32(index++, key.SpellId);
                        stmt.SetUInt32(index++, key.EffectMask);
                        stmt.SetInt32(index++, effect.GetEffIndex());
                        stmt.SetInt32(index++, effect.GetAmount());
                        stmt.SetInt32(index++, effect.GetBaseAmount());
                        trans.Append(stmt);
                    }
                }
            }
        }

        void _SaveGlyphs(SQLTransaction trans)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_GLYPHS);
            stmt.SetInt64(0, GetGUID().GetCounter());
            trans.Append(stmt);

            for (byte spec = 0; spec < PlayerConst.MaxTalentSpecs; spec++)
            {
                for (byte i = 0; i < GetGlyphs(spec).Length; i++)
                {
                    byte index = 0;
                    int glyphId = GetGlyphs(spec)[i];
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_GLYPHS);
                    stmt.SetInt64(index++, GetGUID().GetCounter());
                    stmt.SetUInt8(index++, spec);
                    stmt.SetUInt8(index++, i);
                    stmt.SetUInt16(index++, (ushort)glyphId);

                    trans.Append(stmt);
                }
            }
        }

        void _SaveCurrency(SQLTransaction trans)
        {
            PreparedStatement stmt;
            foreach (var (id, currency) in _currencyStorage)
            {
                CurrencyTypesRecord entry = CliDB.CurrencyTypesStorage.LookupByKey(id);
                if (entry == null) // should never happen
                    continue;

                switch (currency.state)
                {
                    case PlayerCurrencyState.New:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.REP_PLAYER_CURRENCY);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetUInt16(1, (ushort)id);
                        stmt.SetInt32(2, currency.Quantity);
                        stmt.SetInt32(3, currency.WeeklyQuantity);
                        stmt.SetInt32(4, currency.TrackedQuantity);
                        stmt.SetInt32(5, currency.IncreasedCapQuantity);
                        stmt.SetInt32(6, currency.EarnedQuantity);
                        stmt.SetUInt8(7, (byte)currency.Flags);
                        trans.Append(stmt);
                        break;
                    case PlayerCurrencyState.Changed:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_PLAYER_CURRENCY);
                        stmt.SetInt32(0, currency.Quantity);
                        stmt.SetInt32(1, currency.WeeklyQuantity);
                        stmt.SetInt32(2, currency.TrackedQuantity);
                        stmt.SetInt32(3, currency.IncreasedCapQuantity);
                        stmt.SetInt32(4, currency.EarnedQuantity);
                        stmt.SetUInt8(5, (byte)currency.Flags);
                        stmt.SetInt64(6, GetGUID().GetCounter());
                        stmt.SetUInt16(7, (ushort)id);
                        trans.Append(stmt);
                        break;
                    default:
                        break;
                }

                currency.state = PlayerCurrencyState.Unchanged;
            }
        }

        public static void SavePlayerCustomizations(SQLTransaction trans, long guid, List<ChrCustomizationChoice> customizations)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_CUSTOMIZATIONS);
            stmt.SetInt64(0, guid);
            trans.Append(stmt);

            foreach (var customization in customizations)
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHARACTER_CUSTOMIZATION);
                stmt.SetInt64(0, guid);
                stmt.SetInt32(1, customization.ChrCustomizationOptionID);
                stmt.SetInt32(2, customization.ChrCustomizationChoiceID);
                trans.Append(stmt);
            }
        }

        public static void SaveCustomizations(SQLTransaction trans, long guid, List<ChrCustomizationChoice> customizations)
        {
            SavePlayerCustomizations(trans, guid, customizations);
        }

        void _SaveCustomizations(SQLTransaction trans)
        {
            if (!m_customizationsChanged)
                return;

            m_customizationsChanged = false;

            SavePlayerCustomizations(trans, GetGUID().GetCounter(), m_playerData.Customizations);
        }

        void _SaveActions(SQLTransaction trans)
        {
            int traitConfigId = 0;
            
            TraitConfig traitConfig = GetTraitConfig(m_activePlayerData.ActiveCombatTraitConfigID);
            if (traitConfig != null)
            {
                int usedSavedTraitConfigIndex = m_activePlayerData.TraitConfigs.FindIndexIf(savedConfig =>
                {
                    return (TraitConfigType)(int)savedConfig.Type == TraitConfigType.Combat
                        && ((TraitCombatConfigFlags)(int)savedConfig.CombatConfigFlags & TraitCombatConfigFlags.ActiveForSpec) == TraitCombatConfigFlags.None
                        && ((TraitCombatConfigFlags)(int)savedConfig.CombatConfigFlags & TraitCombatConfigFlags.SharedActionBars) == TraitCombatConfigFlags.None
                        && savedConfig.LocalIdentifier == traitConfig.LocalIdentifier;
                });

                if (usedSavedTraitConfigIndex >= 0)
                    traitConfigId = m_activePlayerData.TraitConfigs[usedSavedTraitConfigIndex].ID;
            }

            PreparedStatement stmt;

            foreach (var pair in m_actionButtons.ToList())
            {
                switch (pair.Value.State)
                {
                    case ActionButtonUpdateState.New:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_ACTION);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetUInt8(1, GetActiveTalentGroup());
                        stmt.SetInt32(2, traitConfigId);
                        stmt.SetUInt8(3, pair.Key);
                        stmt.SetInt32(4, pair.Value.Action);
                        stmt.SetUInt8(5, (byte)pair.Value.Type);
                        trans.Append(stmt);

                        pair.Value.State = ActionButtonUpdateState.UnChanged;
                        break;
                    case ActionButtonUpdateState.Changed:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHAR_ACTION);
                        stmt.SetInt32(0, pair.Value.Action);
                        stmt.SetUInt8(1, (byte)pair.Value.Type);
                        stmt.SetInt64(2, GetGUID().GetCounter());
                        stmt.SetUInt8(3, pair.Key);
                        stmt.SetUInt8(4, GetActiveTalentGroup());
                        stmt.SetInt32(5, traitConfigId);
                        trans.Append(stmt);

                        pair.Value.State = ActionButtonUpdateState.UnChanged;
                        break;
                    case ActionButtonUpdateState.Deleted:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_ACTION_BY_BUTTON_SPEC);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetUInt8(1, pair.Key);
                        stmt.SetUInt8(2, GetActiveTalentGroup());
                        stmt.SetInt32(3, traitConfigId);
                        trans.Append(stmt);

                        m_actionButtons.Remove(pair.Key);
                        break;
                    default:
                        break;
                }
            }
        }

        void _SaveQuestStatus(SQLTransaction trans)
        {
            bool isTransaction = trans != null;
            if (!isTransaction)
                trans = new SQLTransaction();

            PreparedStatement stmt;
            bool keepAbandoned = !Global.WorldMgr.GetCleaningFlags().HasAnyFlag(CleaningFlags.Queststatus);

            foreach (var save in m_QuestStatusSave)
            {
                if (save.Value == QuestSaveType.Default)
                {
                    var data = m_QuestStatus.LookupByKey(save.Key);
                    if (data != null && (keepAbandoned || data.Status != QuestStatus.None))
                    {
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.REP_CHAR_QUESTSTATUS);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetInt32(1, save.Key);
                        stmt.SetUInt8(2, (byte)data.Status);
                        stmt.SetBool(3, data.Explored);
                        stmt.SetInt64(4, (UnixTime64)data.AcceptTime);
                        stmt.SetInt64(5, GetQuestSlotEndTime(data.Slot));
                        trans.Append(stmt);

                        // Save objectives
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_QUESTSTATUS_OBJECTIVES_BY_QUEST);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetInt32(1, save.Key);
                        trans.Append(stmt);

                        Quest quest = Global.ObjectMgr.GetQuestTemplate(save.Key);

                        foreach (QuestObjective obj in quest.Objectives)
                        {
                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.REP_CHAR_QUESTSTATUS_OBJECTIVES);
                            stmt.SetInt64(0, GetGUID().GetCounter());
                            stmt.SetInt32(1, save.Key);
                            stmt.SetInt8(2, (sbyte)obj.StorageIndex);
                            stmt.SetInt32(3, GetQuestSlotObjectiveData(data.Slot, obj));
                            trans.Append(stmt);
                        }
                    }
                }
                else
                {
                    // Delete
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_QUESTSTATUS_BY_QUEST);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    stmt.SetInt32(1, save.Key);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_QUESTSTATUS_OBJECTIVES_BY_QUEST);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    stmt.SetInt32(1, save.Key);
                    trans.Append(stmt);
                }
            }

            m_QuestStatusSave.Clear();

            foreach (var save in m_RewardedQuestsSave)
            {
                if (save.Value == QuestSaveType.Default)
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_QUESTSTATUS_REWARDED);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    stmt.SetInt32(1, save.Key);
                    trans.Append(stmt);

                }
                else if (save.Value == QuestSaveType.ForceDelete || !keepAbandoned)
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_QUESTSTATUS_REWARDED_BY_QUEST);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    stmt.SetInt32(1, save.Key);
                    trans.Append(stmt);
                }
            }

            m_RewardedQuestsSave.Clear();

            if (!isTransaction)
                DB.Characters.CommitTransaction(trans);
        }

        void _SaveDailyQuestStatus(SQLTransaction trans)
        {
            if (!m_DailyQuestChanged)
                return;

            m_DailyQuestChanged = false;

            // save last daily quest time for all quests: we need only mostly reset time for reset check anyway

            // we don't need transactions here.
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_QUESTSTATUS_DAILY);
            stmt.SetInt64(0, GetGUID().GetCounter());

            foreach (int questId in m_activePlayerData.DailyQuestsCompleted)
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHARACTER_QUESTSTATUS_DAILY);
                stmt.SetInt64(0, GetGUID().GetCounter());
                stmt.SetInt32(1, questId);
                stmt.SetInt64(2, (UnixTime64)m_lastDailyQuestTime);
                trans.Append(stmt);

            }

            if (!m_DFQuests.Empty())
            {
                foreach (var id in m_DFQuests)
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHARACTER_QUESTSTATUS_DAILY);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    stmt.SetInt32(1, id);
                    stmt.SetInt64(2, (UnixTime64)m_lastDailyQuestTime);
                    trans.Append(stmt);
                }
            }
        }

        void _SaveWeeklyQuestStatus(SQLTransaction trans)
        {
            if (!m_WeeklyQuestChanged || m_weeklyquests.Empty())
                return;

            // we don't need transactions here.
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_QUESTSTATUS_WEEKLY);
            stmt.SetInt64(0, GetGUID().GetCounter());
            trans.Append(stmt);

            foreach (var quest_id in m_weeklyquests)
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHARACTER_QUESTSTATUS_WEEKLY);
                stmt.SetInt64(0, GetGUID().GetCounter());
                stmt.SetInt32(1, quest_id);
                trans.Append(stmt);
            }

            m_WeeklyQuestChanged = false;
        }

        void _SaveSeasonalQuestStatus(SQLTransaction trans)
        {
            if (!m_SeasonalQuestChanged)
                return;

            // we don't need transactions here.
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_QUESTSTATUS_SEASONAL);
            stmt.SetInt64(0, GetGUID().GetCounter());
            trans.Append(stmt);

            m_SeasonalQuestChanged = false;

            if (m_seasonalquests.Empty())
                return;

            foreach (var (eventId, dictionary) in m_seasonalquests)
            {
                foreach (var (questId, completedTime) in dictionary)
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHARACTER_QUESTSTATUS_SEASONAL);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    stmt.SetInt32(1, questId);
                    stmt.SetInt32(2, eventId);
                    stmt.SetInt64(3, (UnixTime64)completedTime);
                    trans.Append(stmt);
                }
            }
        }

        void _SaveMonthlyQuestStatus(SQLTransaction trans)
        {
            if (!m_MonthlyQuestChanged || m_monthlyquests.Empty())
                return;

            // we don't need transactions here.
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_QUESTSTATUS_MONTHLY);
            stmt.SetInt64(0, GetGUID().GetCounter());
            trans.Append(stmt);

            foreach (var questId in m_monthlyquests)
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHARACTER_QUESTSTATUS_MONTHLY);
                stmt.SetInt64(0, GetGUID().GetCounter());
                stmt.SetInt32(1, questId);
                trans.Append(stmt);
            }

            m_MonthlyQuestChanged = false;
        }

        void _SaveTalents(SQLTransaction trans)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_TALENT);
            stmt.SetInt64(0, GetGUID().GetCounter());
            trans.Append(stmt);
            
            for (byte group = 0; group < PlayerConst.MaxSpecializations; ++group)
            {
                var talents = GetPlayerTalents(group);
                var ToRemove = new List<int>();

                foreach (var pair in talents)
                {
                    if (pair.Value.State == PlayerSpellState.Removed)
                    {
                        ToRemove.Add(pair.Key);
                        continue;
                    }

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_TALENT);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    stmt.SetInt32(1, pair.Key);
                    stmt.SetUInt8(2, pair.Value.Rank);
                    stmt.SetUInt8(3, group);
                    trans.Append(stmt);
                }

                foreach (var tab in ToRemove)
                    talents.Remove(tab);
            }
        }

        void _SaveTraits(SQLTransaction trans)
        {
            PreparedStatement stmt;
            foreach (var (traitConfigId, state) in m_traitConfigStates)
            {
                switch (state)
                {
                    case PlayerSpellState.Changed:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_TRAIT_ENTRIES);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetInt32(1, traitConfigId);
                        trans.Append(stmt);

                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_TRAIT_CONFIGS);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetInt32(1, traitConfigId);
                        trans.Append(stmt);

                        TraitConfig traitConfig = GetTraitConfig(traitConfigId);
                        if (traitConfig != null)
                        {
                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_TRAIT_CONFIGS);
                            stmt.SetInt64(0, GetGUID().GetCounter());
                            stmt.SetInt32(1, traitConfig.ID);
                            stmt.SetInt32(2, traitConfig.Type);
                            switch ((TraitConfigType)(int)traitConfig.Type)
                            {
                                case TraitConfigType.Combat:
                                    stmt.SetInt32(3, traitConfig.ChrSpecializationID);
                                    stmt.SetInt32(4, traitConfig.CombatConfigFlags);
                                    stmt.SetInt32(5, traitConfig.LocalIdentifier);
                                    stmt.SetNull(6);
                                    stmt.SetNull(7);
                                    break;
                                case TraitConfigType.Profession:
                                    stmt.SetNull(3);
                                    stmt.SetNull(4);
                                    stmt.SetNull(5);
                                    stmt.SetInt32(6, traitConfig.SkillLineID);
                                    stmt.SetNull(7);
                                    break;
                                case TraitConfigType.Generic:
                                    stmt.SetNull(3);
                                    stmt.SetNull(4);
                                    stmt.SetNull(5);
                                    stmt.SetNull(6);
                                    stmt.SetInt32(7, traitConfig.TraitSystemID);
                                    break;
                                default:
                                    break;
                            }

                            stmt.SetString(8, traitConfig.Name);
                            trans.Append(stmt);

                            foreach (var traitEntry in traitConfig.Entries)
                            {
                                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_TRAIT_ENTRIES);
                                stmt.SetInt64(0, GetGUID().GetCounter());
                                stmt.SetInt32(1, traitConfig.ID);
                                stmt.SetInt32(2, traitEntry.TraitNodeID);
                                stmt.SetInt32(3, traitEntry.TraitNodeEntryID);
                                stmt.SetInt32(4, traitEntry.Rank);
                                stmt.SetInt32(5, traitEntry.GrantedRanks);
                                trans.Append(stmt);
                            }
                        }
                        break;
                    case PlayerSpellState.Removed:
                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_TRAIT_ENTRIES);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetInt32(1, traitConfigId);
                        trans.Append(stmt);

                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_TRAIT_CONFIGS);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetInt32(1, traitConfigId);
                        trans.Append(stmt);

                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_ACTION_BY_TRAIT_CONFIG);
                        stmt.SetInt64(0, GetGUID().GetCounter());
                        stmt.SetInt32(1, traitConfigId);
                        trans.Append(stmt);
                        break;
                    default:
                        break;
                }
            }

            m_traitConfigStates.Clear();
        }

        public void _SaveMail(SQLTransaction trans)
        {
            PreparedStatement stmt;

            foreach (var m in m_mail)
            {
                if (m.state == MailState.Changed)
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_MAIL);
                    stmt.SetInt32(0, m.HasItems() ? 1 : 0);
                    stmt.SetInt64(1, (UnixTime64)m.expire_time);
                    stmt.SetInt64(2, (UnixTime64)m.deliver_time);
                    stmt.SetInt64(3, m.money);
                    stmt.SetInt64(4, m.COD);
                    stmt.SetUInt8(5, (byte)m.checkMask);
                    stmt.SetInt64(6, m.messageID);

                    trans.Append(stmt);

                    if (!m.removedItems.Empty())
                    {
                        foreach (var id in m.removedItems)
                        {
                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_ITEM);
                            stmt.SetInt64(0, id);
                            trans.Append(stmt);
                        }
                        m.removedItems.Clear();
                    }
                    m.state = MailState.Unchanged;
                }
                else if (m.state == MailState.Deleted)
                {
                    if (m.HasItems())
                    {
                        foreach (var mailItemInfo in m.items)
                            Item.DeleteFromDB(trans, mailItemInfo.item_guid);                       
                    }
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_BY_ID);
                    stmt.SetInt64(0, m.messageID);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_ITEM_BY_ID);
                    stmt.SetInt64(0, m.messageID);
                    trans.Append(stmt);
                }
            }

            //deallocate deleted mails...
            foreach (var m in GetMails().Where(m => m.state == MailState.Deleted).ToList())
                m_mail.Remove(m);

            m_mailsUpdated = false;
        }

        void _SaveStoredAuraTeleportLocations(SQLTransaction trans)
        {
            foreach (var pair in m_storedAuraTeleportLocations.ToList())
            {
                var storedLocation = pair.Value;
                if (storedLocation.CurrentState == StoredAuraTeleportLocation.State.Deleted)
                {
                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_AURA_STORED_LOCATION);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    trans.Append(stmt);
                    m_storedAuraTeleportLocations.Remove(pair.Key);
                    continue;
                }

                if (storedLocation.CurrentState == StoredAuraTeleportLocation.State.Changed)
                {
                    PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_AURA_STORED_LOCATION);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHARACTER_AURA_STORED_LOCATION);
                    stmt.SetInt64(0, GetGUID().GetCounter());
                    stmt.SetInt32(1, pair.Key);
                    stmt.SetInt32(2, storedLocation.Loc.GetMapId());
                    stmt.SetFloat(3, storedLocation.Loc.GetPositionX());
                    stmt.SetFloat(4, storedLocation.Loc.GetPositionY());
                    stmt.SetFloat(5, storedLocation.Loc.GetPositionZ());
                    stmt.SetFloat(6, storedLocation.Loc.GetOrientation());
                    trans.Append(stmt);
                }
            }
        }

        void _SaveStats(SQLTransaction trans)
        {
            // check if stat saving is enabled and if char level is high enough
            if (WorldConfig.Values[WorldCfg.MinLevelStatSave].Int32 == 0 || GetLevel() < WorldConfig.Values[WorldCfg.MinLevelStatSave].Int32)
                return;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_STATS);
            stmt.SetInt64(0, GetGUID().GetCounter());
            trans.Append(stmt);

            byte index = 0;
            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_STATS);
            stmt.SetInt64(index++, GetGUID().GetCounter());
            stmt.SetInt64(index++, GetMaxHealth());

            for (byte i = 0; i < (int)PowerType.MaxPerClass; ++i)
                stmt.SetInt32(index++, m_unitData.MaxPower[i]);

            for (Stats i = 0; i < Stats.Max; ++i)
                stmt.SetInt32(index++, (int)GetStat(i));

            for (SpellSchools i = 0; i < SpellSchools.Max; ++i)
                stmt.SetInt32(index++, GetResistance(i));

            stmt.SetFloat(index++, m_activePlayerData.BlockPercentage);
            stmt.SetFloat(index++, m_activePlayerData.DodgePercentage);
            stmt.SetFloat(index++, m_activePlayerData.ParryPercentage);
            stmt.SetFloat(index++, m_activePlayerData.CritPercentage);
            stmt.SetFloat(index++, m_activePlayerData.RangedCritPercentage);
            // @TODO (3.4.3): in wotlk spell crit percentage was split by spell school
            stmt.SetFloat(index++, 0.0f); // m_activePlayerData.SpellCritPercentage);
            stmt.SetInt32(index++, m_unitData.AttackPower);
            stmt.SetInt32(index++, m_unitData.RangedAttackPower);
            stmt.SetInt32(index++, GetBaseSpellPowerBonus());
            stmt.SetInt32(index, 0); // m_activePlayerData.CombatRatings[(int)CombatRating.ResiliencePlayerDamage]);
            stmt.SetFloat(index++, m_activePlayerData.Mastery);
            stmt.SetInt32(index++, m_activePlayerData.Versatility);

            trans.Append(stmt);
        }

        public void SaveInventoryAndGoldToDB(SQLTransaction trans)
        {
            _SaveInventory(trans);
            _SaveCurrency(trans);

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHAR_MONEY);
            stmt.SetInt64(0, GetMoney());
            stmt.SetInt64(1, GetGUID().GetCounter());
            trans.Append(stmt);
        }

        void _SaveEquipmentSets(SQLTransaction trans)
        {
            foreach (var pair in _equipmentSets)
            {
                EquipmentSetInfo eqSet = pair.Value;
                PreparedStatement stmt;
                byte j = 0;
                switch (eqSet.state)
                {
                    case EquipmentSetUpdateState.Unchanged:
                        break;                                      // do nothing
                    case EquipmentSetUpdateState.Changed:
                        if (eqSet.Data.Type == EquipmentSetInfo.EquipmentSetType.Equipment)
                        {
                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_EQUIP_SET);
                            stmt.SetString(j++, eqSet.Data.SetName);
                            stmt.SetString(j++, eqSet.Data.SetIcon);
                            stmt.SetUInt32(j++, eqSet.Data.IgnoreMask);
                            stmt.SetInt32(j++, eqSet.Data.AssignedSpecIndex);

                            for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; ++i)
                                stmt.SetInt64(j++, eqSet.Data.Pieces[i].GetCounter());

                            stmt.SetInt64(j++, GetGUID().GetCounter());
                            stmt.SetInt64(j++, eqSet.Data.Guid);
                            stmt.SetInt32(j, eqSet.Data.SetID);
                        }
                        else
                        {
                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_TRANSMOG_OUTFIT);
                            stmt.SetString(j++, eqSet.Data.SetName);
                            stmt.SetString(j++, eqSet.Data.SetIcon);
                            stmt.SetUInt32(j++, eqSet.Data.IgnoreMask);

                            for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; ++i)
                                stmt.SetInt32(j++, eqSet.Data.Appearances[i]);

                            for (int i = 0; i < eqSet.Data.Enchants.Length; ++i)
                                stmt.SetInt32(j++, eqSet.Data.Enchants[i]);

                            stmt.SetInt64(j++, GetGUID().GetCounter());
                            stmt.SetInt64(j++, eqSet.Data.Guid);
                            stmt.SetInt32(j, eqSet.Data.SetID);
                        }

                        trans.Append(stmt);
                        eqSet.state = EquipmentSetUpdateState.Unchanged;
                        break;
                    case EquipmentSetUpdateState.New:
                        if (eqSet.Data.Type == EquipmentSetInfo.EquipmentSetType.Equipment)
                        {
                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_EQUIP_SET);
                            stmt.SetInt64(j++, GetGUID().GetCounter());
                            stmt.SetInt64(j++, eqSet.Data.Guid);
                            stmt.SetInt32(j++, eqSet.Data.SetID);
                            stmt.SetString(j++, eqSet.Data.SetName);
                            stmt.SetString(j++, eqSet.Data.SetIcon);
                            stmt.SetUInt32(j++, eqSet.Data.IgnoreMask);
                            stmt.SetInt32(j++, eqSet.Data.AssignedSpecIndex);

                            for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; ++i)
                                stmt.SetInt64(j++, eqSet.Data.Pieces[i].GetCounter());
                        }
                        else
                        {
                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_TRANSMOG_OUTFIT);
                            stmt.SetInt64(j++, GetGUID().GetCounter());
                            stmt.SetInt64(j++, eqSet.Data.Guid);
                            stmt.SetInt32(j++, eqSet.Data.SetID);
                            stmt.SetString(j++, eqSet.Data.SetName);
                            stmt.SetString(j++, eqSet.Data.SetIcon);
                            stmt.SetUInt32(j++, eqSet.Data.IgnoreMask);

                            for (byte i = EquipmentSlot.Start; i < EquipmentSlot.End; ++i)
                                stmt.SetInt32(j++, eqSet.Data.Appearances[i]);

                            for (int i = 0; i < eqSet.Data.Enchants.Length; ++i)
                                stmt.SetInt32(j++, eqSet.Data.Enchants[i]);
                        }
                        trans.Append(stmt);
                        eqSet.state = EquipmentSetUpdateState.Unchanged;
                        break;
                    case EquipmentSetUpdateState.Deleted:
                        if (eqSet.Data.Type == EquipmentSetInfo.EquipmentSetType.Equipment)
                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_EQUIP_SET);
                        else
                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_TRANSMOG_OUTFIT);
                        stmt.SetInt64(0, eqSet.Data.Guid);
                        trans.Append(stmt);
                        _equipmentSets.Remove(pair.Key);
                        break;
                }
            }
        }

        void _SaveVoidStorage(SQLTransaction trans)
        {
            PreparedStatement stmt;
            for (byte i = 0; i < SharedConst.VoidStorageMaxSlot; ++i)
            {
                if (_voidStorageItems[i] == null) // unused item
                {
                    // DELETE FROM void_storage WHERE slot = ? AND playerGuid = ?
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_VOID_STORAGE_ITEM_BY_SLOT);
                    stmt.SetUInt8(0, i);
                    stmt.SetInt64(1, GetGUID().GetCounter());
                }

                else
                { 
                    //                                       0        1            2        3       4           5                   6                   7                   8
                    //"REPLACE INTO character_void_storage (itemId, playerGuid, itemEntry, slot, creatorGuid, fixedScalingLevel, randomPropertiesId, randomPropertiesSeed, context) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)");
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.REP_CHAR_VOID_STORAGE_ITEM);
                    stmt.SetInt64(0, _voidStorageItems[i].ItemId);
                    stmt.SetInt64(1, GetGUID().GetCounter());
                    stmt.SetInt32(2, _voidStorageItems[i].ItemEntry);
                    stmt.SetUInt8(3, i);
                    stmt.SetInt64(4, _voidStorageItems[i].CreatorGuid.GetCounter());
                    stmt.SetInt32(5, _voidStorageItems[i].FixedScalingLevel);
                    stmt.SetInt32(6, _voidStorageItems[i].RandomProperties.RandomPropertiesID);
                    stmt.SetInt32(7, _voidStorageItems[i].RandomProperties.RandomPropertiesSeed);
                    stmt.SetUInt8(8, (byte)_voidStorageItems[i].Context);
                }

                trans.Append(stmt);
            }
        }

        void _SaveCUFProfiles(SQLTransaction trans)
        {
            PreparedStatement stmt;
            long lowGuid = GetGUID().GetCounter();

            for (byte i = 0; i < PlayerConst.MaxCUFProfiles; ++i)
            {
                if (_CUFProfiles[i] == null) // unused profile
                {
                    // DELETE FROM character_cuf_profiles WHERE guid = ? and id = ?
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_CUF_PROFILES_BY_ID);
                    stmt.SetInt64(0, lowGuid);
                    stmt.SetUInt8(1, i);
                }
                else
                {
                    // REPLACE INTO character_cuf_profiles (guid, id, name, frameHeight, frameWidth, sortBy, healthText, boolOptions, unk146, unk147, unk148, unk150, unk152, unk154) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.REP_CHAR_CUF_PROFILES);
                    stmt.SetInt64(0, lowGuid);
                    stmt.SetUInt8(1, i);
                    stmt.SetString(2, _CUFProfiles[i].ProfileName);
                    stmt.SetUInt16(3, _CUFProfiles[i].FrameHeight);
                    stmt.SetUInt16(4, _CUFProfiles[i].FrameWidth);
                    stmt.SetUInt8(5, _CUFProfiles[i].SortBy);
                    stmt.SetUInt8(6, _CUFProfiles[i].HealthText);
                    stmt.SetUInt32(7, (uint)_CUFProfiles[i].GetUlongOptionValue()); // 25 of 32 fields used, fits in an int
                    stmt.SetUInt8(8, _CUFProfiles[i].TopPoint);
                    stmt.SetUInt8(9, _CUFProfiles[i].BottomPoint);
                    stmt.SetUInt8(10, _CUFProfiles[i].LeftPoint);
                    stmt.SetUInt16(11, _CUFProfiles[i].TopOffset);
                    stmt.SetUInt16(12, _CUFProfiles[i].BottomOffset);
                    stmt.SetUInt16(13, _CUFProfiles[i].LeftOffset);
                }

                trans.Append(stmt);
            }
        }

        void _SaveInstanceTimeRestrictions(SQLTransaction trans)
        {
            if (_instanceResetTimes.Empty())
                return;

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ACCOUNT_INSTANCE_LOCK_TIMES);
            stmt.SetInt32(0, GetSession().GetAccountId());
            trans.Append(stmt);

            foreach (var pair in _instanceResetTimes)
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_ACCOUNT_INSTANCE_LOCK_TIMES);
                stmt.SetInt32(0, GetSession().GetAccountId());
                stmt.SetInt32(1, pair.Key);
                stmt.SetInt64(2, (UnixTime64)pair.Value);
                trans.Append(stmt);
            }
        }

        void _SaveBGData(SQLTransaction trans)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PLAYER_BGDATA);
            stmt.SetInt64(0, GetGUID().GetCounter());
            trans.Append(stmt);

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_PLAYER_BGDATA);
            stmt.SetInt64(0, GetGUID().GetCounter());
            stmt.SetInt32(1, m_bgData.bgInstanceID);
            stmt.SetUInt16(2, (ushort)m_bgData.bgTeam);
            stmt.SetFloat(3, m_bgData.joinPos.GetPositionX());
            stmt.SetFloat(4, m_bgData.joinPos.GetPositionY());
            stmt.SetFloat(5, m_bgData.joinPos.GetPositionZ());
            stmt.SetFloat(6, m_bgData.joinPos.GetOrientation());
            stmt.SetUInt16(7, (ushort)m_bgData.joinPos.GetMapId());
            stmt.SetInt32(8, m_bgData.taxiPath[0]);
            stmt.SetInt32(9, m_bgData.taxiPath[1]);
            stmt.SetInt32(10, m_bgData.mountSpell);
            stmt.SetUInt64(11, m_bgData.queueId.GetPacked());
            trans.Append(stmt);
        }

        public bool LoadFromDB(ObjectGuid guid, SQLQueryHolder<PlayerLoginQueryLoad> holder)
        {
            SQLResult result = holder.GetResult(PlayerLoginQueryLoad.From);
            if (result.IsEmpty())
            {
                Global.CharacterCacheStorage.GetCharacterNameByGuid(guid, out string cacheName);
                Log.outError(LogFilter.Player, 
                    $"Player.LoadFromDB: Player {cacheName} {guid} not found in table `characters`, can't load.");
                return false;
            }

            int fieldIndex = 1;
            var accountId = result.Read<int>(fieldIndex++);
            var name = result.Read<string>(fieldIndex++);
            var race = (Race)result.Read<byte>(fieldIndex++);
            var class_ = (Class)result.Read<byte>(fieldIndex++);
            var gender = (Gender)result.Read<byte>(fieldIndex++);
            var level = result.Read<byte>(fieldIndex++);
            var xp = result.Read<int>(fieldIndex++);
            var money = result.Read<long>(fieldIndex++);
            var inventorySlots = result.Read<byte>(fieldIndex++);
            var bankSlots = result.Read<byte>(fieldIndex++);
            var restState = (PlayerRestState)result.Read<byte>(fieldIndex++);
            var playerFlags = (PlayerFlags)result.Read<uint>(fieldIndex++);
            var playerFlagsEx = (PlayerFlagsEx)result.Read<uint>(fieldIndex++);
            var position_x = result.Read<float>(fieldIndex++);
            var position_y = result.Read<float>(fieldIndex++);
            var position_z = result.Read<float>(fieldIndex++);
            int mapId = result.Read<ushort>(fieldIndex++);
            var orientation = result.Read<float>(fieldIndex++);
            var taximask = result.Read<string>(fieldIndex++);
            var createTime = (UnixTime64)result.Read<long>(fieldIndex++);
            var createMode = (PlayerCreateMode)result.Read<byte>(fieldIndex++);
            var cinematic = result.Read<byte>(fieldIndex++);
            var totaltime = (Seconds)result.Read<int>(fieldIndex++);
            var leveltime = (Seconds)result.Read<int>(fieldIndex++);
            var rest_bonus = result.Read<float>(fieldIndex++);
            var logout_time = (UnixTime64)result.Read<long>(fieldIndex++);
            var is_logout_resting = result.Read<byte>(fieldIndex++);
            var resettalents_cost = result.Read<uint>(fieldIndex++);
            var resettalents_time = (UnixTime64)result.Read<long>(fieldIndex++);
            var activeTalentGroup = result.Read<byte>(fieldIndex++);
            var bonusTalentGroups = result.Read<byte>(fieldIndex++);
            var trans_x = result.Read<float>(fieldIndex++);
            var trans_y = result.Read<float>(fieldIndex++);
            var trans_z = result.Read<float>(fieldIndex++);
            var trans_o = result.Read<float>(fieldIndex++);
            var transguid = result.Read<long>(fieldIndex++);
            var extra_flags = (PlayerExtraFlags)result.Read<ushort>(fieldIndex++);
            var summonedPetNumber = result.Read<int>(fieldIndex++);
            var atLoginFlags = (AtLoginFlags)result.Read<ushort>(fieldIndex++);
            var zone = result.Read<ushort>(fieldIndex++);
            var online = result.Read<byte>(fieldIndex++);
            var death_expire_time = (UnixTime64)result.Read<long>(fieldIndex++);
            var taxi_path = result.Read<string>(fieldIndex++);
            var dungeonDifficulty = (Difficulty)result.Read<byte>(fieldIndex++);
            var totalKills = result.Read<int>(fieldIndex++);
            var todayKills = result.Read<ushort>(fieldIndex++);
            var yesterdayKills = result.Read<ushort>(fieldIndex++);
            var chosenTitle = result.Read<int>(fieldIndex++);
            var watchedFaction = result.Read<int>(fieldIndex++);
            var drunk = result.Read<byte>(fieldIndex++);
            var health = result.Read<int>(fieldIndex++);

            var powers = new int[(int)PowerType.MaxPerClass];
            for (var i = 0; i < powers.Length; ++i)
                powers[i] = result.Read<int>(fieldIndex++);
            var usedAmmoID = result.Read<int>(fieldIndex++);
            var instance_id = result.Read<int>(fieldIndex++);
            var lootSpecId = (ChrSpecialization)result.Read<int>(fieldIndex++);
            var exploredZones = result.Read<string>(fieldIndex++);
            var knownTitles = result.Read<string>(fieldIndex++);
            var actionBars = result.Read<byte>(fieldIndex++);
            var raidDifficulty = (Difficulty)result.Read<byte>(fieldIndex++);
            var legacyRaidDifficulty = (Difficulty)result.Read<byte>(fieldIndex++);
            var fishingSteps = result.Read<byte>(fieldIndex++);
            var honor = result.Read<int>(fieldIndex++);
            var honorLevel = result.Read<int>(fieldIndex++);
            var honorRestState = (PlayerRestState)result.Read<byte>(fieldIndex++);
            var honorRestBonus = result.Read<float>(fieldIndex++);
            var numRespecs = result.Read<byte>(fieldIndex++);
            var personalTabardEmblemStyle = result.Read<int>(fieldIndex++);
            var personalTabardEmblemColor = result.Read<int>(fieldIndex++);
            var personalTabardBorderStyle = result.Read<int>(fieldIndex++);
            var personalTabardBorderColor = result.Read<int>(fieldIndex++);
            var personalTabardBackgroundColor = result.Read<int>(fieldIndex++);

            // check if the character's account in the db and the logged in account match.
            // player should be able to load/delete character only with correct account!
            if (accountId != GetSession().GetAccountId())
            {
                Log.outError(LogFilter.Player, 
                    $"Player.LoadFromDB: Player (GUID: {guid}) loading from wrong account " +
                    $"(is: {GetSession().GetAccountId()}, should be: {accountId}).");
                return false;
            }

            SQLResult banResult = holder.GetResult(PlayerLoginQueryLoad.Banned);
            if (!banResult.IsEmpty())
            {
                Log.outError(LogFilter.Player, 
                    $"Player.LoadFromDB: Player (GUID: {guid}) is banned, can't load.");
                return false;
            }

            _Create(guid);

            SetName(name);

            // check name limitations
            if (ObjectManager.CheckPlayerName(GetName(), GetSession().GetSessionDbcLocale()) != ResponseCodes.CharNameSuccess ||
                (!GetSession().HasPermission(RBACPermissions.SkipCheckCharacterCreationReservedname) && Global.ObjectMgr.IsReservedName(GetName())))
            {
                PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ADD_AT_LOGIN_FLAG);
                stmt.SetUInt16(0, (ushort)AtLoginFlags.Rename);
                stmt.SetInt64(1, guid.GetCounter());
                DB.Characters.Execute(stmt);
                return false;
            }

            SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.WowAccount), GetSession().GetAccountGUID());
            SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.BnetAccount), GetSession().GetBattlenetAccountGUID());

            if (gender >= Gender.None)
            {
                Log.outError(LogFilter.Player, 
                    $"Player.LoadFromDB: layer {guid} has wrong gender ({gender}), can't be loaded.");
                return false;
            }

            SetRace(race);
            SetClass(class_);
            SetGender(gender);

            // check if race/class combination is valid
            PlayerInfo info = Global.ObjectMgr.GetPlayerInfo(GetRace(), GetClass());
            if (info == null)
            {
                Log.outError(LogFilter.Player, 
                    $"Player.LoadFromDB: Player {guid} has wrong race/class ({GetRace()}/{GetClass()}), can't be loaded.");
                return false;
            }

            SetLevel(level);
            SetXP(xp);

            StringArray exploredZonesStrings = new(exploredZones, ' ');
            for (int i = 0; i < exploredZonesStrings.Length && i / 2 < ActivePlayerData.ExploredZonesSize; ++i)
                SetUpdateFieldFlagValue(ref m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.ExploredZones, i / 2), (ulong)((long.Parse(exploredZonesStrings[i])) << (32 * (i % 2))));

            StringArray knownTitlesStrings = new(knownTitles, ' ');
            if ((knownTitlesStrings.Length % 2) == 0)
            {
                for (int i = 0; i < knownTitlesStrings.Length; ++i)
                    SetUpdateFieldFlagValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.KnownTitles, i / 2), (ulong)((long.Parse(knownTitlesStrings[i])) << (32 * (i % 2))));
            }

            SetObjectScale(1.0f);

            // load achievements before anything else to prevent multiple gains for the same achievement/criteria on every loading (as loading does call UpdateAchievementCriteria)
            m_achievementSys.LoadFromDB(holder.GetResult(PlayerLoginQueryLoad.Achievements), holder.GetResult(PlayerLoginQueryLoad.CriteriaProgress));
            m_questObjectiveCriteriaMgr.LoadFromDB(holder.GetResult(PlayerLoginQueryLoad.QuestStatusObjectivesCriteria), holder.GetResult(PlayerLoginQueryLoad.QuestStatusObjectivesCriteriaProgress));

            SetMoney(Math.Min(money, PlayerConst.MaxMoneyAmount));

            List<ChrCustomizationChoice> customizations = new();
            SQLResult customizationsResult = holder.GetResult(PlayerLoginQueryLoad.Customizations);
            if (!customizationsResult.IsEmpty())
            {
                do
                {
                    ChrCustomizationChoice choice = new();
                    choice.ChrCustomizationOptionID = customizationsResult.Read<int>(0);
                    choice.ChrCustomizationChoiceID = customizationsResult.Read<int>(1);
                    customizations.Add(choice);

                } while (customizationsResult.NextRow());
            }

            SetCustomizations(customizations, false);
            SetInventorySlotCount(inventorySlots);
            SetBankBagSlotCount(bankSlots);
            SetNativeGender(gender);
            SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.Inebriation), drunk);
            ReplaceAllPlayerFlags(playerFlags);
            ReplaceAllPlayerFlagsEx(playerFlagsEx);
            SetWatchedFactionIndex(watchedFaction);

            if (!GetSession().ValidateAppearance(GetRace(), GetClass(), gender, customizations))
            {
                Log.outError(LogFilter.Player, 
                    $"Player.LoadFromDB: Player {guid} has wrong Appearance values (Hair/Skin/Color), can't be loaded.");
                return false;
            }

            // set which actionbars the client has active - DO NOT REMOVE EVER AGAIN (can be changed though, if it does change fieldwise)
            SetMultiActionBars(actionBars);

            m_fishingSteps = fishingSteps;

            InitDisplayIds();

            //Need to call it to initialize m_team (m_team can be calculated from race)
            //Other way is to saves m_team into characters table.
            SetFactionForRace(GetRace());

            // load home bind and check in same time class/race pair, it used later for restore broken positions
            if (!_LoadHomeBind(holder.GetResult(PlayerLoginQueryLoad.HomeBind)))
                return false;

            InitializeSkillFields();
            InitPrimaryProfessions();                               // to max set before any spell loaded

            // init saved position, and fix it later if problematic
            Relocate(position_x, position_y, position_z, orientation);

            SetDungeonDifficultyID(CheckLoadedDungeonDifficultyID(dungeonDifficulty));
            SetRaidDifficultyID(CheckLoadedRaidDifficultyID(raidDifficulty));
            SetLegacyRaidDifficultyID(CheckLoadedLegacyRaidDifficultyID(legacyRaidDifficulty));

            var RelocateToHomebind = new Action(() =>
            {
                mapId = homebind.GetMapId();
                instance_id = 0;
                Relocate(homebind);
            });

            _LoadGroup(holder.GetResult(PlayerLoginQueryLoad.Group));

            _LoadCurrency(holder.GetResult(PlayerLoginQueryLoad.Currency));
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.LifetimeHonorableKills), totalKills);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.TodayHonorableKills), todayKills);
            SetUpdateFieldValue(m_values.ModifyValue(m_activePlayerData).ModifyValue(m_activePlayerData.YesterdayHonorableKills), yesterdayKills);

            _LoadInstanceTimeRestrictions(holder.GetResult(PlayerLoginQueryLoad.InstanceLockTimes));
            _LoadBGData(holder.GetResult(PlayerLoginQueryLoad.BgData));

            GetSession().SetPlayer(this);

            Map map = null;
            bool player_at_bg = false;
            var mapEntry = CliDB.MapStorage.LookupByKey(mapId);
            if (mapEntry == null || !IsPositionValid())
            {
                Log.outError(LogFilter.Player, 
                    $"Player.LoadFromDB: Player (guidlow {guid}) have invalid coordinates (MapId: {mapId} {GetPosition()}). " +
                    $"Teleport to default race/class locations.");
                RelocateToHomebind();
            }
            // Player was saved in Arena or Bg
            else if (mapEntry.IsBattlegroundOrArena)
            {
                Battleground currentBg = null;
                if (m_bgData.bgInstanceID != 0)                                                //saved in Battleground
                    currentBg = Global.BattlegroundMgr.GetBattleground(m_bgData.bgInstanceID, BattlegroundTypeId.None);

                player_at_bg = currentBg != null && currentBg.IsPlayerInBattleground(GetGUID());

                if (player_at_bg && currentBg.GetStatus() != BattlegroundStatus.WaitLeave)
                {
                    map = currentBg.GetBgMap();

                    BattlegroundPlayer bgPlayer = currentBg.GetBattlegroundPlayerData(GetGUID());
                    if (bgPlayer != null)
                    {
                        AddBattlegroundQueueId(bgPlayer.queueTypeId);
                        m_bgData.bgTypeID = bgPlayer.queueTypeId.BattlemasterListId;

                        //join player to Battlegroundgroup
                        currentBg.EventPlayerLoggedIn(this);

                        SetInviteForBattlegroundQueueType(bgPlayer.queueTypeId, currentBg.GetInstanceID());
                        SetMercenaryForBattlegroundQueueType(bgPlayer.queueTypeId, currentBg.IsPlayerMercenaryInBattleground(GetGUID()));
                    }
                }
                // Bg was not found - go to Entry Point
                else
                {
                    // leave bg
                    if (player_at_bg)
                    {
                        player_at_bg = false;
                        currentBg.RemovePlayerAtLeave(GetGUID(), false, true);
                    }

                    // Do not look for instance if bg not found
                    WorldLocation _loc = GetBattlegroundEntryPoint();
                    mapId = _loc.GetMapId();
                    instance_id = 0;

                    if (mapId == -1) // BattlegroundEntry Point not found (???)
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player.LoadFromDB: Player (guidlow {guid}) was in BG in database, " +
                            $"but BG was not found, and entry point was invalid! " +
                            $"Teleport to default race/class locations.");

                        RelocateToHomebind();
                    }
                    else
                        Relocate(_loc);

                    // We are not in BG anymore
                    m_bgData.bgInstanceID = 0;
                }
            }
            // currently we do not support transport in bg
            else if (transguid != 0)
            {
                ObjectGuid transGUID = ObjectGuid.Create(HighGuid.Transport, transguid);

                Transport transport = null;
                Map transportMap = Global.MapMgr.CreateMap(mapId, this);
                if (transportMap != null)
                {
                    Transport transportOnMap = transportMap.GetTransport(transGUID);
                    if (transportOnMap != null)
                    {
                        if (transportOnMap.GetExpectedMapId() != mapId)
                        {
                            mapId = transportOnMap.GetExpectedMapId();
                            instanceId = 0;
                            transportMap = Global.MapMgr.CreateMap(mapId, this);
                            if (transportMap != null)
                                transport = transportMap.GetTransport(transGUID);
                        }
                        else
                            transport = transportOnMap;
                    }
                }

                if (transport != null)
                {
                    float x = trans_x;
                    float y = trans_y;
                    float z = trans_z;
                    float o = trans_o;

                    m_movementInfo.transport.pos = new Position(x, y, z, o);
                    transport.CalculatePassengerPosition(ref x, ref y, ref z, ref o);

                    if (!GridDefines.IsValidMapCoord(x, y, z, o) ||
                        // transport size limited
                        Math.Abs(m_movementInfo.transport.pos.posX) > 250.0f ||
                        Math.Abs(m_movementInfo.transport.pos.posY) > 250.0f ||
                        Math.Abs(m_movementInfo.transport.pos.posZ) > 250.0f)
                    {
                        Log.outError(LogFilter.Player,
                            $"Player.LoadFromDB: Player (guidlow {guid}) have invalid transport coordinates " +
                            $"(X: {x} Y: {y} Z: {z} O: {o}). Teleport to bind location.");

                        m_movementInfo.transport.Reset();
                        RelocateToHomebind();
                    }
                    else
                    {
                        Relocate(x, y, z, o);
                        mapId = transport.GetMapId();

                        transport.AddPassenger(this);
                    }
                }
                else
                {
                    Log.outError(LogFilter.Player, 
                        $"Player.LoadFromDB: Player (guidlow {guid}) have problems with transport guid ({transguid}). " +
                        $"Teleport to bind location.");

                    RelocateToHomebind();
                }
            }
            // currently we do not support taxi in instance
            else if (!taxi_path.IsEmpty())
            {
                instance_id = 0;

                // Not finish taxi flight path
                if (m_bgData.HasTaxiPath())
                {
                    for (int i = 0; i < 2; ++i)
                        m_taxi.AddTaxiDestination(m_bgData.taxiPath[i]);
                }

                if (!m_taxi.LoadTaxiDestinationsFromString(taxi_path, GetTeam()))
                {
                    // problems with taxi path loading
                    TaxiNodesRecord nodeEntry = null;
                    var node_id = m_taxi.GetTaxiSource();
                    if (node_id != 0)
                        nodeEntry = CliDB.TaxiNodesStorage.LookupByKey(node_id);

                    if (nodeEntry == null)                                      // don't know taxi start node, to homebind
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player.LoadFromDB: Character {GetGUID()} have wrong data in taxi destination list, " +
                            $"teleport to homebind.");

                        RelocateToHomebind();
                    }
                    else                                                // have start node, to it
                    {
                        Log.outError(LogFilter.Player, 
                            $"Player.LoadFromDB: Character {GetGUID()} have too short taxi destination list, " +
                            $"teleport to original node.");

                        mapId = nodeEntry.ContinentID;
                        Relocate(nodeEntry.Pos.X, nodeEntry.Pos.Y, nodeEntry.Pos.Z, 0.0f);
                    }
                    m_taxi.ClearTaxiDestinations();
                }

                var nodeid = m_taxi.GetTaxiSource();

                if (nodeid != 0)
                {
                    // save source node as recall coord to prevent recall and fall from sky
                    var nodeEntry = CliDB.TaxiNodesStorage.LookupByKey(nodeid);
                    if (nodeEntry != null && nodeEntry.ContinentID == GetMapId())
                    {
                        Cypher.Assert(nodeEntry != null);                                  // checked in m_taxi.LoadTaxiDestinationsFromString
                        mapId = nodeEntry.ContinentID;
                        Relocate(nodeEntry.Pos.X, nodeEntry.Pos.Y, nodeEntry.Pos.Z, 0.0f);
                    }

                    // flight will started later
                }
            }
            else if (mapEntry.IsDungeon && instanceId != 0)
            {
                // try finding instance by id first
                map = Global.MapMgr.FindMap(mapId, instanceId);
            }

            // Map could be changed before
            mapEntry = CliDB.MapStorage.LookupByKey(mapId);
            // client without expansion support
            if (mapEntry != null)
            {
                if (GetSession().GetExpansion() < mapEntry.Expansion)
                {
                    Log.outDebug(LogFilter.Player, 
                        $"Player.LoadFromDB: Player {GetName()} using client without required expansion " +
                        $"tried login at non accessible map {mapId}.");

                    RelocateToHomebind();
                }
            }

            // NOW player must have valid map
            // load the player's map here if it's not already loaded
            if (map == null)
                map = Global.MapMgr.CreateMap(mapId, this);

            AreaTriggerStruct areaTrigger = null;
            bool check = false;

            if (map == null)
            {
                areaTrigger = Global.ObjectMgr.GetGoBackTrigger(mapId);
                check = true;
            }
            else if (map.IsDungeon()) // if map is dungeon...
            {
                TransferAbortParams denyReason = map.CannotEnter(this); // ... and can't enter map, then look for entry point.
                if (denyReason != null)
                {
                    SendTransferAborted(map.GetId(), denyReason.Reason, denyReason.Arg, denyReason.MapDifficultyXConditionId);
                    areaTrigger = Global.ObjectMgr.GetGoBackTrigger(mapId);
                    check = true;
                }
                else if (instance_id != 0 && Global.InstanceLockMgr.FindActiveInstanceLock(guid, new MapDb2Entries(mapId, map.GetDifficultyID())) != null) // ... and instance is reseted then look for entrance.
                {
                    areaTrigger = Global.ObjectMgr.GetMapEntranceTrigger(mapId);
                    check = true;
                }
            }

            if (check) // in case of special event when creating map...
            {
                if (areaTrigger != null) // ... if we have an areatrigger, then relocate to new map/coordinates.
                {
                    Relocate(areaTrigger.target_X, areaTrigger.target_Y, areaTrigger.target_Z, GetOrientation());
                    if (mapId != areaTrigger.target_mapId)
                    {
                        mapId = areaTrigger.target_mapId;
                        map = Global.MapMgr.CreateMap(mapId, this);
                    }
                }
            }

            if (map == null)
            {
                RelocateToHomebind();
                map = Global.MapMgr.CreateMap(mapId, this);
                if (map == null)
                {
                    Log.outError(LogFilter.Player, 
                        $"Player.LoadFromDB: Player {GetName()} {guid} Map: {mapId}, {GetPosition()}. " +
                        $"Invalid default map coordinates or instance couldn't be created.");

                    return false;
                }
            }

            SetMap(map);
            UpdatePositionData();

            // now that map position is determined, check instance validity
            if (!CheckInstanceValidity(true) && !IsInstanceLoginGameMasterException())
                m_InstanceValid = false;

            if (player_at_bg)
                map.ToBattlegroundMap().GetBG().AddPlayer(this, m_bgData.queueId);

            // randomize first save time in range [CONFIG_INTERVAL_SAVE] around [CONFIG_INTERVAL_SAVE]
            // this must help in case next save after mass player load after server startup
            m_nextSave = (Milliseconds)RandomHelper.IRand(m_nextSave / 2, m_nextSave * 3 / 2);

            SaveRecallPosition();

            ServerTime now = LoopTime.ServerTime;
            ServerTime logoutTime = (ServerTime)logout_time;

            SetUpdateFieldValue(m_values.ModifyValue(m_playerData).ModifyValue(m_playerData.LogoutTime), logout_time);

            // since last logout (in seconds)
            Seconds time_diff = LoopTime.UnixServerTime - logout_time;

            // set value, including drunk invisibility detection
            // calculate sobering. after 15 minutes logged out, the player will be sober again
            if (time_diff < (Seconds)(GetDrunkValue() * 9))
                SetDrunkValue((byte)(GetDrunkValue() - time_diff / 9));
            else
                SetDrunkValue(0);

            m_createTime = (ServerTime)createTime;
            m_createMode = createMode;
            m_cinematic = cinematic;
            m_PlayedTimeTotal = totaltime;
            m_PlayedTimeLevel = leveltime;

            SetTalentResetCost(resettalents_cost);
            SetTalentResetTime((ServerTime)resettalents_time);

            m_taxi.LoadTaxiMask(taximask);            // must be before InitTaxiNodesForLevel

            _LoadPetStable(summonedPetNumber, holder.GetResult(PlayerLoginQueryLoad.PetSlots));

            // Honor system
            // Update Honor kills data
            m_lastHonorUpdateTime = (RealmTime)logoutTime;
            UpdateHonorFields();

            m_deathExpireTime = (ServerTime)death_expire_time;
            if (m_deathExpireTime > now + PlayerConst.MaxDeathCount * PlayerConst.DeathExpireStep)
                m_deathExpireTime = now + PlayerConst.MaxDeathCount * PlayerConst.DeathExpireStep - (Seconds)1;

            RemoveUnitFlag2(UnitFlags2.ForceMovement);

            // make sure the unit is considered out of combat for proper loading
            ClearInCombat();

            // reset stats before loading any modifiers
            InitStatsForLevel();
            InitGlyphsForLevel();
            InitTaxiNodesForLevel();
            InitRunes();

            // rest bonus can only be calculated after InitStatsForLevel()
            _restMgr.LoadRestBonus(RestTypes.XP, restState, rest_bonus);

            // load skills after InitStatsForLevel because it triggering aura apply also
            _LoadSkills(holder.GetResult(PlayerLoginQueryLoad.Skills));
            UpdateSkillsForLevel();

            SetNumRespecs(numRespecs);
            SetActiveTalentGroup(activeTalentGroup);
            SetBonusTalentGroupCount(bonusTalentGroups);

            ChrSpecializationRecord chrSpec = CliDB.ChrSpecializationStorage.LookupByKey((int)lootSpecId);
            if (chrSpec != null)
            {
                if (chrSpec.ClassID == GetClass())
                    SetLootSpecId(lootSpecId);
            }

            UpdateDisplayPower();
            _LoadTalents(holder.GetResult(PlayerLoginQueryLoad.Talents));
            _LoadSpells(holder.GetResult(PlayerLoginQueryLoad.Spells), holder.GetResult(PlayerLoginQueryLoad.SpellFavorites));
            GetSession().GetCollectionMgr().LoadToys();
            GetSession().GetCollectionMgr().LoadHeirlooms();
            GetSession().GetCollectionMgr().LoadMounts();
            GetSession().GetCollectionMgr().LoadItemAppearances();
            GetSession().GetCollectionMgr().LoadTransmogIllusions();

            _LoadGlyphs(holder.GetResult(PlayerLoginQueryLoad.Glyphs));
            _LoadAuras(holder.GetResult(PlayerLoginQueryLoad.Auras), holder.GetResult(PlayerLoginQueryLoad.AuraEffects), time_diff);
            _LoadGlyphAuras();
            // add ghost flag (must be after aura load: PLAYER_FLAGS_GHOST set in aura)
            if (HasPlayerFlag(PlayerFlags.Ghost))
                m_deathState = DeathState.Dead;

            // Load spell locations - must be after loading auras
            _LoadStoredAuraTeleportLocations(holder.GetResult(PlayerLoginQueryLoad.AuraStoredLocations));

            // after spell load, learn rewarded spell if need also
            _LoadQuestStatus(holder.GetResult(PlayerLoginQueryLoad.QuestStatus));
            _LoadQuestStatusObjectives(holder.GetResult(PlayerLoginQueryLoad.QuestStatusObjectives));
            _LoadQuestStatusRewarded(holder.GetResult(PlayerLoginQueryLoad.QuestStatusRew));
            _LoadDailyQuestStatus(holder.GetResult(PlayerLoginQueryLoad.DailyQuestStatus));
            _LoadWeeklyQuestStatus(holder.GetResult(PlayerLoginQueryLoad.WeeklyQuestStatus));
            _LoadSeasonalQuestStatus(holder.GetResult(PlayerLoginQueryLoad.SeasonalQuestStatus));
            _LoadMonthlyQuestStatus(holder.GetResult(PlayerLoginQueryLoad.MonthlyQuestStatus));
            _LoadRandomBGStatus(holder.GetResult(PlayerLoginQueryLoad.RandomBg));

            // after spell and quest load
            InitTalentForLevel();
            LearnDefaultSkills();
            LearnCustomSpells();

            _LoadTraits(holder.GetResult(PlayerLoginQueryLoad.TraitConfigs), holder.GetResult(PlayerLoginQueryLoad.TraitEntries)); // must be after loading spells

            // must be before inventory (some items required reputation check)
            reputationMgr.LoadFromDB(holder.GetResult(PlayerLoginQueryLoad.Reputation));

            _LoadInventory(holder.GetResult(PlayerLoginQueryLoad.Inventory), time_diff);

            if (IsVoidStorageUnlocked())
                _LoadVoidStorage(holder.GetResult(PlayerLoginQueryLoad.VoidStorage));

            // update items with duration and realtime
            UpdateItemDuration(time_diff, true);

            StartLoadingActionButtons();

            // unread mails and next delivery time, actual mails not loaded
            _LoadMail(holder.GetResult(PlayerLoginQueryLoad.Mails), holder.GetResult(PlayerLoginQueryLoad.MailItems));

            m_social = Global.SocialMgr.LoadFromDB(holder.GetResult(PlayerLoginQueryLoad.SocialList), GetGUID());

            // check PLAYER_CHOSEN_TITLE compatibility with PLAYER__FIELD_KNOWN_TITLES
            // note: PLAYER__FIELD_KNOWN_TITLES updated at quest status loaded
            if (chosenTitle != 0 && !HasTitle(chosenTitle))
                chosenTitle = 0;

            SetChosenTitle(chosenTitle);

            // has to be called after last Relocate() in Player.LoadFromDB
            SetFallInformation(0, GetPositionZ());

            GetSpellHistory().LoadFromDB<Player>(holder.GetResult(PlayerLoginQueryLoad.SpellCooldowns), holder.GetResult(PlayerLoginQueryLoad.SpellCharges));

            var savedHealth = health;
            if (savedHealth == 0)
                m_deathState = DeathState.Corpse;

            // Spell code allow apply any auras to dead character in load time in aura/spell/item loading
            // Do now before stats re-calculation cleanup for ghost state unexpected auras
            if (!IsAlive())
                RemoveAllAurasOnDeath();
            else
                RemoveAllAurasRequiringDeadTarget();

            //apply all stat bonuses from items and auras
            SetCanModifyStats(true);
            UpdateAllStats();

            // restore remembered power/health values (but not more max values)
            SetHealth(savedHealth > GetMaxHealth() ? GetMaxHealth() : savedHealth);
            int loadedPowers = 0;
            for (PowerType i = 0; i < PowerType.Max; ++i)
            {
                if (Global.DB2Mgr.GetPowerIndexByClass(i, GetClass()) != (int)PowerType.Max)
                {
                    int savedPower = powers[loadedPowers];
                    int maxPower = m_unitData.MaxPower[loadedPowers];
                    SetPower(i, (savedPower > maxPower ? maxPower : savedPower));
                    if (++loadedPowers >= (int)PowerType.MaxPerClass)
                        break;
                }
            }

            for (; loadedPowers < (int)PowerType.MaxPerClass; ++loadedPowers)
                SetUpdateFieldValue(ref m_values.ModifyValue(m_unitData).ModifyValue(m_unitData.Power, loadedPowers), 0);

            SetUsedAmmoId(usedAmmoID);

            Log.outDebug(LogFilter.Player, 
                $"Player.LoadFromDB: The value of player {GetName()} after load item and aura is: ");            

            // GM state
            if (GetSession().HasPermission(RBACPermissions.RestoreSavedGmState))
            {
                switch (WorldConfig.Values[WorldCfg.GmLoginState].Int32)
                {
                    default:
                    case 0:
                        break;             // disable
                    case 1:
                        SetGameMaster(true);
                        break;             // enable
                    case 2:                                         // save state
                        if (extra_flags.HasAnyFlag(PlayerExtraFlags.GMOn))
                            SetGameMaster(true);
                        break;
                }

                switch (WorldConfig.Values[WorldCfg.GmVisibleState].Int32)
                {
                    default:
                    case 0:
                        SetGMVisible(false);
                        break;             // invisible
                    case 1:
                        break;             // visible
                    case 2:                                         // save state
                        if (extra_flags.HasAnyFlag(PlayerExtraFlags.GMInvisible))
                            SetGMVisible(false);
                        break;
                }

                switch (WorldConfig.Values[WorldCfg.GmChat].Int32)
                {
                    default:
                    case 0:
                        break;                 // disable
                    case 1:
                        SetGMChat(true);
                        break;                 // enable
                    case 2:                                         // save state
                        if (extra_flags.HasAnyFlag(PlayerExtraFlags.GMChat))
                            SetGMChat(true);
                        break;
                }

                switch (WorldConfig.Values[WorldCfg.GmWhisperingTo].Int32)
                {
                    default:
                    case 0:
                        break;         // disable
                    case 1:
                        SetAcceptWhispers(true);
                        break;         // enable
                    case 2:                                         // save state
                        if (extra_flags.HasAnyFlag(PlayerExtraFlags.AcceptWhispers))
                            SetAcceptWhispers(true);
                        break;
                }
            }

            InitPvP();

            // RaF stuff.
            if (GetSession().IsARecruiter() || (GetSession().GetRecruiterId() != 0))
                SetDynamicFlag(UnitDynFlags.ReferAFriend);

            _LoadDeclinedNames(holder.GetResult(PlayerLoginQueryLoad.DeclinedNames));

            _LoadEquipmentSets(holder.GetResult(PlayerLoginQueryLoad.EquipmentSets));
            _LoadTransmogOutfits(holder.GetResult(PlayerLoginQueryLoad.TransmogOutfits));

            _LoadCUFProfiles(holder.GetResult(PlayerLoginQueryLoad.CufProfiles));

            _InitHonorLevelOnLoadFromDB(honor, honorLevel);

            _restMgr.LoadRestBonus(RestTypes.Honor, honorRestState, honorRestBonus);
            if (time_diff > 0)
            {
                //speed collect rest bonus in offline, in logout, far from tavern, city (section/in hour)
                float bubble0 = 0.031f;
                //speed collect rest bonus in offline, in logout, in tavern, city (section/in hour)
                float bubble1 = 0.125f;
                float bubble = is_logout_resting > 0
                    ? bubble1 * WorldConfig.Values[WorldCfg.RateRestOfflineInTavernOrCity].Float
                    : bubble0 * WorldConfig.Values[WorldCfg.RateRestOfflineInWilderness].Float;

                _restMgr.AddRestBonus(RestTypes.XP, time_diff * _restMgr.CalcExtraPerSec(RestTypes.XP, bubble));
            }

            // Unlock battle pet system if it's enabled in bnet account
            if (GetSession().GetBattlePetMgr().IsBattlePetSystemEnabled())
                SpellBook.Learn(SharedConst.SpellBattlePetTraining, false);

            m_achievementSys.CheckAllAchievementCriteria(this);
            m_questObjectiveCriteriaMgr.CheckAllQuestObjectiveCriteria(this);

            PushQuests();

            return true;
        }

        public void SaveToDB(bool create = false)
        {
            SQLTransaction loginTransaction = new();
            SQLTransaction characterTransaction = new();

            SaveToDB(loginTransaction, characterTransaction, create);

            DB.Characters.CommitTransaction(characterTransaction);
            DB.Login.CommitTransaction(loginTransaction);
        }

        public void SaveToDB(SQLTransaction loginTransaction, SQLTransaction characterTransaction, bool create = false)
        {
            // delay auto save at any saves (manual, in code, or autosave)
            m_nextSave = WorldConfig.Values[WorldCfg.IntervalSave].Milliseconds;

            //lets allow only players in world to be saved
            if (IsBeingTeleportedFar())
            {
                ScheduleDelayedOperation(PlayerDelayedOperations.SavePlayer);
                return;
            }

            // first save/honor gain after midnight will also update the player's honor fields
            UpdateHonorFields();

            Log.outDebug(LogFilter.Player, 
                $"Player::SaveToDB: The value of player {GetName()} at save: ");

            if (!create)
                Global.ScriptMgr.OnPlayerSave(this);

            PreparedStatement stmt;
            byte index = 0;

            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_FISHINGSTEPS);
            stmt.SetInt64(0, GetGUID().GetCounter());
            characterTransaction.Append(stmt);

            static float finiteAlways(float f) { return float.IsFinite(f) ? f : 0.0f; };

            if (create)
            {
                //! Insert query
                /// @todo: Filter out more redundant fields that can take their default value at player create
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHARACTER);
                stmt.SetInt64(index++, GetGUID().GetCounter());
                stmt.SetInt32(index++, GetSession().GetAccountId());
                stmt.SetString(index++, GetName());
                stmt.SetUInt8(index++, (byte)GetRace());
                stmt.SetUInt8(index++, (byte)GetClass());
                stmt.SetUInt8(index++, (byte)GetNativeGender());   // save gender from PLAYER_BYTES_3, UNIT_BYTES_0 changes with every transform effect
                stmt.SetUInt8(index++, (byte)GetLevel());
                stmt.SetInt32(index++, GetXP());
                stmt.SetInt64(index++, GetMoney());
                stmt.SetUInt8(index++, GetInventorySlotCount());
                stmt.SetUInt8(index++, GetBankBagSlotCount());
                stmt.SetUInt8(index++, m_activePlayerData.RestInfo[(int)RestTypes.XP].StateID);
                stmt.SetUInt32(index++, (uint)m_playerData.PlayerFlags.GetValue());
                stmt.SetUInt32(index++, (uint)m_playerData.PlayerFlagsEx.GetValue());
                stmt.SetUInt16(index++, (ushort)GetMapId());
                stmt.SetInt32(index++, GetInstanceId());
                stmt.SetUInt8(index++, (byte)GetDungeonDifficultyID());
                stmt.SetUInt8(index++, (byte)GetRaidDifficultyID());
                stmt.SetUInt8(index++, (byte)GetLegacyRaidDifficultyID());
                stmt.SetFloat(index++, finiteAlways(GetPositionX()));
                stmt.SetFloat(index++, finiteAlways(GetPositionY()));
                stmt.SetFloat(index++, finiteAlways(GetPositionZ()));
                stmt.SetFloat(index++, finiteAlways(GetOrientation()));
                stmt.SetFloat(index++, finiteAlways(GetTransOffsetX()));
                stmt.SetFloat(index++, finiteAlways(GetTransOffsetY()));
                stmt.SetFloat(index++, finiteAlways(GetTransOffsetZ()));
                stmt.SetFloat(index++, finiteAlways(GetTransOffsetO()));
                long transLowGUID = 0;
                Transport transport = GetTransport<Transport>();
                if (transport != null)
                    transLowGUID = transport.GetGUID().GetCounter();
                stmt.SetInt64(index++, transLowGUID);

                StringBuilder ss = new();
                for (int i = 0; i < m_taxi.m_taximask.Length; ++i)
                    ss.Append(m_taxi.m_taximask[i] + " ");

                stmt.SetString(index++, ss.ToString());
                stmt.SetInt64(index++, (UnixTime64)m_createTime);
                stmt.SetUInt8(index++, (byte)m_createMode);
                stmt.SetUInt8(index++, m_cinematic);
                stmt.SetInt32(index++, m_PlayedTimeTotal);
                stmt.SetInt32(index++, m_PlayedTimeLevel);
                stmt.SetFloat(index++, finiteAlways(_restMgr.GetRestBonus(RestTypes.XP)));
                stmt.SetInt64(index++, LoopTime.UnixServerTime);
                stmt.SetInt32(index++, (HasPlayerFlag(PlayerFlags.Resting) ? 1 : 0));
                //save, far from tavern/city
                //save, but in tavern/city
                stmt.SetUInt32(index++, GetTalentResetCost());
                stmt.SetInt64(index++, (UnixTime64)GetTalentResetTime());
                stmt.SetUInt8(index++, GetActiveTalentGroup());
                stmt.SetUInt8(index++, GetBonusTalentGroupCount());
                stmt.SetUInt16(index++, (ushort)m_ExtraFlags);
                stmt.SetInt32(index++, 0); // summonedPetNumber
                stmt.SetUInt16(index++, (ushort)atLoginFlags);
                stmt.SetInt64(index++, (UnixTime64)m_deathExpireTime);

                ss.Clear();
                ss.Append(m_taxi.SaveTaxiDestinationsToString());

                stmt.SetString(index++, ss.ToString());
                stmt.SetInt32(index++, m_activePlayerData.LifetimeHonorableKills);
                stmt.SetUInt16(index++, m_activePlayerData.TodayHonorableKills);
                stmt.SetUInt16(index++, m_activePlayerData.YesterdayHonorableKills);
                stmt.SetInt32(index++, m_playerData.PlayerTitle);
                stmt.SetUInt32(index++, (uint)m_activePlayerData.WatchedFactionIndex.GetValue()); //TODO: change to signed in DB
                stmt.SetUInt8(index++, GetDrunkValue());
                stmt.SetUInt32(index++, (uint)GetHealth());

                for (int i = 0; i < (int)PowerType.MaxPerClass; ++i)
                    stmt.SetInt32(index++, m_unitData.Power[i]);

                stmt.SetInt32(index++, GetUsedAmmoId());
                stmt.SetUInt32(index++, GetSession().GetLatency());
                stmt.SetInt32(index++, (int)GetLootSpecId());

                ss.Clear();
                for (int i = 0; i < PlayerConst.ExploredZonesSize; ++i)
                    ss.Append($"{(uint)(m_activePlayerData.ExploredZones[i] & 0xFFFFFFFF)} {(uint)((m_activePlayerData.ExploredZones[i] >> 32) & 0xFFFFFFFF)} ");

                stmt.SetString(index++, ss.ToString());

                ss.Clear();
                // cache equipment...
                for (byte i = 0; i < InventorySlots.ReagentBagEnd; ++i)
                {
                    Item item = GetItemByPos(i);
                    if (item != null)
                    {
                        ss.Append($"{(uint)item.GetTemplate().GetInventoryType()} {item.GetDisplayId(this)} ");
                        SpellItemEnchantmentRecord enchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(item.GetVisibleEnchantmentId(this));
                        if (enchant != null)
                            ss.Append(enchant.ItemVisual);
                        else
                            ss.Append('0');

                        ss.Append($" {(uint)CliDB.ItemStorage.LookupByKey(item.GetVisibleEntry(this)).SubclassID.data} {(uint)item.GetVisibleSecondaryModifiedAppearanceId(this)} ");
                    }
                    else
                        ss.Append("0 0 0 0 0 ");
                }

                stmt.SetString(index++, ss.ToString());

                ss.Clear();
                for (int i = 0; i < m_activePlayerData.KnownTitles.Size(); ++i)
                    ss.Append($"{(uint)(m_activePlayerData.KnownTitles[i] & 0xFFFFFFFF)} {(uint)((m_activePlayerData.KnownTitles[i] >> 32) & 0xFFFFFFFF)} ");

                stmt.SetString(index++, ss.ToString());

                stmt.SetUInt8(index++, m_activePlayerData.MultiActionBars);
                stmt.SetUInt32(index++, Global.RealmMgr.GetMinorMajorBugfixVersionForBuild(Global.WorldMgr.GetRealm().Build));
            }
            else
            {
                // Update query
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHARACTER);
                stmt.SetString(index++, GetName());
                stmt.SetUInt8(index++, (byte)GetRace());
                stmt.SetUInt8(index++, (byte)GetClass());
                stmt.SetUInt8(index++, (byte)GetNativeGender());   // save gender from PLAYER_BYTES_3, UNIT_BYTES_0 changes with every transform effect
                stmt.SetUInt8(index++, (byte)GetLevel());
                stmt.SetUInt32(index++, (uint)GetXP()); // TODO: change to signed in DB
                stmt.SetInt64(index++, GetMoney());
                stmt.SetUInt8(index++, GetInventorySlotCount());
                stmt.SetUInt8(index++, GetBankBagSlotCount());
                stmt.SetUInt8(index++, m_activePlayerData.RestInfo[(int)RestTypes.XP].StateID);
                stmt.SetUInt32(index++, (uint)m_playerData.PlayerFlags.GetValue());
                stmt.SetUInt32(index++, (uint)m_playerData.PlayerFlagsEx.GetValue());

                if (!IsBeingTeleported())
                {
                    stmt.SetUInt16(index++, (ushort)GetMapId());
                    stmt.SetInt32(index++, GetInstanceId());
                    stmt.SetUInt8(index++, (byte)GetDungeonDifficultyID());
                    stmt.SetUInt8(index++, (byte)GetRaidDifficultyID());
                    stmt.SetUInt8(index++, (byte)GetLegacyRaidDifficultyID());
                    stmt.SetFloat(index++, finiteAlways(GetPositionX()));
                    stmt.SetFloat(index++, finiteAlways(GetPositionY()));
                    stmt.SetFloat(index++, finiteAlways(GetPositionZ()));
                    stmt.SetFloat(index++, finiteAlways(GetOrientation()));
                }
                else
                {
                    stmt.SetUInt16(index++, (ushort)GetTeleportDest().GetMapId());
                    stmt.SetInt32(index++, 0);
                    stmt.SetUInt8(index++, (byte)GetDungeonDifficultyID());
                    stmt.SetUInt8(index++, (byte)GetRaidDifficultyID());
                    stmt.SetUInt8(index++, (byte)GetLegacyRaidDifficultyID());
                    stmt.SetFloat(index++, finiteAlways(GetTeleportDest().GetPositionX()));
                    stmt.SetFloat(index++, finiteAlways(GetTeleportDest().GetPositionY()));
                    stmt.SetFloat(index++, finiteAlways(GetTeleportDest().GetPositionZ()));
                    stmt.SetFloat(index++, finiteAlways(GetTeleportDest().GetOrientation()));
                }

                stmt.SetFloat(index++, finiteAlways(GetTransOffsetX()));
                stmt.SetFloat(index++, finiteAlways(GetTransOffsetY()));
                stmt.SetFloat(index++, finiteAlways(GetTransOffsetZ()));
                stmt.SetFloat(index++, finiteAlways(GetTransOffsetO()));
                long transLowGUID = 0;
                Transport transport = GetTransport<Transport>();
                if (transport != null)
                    transLowGUID = transport.GetGUID().GetCounter();
                stmt.SetInt64(index++, transLowGUID);

                StringBuilder ss = new();
                for (int i = 0; i < m_taxi.m_taximask.Length; ++i)
                    ss.Append(m_taxi.m_taximask[i] + " ");

                stmt.SetString(index++, ss.ToString());
                stmt.SetUInt8(index++, m_cinematic);
                stmt.SetInt32(index++, m_PlayedTimeTotal);
                stmt.SetInt32(index++, m_PlayedTimeLevel);
                stmt.SetFloat(index++, finiteAlways(_restMgr.GetRestBonus(RestTypes.XP)));
                stmt.SetInt64(index++, LoopTime.UnixServerTime);
                stmt.SetInt32(index++, HasPlayerFlag(PlayerFlags.Resting) ? 1 : 0);
                //save, far from tavern/city
                //save, but in tavern/city
                stmt.SetUInt32(index++, GetTalentResetCost());
                stmt.SetInt64(index++, (UnixTime64)GetTalentResetTime());
                stmt.SetUInt8(index++, GetNumRespecs());
                stmt.SetUInt8(index++, GetActiveTalentGroup());
                stmt.SetUInt8(index++, GetBonusTalentGroupCount());
                stmt.SetUInt16(index++, (ushort)m_ExtraFlags);
                PetStable petStable = GetPetStable();
                if (petStable != null)
                    stmt.SetInt32(index++, petStable.GetCurrentPet() != null && petStable.GetCurrentPet().Health > 0 ? petStable.GetCurrentPet().PetNumber : 0); // summonedPetNumber
                else
                    stmt.SetInt32(index++, 0); // summonedPetNumber
                stmt.SetUInt16(index++, (ushort)atLoginFlags);
                stmt.SetUInt16(index++, (ushort)GetZoneId());
                stmt.SetInt64(index++, (UnixTime64)m_deathExpireTime);

                ss.Clear();
                ss.Append(m_taxi.SaveTaxiDestinationsToString());

                stmt.SetString(index++, ss.ToString());
                stmt.SetInt32(index++, m_activePlayerData.LifetimeHonorableKills);
                stmt.SetUInt16(index++, m_activePlayerData.TodayHonorableKills);
                stmt.SetUInt16(index++, m_activePlayerData.YesterdayHonorableKills);
                stmt.SetInt32(index++, m_playerData.PlayerTitle);
                stmt.SetUInt32(index++, (uint)m_activePlayerData.WatchedFactionIndex.GetValue());
                stmt.SetUInt8(index++, GetDrunkValue());
                stmt.SetUInt32(index++, (uint)GetHealth());

                for (int i = 0; i < (int)PowerType.MaxPerClass; ++i)
                    stmt.SetInt32(index++, m_unitData.Power[i]);

                stmt.SetInt32(index++, GetUsedAmmoId());
                stmt.SetUInt32(index++, GetSession().GetLatency());
                stmt.SetUInt32(index++, (uint)GetLootSpecId());

                ss.Clear();
                for (int i = 0; i < PlayerConst.ExploredZonesSize; ++i)
                    ss.Append($"{(uint)(m_activePlayerData.ExploredZones[i] & 0xFFFFFFFF)} {(uint)((m_activePlayerData.ExploredZones[i] >> 32) & 0xFFFFFFFF)} ");

                stmt.SetString(index++, ss.ToString());

                ss.Clear();
                // cache equipment...
                for (byte i = 0; i < InventorySlots.ReagentBagEnd; ++i)
                {
                    Item item = GetItemByPos(i);
                    if (item != null)
                    {
                        ss.Append($"{(uint)item.GetTemplate().GetInventoryType()} {item.GetDisplayId(this)} ");
                        SpellItemEnchantmentRecord enchant = CliDB.SpellItemEnchantmentStorage.LookupByKey(item.GetVisibleEnchantmentId(this));
                        if (enchant != null)
                            ss.Append(enchant.ItemVisual);
                        else
                            ss.Append('0');

                        ss.Append($" {(uint)CliDB.ItemStorage.LookupByKey(item.GetVisibleEntry(this)).SubclassID.data} {(uint)item.GetVisibleSecondaryModifiedAppearanceId(this)} ");
                    }
                    else
                        ss.Append("0 0 0 0 0 ");
                }

                stmt.SetString(index++, ss.ToString());

                ss.Clear();
                for (int i = 0; i < m_activePlayerData.KnownTitles.Size(); ++i)
                    ss.Append($"{(uint)(m_activePlayerData.KnownTitles[i] & 0xFFFFFFFF)} {(uint)((m_activePlayerData.KnownTitles[i] >> 32) & 0xFFFFFFFF)} ");

                stmt.SetString(index++, ss.ToString());
                stmt.SetUInt8(index++, m_activePlayerData.MultiActionBars);

                stmt.SetInt32(index++, IsInWorld && !GetSession().PlayerLogout() ? 1 : 0);
                stmt.SetInt32(index++, m_activePlayerData.Honor);
                stmt.SetInt32(index++, GetHonorLevel());
                stmt.SetUInt8(index++, m_activePlayerData.RestInfo[(int)RestTypes.Honor].StateID);
                stmt.SetFloat(index++, finiteAlways(_restMgr.GetRestBonus(RestTypes.Honor)));
                stmt.SetUInt32(index++, Global.RealmMgr.GetMinorMajorBugfixVersionForBuild(Global.WorldMgr.GetRealm().Build));

                // Index
                stmt.SetInt64(index, GetGUID().GetCounter());
            }

            characterTransaction.Append(stmt);

            if (m_fishingSteps != 0)
            {
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.INS_CHAR_FISHINGSTEPS);
                index = 0;
                stmt.SetInt64(index++, GetGUID().GetCounter());
                stmt.SetUInt8(index++, m_fishingSteps);
                characterTransaction.Append(stmt);
            }

            if (m_mailsUpdated)                                     //save mails only when needed
                _SaveMail(characterTransaction);

            _SaveCustomizations(characterTransaction);
            _SaveBGData(characterTransaction);
            _SaveInventory(characterTransaction);
            _SaveVoidStorage(characterTransaction);
            _SaveQuestStatus(characterTransaction);
            _SaveDailyQuestStatus(characterTransaction);
            _SaveWeeklyQuestStatus(characterTransaction);
            _SaveSeasonalQuestStatus(characterTransaction);
            _SaveMonthlyQuestStatus(characterTransaction);
            _SaveGlyphs(characterTransaction);
            _SaveTalents(characterTransaction);
            _SaveTraits(characterTransaction);
            _SaveSpells(characterTransaction);
            GetSpellHistory().SaveToDB<Player>(characterTransaction);
            _SaveActions(characterTransaction);
            _SaveAuras(characterTransaction);
            _SaveSkills(characterTransaction);
            _SaveStoredAuraTeleportLocations(characterTransaction);
            m_achievementSys.SaveToDB(characterTransaction);
            reputationMgr.SaveToDB(characterTransaction);
            m_questObjectiveCriteriaMgr.SaveToDB(characterTransaction);
            _SaveEquipmentSets(characterTransaction);
            GetSession().SaveTutorialsData(characterTransaction);                 // changed only while character in game
            _SaveInstanceTimeRestrictions(characterTransaction);
            _SaveCurrency(characterTransaction);
            _SaveCUFProfiles(characterTransaction);

            // check if stats should only be saved on logout
            // save stats can be out of transaction
            if (GetSession().IsLogingOut() || !WorldConfig.Values[WorldCfg.StatsSaveOnlyOnLogout].Bool)
                _SaveStats(characterTransaction);

            // TODO: Move this out
            GetSession().GetCollectionMgr().SaveAccountToys(loginTransaction);
            GetSession().GetBattlePetMgr().SaveToDB(loginTransaction);
            GetSession().GetCollectionMgr().SaveAccountHeirlooms(loginTransaction);
            GetSession().GetCollectionMgr().SaveAccountMounts(loginTransaction);
            GetSession().GetCollectionMgr().SaveAccountItemAppearances(loginTransaction);

            stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_BNET_LAST_PLAYER_CHARACTERS);
            stmt.SetInt32(0, GetSession().GetAccountId());
            stmt.SetUInt8(1, Global.WorldMgr.GetRealmId().Region);
            stmt.SetUInt8(2, Global.WorldMgr.GetRealmId().Site);
            loginTransaction.Append(stmt);

            stmt = LoginDatabase.GetPreparedStatement(LoginStatements.INS_BNET_LAST_PLAYER_CHARACTERS);
            stmt.SetInt32(0, GetSession().GetAccountId());
            stmt.SetUInt8(1, Global.WorldMgr.GetRealmId().Region);
            stmt.SetUInt8(2, Global.WorldMgr.GetRealmId().Site);
            stmt.SetInt32(3, Global.WorldMgr.GetRealmId().Index);
            stmt.SetString(4, GetName());
            stmt.SetInt64(5, GetGUID().GetCounter());
            stmt.SetInt32(6, LoopTime.UnixServerTime);
            loginTransaction.Append(stmt);

            // save pet (hunter pet level and experience and all Type pets health/mana).
            Pet pet = GetPet();
            if (pet != null)
                pet.SavePetToDB(PetSaveMode.AsCurrent);
        }

        public static int GetZoneIdFromDB(ObjectGuid guid)
        {
            long guidLow = guid.GetCounter();
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHAR_ZONE);
            stmt.SetInt64(0, guidLow);
            SQLResult result = DB.Characters.Query(stmt);

            if (result.IsEmpty())
                return 0;

            int zone = result.Read<ushort>(0);
            if (zone == 0)
            {
                // stored zone is zero, use generic and slow zone detection
                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHAR_POSITION_XYZ);
                stmt.SetInt64(0, guidLow);
                SQLResult posResult = DB.Characters.Query(stmt);

                if (posResult.IsEmpty())
                    return 0;

                int map = posResult.Read<short>(0);
                float posx = posResult.Read<float>(1);
                float posy = posResult.Read<float>(2);
                float posz = posResult.Read<float>(3);

                if (!CliDB.MapStorage.ContainsKey(map))
                    return 0;

                zone = Global.TerrainMgr.GetZoneId(PhasingHandler.EmptyPhaseShift, map, posx, posy, posz);

                if (zone > 0)
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_ZONE);

                    stmt.SetInt32(0, zone);
                    stmt.SetInt64(1, guidLow);

                    DB.Characters.Execute(stmt);
                }
            }

            return zone;
        }

        public static void RemovePetitionsAndSigns(ObjectGuid guid)
        {
            Global.PetitionMgr.RemoveSignaturesBySigner(guid);
            Global.PetitionMgr.RemovePetitionsByOwner(guid);
        }

        public static void DeleteFromDB(ObjectGuid playerGuid, int accountId, bool updateRealmChars = true, bool deleteFinally = false)
        {
            // Avoid realm-update for non-existing account
            if (accountId == 0)
                updateRealmChars = false;

            // Convert guid to low GUID for CharacterNameData, but also other methods on success
            var guid = playerGuid.GetCounter();
            CharDeleteMethod charDelete_method = (CharDeleteMethod)WorldConfig.Values[WorldCfg.ChardeleteMethod].Int32;
            CharacterCacheEntry characterInfo = Global.CharacterCacheStorage.GetCharacterCacheByGuid(playerGuid);
            string name = "<Unknown>";
            if (characterInfo != null)
                name = characterInfo.Name;

            if (deleteFinally)
                charDelete_method = CharDeleteMethod.Remove;
            else if (characterInfo != null)    // To avoid a Select, we select loaded data. If it doesn't exist, return.
            {
                // Define the required variables
                int charDeleteMinLvl;

                if (characterInfo.ClassId == Class.DeathKnight)
                    charDeleteMinLvl = WorldConfig.Values[WorldCfg.ChardeleteDeathKnightMinLevel].Int32;
                else if (characterInfo.ClassId == Class.DemonHunter)
                    charDeleteMinLvl = WorldConfig.Values[WorldCfg.ChardeleteDemonHunterMinLevel].Int32;
                else
                    charDeleteMinLvl = WorldConfig.Values[WorldCfg.ChardeleteMinLevel].Int32;

                // if we want to finalize the character removal or the character
                // does not meet the level requirement of either heroic or non-heroic settings,
                // we set it to mode CHAR_DELETE_REMOVE
                if (characterInfo.Level < charDeleteMinLvl)
                    charDelete_method = CharDeleteMethod.Remove;
            }

            SQLTransaction trans = new();
            SQLTransaction loginTransaction = new();

            var guildId = Global.CharacterCacheStorage.GetCharacterGuildIdByGuid(playerGuid);
            if (guildId != 0)
            {
                Guild guild = Global.GuildMgr.GetGuildById(guildId);
                if (guild != null)
                    guild.DeleteMember(trans, playerGuid, false, false, true);
            }

            // remove from arena teams
            LeaveAllArenaTeams(playerGuid);

            // the player was uninvited already on logout so just remove from group
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_GROUP_MEMBER);
            stmt.SetInt64(0, guid);
            SQLResult resultGroup = DB.Characters.Query(stmt);
            {
                if (!resultGroup.IsEmpty())
                {
                    var group = Global.GroupMgr.GetGroupByDbStoreId(resultGroup.Read<int>(0));
                    if (group != null)
                        RemoveFromGroup(group, playerGuid);
                }
            }

            // Remove signs from petitions (also remove petitions if owner);
            RemovePetitionsAndSigns(playerGuid);

            switch (charDelete_method)
            {
                // Completely remove from the database
                case CharDeleteMethod.Remove:
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHAR_COD_ITEM_MAIL);
                    stmt.SetInt64(0, guid);
                    SQLResult resultMail = DB.Characters.Query(stmt);
                    {
                        if (!resultMail.IsEmpty())
                        {
                            MultiMap<long, Item> itemsByMail = new();

                            stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_MAILITEMS);
                            stmt.SetInt64(0, guid);
                            SQLResult resultItems = DB.Characters.Query(stmt);
                            {
                                if (!resultItems.IsEmpty())
                                {
                                    do
                                    {
                                        long mailId = resultItems.Read<long>(46);
                                        Item mailItem = _LoadMailedItem(playerGuid, null, mailId, null, resultItems.GetFields());
                                        if (mailItem != null)
                                            itemsByMail.Add(mailId, mailItem);

                                    } while (resultItems.NextRow());
                                }
                            }

                            do
                            {
                                long mail_id = resultMail.Read<long>(0);
                                MailMessageType mailType = (MailMessageType)resultMail.Read<byte>(1);
                                ushort mailTemplateId = resultMail.Read<ushort>(2);
                                long sender = resultMail.Read<long>(3);
                                string subject = resultMail.Read<string>(4);
                                string body = resultMail.Read<string>(5);
                                long money = resultMail.Read<long>(6);
                                bool has_items = resultMail.Read<bool>(7);

                                // We can return mail now
                                // So firstly delete the old one
                                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_BY_ID);
                                stmt.SetInt64(0, mail_id);
                                trans.Append(stmt);

                                // Mail is not from player
                                if (mailType != MailMessageType.Normal)
                                {
                                    if (has_items)
                                    {
                                        stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_ITEM_BY_ID);
                                        stmt.SetInt64(0, mail_id);
                                        trans.Append(stmt);
                                    }
                                    continue;
                                }

                                MailDraft draft = new(subject, body);
                                if (mailTemplateId != 0)
                                    draft = new MailDraft(mailTemplateId, false);    // items are already included

                                var itemsList = itemsByMail[mail_id];
                                if (!itemsList.Empty())
                                {
                                    foreach (var item in itemsList)
                                        draft.AddItem(item);

                                    itemsByMail.Remove(mail_id);
                                }

                                stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_ITEM_BY_ID);
                                stmt.SetInt64(0, mail_id);
                                trans.Append(stmt);

                                var pl_account = Global.CharacterCacheStorage.GetCharacterAccountIdByGuid(ObjectGuid.Create(HighGuid.Player, guid));

                                draft.AddMoney(money).SendReturnToSender(pl_account, guid, sender, trans);
                            }
                            while (resultMail.NextRow());

                            // Free remaining items
                            foreach (var pair in itemsByMail)
                                pair.Value.Dispose();
                        }
                    }

                    // Unsummon and delete for pets in world is not required: player deleted from CLI or character list with not loaded pet.
                    // NOW we can finally clear other DB data related to character
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHAR_PET_IDS);
                    stmt.SetInt64(0, guid);
                    SQLResult resultPets = DB.Characters.Query(stmt);
                    {

                        if (!resultPets.IsEmpty())
                        {
                            do
                            {
                                int petguidlow = resultPets.Read<int>(0);
                                Pet.DeleteFromDB(petguidlow);
                            } while (resultPets.NextRow());
                        }
                    }

                    // Delete char from social list of online chars
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHAR_SOCIAL);
                    stmt.SetInt64(0, guid);
                    SQLResult resultFriends = DB.Characters.Query(stmt);
                    {
                        if (!resultFriends.IsEmpty())
                        {
                            do
                            {
                                var playerFriend = Global.ObjAccessor.FindPlayer(ObjectGuid.Create(HighGuid.Player, resultFriends.Read<long>(0)));
                                if (playerFriend != null)
                                {
                                    playerFriend.GetSocial().RemoveFromSocialList(playerGuid, SocialFlag.All);
                                    Global.SocialMgr.SendFriendStatus(playerFriend, FriendsResult.Removed, playerGuid);
                                }
                            } while (resultFriends.NextRow());
                        }
                    }

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_CUSTOMIZATIONS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PLAYER_ACCOUNT_DATA);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_DECLINED_NAME);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_ACTION);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_ARENA_STATS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_AURA_EFFECT);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_AURA);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PLAYER_BGDATA);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_BATTLEGROUND_RANDOM);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_CUF_PROFILES);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PLAYER_CURRENCY);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_GIFT);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_PLAYER_HOMEBIND);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_INSTANCE_LOCK_BY_GUID);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_INVENTORY);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_QUESTSTATUS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_QUESTSTATUS_OBJECTIVES);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_QUESTSTATUS_REWARDED);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_REPUTATION);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_SPELL);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_SPELL_COOLDOWNS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_SPELL_CHARGES);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_GEMS_BY_OWNER);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_TRANSMOG_BY_OWNER);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_ITEM_INSTANCE_BY_OWNER);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_SOCIAL_BY_FRIEND);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_SOCIAL_BY_GUID);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_MAIL_ITEMS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_PET_BY_OWNER);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_PET_DECLINEDNAME_BY_OWNER);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_ACHIEVEMENTS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_ACHIEVEMENT_PROGRESS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_EQUIPMENTSETS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_TRANSMOG_OUTFITS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_EVENTLOG_BY_PLAYER);
                    stmt.SetInt64(0, guid);
                    stmt.SetInt64(1, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_GUILD_BANK_EVENTLOG_BY_PLAYER);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_GLYPHS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_QUESTSTATUS_DAILY);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_QUESTSTATUS_WEEKLY);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_QUESTSTATUS_MONTHLY);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_QUESTSTATUS_SEASONAL);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_TALENT);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_SKILLS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_STATS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_VOID_STORAGE_ITEM_BY_CHAR_GUID);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_FISHINGSTEPS);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_FAVORITE_AUCTIONS_BY_CHAR);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHARACTER_AURA_STORED_LOCATIONS_BY_GUID);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_BATTLE_PET_DECLINED_NAME_BY_OWNER);
                    stmt.SetInt64(0, guid);
                    stmt.SetInt32(1, Global.WorldMgr.GetRealmId().Index);
                    loginTransaction.Append(stmt);

                    stmt = LoginDatabase.GetPreparedStatement(LoginStatements.DEL_BATTLE_PETS_BY_OWNER);
                    stmt.SetInt64(0, guid);
                    stmt.SetInt32(1, Global.WorldMgr.GetRealmId().Index);
                    loginTransaction.Append(stmt);

                    Corpse.DeleteFromDB(playerGuid, trans);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_TRAIT_ENTRIES_BY_CHAR);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.DEL_CHAR_TRAIT_CONFIGS_BY_CHAR);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    Global.CharacterCacheStorage.DeleteCharacterCacheEntry(playerGuid, name);
                    break;
                }

                // The character gets unlinked from the account, the name gets freed up and appears as deleted ingame
                case CharDeleteMethod.Unlink:
                {
                    stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_DELETE_INFO);
                    stmt.SetInt64(0, guid);
                    trans.Append(stmt);

                    Global.CharacterCacheStorage.UpdateCharacterInfoDeleted(playerGuid, true, "");
                    break;
                }
                default:
                    Log.outError(LogFilter.Player, 
                        $"Player.DeleteFromDB: Tried to delete player ({playerGuid}) " +
                        $"with unsupported delete method ({charDelete_method}).");

                    if (trans.commands.Count > 0)
                        DB.Characters.CommitTransaction(trans);
                    return;
            }

            DB.Login.CommitTransaction(loginTransaction);
            DB.Characters.CommitTransaction(trans);

            if (updateRealmChars)
                Global.WorldMgr.UpdateRealmCharCount(accountId);
        }

        public static bool DeleteOldCharacters()
        {
            TimeSpan keepTime = WorldConfig.Values[WorldCfg.ChardeleteKeepDays].TimeSpan;
            return DeleteOldCharacters(keepTime);
        }

        public static bool DeleteOldCharacters(TimeSpan keepTime)
        {
            if (keepTime <= TimeSpan.Zero)
            {
                Log.outDebug(LogFilter.Player,
                    $"Player:DeleteOldChars: There are nothing to delete " +
                    $"for negative or zero-time ({Time.SpanToTimeString(keepTime)}).");
                return false;
            }                

            Log.outInfo(LogFilter.Player, 
                $"Player:DeleteOldChars: Deleting all characters which have been deleted {Time.SpanToTimeString(keepTime)} before...");

            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHAR_OLD_CHARS);
            stmt.SetInt64(0, (UnixTime64)(LoopTime.ServerTime - keepTime));
            SQLResult result = DB.Characters.Query(stmt);

            if (!result.IsEmpty())
            {
                int count = 0;
                do
                {
                    DeleteFromDB(ObjectGuid.Create(HighGuid.Player, result.Read<long>(0)), result.Read<int>(1), true, true);
                    count++;
                }
                while (result.NextRow());
                Log.outDebug(LogFilter.Player, $"Player:DeleteOldChars: Deleted {count} character(s)");
            }

            return true;
        }

        public static void SavePositionInDB(WorldLocation loc, int zoneId, ObjectGuid guid, SQLTransaction trans = null)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.UPD_CHARACTER_POSITION);
            stmt.SetFloat(0, loc.GetPositionX());
            stmt.SetFloat(1, loc.GetPositionY());
            stmt.SetFloat(2, loc.GetPositionZ());
            stmt.SetFloat(3, loc.GetOrientation());
            stmt.SetUInt16(4, (ushort)loc.GetMapId());
            stmt.SetInt32(5, zoneId);
            stmt.SetInt64(6, guid.GetCounter());

            DB.Characters.ExecuteOrAppend(trans, stmt);
        }

        public static bool LoadPositionFromDB(out WorldLocation loc, out bool inFlight, ObjectGuid guid)
        {
            PreparedStatement stmt = CharacterDatabase.GetPreparedStatement(CharStatements.SEL_CHAR_POSITION);
            stmt.SetInt64(0, guid.GetCounter());
            SQLResult result = DB.Characters.Query(stmt);

            loc = new WorldLocation();
            inFlight = false;

            if (result.IsEmpty())
                return false;

            loc.posX = result.Read<float>(0);
            loc.posY = result.Read<float>(1);
            loc.posZ = result.Read<float>(2);
            loc.Orientation = result.Read<float>(3);
            loc.SetMapId(result.Read<ushort>(4));
            inFlight = !string.IsNullOrEmpty(result.Read<string>(5));

            return true;
        }
    }

    public enum CharDeleteMethod
    {
        Remove = 0,                      // Completely remove from the database
        Unlink = 1                       // The character gets unlinked from the account,
        // the name gets freed up and appears as deleted ingame
    }
}
