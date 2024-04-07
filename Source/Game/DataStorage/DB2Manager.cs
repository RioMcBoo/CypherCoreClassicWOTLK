// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using Game.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Game.DataStorage
{
    using static CliDB;

    public class DB2Manager : Singleton<DB2Manager>
    {
        DB2Manager()
        {
            for (uint i = 0; i < (int)Class.Max; ++i)
            {
                _powersByClass[i] = new int[(int)PowerType.Max];

                for (int j = 0; j < (int)PowerType.Max; ++j)
                    _powersByClass[i][j] = (int)PowerType.Max;
            }

            for (uint i = 0; i < (int)Locale.Total + 1; ++i)
                _nameValidators[i] = new List<string>();

            for (var i = 0; i < (int)Locale.Total; ++i)
            {
                _hotfixBlob[i] = new Dictionary<(uint tableHas, int recordId), byte[]>();
                _hotfixOptionalData[i] = new MultiMap<(uint tableHas, int recordId), HotfixOptionalData>();
            }
        }

        public void LoadStores()
        {
            foreach (var areaGroupMember in AreaGroupMemberStorage.Values)
                _areaGroupMembers.Add(areaGroupMember.AreaGroupID, areaGroupMember.AreaID);

            foreach (BattlemasterListRecord battlemaster in BattlemasterListStorage.Values)
            {
                if (battlemaster.MaxLevel < battlemaster.MinLevel)
                {
                    Log.outError(LogFilter.ServerLoading, $"Battlemaster ({battlemaster.Id}) contains bad values for MinLevel ({battlemaster.MinLevel}) and MaxLevel ({battlemaster.MaxLevel}). Swapping values.");
                    (battlemaster.MaxLevel, battlemaster.MinLevel) = (battlemaster.MinLevel, battlemaster.MaxLevel);
                }
                if (battlemaster.MaxPlayers < battlemaster.MinPlayers)
                {
                    Log.outError(LogFilter.ServerLoading, $"Battlemaster ({battlemaster.Id}) contains bad values for MinPlayers ({battlemaster.MinPlayers}) and MaxPlayers ({battlemaster.MaxPlayers}). Swapping values.");
                    (battlemaster.MaxPlayers, battlemaster.MinPlayers) = (battlemaster.MinPlayers, battlemaster.MaxPlayers);
                }
            }

            foreach (var uiDisplay in ChrClassUIDisplayStorage.Values)
            {
                Cypher.Assert(uiDisplay.ChrClassesID < Class.Max);
                _uiDisplayByClass[(int)uiDisplay.ChrClassesID] = uiDisplay;
            }

            var powers = new List<ChrClassesXPowerTypesRecord>();
            foreach (var chrClasses in ChrClassesXPowerTypesStorage.Values)
                powers.Add(chrClasses);

            powers.Sort(new ChrClassesXPowerTypesRecordComparer());
            foreach (var power in powers)
            {
                // Personal serial number of the power type for each class. Simply skips unused power types during counting.
                var index = 0;
                for (int j = 0; j < (int)PowerType.Max; ++j)
                    if (_powersByClass[power.ClassID][j] != (int)PowerType.Max)
                        ++index;

                _powersByClass[power.ClassID][(int)power.PowerType] = index;
            }

            foreach (var customizationChoice in ChrCustomizationChoiceStorage.Values)
                _chrCustomizationChoicesByOption.Add(customizationChoice.ChrCustomizationOptionID, customizationChoice);

            MultiMap<int, (int, ShapeShiftForm)> shapeshiftFormByModel = new();
            Dictionary<int, ChrCustomizationDisplayInfoRecord> displayInfoByCustomizationChoice = new();

            // build shapeshift form model lookup
            foreach (ChrCustomizationElementRecord customizationElement in ChrCustomizationElementStorage.Values)
            {
                ChrCustomizationDisplayInfoRecord customizationDisplayInfo = ChrCustomizationDisplayInfoStorage.LookupByKey(customizationElement.ChrCustomizationDisplayInfoID);
                if (customizationDisplayInfo != null)
                {
                    ChrCustomizationChoiceRecord customizationChoice = ChrCustomizationChoiceStorage.LookupByKey(customizationElement.ChrCustomizationChoiceID);
                    if (customizationChoice != null)
                    {
                        displayInfoByCustomizationChoice[customizationElement.ChrCustomizationChoiceID] = customizationDisplayInfo;
                        ChrCustomizationOptionRecord customizationOption = ChrCustomizationOptionStorage.LookupByKey(customizationChoice.ChrCustomizationOptionID);
                        if (customizationOption != null)
                            shapeshiftFormByModel.Add(customizationOption.ChrModelID, (customizationOption.Id, customizationDisplayInfo.ShapeshiftFormID));
                    }
                }
            }

            MultiMap<int, ChrCustomizationOptionRecord> customizationOptionsByModel = new();
            foreach (ChrCustomizationOptionRecord customizationOption in ChrCustomizationOptionStorage.Values)
                customizationOptionsByModel.Add(customizationOption.ChrModelID, customizationOption);

            foreach (ChrCustomizationReqChoiceRecord reqChoice in ChrCustomizationReqChoiceStorage.Values)
            {
                ChrCustomizationChoiceRecord customizationChoice = ChrCustomizationChoiceStorage.LookupByKey(reqChoice.ChrCustomizationChoiceID);
                if (customizationChoice != null)
                {
                    if (!_chrCustomizationRequiredChoices.ContainsKey(reqChoice.ChrCustomizationReqID))
                        _chrCustomizationRequiredChoices[reqChoice.ChrCustomizationReqID] = new MultiMap<int, int>();

                    _chrCustomizationRequiredChoices[reqChoice.ChrCustomizationReqID].Add(customizationChoice.ChrCustomizationOptionID, reqChoice.ChrCustomizationChoiceID);
                }
            }

            Dictionary<Race, Race> parentRaces = new();
            foreach (ChrRacesRecord chrRace in ChrRacesStorage.Values)
                if (chrRace.UnalteredVisualRaceID != 0)
                    parentRaces[chrRace.UnalteredVisualRaceID] = chrRace.Id;

            foreach (ChrRaceXChrModelRecord raceModel in ChrRaceXChrModelStorage.Values)
            {
                ChrModelRecord model = ChrModelStorage.LookupByKey(raceModel.ChrModelID);
                if (model != null)
                {
                    _chrModelsByRaceAndGender[(raceModel.ChrRacesID, raceModel.Sex)] = model;

                    var customizationOptionsForModel = customizationOptionsByModel.LookupByKey(model.Id);
                    if (customizationOptionsForModel != null)
                    {
                        _chrCustomizationOptionsByRaceAndGender.AddRange((raceModel.ChrRacesID, raceModel.Sex), customizationOptionsForModel);

                        var parentRace = parentRaces.LookupByKey(raceModel.ChrRacesID);
                        if (parentRace != 0)
                            _chrCustomizationOptionsByRaceAndGender.AddRange((parentRace, raceModel.Sex), customizationOptionsForModel);
                    }

                    // link shapeshift displays to race/gender/form
                    foreach (var shapeshiftOptionsForModel in shapeshiftFormByModel.LookupByKey(model.Id))
                    {
                        ShapeshiftFormModelData data = new();
                        data.OptionID = shapeshiftOptionsForModel.Item1;
                        data.Choices = _chrCustomizationChoicesByOption.LookupByKey(shapeshiftOptionsForModel.Item1);
                        if (!data.Choices.Empty())
                        {
                            for (int i = 0; i < data.Choices.Count; ++i)
                                data.Displays.Add(displayInfoByCustomizationChoice.LookupByKey(data.Choices[i].Id));
                        }

                        _chrCustomizationChoicesForShapeshifts[(raceModel.ChrRacesID, raceModel.Sex, shapeshiftOptionsForModel.Item2)] = data;
                    }
                }
            }

            foreach (ChrSpecializationRecord chrSpec in ChrSpecializationStorage.Values)
            {
                //ASSERT(chrSpec.ClassID < MAX_CLASSES);
                //ASSERT(chrSpec.OrderIndex < MAX_SPECIALIZATIONS);

                Class storageIndex = chrSpec.ClassID;
                if (chrSpec.HasFlag(ChrSpecializationFlag.PetOverrideSpec))
                {
                    //ASSERT(!chrSpec.ClassID);
                    storageIndex = Class.Max;
                }
                if (_chrSpecializationsByIndex[(int)storageIndex] == null)
                    _chrSpecializationsByIndex[(int)storageIndex] = new ChrSpecializationRecord[PlayerConst.MaxSpecializations];

                _chrSpecializationsByIndex[(int)storageIndex][chrSpec.OrderIndex] = chrSpec;
            }

            foreach (ConditionalContentTuningRecord conditionalContentTuning in ConditionalContentTuningStorage.Values)
                _conditionalContentTuning.Add(conditionalContentTuning.ParentContentTuningID, conditionalContentTuning);

            foreach (CurrencyContainerRecord currencyContainer in CurrencyContainerStorage.Values)
                _currencyContainers.Add(currencyContainer.CurrencyTypesID, currencyContainer);

            MultiMap<int, CurvePointRecord> unsortedPoints = new();
            foreach (var curvePoint in CurvePointStorage.Values)
                if (CurveStorage.ContainsKey(curvePoint.CurveID))
                    unsortedPoints.Add(curvePoint.CurveID, curvePoint);

            foreach (var curveId in unsortedPoints.Keys)
            {
                var curvePoints = unsortedPoints[curveId];
                curvePoints.Sort((point1, point2) => point1.OrderIndex.CompareTo(point2.OrderIndex));
                _curvePoints.AddRange(curveId, curvePoints.Select(p => p.Pos));
            }

            foreach (EmotesTextSoundRecord emoteTextSound in EmotesTextSoundStorage.Values)
                _emoteTextSounds[(emoteTextSound.EmotesTextId, emoteTextSound.RaceId, emoteTextSound.SexId, emoteTextSound.ClassId)] = emoteTextSound;

            foreach (ExpectedStatRecord expectedStat in ExpectedStatStorage.Values)
                _expectedStatsByLevel[(expectedStat.Lvl, expectedStat.ExpansionID)] = expectedStat;

            foreach (FactionRecord faction in FactionStorage.Values)
                if (faction.ParentFactionID != 0)
                    _factionTeams.Add(faction.ParentFactionID, faction.Id);

            foreach (FriendshipRepReactionRecord friendshipRepReaction in FriendshipRepReactionStorage.Values)
                _friendshipRepReactions.Add(friendshipRepReaction.FriendshipRepID, friendshipRepReaction);

            foreach (var key in _friendshipRepReactions.Keys)
                _friendshipRepReactions[key].Sort(new FriendshipRepReactionRecordComparer());

            foreach (GameObjectDisplayInfoRecord gameObjectDisplayInfo in GameObjectDisplayInfoStorage.Values)
            {
                if (gameObjectDisplayInfo.GeoBoxMax.X < gameObjectDisplayInfo.GeoBoxMin.X)
                    (gameObjectDisplayInfo.GeoBoxMax, gameObjectDisplayInfo.GeoBoxMin) = (gameObjectDisplayInfo.GeoBoxMin, gameObjectDisplayInfo.GeoBoxMax);
            }

            foreach (HeirloomRecord heirloom in HeirloomStorage.Values)
                _heirlooms[heirloom.ItemID] = heirloom;

            foreach (GlyphBindableSpellRecord glyphBindableSpell in GlyphBindableSpellStorage.Values)
                _glyphBindableSpells.Add(glyphBindableSpell.GlyphPropertiesID, glyphBindableSpell.SpellID);

            foreach (GlyphRequiredSpecRecord glyphRequiredSpec in GlyphRequiredSpecStorage.Values)
                _glyphRequiredSpecs.Add(glyphRequiredSpec.GlyphPropertiesID, (ChrSpecialization)glyphRequiredSpec.ChrSpecializationID);

            foreach (ItemChildEquipmentRecord itemChildEquipment in ItemChildEquipmentStorage.Values)
            {
                //ASSERT(_itemChildEquipment.find(itemChildEquipment.ParentItemID) == _itemChildEquipment.end(), "Item must have max 1 child item.");
                _itemChildEquipment[itemChildEquipment.ParentItemID] = itemChildEquipment;
            }

            foreach (ItemClassRecord itemClass in ItemClassStorage.Values)
            {
                //ASSERT(itemClass.ClassID < _itemClassByOldEnum.size());
                //ASSERT(!_itemClassByOldEnum[itemClass.ClassID]);
                _itemClassByOldEnum[itemClass.ClassID] = itemClass;
            }

            foreach (ItemCurrencyCostRecord itemCurrencyCost in ItemCurrencyCostStorage.Values)
                _itemsWithCurrencyCost.Add(itemCurrencyCost.ItemID);

            foreach (ItemEffectRecord itemEffect in ItemEffectStorage.Values)
                _itemEffectsByItemId.Add(itemEffect.ParentItemID, itemEffect);


            foreach (ItemLimitCategoryConditionRecord condition in ItemLimitCategoryConditionStorage.Values)
                _itemCategoryConditions.Add(condition.ParentItemLimitCategoryID, condition);

            foreach (var appearanceMod in ItemModifiedAppearanceStorage.Values)
            {
                //ASSERT(appearanceMod.ItemID <= -1);
                _itemModifiedAppearancesByItem[appearanceMod.ItemID | (appearanceMod.ItemAppearanceModifierID << 24)] = appearanceMod;
            }

            foreach (ItemSetSpellRecord itemSetSpell in ItemSetSpellStorage.Values)
                _itemSetSpells.Add(itemSetSpell.ItemSetID, itemSetSpell);

            foreach (var itemSpecOverride in ItemSpecOverrideStorage.Values)
                _itemSpecOverrides.Add(itemSpecOverride.ItemID, itemSpecOverride);

            foreach (JournalTierRecord journalTier in JournalTierStorage.Values)
                _journalTiersByIndex.Add(journalTier);

            foreach (MapDifficultyRecord entry in MapDifficultyStorage.Values)
            {
                if (!_mapDifficulties.ContainsKey(entry.MapID))
                    _mapDifficulties[entry.MapID] = new Dictionary<Difficulty, MapDifficultyRecord>();

                _mapDifficulties[entry.MapID][entry.DifficultyID] = entry;
            }

            List<MapDifficultyXConditionRecord> mapDifficultyConditions = new();
            foreach (var mapDifficultyCondition in MapDifficultyXConditionStorage.Values)
                mapDifficultyConditions.Add(mapDifficultyCondition);

            mapDifficultyConditions = mapDifficultyConditions.OrderBy(p => p.OrderIndex).ToList();

            foreach (var mapDifficultyCondition in mapDifficultyConditions)
            {
                PlayerConditionRecord playerCondition = PlayerConditionStorage.LookupByKey(mapDifficultyCondition.PlayerConditionID);
                if (playerCondition != null)
                    _mapDifficultyConditions.Add(mapDifficultyCondition.MapDifficultyID, (mapDifficultyCondition.Id, playerCondition));
            }

            foreach (var mount in MountStorage.Values)
                _mountsBySpellId[mount.SourceSpellID] = mount;

            foreach (MountTypeXCapabilityRecord mountTypeCapability in MountTypeXCapabilityStorage.Values)
                _mountCapabilitiesByType.Add(mountTypeCapability.MountTypeID, mountTypeCapability);

            foreach (var key in _mountCapabilitiesByType.Keys)
                _mountCapabilitiesByType[key].Sort(new MountTypeXCapabilityRecordComparer());

            foreach (MountXDisplayRecord mountDisplay in MountXDisplayStorage.Values)
                _mountDisplays.Add(mountDisplay.MountID, mountDisplay);

            foreach (var entry in NameGenStorage.Values)
            {
                if (!_nameGenData.ContainsKey(entry.RaceID))
                {
                    _nameGenData[entry.RaceID] = new List<NameGenRecord>[2];
                    for (var i = 0; i < 2; ++i)
                        _nameGenData[entry.RaceID][i] = new List<NameGenRecord>();
                }

                _nameGenData[entry.RaceID][(int)entry.Sex].Add(entry);
            }

            foreach (var namesProfanity in NamesProfanityStorage.Values)
            {
                Cypher.Assert(namesProfanity.Language < Locale.Total || namesProfanity.Language == Locale.AllLanguages);
                if (namesProfanity.Language != Locale.AllLanguages)
                    _nameValidators[(int)namesProfanity.Language].Add(namesProfanity.Name);
                else
                    for (int i = 0; i < (int)Locale.Total; ++i)
                    {
                        if (i == (int)Locale.None)
                            continue;

                        _nameValidators[i].Add(namesProfanity.Name);
                    }
            }

            foreach (var namesReserved in NamesReservedStorage.Values)
                _nameValidators[(int)Locale.Total].Add(namesReserved.Name);

            foreach (var namesReserved in NamesReservedLocaleStorage.Values)
            {
                Cypher.Assert(namesReserved.LocaleMask.HasAnyFlag(LocaleMask.Total - 1));
                for (int i = 1; i < (int)LocaleMask.Total; i = i << 1)
                {
                    if (i.HasFlag((int)LocaleMask.None))
                        continue;

                    if (((int)namesReserved.LocaleMask).HasFlag(i))
                        _nameValidators[i].Add(namesReserved.Name);
                }
            }

            foreach (ParagonReputationRecord paragonReputation in ParagonReputationStorage.Values)
                if (FactionStorage.HasRecord(paragonReputation.FactionID))
                    _paragonReputations[paragonReputation.FactionID] = paragonReputation;

            foreach (var group in PhaseXPhaseGroupStorage.Values)
            {
                PhaseRecord phase = PhaseStorage.LookupByKey(group.PhaseId);
                if (phase != null)
                    _phasesByGroup.Add(group.PhaseGroupID, phase.Id);
            }

            foreach (PowerTypeRecord powerType in PowerTypeStorage.Values)
            {
                Cypher.Assert(powerType.PowerTypeEnum < PowerType.Max);

                _powerTypes[powerType.PowerTypeEnum] = powerType;
            }

            foreach (PvpDifficultyRecord record in PvpDifficultyStorage.Values)
            {
                Cypher.Assert(record.BracketId < BattlegroundBracketId.Max,
                    $"PvpDifficulty bracket {record.BracketId} exceeded max allowed value {BattlegroundBracketId.Max}");
            }

            foreach (PvpItemRecord pvpItem in PvpItemStorage.Values)
                _pvpItemBonus[pvpItem.ItemID] = pvpItem.ItemLevelDelta;

            foreach (QuestLineXQuestRecord questLineQuest in QuestLineXQuestStorage.Values)
                _questsByQuestLine.Add(questLineQuest.QuestLineID, questLineQuest);

            foreach (QuestPackageItemRecord questPackageItem in QuestPackageItemStorage.Values)
            {
                if (!_questPackages.ContainsKey(questPackageItem.PackageID))
                    _questPackages[questPackageItem.PackageID] = Tuple.Create(new List<QuestPackageItemRecord>(), new List<QuestPackageItemRecord>());

                if (questPackageItem.DisplayType != QuestPackageFilter.Unmatched)
                    _questPackages[questPackageItem.PackageID].Item1.Add(questPackageItem);
                else
                    _questPackages[questPackageItem.PackageID].Item2.Add(questPackageItem);
            }

            foreach (RewardPackXCurrencyTypeRecord rewardPackXCurrencyType in RewardPackXCurrencyTypeStorage.Values)
                _rewardPackCurrencyTypes.Add(rewardPackXCurrencyType.RewardPackID, rewardPackXCurrencyType);

            foreach (RewardPackXItemRecord rewardPackXItem in RewardPackXItemStorage.Values)
                _rewardPackItems.Add(rewardPackXItem.RewardPackID, rewardPackXItem);

            foreach (SkillLineRecord skill in SkillLineStorage.Values)
            {
                if (skill.ParentSkillLineID != 0)
                    _skillLinesByParentSkillLine.Add(skill.ParentSkillLineID, skill);
            }

            foreach (SkillLineAbilityRecord skillLineAbility in SkillLineAbilityStorage.Values)
                _skillLineAbilitiesBySkillupSkill.Add(skillLineAbility.SkillupSkillLineID != 0 ? skillLineAbility.SkillupSkillLineID : skillLineAbility.SkillLine, skillLineAbility);

            foreach (SkillRaceClassInfoRecord entry in SkillRaceClassInfoStorage.Values)
            {
                if (SkillLineStorage.ContainsKey((int)entry.SkillID))
                    _skillRaceClassInfoBySkill.Add(entry.SkillID, entry);
            }

            foreach (var specSpells in SpecializationSpellsStorage.Values)
                _specializationSpellsBySpec.Add(specSpells.SpecID, specSpells);

            foreach (SpecSetMemberRecord specSetMember in SpecSetMemberStorage.Values)
                _specsBySpecSet.Add((specSetMember.SpecSetID, specSetMember.ChrSpecializationID));

            foreach (SpellClassOptionsRecord classOption in SpellClassOptionsStorage.Values)
                _spellFamilyNames.Add(classOption.SpellClassSet);

            foreach (SpellProcsPerMinuteModRecord ppmMod in SpellProcsPerMinuteModStorage.Values)
                _spellProcsPerMinuteMods.Add(ppmMod.SpellProcsPerMinuteID, ppmMod);

            foreach (SpellVisualMissileRecord spellVisualMissile in SpellVisualMissileStorage.Values)
                _spellVisualMissilesBySet.Add(spellVisualMissile.SpellVisualMissileSetID, spellVisualMissile);

            foreach (ScalingStatValuesRecord scalingStatValue in ScalingStatValuesStorage.Values)
                _scalingStatValuesByLevel.Add(scalingStatValue.Charlevel, scalingStatValue);

            foreach (TalentRecord talentInfo in TalentStorage.Values)
            {
                Cypher.Assert(talentInfo.ClassID < Class.Max);
                Cypher.Assert(talentInfo.TierID < PlayerConst.MaxTalentTiers);
                Cypher.Assert(talentInfo.ColumnIndex < PlayerConst.MaxTalentColumns);
                _talentsByPosition[(int)talentInfo.ClassID][talentInfo.TierID][talentInfo.ColumnIndex].Add(talentInfo);
            }

            //for (var i = 0; i < (int)Class.Max; ++i)
            //{
            //    _talentsByPosition[i] = new List<TalentRecord>[PlayerConst.MaxTalentTiers][];
            //    for (var x = 0; x < PlayerConst.MaxTalentTiers; ++x)
            //    {
            //        _talentsByPosition[i][x] = new List<TalentRecord>[PlayerConst.MaxTalentColumns];

            //        for (var c = 0; c < PlayerConst.MaxTalentColumns; ++c)
            //            _talentsByPosition[i][x][c] = new List<TalentRecord>();
            //    }
            //}

            //// create talent spells set
            //foreach (TalentRecord talentInfo in TalentStorage.Values)
            //{
            //    TalentTabRecord talentTab = TalentTabStorage.LookupByKey(talentInfo.TabID);
            //    for (byte j = 0; j < PlayerConst.MaxTalentRank; ++j)
            //    {
            //        if (talentInfo.SpellRank[j] != 0)
            //        {
            //            TalentSpellPosMap[(uint)talentInfo.SpellRank[j]] = new TalentSpellPos((ushort)talentInfo.Id, j);
            //            if (talentTab != null && talentTab.PetTalentMask != 0)
            //                PetTalentSpells.Add((uint)talentInfo.SpellRank[j]);
            //        }
            //    }
            //}            

            //// prepare fast data access to bit pos of talent ranks for use at inspecting
            //{
            //    // now have all max ranks (and then bit amount used for store talent ranks in inspect)
            //    foreach (TalentTabRecord talentTabInfo in TalentTabStorage.Values)
            //    {
            //        // prevent memory corruption; otherwise cls will become 12 below
            //        if (((Class)talentTabInfo.ClassMask & Class.Playable) == 0)
            //            continue;

            //        // store class talent tab pages
            //        for (int cls = 1; cls < (int)Class.Max; ++cls)
            //        {
            //            if ((talentTabInfo.ClassMask & (1 << (cls - 1))) != 0)
            //            {
            //                if (TalentTabPages[cls] == null)
            //                    TalentTabPages[cls] = new uint[PlayerConst.MaxTalentTabs];
            //                TalentTabPages[cls][talentTabInfo.OrderIndex] = talentTabInfo.Id;
            //            }
            //        }
            //    }
            //}            

            foreach (ToyRecord toy in ToyStorage.Values)
            {
                _toys.Add(toy.ItemID);
            }

            foreach (TransmogSetItemRecord transmogSetItem in TransmogSetItemStorage.Values)
            {
                TransmogSetRecord set = TransmogSetStorage.LookupByKey(transmogSetItem.TransmogSetID);
                if (set == null)
                    continue;

                _transmogSetsByItemModifiedAppearance.Add(transmogSetItem.ItemModifiedAppearanceID, set);
                _transmogSetItemsByTransmogSet.Add(transmogSetItem.TransmogSetID, transmogSetItem);
            }

            for (var i = 0; i < (int)UiMapSystem.Max; ++i)
            {
                _uiMapAssignmentByMap[i] = new MultiMap<int, UiMapAssignmentRecord>();
                _uiMapAssignmentByArea[i] = new MultiMap<int, UiMapAssignmentRecord>();
                _uiMapAssignmentByWmoDoodadPlacement[i] = new MultiMap<int, UiMapAssignmentRecord>();
                _uiMapAssignmentByWmoGroup[i] = new MultiMap<int, UiMapAssignmentRecord>();
            }

            MultiMap<int, UiMapAssignmentRecord> uiMapAssignmentByUiMap = new();
            foreach (UiMapAssignmentRecord uiMapAssignment in UiMapAssignmentStorage.Values)
            {
                uiMapAssignmentByUiMap.Add(uiMapAssignment.UiMapID, uiMapAssignment);
                UiMapRecord uiMap = UiMapStorage.LookupByKey(uiMapAssignment.UiMapID);
                if (uiMap != null)
                {
                    Cypher.Assert(uiMap.System < UiMapSystem.Max, $"UiMapSystem.Max must be at least {uiMap.System + 1}");
                    if (uiMapAssignment.MapID >= 0)
                        _uiMapAssignmentByMap[(int)uiMap.System].Add(uiMapAssignment.MapID, uiMapAssignment);
                    if (uiMapAssignment.AreaID != 0)
                        _uiMapAssignmentByArea[(int)uiMap.System].Add(uiMapAssignment.AreaID, uiMapAssignment);
                    if (uiMapAssignment.WmoDoodadPlacementID != 0)
                        _uiMapAssignmentByWmoDoodadPlacement[(int)uiMap.System].Add(uiMapAssignment.WmoDoodadPlacementID, uiMapAssignment);
                    if (uiMapAssignment.WmoGroupID != 0)
                        _uiMapAssignmentByWmoGroup[(int)uiMap.System].Add(uiMapAssignment.WmoGroupID, uiMapAssignment);
                }
            }

            Dictionary<(int, int), UiMapLinkRecord> uiMapLinks = new();
            foreach (UiMapLinkRecord uiMapLink in UiMapLinkStorage.Values)
                uiMapLinks[(uiMapLink.ParentUiMapID, uiMapLink.ChildUiMapID)] = uiMapLink;

            foreach (UiMapRecord uiMap in UiMapStorage.Values)
            {
                UiMapBounds bounds = new();
                UiMapRecord parentUiMap = UiMapStorage.LookupByKey(uiMap.ParentUiMapID);
                if (parentUiMap != null)
                {
                    if (parentUiMap.HasAnyFlag(UiMapFlag.NoWorldPositions))
                        continue;
                    UiMapAssignmentRecord uiMapAssignment = null;
                    UiMapAssignmentRecord parentUiMapAssignment = null;
                    foreach (var uiMapAssignmentForMap in uiMapAssignmentByUiMap.LookupByKey(uiMap.Id))
                    {
                        if (uiMapAssignmentForMap.MapID >= 0 &&
                            uiMapAssignmentForMap.Region[1].X - uiMapAssignmentForMap.Region[0].X > 0 &&
                            uiMapAssignmentForMap.Region[1].Y - uiMapAssignmentForMap.Region[0].Y > 0)
                        {
                            uiMapAssignment = uiMapAssignmentForMap;
                            break;
                        }
                    }
                    if (uiMapAssignment == null)
                        continue;

                    foreach (var uiMapAssignmentForMap in uiMapAssignmentByUiMap.LookupByKey(uiMap.ParentUiMapID))
                    {
                        if (uiMapAssignmentForMap.MapID == uiMapAssignment.MapID &&
                            uiMapAssignmentForMap.Region[1].X - uiMapAssignmentForMap.Region[0].X > 0 &&
                            uiMapAssignmentForMap.Region[1].Y - uiMapAssignmentForMap.Region[0].Y > 0)
                        {
                            parentUiMapAssignment = uiMapAssignmentForMap;
                            break;
                        }
                    }
                    if (parentUiMapAssignment == null)
                        continue;

                    float parentXsize = parentUiMapAssignment.Region[1].X - parentUiMapAssignment.Region[0].X;
                    float parentYsize = parentUiMapAssignment.Region[1].Y - parentUiMapAssignment.Region[0].Y;
                    float bound0scale = (uiMapAssignment.Region[1].X - parentUiMapAssignment.Region[0].X) / parentXsize;
                    float bound0 = ((1.0f - bound0scale) * parentUiMapAssignment.UiMax.Y) + (bound0scale * parentUiMapAssignment.UiMin.Y);
                    float bound2scale = (uiMapAssignment.Region[0].X - parentUiMapAssignment.Region[0].X) / parentXsize;
                    float bound2 = ((1.0f - bound2scale) * parentUiMapAssignment.UiMax.Y) + (bound2scale * parentUiMapAssignment.UiMin.Y);
                    float bound1scale = (uiMapAssignment.Region[1].Y - parentUiMapAssignment.Region[0].Y) / parentYsize;
                    float bound1 = ((1.0f - bound1scale) * parentUiMapAssignment.UiMax.X) + (bound1scale * parentUiMapAssignment.UiMin.X);
                    float bound3scale = (uiMapAssignment.Region[0].Y - parentUiMapAssignment.Region[0].Y) / parentYsize;
                    float bound3 = ((1.0f - bound3scale) * parentUiMapAssignment.UiMax.X) + (bound3scale * parentUiMapAssignment.UiMin.X);
                    if ((bound3 - bound1) > 0.0f || (bound2 - bound0) > 0.0f)
                    {
                        bounds.Bounds[0] = bound0;
                        bounds.Bounds[1] = bound1;
                        bounds.Bounds[2] = bound2;
                        bounds.Bounds[3] = bound3;
                        bounds.IsUiAssignment = true;
                    }
                }

                UiMapLinkRecord uiMapLink = uiMapLinks.LookupByKey((uiMap.ParentUiMapID, uiMap.Id));
                if (uiMapLink != null)
                {
                    bounds.IsUiAssignment = false;
                    bounds.IsUiLink = true;
                    bounds.Bounds[0] = uiMapLink.UiMin.Y;
                    bounds.Bounds[1] = uiMapLink.UiMin.X;
                    bounds.Bounds[2] = uiMapLink.UiMax.Y;
                    bounds.Bounds[3] = uiMapLink.UiMax.X;
                }

                _uiMapBounds[uiMap.Id] = bounds;
            }

            foreach (UiMapXMapArtRecord uiMapArt in UiMapXMapArtStorage.Values)
                if (uiMapArt.PhaseID != 0)
                    _uiMapPhases.Add(uiMapArt.PhaseID);

            foreach (WMOAreaTableRecord entry in WMOAreaTableStorage.Values)
                _wmoAreaTableLookup[Tuple.Create((short)entry.WmoID, (sbyte)entry.NameSetID, entry.WmoGroupID)] = entry;
        }

        public IDB2Storage GetStorage(uint type)
        {
            return _storage.LookupByKey(type);
        }

        public void LoadHotfixData(BitSet availableDb2Locales)
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.Hotfix.Query("SELECT Id, UniqueId, TableHash, RecordId, Status FROM hotfix_data ORDER BY Id");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 hotfix info entries.");
                return;
            }

            Dictionary<(uint tableHash, int recordId), bool> deletedRecords = new();

            uint count = 0;
            do
            {
                int id = result.Read<int>(0);
                int uniqueId = result.Read<int>(1);
                uint tableHash = result.Read<uint>(2);
                int recordId = result.Read<int>(3);
                HotfixRecord.Status status = (HotfixRecord.Status)result.Read<byte>(4);
                if (status == HotfixRecord.Status.Valid && !_storage.ContainsKey(tableHash))
                {
                    var key = (tableHash, recordId);
                    for (int locale = 0; locale < (int)Locale.Total; ++locale)
                    {
                        if (!availableDb2Locales[locale])
                            continue;

                        if (!_hotfixBlob[locale].ContainsKey(key))
                            availableDb2Locales[locale] = false;
                    }

                    if (!availableDb2Locales.Any())
                    {
                        Log.outError(LogFilter.Sql, $"Table `hotfix_data` references unknown DB2 store by hash 0x{tableHash:X} and has no reference to `hotfix_blob` in hotfix id {id} with RecordID: {recordId}");
                        continue;
                    }
                }

                HotfixRecord hotfixRecord = new();
                hotfixRecord.TableHash = tableHash;
                hotfixRecord.RecordID = recordId;
                hotfixRecord.ID.PushID = id;
                hotfixRecord.ID.UniqueID = uniqueId;
                hotfixRecord.HotfixStatus = status;
                hotfixRecord.AvailableLocalesMask = (uint)availableDb2Locales.ToBlockRange()[0];//Ulgy i know

                if (!_hotfixData.ContainsKey(id))
                    _hotfixData[id] = new();

                HotfixPush push = _hotfixData[id];
                push.Records.Add(hotfixRecord);
                push.AvailableLocalesMask |= hotfixRecord.AvailableLocalesMask;

                deletedRecords[(tableHash, recordId)] = status == HotfixRecord.Status.RecordRemoved;

                ++count;
            } while (result.NextRow());

            foreach (var itr in deletedRecords)
            {
                if (itr.Value)
                {
                    var store = _storage.LookupByKey(itr.Key.tableHash);
                    if (store != null)
                        store.EraseRecord(itr.Key.recordId);
                }
            }

            Log.outInfo(LogFilter.Server, "Loaded {0} hotfix info entries in {1} ms", count, Time.GetMSTimeDiffToNow(oldMSTime));
        }

        public void LoadHotfixBlob(BitSet availableDb2Locales)
        {
            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.Hotfix.Query("SELECT TableHash, RecordId, locale, `Blob` FROM hotfix_blob ORDER BY TableHash");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 hotfix blob entries.");
                return;
            }

            uint hotfixBlobCount = 0;
            do
            {
                uint tableHash = result.Read<uint>(0);
                var storeItr = _storage.LookupByKey(tableHash);
                if (storeItr != null)
                {
                    Log.outError(LogFilter.Sql, $"Table hash 0x{tableHash:X} points to a loaded DB2 store {storeItr.GetName()}, fill related table instead of hotfix_blob");
                    continue;
                }

                int recordId = result.Read<int>(1);
                string localeName = result.Read<string>(2);

                Locale locale = localeName.ToEnum<Locale>();
                if (!SharedConst.IsValidLocale(locale))
                {
                    Log.outError(LogFilter.Sql, $"`hotfix_blob` contains invalid locale: {localeName} at TableHash: 0x{tableHash:X} and RecordID: {recordId}");
                    continue;
                }

                if (!availableDb2Locales[(int)locale])
                    continue;

                _hotfixBlob[(int)locale][(tableHash, recordId)] = result.Read<byte[]>(3);
                hotfixBlobCount++;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {hotfixBlobCount} hotfix blob records in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }

        public void LoadHotfixOptionalData(BitSet availableDb2Locales)
        {
            // Register allowed optional data keys
            _allowedHotfixOptionalData.Add(BroadcastTextStorage.GetTableHash(), (TactKeyStorage.GetTableHash(), ValidateBroadcastTextTactKeyOptionalData));

            uint oldMSTime = Time.GetMSTime();

            SQLResult result = DB.Hotfix.Query("SELECT TableHash, RecordId, locale, `Key`, `Data` FROM hotfix_optional_data ORDER BY TableHash");
            if (result.IsEmpty())
            {
                Log.outInfo(LogFilter.ServerLoading, "Loaded 0 hotfix optional data records.");
                return;
            }

            uint hotfixOptionalDataCount = 0;
            do
            {
                uint tableHash = result.Read<uint>(0);
                var allowedHotfixes = _allowedHotfixOptionalData.LookupByKey(tableHash);
                if (allowedHotfixes.Empty())
                {
                    Log.outError(LogFilter.Sql, $"Table `hotfix_optional_data` references DB2 store by hash 0x{tableHash:X} that is not allowed to have optional data");
                    continue;
                }

                uint recordId = result.Read<uint>(1);
                var db2storage = _storage.LookupByKey(tableHash);
                if (db2storage == null)
                {
                    Log.outError(LogFilter.Sql, $"Table `hotfix_optional_data` references unknown DB2 store by hash 0x{tableHash:X} with RecordID: {recordId}");
                    continue;
                }

                string localeName = result.Read<string>(2);
                Locale locale = localeName.ToEnum<Locale>();

                if (!SharedConst.IsValidLocale(locale))
                {
                    Log.outError(LogFilter.Sql, $"`hotfix_optional_data` contains invalid locale: {localeName} at TableHash: 0x{tableHash:X} and RecordID: {recordId}");
                    continue;
                }

                if (!availableDb2Locales[(int)locale])
                    continue;

                HotfixOptionalData optionalData = new();
                optionalData.Key = result.Read<int>(3);
                var allowedHotfixItr = allowedHotfixes.Find(v =>
                {
                    return v.Item1 == optionalData.Key;
                });
                if (allowedHotfixItr == default)
                {
                    Log.outError(LogFilter.Sql, $"Table `hotfix_optional_data` references non-allowed optional data key 0x{optionalData.Key:X} for DB2 store by hash 0x{tableHash:X} and RecordID: {recordId}");
                    continue;
                }

                optionalData.Data = result.Read<byte[]>(4);
                if (!allowedHotfixItr.Item2(optionalData.Data))
                {
                    Log.outError(LogFilter.Sql, $"Table `hotfix_optional_data` contains invalid data for DB2 store 0x{tableHash:X}, RecordID: {recordId} and Key: 0x{optionalData.Key:X}");
                    continue;
                }

                _hotfixOptionalData[(int)locale].Add((tableHash, (int)recordId), optionalData);
                hotfixOptionalDataCount++;
            } while (result.NextRow());

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {hotfixOptionalDataCount} hotfix optional data records in {Time.GetMSTimeDiffToNow(oldMSTime)} ms");
        }

        public bool ValidateBroadcastTextTactKeyOptionalData(byte[] data)
        {
            return data.Length == 8 + 16;
        }

        public uint GetHotfixCount() { return (uint)_hotfixData.Count; }

        public Dictionary<int, HotfixPush> GetHotfixData() { return _hotfixData; }

        public byte[] GetHotfixBlobData(uint tableHash, int recordId, Locale locale)
        {
            Cypher.Assert(SharedConst.IsValidLocale(locale), $"Locale {locale} is invalid locale");

            return _hotfixBlob[(int)locale].LookupByKey((tableHash, recordId));
        }

        public List<HotfixOptionalData> GetHotfixOptionalData(uint tableHash, int recordId, Locale locale)
        {
            Cypher.Assert(SharedConst.IsValidLocale(locale), $"Locale {locale} is invalid locale");

            return _hotfixOptionalData[(int)locale].LookupByKey((tableHash, recordId));
        }

        public int GetEmptyAnimStateID()
        {
            //return AnimationDataStorage.GetNumRows();

            /*fixed animations for creatures and gameobjects which were broken due to a mismatching AnimationEntry storage sizes between retail and wotlk classic clients
            For some reason the WotLK client expects the retail value for these two affected animation fields and since we have no way to get that value in a clean manner, 
            we have to resort to a hardcoded value for now */

            return 1772; // the Classic client expects the retail storage size so we have to hardcode the value
        }

        public List<int> GetAreasForGroup(int areaGroupId)
        {
            return _areaGroupMembers.LookupByKey(areaGroupId);
        }

        public bool IsInArea(int objectAreaId, int areaId)
        {
            do
            {
                if (objectAreaId == areaId)
                    return true;

                AreaTableRecord objectArea = AreaTableStorage.LookupByKey(objectAreaId);
                if (objectArea == null)
                    break;

                objectAreaId = objectArea.ParentAreaID;
            } while (objectAreaId != 0);

            return false;
        }

        public string GetBroadcastTextValue(BroadcastTextRecord broadcastText, Locale locale = Locale.enUS, Gender gender = Gender.Male, bool forceGender = false)
        {
            if ((gender == Gender.Female || gender == Gender.None) && (forceGender || broadcastText.Text1.HasString(SharedConst.DefaultLocale)))
            {
                if (broadcastText.Text1.HasString(locale))
                    return broadcastText.Text1[locale];

                return broadcastText.Text1[SharedConst.DefaultLocale];
            }

            if (broadcastText.Text.HasString(locale))
                return broadcastText.Text[locale];

            return broadcastText.Text[SharedConst.DefaultLocale];
        }

        public int GetBroadcastTextDuration(int broadcastTextId, Locale locale = Locale.enUS)
        {
            return _broadcastTextDurations.LookupByKey((broadcastTextId, SharedConst.WowLocaleToCascLocaleBit[(int)locale]));
        }

        public ChrClassUIDisplayRecord GetUiDisplayForClass(Class unitClass)
        {
            Cypher.Assert(unitClass < Class.Max);
            return _uiDisplayByClass[(int)unitClass];
        }

        public string GetClassName(Class class_, Locale locale = Locale.enUS)
        {
            ChrClassesRecord classEntry = ChrClassesStorage.LookupByKey((int)class_);
            if (classEntry == null)
                return "";

            if (classEntry.Name[locale][0] != '\0')
                return classEntry.Name[locale];

            return classEntry.Name[Locale.enUS];
        }

        public int GetPowerIndexByClass(PowerType powerType, Class classId)
        {
            return _powersByClass[(int)classId][(int)powerType];
        }

        public List<ChrCustomizationChoiceRecord> GetCustomiztionChoices(int chrCustomizationOptionId)
        {
            return _chrCustomizationChoicesByOption.LookupByKey(chrCustomizationOptionId);
        }

        public List<ChrCustomizationOptionRecord> GetCustomiztionOptions(Race race, Gender gender)
        {
            return _chrCustomizationOptionsByRaceAndGender.LookupByKey((race, gender));
        }

        public MultiMap<int, int> GetRequiredCustomizationChoices(int chrCustomizationReqId)
        {
            return _chrCustomizationRequiredChoices.LookupByKey(chrCustomizationReqId);
        }

        public ChrModelRecord GetChrModel(Race race, Gender gender)
        {
            return _chrModelsByRaceAndGender.LookupByKey((race, gender));
        }

        public string GetChrRaceName(Race race, Locale locale = Locale.enUS)
        {
            ChrRacesRecord raceEntry = ChrRacesStorage.LookupByKey((int)race);
            if (raceEntry == null)
                return "";

            if (raceEntry.Name[locale][0] != '\0')
                return raceEntry.Name[locale];

            return raceEntry.Name[Locale.enUS];
        }

        public ChrSpecializationRecord GetChrSpecializationByIndex(Class class_, int index)
        {
            var classSet = _chrSpecializationsByIndex.LookupByIndex((int)class_);
            if (classSet != null)
            {
                var specialization = classSet.LookupByIndex(index);
                if (specialization != null)
                    return specialization;
            }

            return new ChrSpecializationRecord();
        }

        public ChrSpecializationRecord GetDefaultChrSpecializationForClass(Class class_)
        {
            return GetChrSpecializationByIndex(class_, PlayerConst.InitialSpecializationIndex);
        }

        public int GetRedirectedContentTuningId(int contentTuningId, uint redirectFlag)
        {
            foreach (var conditionalContentTuning in _conditionalContentTuning.LookupByKey(contentTuningId))
                if ((conditionalContentTuning.RedirectFlag & redirectFlag) != 0)
                    return conditionalContentTuning.RedirectContentTuningID;

            return contentTuningId;
        }

        public ContentTuningLevels? GetContentTuningData(int contentTuningId, uint redirectFlag, bool forItem = false)
        {
            ContentTuningRecord contentTuning = ContentTuningStorage.LookupByKey(GetRedirectedContentTuningId(contentTuningId, redirectFlag));
            if (contentTuning == null)
                return null;

            if (forItem && contentTuning.HasFlag(ContentTuningFlag.DisabledForItem))
                return null;

            //static int getLevelAdjustment(ContentTuningCalcType Type) => Type switch
            //{
            //    ContentTuningCalcType.PlusOne => 1,
            //    ContentTuningCalcType.PlusMaxLevelForExpansion => (int)Global.ObjectMgr.GetMaxLevelForExpansion((Expansion)WorldConfig.GetUIntValue(WorldCfg.Expansion)),
            //    _ => 0
            //};

            ContentTuningLevels levels = new();
            levels.MinLevel = (short)Math.Clamp(contentTuning.MinLevel, SharedConst.MinLevel, SharedConst.MaxLevel);
            levels.MaxLevel = (short)Math.Clamp(contentTuning.MaxLevel, SharedConst.MinLevel, SharedConst.MaxLevel);

            levels.MinLevelWithDelta = levels.MinLevel;
            levels.MaxLevelWithDelta = levels.MaxLevel;

            levels.TargetLevelMin = levels.MinLevel;
            levels.TargetLevelMax = levels.MaxLevel;

            return levels;
        }

        public bool HasContentTuningLabel(int contentTuningId, int label)
        {
            return _contentTuningLabels.Contains((contentTuningId, label));
        }

        public string GetCreatureFamilyPetName(CreatureFamily petfamily, Locale locale)
        {
            if (petfamily == CreatureFamily.None)
                return null;

            CreatureFamilyRecord petFamily = CreatureFamilyStorage.LookupByKey((int)petfamily);
            if (petFamily == null)
                return "";

            return petFamily.Name[locale][0] != '\0' ? petFamily.Name[locale] : "";
        }

        public CurrencyContainerRecord GetCurrencyContainerForCurrencyQuantity(int currencyId, int quantity)
        {
            foreach (var record in _currencyContainers.LookupByKey(currencyId))
                if (quantity >= record.MinAmount && (record.MaxAmount == 0 || quantity <= record.MaxAmount))
                    return record;

            return null;
        }

        public (float first, float last) GetCurveXAxisRange(int curveId)
        {
            var points = _curvePoints.LookupByKey(curveId);
            if (!points.Empty())
                return ((points.First().X, points.Last().X));

            return (0.0f, 0.0f);
        }

        static CurveInterpolationMode DetermineCurveType(CurveRecord curve, List<Vector2> points)
        {
            switch (curve.Type)
            {
                case 1:
                    return points.Count < 4 ? CurveInterpolationMode.Cosine : CurveInterpolationMode.CatmullRom;
                case 2:
                {
                    switch (points.Count)
                    {
                        case 1:
                            return CurveInterpolationMode.Constant;
                        case 2:
                            return CurveInterpolationMode.Linear;
                        case 3:
                            return CurveInterpolationMode.Bezier3;
                        case 4:
                            return CurveInterpolationMode.Bezier4;
                        default:
                            break;
                    }
                    return CurveInterpolationMode.Bezier;
                }
                case 3:
                    return CurveInterpolationMode.Cosine;
                default:
                    break;
            }

            return points.Count != 1 ? CurveInterpolationMode.Linear : CurveInterpolationMode.Constant;
        }

        public float GetCurveValueAt(int curveId, float x)
        {
            var curve = CurveStorage.LookupByKey(curveId);
            var points = _curvePoints.LookupByKey(curveId);
            if (points.Empty())
                return 0.0f;

            return GetCurveValueAt(DetermineCurveType(curve, points), points, x);
        }

        public float GetCurveValueAt(CurveInterpolationMode mode, IList<Vector2> points, float x)
        {
            switch (mode)
            {
                case CurveInterpolationMode.Linear:
                {
                    int pointIndex = 0;
                    while (pointIndex < points.Count && points[pointIndex].X <= x)
                        ++pointIndex;
                    if (pointIndex == 0)
                        return points[0].Y;
                    if (pointIndex >= points.Count)
                        return points[points.Count - 1].Y;
                    float xDiff = points[pointIndex].X - points[pointIndex - 1].X;
                    if (xDiff == 0.0)
                        return points[pointIndex].Y;
                    return (((x - points[pointIndex - 1].X) / xDiff) * (points[pointIndex].Y - points[pointIndex - 1].Y)) + points[pointIndex - 1].Y;
                }
                case CurveInterpolationMode.Cosine:
                {
                    int pointIndex = 0;
                    while (pointIndex < points.Count && points[pointIndex].X <= x)
                        ++pointIndex;
                    if (pointIndex == 0)
                        return points[0].Y;
                    if (pointIndex >= points.Count)
                        return points[points.Count - 1].Y;
                    float xDiff = points[pointIndex].X - points[pointIndex - 1].X;
                    if (xDiff == 0.0)
                        return points[pointIndex].Y;
                    return (float)((points[pointIndex].Y - points[pointIndex - 1].Y) * (1.0f - Math.Cos((x - points[pointIndex - 1].X) / xDiff * Math.PI)) * 0.5f) + points[pointIndex - 1].Y;
                }
                case CurveInterpolationMode.CatmullRom:
                {
                    int pointIndex = 1;
                    while (pointIndex < points.Count && points[pointIndex].X <= x)
                        ++pointIndex;
                    if (pointIndex == 1)
                        return points[1].Y;
                    if (pointIndex >= points.Count - 1)
                        return points[^2].Y;
                    float xDiff = points[pointIndex].X - points[pointIndex - 1].X;
                    if (xDiff == 0.0)
                        return points[pointIndex].Y;

                    float mu = (x - points[pointIndex - 1].X) / xDiff;
                    float a0 = -0.5f * points[pointIndex - 2].Y + 1.5f * points[pointIndex - 1].Y - 1.5f * points[pointIndex].Y + 0.5f * points[pointIndex + 1].Y;
                    float a1 = points[pointIndex - 2].Y - 2.5f * points[pointIndex - 1].Y + 2.0f * points[pointIndex].Y - 0.5f * points[pointIndex + 1].Y;
                    float a2 = -0.5f * points[pointIndex - 2].Y + 0.5f * points[pointIndex].Y;
                    float a3 = points[pointIndex - 1].Y;

                    return a0 * mu * mu * mu + a1 * mu * mu + a2 * mu + a3;
                }
                case CurveInterpolationMode.Bezier3:
                {
                    float xDiff = points[2].X - points[0].X;
                    if (xDiff == 0.0)
                        return points[1].Y;
                    float mu = (x - points[0].X) / xDiff;
                    return ((1.0f - mu) * (1.0f - mu) * points[0].Y) + (1.0f - mu) * 2.0f * mu * points[1].Y + mu * mu * points[2].Y;
                }
                case CurveInterpolationMode.Bezier4:
                {
                    float xDiff = points[3].X - points[0].X;
                    if (xDiff == 0.0)
                        return points[1].Y;
                    float mu = (x - points[0].X) / xDiff;
                    return (1.0f - mu) * (1.0f - mu) * (1.0f - mu) * points[0].Y
                        + 3.0f * mu * (1.0f - mu) * (1.0f - mu) * points[1].Y
                        + 3.0f * mu * mu * (1.0f - mu) * points[2].Y
                        + mu * mu * mu * points[3].Y;
                }
                case CurveInterpolationMode.Bezier:
                {
                    float xDiff = points[points.Count - 1].X - points[0].X;
                    if (xDiff == 0.0f)
                        return points[points.Count - 1].Y;

                    float[] tmp = new float[points.Count];
                    for (int c = 0; c < points.Count; ++c)
                        tmp[c] = points[c].Y;

                    float mu = (x - points[0].X) / xDiff;
                    int i = points.Count - 1;
                    while (i > 0)
                    {
                        for (int k = 0; k < i; ++k)
                        {
                            float val = tmp[k] + mu * (tmp[k + 1] - tmp[k]);
                            tmp[k] = val;
                        }
                        --i;
                    }
                    return tmp[0];
                }
                case CurveInterpolationMode.Constant:
                    return points[0].Y;
                default:
                    break;
            }

            return 0.0f;
        }

        public EmotesTextSoundRecord GetTextSoundEmoteFor(int emote, Race race, Gender gender, Class class_)
        {
            var emoteTextSound = _emoteTextSounds.LookupByKey((emote, race, gender, class_));
            if (emoteTextSound != null)
                return emoteTextSound;

            emoteTextSound = _emoteTextSounds.LookupByKey((emote, race, gender, Class.None));
            if (emoteTextSound != null)
                return emoteTextSound;

            return null;
        }

        public float EvaluateExpectedStat(ExpectedStatType stat, int level, Expansion expansion, int contentTuningId, Class unitClass)
        {
            var expectedStatRecord = _expectedStatsByLevel.LookupByKey((level, expansion));
            if (expectedStatRecord == null)
                expectedStatRecord = _expectedStatsByLevel.LookupByKey((level, Expansion.Unk));
            if (expectedStatRecord == null)
                return 1.0f;

            ExpectedStatModRecord classMod = null;
            switch (unitClass)
            {
                case Class.Warrior:
                    classMod = ExpectedStatModStorage.LookupByKey(4);
                    break;
                case Class.Paladin:
                    classMod = ExpectedStatModStorage.LookupByKey(2);
                    break;
                case Class.Rogue:
                    classMod = ExpectedStatModStorage.LookupByKey(3);
                    break;
                case Class.Mage:
                    classMod = ExpectedStatModStorage.LookupByKey(1);
                    break;
                default:
                    break;
            }

            float value = 0.0f;
            switch (stat)
            {
                case ExpectedStatType.CreatureHealth:
                    value = expectedStatRecord.CreatureHealth;
                    if (classMod != null)
                        value *= classMod.CreatureHealthMod;
                    break;
                case ExpectedStatType.PlayerHealth:
                    value = expectedStatRecord.PlayerHealth;
                    if (classMod != null)
                        value *= classMod.PlayerHealthMod;
                    break;
                case ExpectedStatType.CreatureAutoAttackDps:
                    value = expectedStatRecord.CreatureAutoAttackDps;
                    if (classMod != null)
                        value *= classMod.CreatureAutoAttackDPSMod;
                    break;
                case ExpectedStatType.CreatureArmor:
                    value = expectedStatRecord.CreatureArmor;
                    if (classMod != null)
                        value *= classMod.CreatureArmorMod;
                    break;
                case ExpectedStatType.PlayerMana:
                    value = expectedStatRecord.PlayerMana;
                    if (classMod != null)
                        value *= classMod.PlayerManaMod;
                    break;
                case ExpectedStatType.PlayerPrimaryStat:
                    value = expectedStatRecord.PlayerPrimaryStat;
                    if (classMod != null)
                        value *= classMod.PlayerPrimaryStatMod;
                    break;
                case ExpectedStatType.PlayerSecondaryStat:
                    value = expectedStatRecord.PlayerSecondaryStat;
                    if (classMod != null)
                        value *= classMod.PlayerSecondaryStatMod;
                    break;
                case ExpectedStatType.ArmorConstant:
                    value = expectedStatRecord.ArmorConstant;
                    if (classMod != null)
                        value *= classMod.ArmorConstantMod;
                    break;
                case ExpectedStatType.None:
                    break;
                case ExpectedStatType.CreatureSpellDamage:
                    value = expectedStatRecord.CreatureSpellDamage;
                    if (classMod != null)
                        value *= classMod.CreatureSpellDamageMod;
                    break;
                default:
                    break;
            }
            return value;

        }

        public List<int> GetFactionTeamList(int faction)
        {
            return _factionTeams.LookupByKey(faction);
        }

        public List<FriendshipRepReactionRecord> GetFriendshipRepReactions(int friendshipRepID)
        {
            return _friendshipRepReactions.LookupByKey(friendshipRepID);
        }

        public List<int> GetGlyphBindableSpells(int glyphPropertiesId)
        {
            return _glyphBindableSpells.LookupByKey(glyphPropertiesId);
        }

        public List<ChrSpecialization> GetGlyphRequiredSpecs(int glyphPropertiesId)
        {
            return _glyphRequiredSpecs.LookupByKey(glyphPropertiesId);
        }

        public HeirloomRecord GetHeirloomByItemId(int itemId)
        {
            return _heirlooms.LookupByKey(itemId);
        }

        public ItemChildEquipmentRecord GetItemChildEquipment(int itemId)
        {
            return _itemChildEquipment.LookupByKey(itemId);
        }

        public ItemClassRecord GetItemClassByOldEnum(ItemClass itemClass)
        {
            return _itemClassByOldEnum[(int)itemClass];
        }

        public List<ItemLimitCategoryConditionRecord> GetItemLimitCategoryConditions(int categoryId)
        {
            return _itemCategoryConditions.LookupByKey(categoryId);
        }

        public int GetItemDisplayId(int itemId, int appearanceModId)
        {
            ItemModifiedAppearanceRecord modifiedAppearance = GetItemModifiedAppearance(itemId, appearanceModId);
            if (modifiedAppearance != null)
            {
                ItemAppearanceRecord itemAppearance = ItemAppearanceStorage.LookupByKey(modifiedAppearance.ItemAppearanceID);
                if (itemAppearance != null)
                    return itemAppearance.ItemDisplayInfoID;
            }

            return 0;
        }

        public ItemModifiedAppearanceRecord GetItemModifiedAppearance(int itemId, int appearanceModId)
        {
            var itemModifiedAppearance = _itemModifiedAppearancesByItem.LookupByKey(itemId | (appearanceModId << 24));
            if (itemModifiedAppearance != null)
                return itemModifiedAppearance;

            // Fall back to unmodified appearance
            if (appearanceModId != 0)
            {
                itemModifiedAppearance = _itemModifiedAppearancesByItem.LookupByKey(itemId);
                if (itemModifiedAppearance != null)
                    return itemModifiedAppearance;
            }

            return null;
        }

        public ItemModifiedAppearanceRecord GetDefaultItemModifiedAppearance(int itemId)
        {
            return _itemModifiedAppearancesByItem.LookupByKey(itemId);
        }

        public List<ItemSetSpellRecord> GetItemSetSpells(int itemSetId)
        {
            return _itemSetSpells.LookupByKey(itemSetId);
        }

        public List<ItemSpecOverrideRecord> GetItemSpecOverrides(int itemId)
        {
            return _itemSpecOverrides.LookupByKey(itemId);
        }

        public JournalTierRecord GetJournalTier(int index)
        {
            if (index < _journalTiersByIndex.Count)
                return _journalTiersByIndex[index];
            return null;
        }

        public LFGDungeonsRecord GetLfgDungeon(int mapId, Difficulty difficulty)
        {
            foreach (LFGDungeonsRecord dungeon in LFGDungeonsStorage.Values)
                if (dungeon.MapID == mapId && dungeon.DifficultyID == difficulty)
                    return dungeon;

            return null;
        }

        public int GetDefaultMapLight(uint mapId)
        {
            foreach (var light in LightStorage.Values.Reverse())
            {
                if (light.ContinentID == mapId && light.GameCoords.X == 0.0f && light.GameCoords.Y == 0.0f && light.GameCoords.Z == 0.0f)
                    return light.Id;
            }

            return 0;
        }

        public uint[] GetTalentTabPages(uint cls)
        {
            return TalentTabPages[cls];
        }

        public uint GetLiquidFlags(int liquidType)
        {
            LiquidTypeRecord liq = LiquidTypeStorage.LookupByKey(liquidType);
            if (liq != null)
                return 1u << liq.SoundBank;

            return 0;
        }

        public MapDifficultyRecord GetDefaultMapDifficulty(int mapId)
        {
            Difficulty NotUsed = Difficulty.None;
            return GetDefaultMapDifficulty(mapId, ref NotUsed);
        }
        public MapDifficultyRecord GetDefaultMapDifficulty(int mapId, ref Difficulty difficulty)
        {
            var dicMapDiff = _mapDifficulties.LookupByKey(mapId);
            if (dicMapDiff == null)
                return null;

            if (dicMapDiff.Empty())
                return null;

            foreach (var pair in dicMapDiff)
            {
                DifficultyRecord difficultyEntry = DifficultyStorage.LookupByKey((int)pair.Key);
                if (difficultyEntry == null)
                    continue;

                if (difficultyEntry.HasAnyFlag(DifficultyFlags.Default))
                {
                    difficulty = pair.Key;
                    return pair.Value;
                }
            }

            difficulty = dicMapDiff.First().Key;

            return dicMapDiff.First().Value;
        }

        public MapDifficultyRecord GetMapDifficultyData(int mapId, Difficulty difficulty)
        {
            var dictionaryMapDiff = _mapDifficulties.LookupByKey(mapId);
            if (dictionaryMapDiff == null)
                return null;

            var mapDifficulty = dictionaryMapDiff.LookupByKey(difficulty);
            if (mapDifficulty == null)
                return null;

            return mapDifficulty;
        }

        public MapDifficultyRecord GetDownscaledMapDifficultyData(int mapId, ref Difficulty difficulty)
        {
            DifficultyRecord diffEntry = DifficultyStorage.LookupByKey((int)difficulty);
            if (diffEntry == null)
                return GetDefaultMapDifficulty(mapId, ref difficulty);

            Difficulty tmpDiff = difficulty;
            MapDifficultyRecord mapDiff = GetMapDifficultyData(mapId, tmpDiff);
            while (mapDiff == null)
            {
                tmpDiff = diffEntry.FallbackDifficultyID;
                diffEntry = DifficultyStorage.LookupByKey((int)tmpDiff);
                if (diffEntry == null)
                    return GetDefaultMapDifficulty(mapId, ref difficulty);

                // pull new data
                mapDiff = GetMapDifficultyData(mapId, tmpDiff); // we are 10 normal or 25 normal
            }

            difficulty = tmpDiff;
            return mapDiff;
        }

        public List<(int, PlayerConditionRecord)> GetMapDifficultyConditions(Difficulty mapDifficultyId)
        {
            return _mapDifficultyConditions.LookupByKey(mapDifficultyId);
        }

        public MountRecord GetMount(int spellId)
        {
            return _mountsBySpellId.LookupByKey(spellId);
        }

        public MountRecord GetMountById(int id)
        {
            return MountStorage.LookupByKey(id);
        }

        public List<MountTypeXCapabilityRecord> GetMountCapabilities(int mountType)
        {
            return _mountCapabilitiesByType.LookupByKey(mountType);
        }

        public List<MountXDisplayRecord> GetMountDisplays(int mountId)
        {
            return _mountDisplays.LookupByKey(mountId);
        }

        public string GetNameGenEntry(Race race, Gender gender)
        {
            Cypher.Assert(gender < Gender.None);
            var listNameGen = _nameGenData.LookupByKey(race);
            if (listNameGen == null)
                return "";

            if (listNameGen[(int)gender].Empty())
                return "";

            return listNameGen[(int)gender].SelectRandom().Name;
        }

        public ResponseCodes ValidateName(string name, Locale locale)
        {
            foreach (var testName in _nameValidators[(int)locale])
                if (testName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return ResponseCodes.CharNameProfane;

            // regexes at TOTAL_LOCALES are loaded from NamesReserved which is not locale specific
            foreach (var testName in _nameValidators[(int)Locale.Total])
                if (testName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return ResponseCodes.CharNameReserved;

            return ResponseCodes.CharNameSuccess;
        }

        public int GetNumTalentsAtLevel(int level, Class playerClass)
        {
            NumTalentsAtLevelRecord numTalentsAtLevel = NumTalentsAtLevelStorage.LookupByKey(level);
            if (numTalentsAtLevel == null)
                numTalentsAtLevel = NumTalentsAtLevelStorage.LastOrDefault().Value;
            if (numTalentsAtLevel != null)
            {
                return playerClass switch
                {
                    Class.Deathknight => numTalentsAtLevel.NumTalentsDeathKnight,
                    Class.DemonHunter => numTalentsAtLevel.NumTalentsDemonHunter,
                    _ => numTalentsAtLevel.NumTalents,
                };
            }
            return 0;
        }

        public ParagonReputationRecord GetParagonReputation(int factionId)
        {
            return _paragonReputations.LookupByKey(factionId);
        }

        public PvpDifficultyRecord GetBattlegroundBracketByLevel(int mapid, int level)
        {
            PvpDifficultyRecord maxEntry = null;              // used for level > max listed level case
            foreach (var entry in PvpDifficultyStorage.Values)
            {
                // skip unrelated and too-high brackets
                if (entry.MapID != mapid || entry.MinLevel > level)
                    continue;

                // exactly fit
                if (entry.MaxLevel >= level)
                    return entry;

                // remember for possible out-of-range case (search higher from existed)
                if (maxEntry == null || maxEntry.MaxLevel < entry.MaxLevel)
                    maxEntry = entry;
            }

            return maxEntry;
        }

        public PvpDifficultyRecord GetBattlegroundBracketById(int mapid, BattlegroundBracketId id)
        {
            foreach (var entry in PvpDifficultyStorage.Values)
                if (entry.MapID == mapid && entry.BracketId == id)
                    return entry;

            return null;
        }

        public List<QuestLineXQuestRecord> GetQuestsForQuestLine(int questLineId)
        {
            return _questsByQuestLine.LookupByKey(questLineId);
        }

        public List<QuestPackageItemRecord> GetQuestPackageItems(int questPackageID)
        {
            if (_questPackages.ContainsKey(questPackageID))
                return _questPackages[questPackageID].Item1;

            return null;
        }

        public List<QuestPackageItemRecord> GetQuestPackageItemsFallback(int questPackageID)
        {
            return _questPackages.LookupByKey(questPackageID).Item2;
        }

        public uint GetQuestUniqueBitFlag(int questId)
        {
            QuestV2Record v2 = QuestV2Storage.LookupByKey(questId);
            if (v2 == null)
                return 0;

            return v2.UniqueBitFlag;
        }

        public List<int> GetPhasesForGroup(int group)
        {
            return _phasesByGroup.LookupByKey(group);
        }

        public PowerTypeRecord GetPowerTypeEntry(PowerType power)
        {
            if (!_powerTypes.ContainsKey(power))
                return null;

            return _powerTypes[power];
        }

        public PowerTypeRecord GetPowerTypeByName(string name)
        {
            foreach (PowerTypeRecord powerType in PowerTypeStorage.Values)
            {
                string powerName = powerType.NameGlobalStringTag;
                if (powerName.ToLower() == name)
                    return powerType;

                powerName = powerName.Replace("_", "");
                if (powerName == name)
                    return powerType;
            }

            return null;
        }

        public byte GetPvpItemLevelBonus(int itemId)
        {
            return _pvpItemBonus.LookupByKey(itemId);
        }

        public List<RewardPackXCurrencyTypeRecord> GetRewardPackCurrencyTypesByRewardID(int rewardPackID)
        {
            return _rewardPackCurrencyTypes.LookupByKey(rewardPackID);
        }

        public List<RewardPackXItemRecord> GetRewardPackItemsByRewardID(int rewardPackID)
        {
            return _rewardPackItems.LookupByKey(rewardPackID);
        }

        public ShapeshiftFormModelData GetShapeshiftFormModelData(Race race, Gender gender, ShapeShiftForm form)
        {
            return _chrCustomizationChoicesForShapeshifts.LookupByKey((race, gender, form));
        }

        public List<SkillLineRecord> GetSkillLinesForParentSkill(SkillType parentSkillId)
        {
            return _skillLinesByParentSkillLine.LookupByKey(parentSkillId);
        }

        public List<SkillLineAbilityRecord> GetSkillLineAbilitiesBySkill(SkillType skillId)
        {
            return _skillLineAbilitiesBySkillupSkill.LookupByKey(skillId);
        }

        public SkillRaceClassInfoRecord GetSkillRaceClassInfo(SkillType skill, Race race, Class class_)
        {
            var bounds = _skillRaceClassInfoBySkill.LookupByKey(skill);
            foreach (var skllRaceClassInfo in bounds)
            {
                var raceMask = skllRaceClassInfo.RaceMask;
                if (raceMask != RaceMask.None && !raceMask.HasRace(race))
                    continue;
                if (skllRaceClassInfo.ClassMask != ClassMask.None && !skllRaceClassInfo.ClassMask.HasClass(class_))
                    continue;

                return skllRaceClassInfo;
            }

            return null;
        }

        public List<SkillRaceClassInfoRecord> GetSkillRaceClassInfo(SkillType skill)
        {
            return _skillRaceClassInfoBySkill.LookupByKey(skill);
        }

        public List<SpecializationSpellsRecord> GetSpecializationSpells(ChrSpecialization specId)
        {
            return _specializationSpellsBySpec.LookupByKey(specId);
        }

        public bool IsSpecSetMember(int specSetId, ChrSpecialization specId)
        {
            return _specsBySpecSet.Contains((specSetId, specId));
        }

        public bool IsValidSpellFamiliyName(SpellFamilyNames family)
        {
            return _spellFamilyNames.Contains(family);
        }

        public List<SpellProcsPerMinuteModRecord> GetSpellProcsPerMinuteMods(int spellprocsPerMinuteId)
        {
            return _spellProcsPerMinuteMods.LookupByKey(spellprocsPerMinuteId);
        }

        public List<SpellVisualMissileRecord> GetSpellVisualMissiles(int spellVisualMissileSetId)
        {
            return _spellVisualMissilesBySet.LookupByKey(spellVisualMissileSetId);
        }

        public List<TalentRecord> GetTalentsByPosition(Class class_, uint tier, uint column)
        {
            return _talentsByPosition[(int)class_][tier][column];
        }

        public bool IsTotemCategoryCompatibleWith(int itemTotemCategoryId, int requiredTotemCategoryId, bool requireAllTotems = true)
        {
            if (requiredTotemCategoryId == 0)
                return true;
            if (itemTotemCategoryId == 0)
                return false;

            TotemCategoryRecord itemEntry = TotemCategoryStorage.LookupByKey(itemTotemCategoryId);
            if (itemEntry == null)
                return false;
            TotemCategoryRecord reqEntry = TotemCategoryStorage.LookupByKey(requiredTotemCategoryId);
            if (reqEntry == null)
                return false;

            if (itemEntry.TotemCategoryType != reqEntry.TotemCategoryType)
                return false;

            int sharedMask = itemEntry.TotemCategoryMask & reqEntry.TotemCategoryMask;
            return requireAllTotems ? sharedMask == reqEntry.TotemCategoryMask : sharedMask != 0;
        }

        public bool IsToyItem(int toy)
        {
            return _toys.Contains(toy);
        }

        public List<TransmogSetRecord> GetTransmogSetsForItemModifiedAppearance(int itemModifiedAppearanceId)
        {
            return _transmogSetsByItemModifiedAppearance.LookupByKey(itemModifiedAppearanceId);
        }

        public List<TransmogSetItemRecord> GetTransmogSetItems(int transmogSetId)
        {
            return _transmogSetItemsByTransmogSet.LookupByKey(transmogSetId);
        }

        static bool CheckUiMapAssignmentStatus(float x, float y, float z, int? mapId, int areaId, int wmoDoodadPlacementId, int wmoGroupId, UiMapAssignmentRecord uiMapAssignment, out UiMapAssignmentStatus status)
        {
            status = new UiMapAssignmentStatus();
            status.UiMapAssignment = uiMapAssignment;
            // x,y not in region
            if (x < uiMapAssignment.Region[0].X || x > uiMapAssignment.Region[1].X || y < uiMapAssignment.Region[0].Y || y > uiMapAssignment.Region[1].Y)
            {
                float xDiff, yDiff;
                if (x >= uiMapAssignment.Region[0].X)
                {
                    xDiff = 0.0f;
                    if (x > uiMapAssignment.Region[1].X)
                        xDiff = x - uiMapAssignment.Region[0].X;
                }
                else
                    xDiff = uiMapAssignment.Region[0].X - x;

                if (y >= uiMapAssignment.Region[0].Y)
                {
                    yDiff = 0.0f;
                    if (y > uiMapAssignment.Region[1].Y)
                        yDiff = y - uiMapAssignment.Region[0].Y;
                }
                else
                    yDiff = uiMapAssignment.Region[0].Y - y;

                status.Outside.DistanceToRegionEdgeSquared = xDiff * xDiff + yDiff * yDiff;
            }
            else
            {
                status.Inside.DistanceToRegionCenterSquared =
                    (x - (uiMapAssignment.Region[0].X + uiMapAssignment.Region[1].X) * 0.5f) * (x - (uiMapAssignment.Region[0].X + uiMapAssignment.Region[1].X) * 0.5f)
                    + (y - (uiMapAssignment.Region[0].Y + uiMapAssignment.Region[1].Y) * 0.5f) * (y - (uiMapAssignment.Region[0].Y + uiMapAssignment.Region[1].Y) * 0.5f);
                status.Outside.DistanceToRegionEdgeSquared = 0.0f;
            }

            // z not in region
            if (z < uiMapAssignment.Region[0].Z || z > uiMapAssignment.Region[1].Z)
            {
                if (z < uiMapAssignment.Region[1].Z)
                {
                    if (z < uiMapAssignment.Region[0].Z)
                        status.Outside.DistanceToRegionBottom = Math.Min(uiMapAssignment.Region[0].Z - z, 10000.0f);
                }
                else
                    status.Outside.DistanceToRegionTop = Math.Min(z - uiMapAssignment.Region[1].Z, 10000.0f);
            }
            else
            {
                status.Outside.DistanceToRegionTop = 0.0f;
                status.Outside.DistanceToRegionBottom = 0.0f;
                status.Inside.DistanceToRegionBottom = Math.Min(uiMapAssignment.Region[0].Z - z, 10000.0f);
            }

            if (areaId != 0 && uiMapAssignment.AreaID != 0)
            {
                sbyte areaPriority = 0;
                if (areaId != 0)
                {
                    while (areaId != uiMapAssignment.AreaID)
                    {
                        AreaTableRecord areaEntry = AreaTableStorage.LookupByKey(areaId);
                        if (areaEntry != null)
                        {
                            areaId = areaEntry.ParentAreaID;
                            ++areaPriority;
                        }
                        else
                            return false;
                    }
                }
                else
                    return false;

                status.AreaPriority = areaPriority;
            }

            if (mapId.HasValue && mapId >= 0 && uiMapAssignment.MapID >= 0)
            {
                if (mapId != uiMapAssignment.MapID)
                {
                    MapRecord mapEntry = MapStorage.LookupByKey(mapId.Value);
                    if (mapEntry != null)
                    {
                        if (mapEntry.ParentMapID == uiMapAssignment.MapID)
                            status.MapPriority = 1;
                        else if (mapEntry.CosmeticParentMapID == uiMapAssignment.MapID)
                            status.MapPriority = 2;
                        else
                            return false;
                    }
                    else
                        return false;
                }
                else
                    status.MapPriority = 0;
            }

            if (wmoGroupId != 0 || wmoDoodadPlacementId != 0)
            {
                if (uiMapAssignment.WmoGroupID != 0 || uiMapAssignment.WmoDoodadPlacementID != 0)
                {
                    bool hasDoodadPlacement = false;
                    if (wmoDoodadPlacementId != 0 && uiMapAssignment.WmoDoodadPlacementID != 0)
                    {
                        if (wmoDoodadPlacementId != uiMapAssignment.WmoDoodadPlacementID)
                            return false;

                        hasDoodadPlacement = true;
                    }

                    if (wmoGroupId != 0 && uiMapAssignment.WmoGroupID != 0)
                    {
                        if (wmoGroupId != uiMapAssignment.WmoGroupID)
                            return false;

                        if (hasDoodadPlacement)
                            status.WmoPriority = 0;
                        else
                            status.WmoPriority = 2;
                    }
                    else if (hasDoodadPlacement)
                        status.WmoPriority = 1;
                }
            }

            return true;
        }

        UiMapAssignmentRecord FindNearestMapAssignment(float x, float y, float z, int? mapId, int areaId, int wmoDoodadPlacementId, int wmoGroupId, UiMapSystem system)
        {
            UiMapAssignmentStatus nearestMapAssignment = new();
            var iterateUiMapAssignments = new Action<MultiMap<int, UiMapAssignmentRecord>, int>((assignments, id) =>
            {
                foreach (var assignment in assignments.LookupByKey(id))
                {
                    UiMapAssignmentStatus status;
                    if (CheckUiMapAssignmentStatus(x, y, z, mapId, areaId, wmoDoodadPlacementId, wmoGroupId, assignment, out status))
                        if (status < nearestMapAssignment)
                            nearestMapAssignment = status;
                }
            });

            iterateUiMapAssignments(_uiMapAssignmentByWmoGroup[(int)system], wmoGroupId);
            iterateUiMapAssignments(_uiMapAssignmentByWmoDoodadPlacement[(int)system], wmoDoodadPlacementId);

            AreaTableRecord areaEntry = AreaTableStorage.LookupByKey(areaId);
            while (areaEntry != null)
            {
                iterateUiMapAssignments(_uiMapAssignmentByArea[(int)system], areaEntry.Id);
                areaEntry = AreaTableStorage.LookupByKey(areaEntry.ParentAreaID);
            }

            if (mapId.HasValue && mapId > 0)
            {
                MapRecord mapEntry = MapStorage.LookupByKey(mapId.Value);
                if (mapEntry != null)
                {
                    iterateUiMapAssignments(_uiMapAssignmentByMap[(int)system], mapEntry.Id);
                    if (mapEntry.ParentMapID >= 0)
                        iterateUiMapAssignments(_uiMapAssignmentByMap[(int)system], mapEntry.ParentMapID);
                    if (mapEntry.CosmeticParentMapID >= 0)
                        iterateUiMapAssignments(_uiMapAssignmentByMap[(int)system], mapEntry.CosmeticParentMapID);
                }
            }

            return nearestMapAssignment.UiMapAssignment;
        }

        Vector2 CalculateGlobalUiMapPosition(int uiMapID, Vector2 uiPosition)
        {
            UiMapRecord uiMap = UiMapStorage.LookupByKey(uiMapID);
            while (uiMap != null)
            {
                if (uiMap.Type <= UiMapType.Continent)
                    break;

                UiMapBounds bounds = _uiMapBounds.LookupByKey(uiMap.Id);
                if (bounds == null || !bounds.IsUiAssignment)
                    break;

                uiPosition.X = ((1.0f - uiPosition.X) * bounds.Bounds[1]) + (bounds.Bounds[3] * uiPosition.X);
                uiPosition.Y = ((1.0f - uiPosition.Y) * bounds.Bounds[0]) + (bounds.Bounds[2] * uiPosition.Y);

                uiMap = UiMapStorage.LookupByKey(uiMap.ParentUiMapID);
            }

            return uiPosition;
        }

        public bool GetUiMapPosition(float x, float y, float z, int? mapId, int areaId, int wmoDoodadPlacementId, int wmoGroupId, UiMapSystem system, bool local, out Vector2 newPos)
        {
            return GetUiMapPosition(x, y, z, mapId, areaId, wmoDoodadPlacementId, wmoGroupId, system, local, out _, out newPos);
        }

        public bool GetUiMapPosition(float x, float y, float z, int? mapId, int areaId, int wmoDoodadPlacementId, int wmoGroupId, UiMapSystem system, bool local, out int uiMapId)
        {
            return GetUiMapPosition(x, y, z, mapId, areaId, wmoDoodadPlacementId, wmoGroupId, system, local, out uiMapId, out _);
        }

        public bool GetUiMapPosition(float x, float y, float z, int? mapId, int areaId, int wmoDoodadPlacementId, int wmoGroupId, UiMapSystem system, bool local, out int uiMapId, out Vector2 newPos)
        {
            uiMapId = -1;
            newPos = new Vector2();

            UiMapAssignmentRecord uiMapAssignment = FindNearestMapAssignment(x, y, z, mapId, areaId, wmoDoodadPlacementId, wmoGroupId, system);
            if (uiMapAssignment == null)
                return false;

            uiMapId = uiMapAssignment.UiMapID;

            Vector2 relativePosition = new(0.5f, 0.5f);
            Vector2 regionSize = new(uiMapAssignment.Region[1].X - uiMapAssignment.Region[0].X, uiMapAssignment.Region[1].Y - uiMapAssignment.Region[0].Y);
            if (regionSize.X > 0.0f)
                relativePosition.X = (x - uiMapAssignment.Region[0].X) / regionSize.X;
            if (regionSize.Y > 0.0f)
                relativePosition.Y = (y - uiMapAssignment.Region[0].Y) / regionSize.Y;

            // x any y are swapped
            Vector2 uiPosition = new(((1.0f - (1.0f - relativePosition.Y)) * uiMapAssignment.UiMin.X) + ((1.0f - relativePosition.Y) * uiMapAssignment.UiMax.X), ((1.0f - (1.0f - relativePosition.X)) * uiMapAssignment.UiMin.Y) + ((1.0f - relativePosition.X) * uiMapAssignment.UiMax.Y));

            if (!local)
                uiPosition = CalculateGlobalUiMapPosition(uiMapAssignment.UiMapID, uiPosition);

            newPos = uiPosition;
            return true;
        }


        public TalentSpellPos GetTalentSpellPos(int spellId)
        {
            TalentSpellPosMap.TryGetValue(spellId, out TalentSpellPos tsp);
            return tsp;
        }

        public uint GetTalentSpellCost(int spellId)
        {
            if (GetTalentSpellPos(spellId) is TalentSpellPos pos)
                return (uint)pos.Rank + 1;

            return 0;
        }

        public ContentLevels GetContentLevelsForMapAndZone(int mapid, int zoneId)
        {
            if (mapid == 530 || mapid == 571)
            {
                switch (zoneId)
                {
                    case 3430: // Eversong Woods
                    case 3433: // Ghostlands
                    case 3487: // SilvermoonCity
                        return ContentLevels.Content_1_60;
                    case 3524: // Azurmyst Isle
                    case 3557: // The Exodar
                    case 3525: // Bloodmyst Isle
                        return ContentLevels.Content_1_60;
                    default:
                        break;
                }
            }

            // Special Case
            if (mapid == 609) // DeathknightStart Ebon Hold
                return ContentLevels.Content_61_70;

            if (mapid < 2)
                return ContentLevels.Content_1_60;

            MapRecord mapEntry = MapStorage.LookupByKey(mapid);
            if (mapEntry == null)
                return ContentLevels.Content_1_60;

            switch (mapEntry.Expansion)
            {
                default: return ContentLevels.Content_1_60;
                case Expansion.BurningCrusade: return ContentLevels.Content_61_70;
                case Expansion.WrathOfTheLichKing: return ContentLevels.Content_71_80;
            }
        }

        public bool Zone2MapCoordinates(int areaId, ref float x, ref float y)
        {
            AreaTableRecord areaEntry = AreaTableStorage.LookupByKey(areaId);
            if (areaEntry == null)
                return false;

            foreach (var assignment in _uiMapAssignmentByArea[(int)UiMapSystem.World].LookupByKey(areaId))
            {
                if (assignment.MapID >= 0 && assignment.MapID != areaEntry.ContinentID)
                    continue;

                float tmpY = (y - assignment.UiMax.Y) / (assignment.UiMin.Y - assignment.UiMax.Y);
                float tmpX = (x - assignment.UiMax.X) / (assignment.UiMin.X - assignment.UiMax.X);
                x = assignment.Region[0].X + tmpY * (assignment.Region[1].X - assignment.Region[0].X);
                y = assignment.Region[0].Y + tmpX * (assignment.Region[1].Y - assignment.Region[0].Y);

                return true;
            }

            return false;
        }

        public void Map2ZoneCoordinates(int areaId, ref float x, ref float y)
        {
            Vector2 zoneCoords;
            if (!GetUiMapPosition(x, y, 0.0f, null, areaId, 0, 0, UiMapSystem.World, true, out zoneCoords))
                return;

            x = zoneCoords.Y * 100.0f;
            y = zoneCoords.X * 100.0f;
        }

        public bool IsUiMapPhase(int phaseId)
        {
            return _uiMapPhases.Contains(phaseId);
        }

        public WMOAreaTableRecord GetWMOAreaTable(int rootId, int adtId, int groupId)
        {
            var wmoAreaTable = _wmoAreaTableLookup.LookupByKey(Tuple.Create((short)rootId, (sbyte)adtId, groupId));
            if (wmoAreaTable != null)
                return wmoAreaTable;

            return null;
        }

        public List<ItemEffectRecord> GetItemEffectsForItemId(int itemId)
        {
            return _itemEffectsByItemId[itemId];
        }

        public ScalingStatValuesRecord GetScalingStatValuesForLevel(int level)
        {
            return _scalingStatValuesByLevel[level];
        }

        public bool HasItemCurrencyCost(int itemId) { return _itemsWithCurrencyCost.Contains(itemId); }

        public Dictionary<int, Dictionary<Difficulty, MapDifficultyRecord>> GetMapDifficulties() { return _mapDifficulties; }

        public void AddDB2<TKey, TRecord>(uint tableHash, DB6Storage<TKey, TRecord> store) where TRecord : new() where TKey : struct
        {
            _storage[tableHash] = store;
        }

        delegate bool AllowedHotfixOptionalData(byte[] data);

        Dictionary<uint, IDB2Storage> _storage = new();
        Dictionary<int, HotfixPush> _hotfixData = new();
        Dictionary<(uint tableHash, int recordId), byte[]>[] _hotfixBlob = new Dictionary<(uint tableHash, int recordId), byte[]>[(int)Locale.Total];
        MultiMap<uint, (uint, AllowedHotfixOptionalData)> _allowedHotfixOptionalData = new();
        MultiMap<(uint tableHash, int recordId), HotfixOptionalData>[] _hotfixOptionalData = new MultiMap<(uint tableHash, int recordId), HotfixOptionalData>[(int)Locale.Total];

        MultiMap<int, int> _areaGroupMembers = new();
        Dictionary<(int broadcastTextId, CascLocaleBit cascLocaleBit), int> _broadcastTextDurations = new();
        ChrClassUIDisplayRecord[] _uiDisplayByClass = new ChrClassUIDisplayRecord[(int)Class.Max];
        int[][] _powersByClass = new int[(int)Class.Max][];
        MultiMap<int, ChrCustomizationChoiceRecord> _chrCustomizationChoicesByOption = new();
        Dictionary<(Race, Gender), ChrModelRecord> _chrModelsByRaceAndGender = new();
        Dictionary<(Race, Gender, ShapeShiftForm), ShapeshiftFormModelData> _chrCustomizationChoicesForShapeshifts = new();
        MultiMap<(Race, Gender), ChrCustomizationOptionRecord> _chrCustomizationOptionsByRaceAndGender = new();
        Dictionary<int, MultiMap<int, int>> _chrCustomizationRequiredChoices = new();
        ChrSpecializationRecord[][] _chrSpecializationsByIndex = new ChrSpecializationRecord[(int)Class.Max + 1][];
        MultiMap<int, ConditionalContentTuningRecord> _conditionalContentTuning = new();
        MultiMap<int, CurrencyContainerRecord> _currencyContainers = new();
        List<(int, int)> _contentTuningLabels = new();
        MultiMap<int, Vector2> _curvePoints = new();
        Dictionary<(int, Race, Gender, Class), EmotesTextSoundRecord> _emoteTextSounds = new();
        Dictionary<(int, Expansion), ExpectedStatRecord> _expectedStatsByLevel = new();
        MultiMap<int, int> _factionTeams = new();
        MultiMap<int, FriendshipRepReactionRecord> _friendshipRepReactions = new();
        Dictionary<int, HeirloomRecord> _heirlooms = new();
        MultiMap<int, int> _glyphBindableSpells = new();
        MultiMap<int, ChrSpecialization> _glyphRequiredSpecs = new();
        Dictionary<int, ItemChildEquipmentRecord> _itemChildEquipment = new();
        ItemClassRecord[] _itemClassByOldEnum = new ItemClassRecord[20];
        List<int> _itemsWithCurrencyCost = new();
        MultiMap<int, ItemLimitCategoryConditionRecord> _itemCategoryConditions = new();
        Dictionary<int, ItemModifiedAppearanceRecord> _itemModifiedAppearancesByItem = new();
        MultiMap<int, ItemSetSpellRecord> _itemSetSpells = new();
        MultiMap<int, ItemSpecOverrideRecord> _itemSpecOverrides = new();
        MultiMap<int, ItemEffectRecord> _itemEffectsByItemId = new();
        List<JournalTierRecord> _journalTiersByIndex = new();
        Dictionary<int, Dictionary<Difficulty, MapDifficultyRecord>> _mapDifficulties = new();
        MultiMap<Difficulty, (int, PlayerConditionRecord)> _mapDifficultyConditions = new();
        Dictionary<int, MountRecord> _mountsBySpellId = new();
        MultiMap<int, MountTypeXCapabilityRecord> _mountCapabilitiesByType = new();
        MultiMap<int, MountXDisplayRecord> _mountDisplays = new();
        Dictionary<Race, List<NameGenRecord>[]> _nameGenData = new();
        List<string>[] _nameValidators = new List<string>[(int)Locale.Total + 1];
        Dictionary<int, ParagonReputationRecord> _paragonReputations = new();
        MultiMap<int, int> _phasesByGroup = new();
        Dictionary<PowerType, PowerTypeRecord> _powerTypes = new();
        Dictionary<int, byte> _pvpItemBonus = new();
        MultiMap<int, QuestLineXQuestRecord> _questsByQuestLine = new();
        Dictionary<int, Tuple<List<QuestPackageItemRecord>, List<QuestPackageItemRecord>>> _questPackages = new();
        MultiMap<int, RewardPackXCurrencyTypeRecord> _rewardPackCurrencyTypes = new();
        MultiMap<int, RewardPackXItemRecord> _rewardPackItems = new();
        MultiMap<SkillType, SkillLineRecord> _skillLinesByParentSkillLine = new();
        MultiMap<SkillType, SkillLineAbilityRecord> _skillLineAbilitiesBySkillupSkill = new();
        MultiMap<SkillType, SkillRaceClassInfoRecord> _skillRaceClassInfoBySkill = new();
        MultiMap<ChrSpecialization, SpecializationSpellsRecord> _specializationSpellsBySpec = new();
        List<(int, ChrSpecialization)> _specsBySpecSet = new();
        List<SpellFamilyNames> _spellFamilyNames = new();
        MultiMap<int, SpellProcsPerMinuteModRecord> _spellProcsPerMinuteMods = new();
        MultiMap<int, SpellVisualMissileRecord> _spellVisualMissilesBySet = new();
        Dictionary<int, ScalingStatValuesRecord> _scalingStatValuesByLevel = new();
        List<TalentRecord>[][][] _talentsByPosition = new List<TalentRecord>[(int)Class.Max][][];
        List<int> _toys = new();
        MultiMap<int, TransmogSetRecord> _transmogSetsByItemModifiedAppearance = new();
        MultiMap<int, TransmogSetItemRecord> _transmogSetItemsByTransmogSet = new();
        Dictionary<int, UiMapBounds> _uiMapBounds = new();
        MultiMap<int, UiMapAssignmentRecord>[] _uiMapAssignmentByMap = new MultiMap<int, UiMapAssignmentRecord>[(int)UiMapSystem.Max];
        MultiMap<int, UiMapAssignmentRecord>[] _uiMapAssignmentByArea = new MultiMap<int, UiMapAssignmentRecord>[(int)UiMapSystem.Max];
        MultiMap<int, UiMapAssignmentRecord>[] _uiMapAssignmentByWmoDoodadPlacement = new MultiMap<int, UiMapAssignmentRecord>[(int)UiMapSystem.Max];
        MultiMap<int, UiMapAssignmentRecord>[] _uiMapAssignmentByWmoGroup = new MultiMap<int, UiMapAssignmentRecord>[(int)UiMapSystem.Max];
        List<int> _uiMapPhases = new();
        Dictionary<Tuple<short, sbyte, int>, WMOAreaTableRecord> _wmoAreaTableLookup = new();
    }


    class UiMapBounds
    {
        // these coords are mixed when calculated and used... its a mess
        public float[] Bounds = new float[4];
        public bool IsUiAssignment;
        public bool IsUiLink;
    }

    class UiMapAssignmentStatus
    {
        public UiMapAssignmentRecord UiMapAssignment;
        public InsideStruct Inside;
        public OutsideStruct Outside;
        public sbyte MapPriority;
        public sbyte AreaPriority;
        public sbyte WmoPriority;

        public UiMapAssignmentStatus()
        {
            Inside = new InsideStruct();
            Outside = new OutsideStruct();
            MapPriority = 3;
            AreaPriority = -1;
            WmoPriority = 3;
        }

        // distances if inside
        public class InsideStruct
        {
            public float DistanceToRegionCenterSquared = float.MaxValue;
            public float DistanceToRegionBottom = float.MaxValue;
        }

        // distances if outside
        public class OutsideStruct
        {
            public float DistanceToRegionEdgeSquared = float.MaxValue;
            public float DistanceToRegionTop = float.MaxValue;
            public float DistanceToRegionBottom = float.MaxValue;
        }

        bool IsInside()
        {
            return Outside.DistanceToRegionEdgeSquared < float.Epsilon &&
                Math.Abs(Outside.DistanceToRegionTop) < float.Epsilon &&
                Math.Abs(Outside.DistanceToRegionBottom) < float.Epsilon;
        }

        public static bool operator <(UiMapAssignmentStatus left, UiMapAssignmentStatus right)
        {
            bool leftInside = left.IsInside();
            bool rightInside = right.IsInside();
            if (leftInside != rightInside)
                return leftInside;

            if (left.UiMapAssignment != null && right.UiMapAssignment != null &&
                left.UiMapAssignment.UiMapID == right.UiMapAssignment.UiMapID &&
                left.UiMapAssignment.OrderIndex != right.UiMapAssignment.OrderIndex)
                return left.UiMapAssignment.OrderIndex < right.UiMapAssignment.OrderIndex;

            if (left.WmoPriority != right.WmoPriority)
                return left.WmoPriority < right.WmoPriority;

            if (left.AreaPriority != right.AreaPriority)
                return left.AreaPriority < right.AreaPriority;

            if (left.MapPriority != right.MapPriority)
                return left.MapPriority < right.MapPriority;

            if (leftInside)
            {
                if (left.Inside.DistanceToRegionBottom != right.Inside.DistanceToRegionBottom)
                    return left.Inside.DistanceToRegionBottom < right.Inside.DistanceToRegionBottom;

                float leftUiSizeX = left.UiMapAssignment != null ? (left.UiMapAssignment.UiMax.X - left.UiMapAssignment.UiMin.X) : 0.0f;
                float rightUiSizeX = right.UiMapAssignment != null ? (right.UiMapAssignment.UiMax.X - right.UiMapAssignment.UiMin.X) : 0.0f;

                if (leftUiSizeX > float.Epsilon && rightUiSizeX > float.Epsilon)
                {
                    float leftScale = (left.UiMapAssignment.Region[1].X - left.UiMapAssignment.Region[0].X) / leftUiSizeX;
                    float rightScale = (right.UiMapAssignment.Region[1].X - right.UiMapAssignment.Region[0].X) / rightUiSizeX;
                    if (leftScale != rightScale)
                        return leftScale < rightScale;
                }

                if (left.Inside.DistanceToRegionCenterSquared != right.Inside.DistanceToRegionCenterSquared)
                    return left.Inside.DistanceToRegionCenterSquared < right.Inside.DistanceToRegionCenterSquared;
            }
            else
            {
                if (left.Outside.DistanceToRegionTop != right.Outside.DistanceToRegionTop)
                    return left.Outside.DistanceToRegionTop < right.Outside.DistanceToRegionTop;

                if (left.Outside.DistanceToRegionBottom != right.Outside.DistanceToRegionBottom)
                    return left.Outside.DistanceToRegionBottom < right.Outside.DistanceToRegionBottom;

                if (left.Outside.DistanceToRegionEdgeSquared != right.Outside.DistanceToRegionEdgeSquared)
                    return left.Outside.DistanceToRegionEdgeSquared < right.Outside.DistanceToRegionEdgeSquared;
            }

            return true;
        }

        public static bool operator >(UiMapAssignmentStatus left, UiMapAssignmentStatus right)
        {
            bool leftInside = left.IsInside();
            bool rightInside = right.IsInside();
            if (leftInside != rightInside)
                return leftInside;

            if (left.UiMapAssignment != null && right.UiMapAssignment != null &&
                left.UiMapAssignment.UiMapID == right.UiMapAssignment.UiMapID &&
                left.UiMapAssignment.OrderIndex != right.UiMapAssignment.OrderIndex)
                return left.UiMapAssignment.OrderIndex > right.UiMapAssignment.OrderIndex;

            if (left.WmoPriority != right.WmoPriority)
                return left.WmoPriority > right.WmoPriority;

            if (left.AreaPriority != right.AreaPriority)
                return left.AreaPriority > right.AreaPriority;

            if (left.MapPriority != right.MapPriority)
                return left.MapPriority > right.MapPriority;

            if (leftInside)
            {
                if (left.Inside.DistanceToRegionBottom != right.Inside.DistanceToRegionBottom)
                    return left.Inside.DistanceToRegionBottom > right.Inside.DistanceToRegionBottom;

                float leftUiSizeX = left.UiMapAssignment != null ? (left.UiMapAssignment.UiMax.X - left.UiMapAssignment.UiMin.X) : 0.0f;
                float rightUiSizeX = right.UiMapAssignment != null ? (right.UiMapAssignment.UiMax.X - right.UiMapAssignment.UiMin.X) : 0.0f;

                if (leftUiSizeX > float.Epsilon && rightUiSizeX > float.Epsilon)
                {
                    float leftScale = (left.UiMapAssignment.Region[1].X - left.UiMapAssignment.Region[0].X) / leftUiSizeX;
                    float rightScale = (right.UiMapAssignment.Region[1].X - right.UiMapAssignment.Region[0].X) / rightUiSizeX;
                    if (leftScale != rightScale)
                        return leftScale > rightScale;
                }

                if (left.Inside.DistanceToRegionCenterSquared != right.Inside.DistanceToRegionCenterSquared)
                    return left.Inside.DistanceToRegionCenterSquared > right.Inside.DistanceToRegionCenterSquared;
            }
            else
            {
                if (left.Outside.DistanceToRegionTop != right.Outside.DistanceToRegionTop)
                    return left.Outside.DistanceToRegionTop > right.Outside.DistanceToRegionTop;

                if (left.Outside.DistanceToRegionBottom != right.Outside.DistanceToRegionBottom)
                    return left.Outside.DistanceToRegionBottom > right.Outside.DistanceToRegionBottom;

                if (left.Outside.DistanceToRegionEdgeSquared != right.Outside.DistanceToRegionEdgeSquared)
                    return left.Outside.DistanceToRegionEdgeSquared > right.Outside.DistanceToRegionEdgeSquared;
            }

            return true;
        }
    }

    public class HotfixRecord
    {
        public uint TableHash;
        public int RecordID;
        public HotfixId ID;
        public Status HotfixStatus = Status.Invalid;
        public uint AvailableLocalesMask;

        public void Write(WorldPacket data)
        {
            ID.Write(data);
            data.WriteUInt32(TableHash);
            data.WriteInt32(RecordID);
        }

        public void Read(WorldPacket data)
        {
            ID.Read(data);
            TableHash = data.ReadUInt32();
            RecordID = data.ReadInt32();
        }

        public enum Status
        {
            NotSet = 0,
            Valid = 1,
            RecordRemoved = 2,
            Invalid = 3,
            NotPublic = 4
        }
    }

    public struct HotfixId
    {
        public int PushID;
        public int UniqueID;

        public void Write(WorldPacket data)
        {
            data.WriteInt32(PushID);
            data.WriteInt32(UniqueID);
        }

        public void Read(WorldPacket data)
        {
            PushID = data.ReadInt32();
            UniqueID = data.ReadInt32();
        }
    }

    public class HotfixOptionalData
    {
        public int Key;
        public byte[] Data;
    }

    public class HotfixPush
    {
        public List<HotfixRecord> Records = new();
        public uint AvailableLocalesMask;
    }

    class ChrClassesXPowerTypesRecordComparer : IComparer<ChrClassesXPowerTypesRecord>
    {
        public int Compare(ChrClassesXPowerTypesRecord left, ChrClassesXPowerTypesRecord right)
        {
            if (left.ClassID != right.ClassID)
                return left.ClassID.CompareTo(right.ClassID);
            return left.PowerType.CompareTo(right.PowerType);
        }
    }

    class FriendshipRepReactionRecordComparer : IComparer<FriendshipRepReactionRecord>
    {
        public int Compare(FriendshipRepReactionRecord left, FriendshipRepReactionRecord right)
        {
            return left.ReactionThreshold.CompareTo(right.ReactionThreshold);
        }
    }

    class MountTypeXCapabilityRecordComparer : IComparer<MountTypeXCapabilityRecord>
    {
        public int Compare(MountTypeXCapabilityRecord left, MountTypeXCapabilityRecord right)
        {
            if (left.MountTypeID == right.MountTypeID)
                return left.OrderIndex.CompareTo(right.OrderIndex);
            return left.Id.CompareTo(right.Id);
        }
    }

    public struct ContentTuningLevels
    {
        public short MinLevel;
        public short MaxLevel;
        public short MinLevelWithDelta;
        public short MaxLevelWithDelta;
        public short TargetLevelMin;
        public short TargetLevelMax;
    }

    public class ShapeshiftFormModelData
    {
        public int OptionID;
        public List<ChrCustomizationChoiceRecord> Choices = new();
        public List<ChrCustomizationDisplayInfoRecord> Displays = new();
    }

    public class TalentSpellPos
    {
        public TalentSpellPos(ushort TalentID, byte Rank)
        {
            this.TalentID = TalentID;
            this.Rank = Rank;
        }

        public ushort TalentID;
        public byte Rank;
    };
}
