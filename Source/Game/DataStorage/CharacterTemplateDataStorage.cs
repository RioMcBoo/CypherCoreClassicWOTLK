﻿// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Framework.Database;
using System.Collections.Generic;

namespace Game.DataStorage
{
    public class CharacterTemplateDataStorage : Singleton<CharacterTemplateDataStorage>
    {
        CharacterTemplateDataStorage() { }

        public void LoadCharacterTemplates()
        {
            RelativeTime oldMSTime = Time.NowRelative;
            _characterTemplateStore.Clear();

            MultiMap<int, CharacterTemplateClass> characterTemplateClasses = new();
            {
                SQLResult classesResult = DB.World.Query("SELECT TemplateId, FactionGroup, Class FROM character_template_class");
                if (!classesResult.IsEmpty())
                {
                    do
                    {
                        int templateId = classesResult.Read<int>(0);
                        FactionMasks factionGroup = (FactionMasks)classesResult.Read<byte>(1);
                        Class classID = (Class)classesResult.Read<byte>(2);

                        if (!factionGroup.HasFlag(FactionMasks.Player) || !factionGroup.HasFlag(FactionMasks.Alliance | FactionMasks.Horde))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Faction group {factionGroup} defined for character template {templateId} " +
                                $"in `character_template_class` is invalid. Skipped.");
                            continue;
                        }

                        if (!CliDB.ChrClassesStorage.ContainsKey(classID))
                        {
                            Log.outError(LogFilter.Sql, 
                                $"Class {classID} defined for character template {templateId} " +
                                $"in `character_template_class` does not exists, skipped.");
                            continue;
                        }

                        characterTemplateClasses.Add(templateId, new CharacterTemplateClass(factionGroup, classID));
                    }
                    while (classesResult.NextRow());
                }
                else
                {
                    Log.outInfo(LogFilter.ServerLoading, 
                        "Loaded 0 character template classes. DB table `character_template_class` is empty.");
                }
            }

            {
                SQLResult templates = DB.World.Query("SELECT Id, Name, Description, Level FROM character_template");
                if (templates.IsEmpty())
                {
                    Log.outInfo(LogFilter.ServerLoading, 
                        "Loaded 0 character templates. DB table `character_template` is empty.");
                    return;
                }

                do
                {
                    CharacterTemplate templ = new();
                    templ.TemplateSetId = templates.Read<int>(0);
                    templ.Name = templates.Read<string>(1);
                    templ.Description = templates.Read<string>(2);
                    templ.Level = templates.Read<byte>(3);
                    templ.Classes = characterTemplateClasses.Extract(templ.TemplateSetId);

                    if (templ.Classes.Empty())
                    {
                        Log.outError(LogFilter.Sql, 
                            $"Character template {templ.TemplateSetId} does not have any classes " +
                            $"defined in `character_template_class`. Skipped.");
                        continue;
                    }

                    _characterTemplateStore[templ.TemplateSetId] = templ;
                }
                while (templates.NextRow());
            }

            Log.outInfo(LogFilter.ServerLoading, $"Loaded {_characterTemplateStore.Count} character templates in {Time.Diff(oldMSTime)} ms.");
        }

        public Dictionary<int, CharacterTemplate> GetCharacterTemplates()
        {
            return _characterTemplateStore;
        }

        public CharacterTemplate GetCharacterTemplate(int templateId)
        {
            return _characterTemplateStore.LookupByKey(templateId);
        }

        Dictionary<int, CharacterTemplate> _characterTemplateStore = new();
    }

    public struct CharacterTemplateClass
    {
        public CharacterTemplateClass(FactionMasks factionGroup, Class classID)
        {
            FactionGroup = factionGroup;
            ClassID = classID;
        }

        public FactionMasks FactionGroup;
        public Class ClassID;
    }

    public class CharacterTemplate
    {
        public int TemplateSetId;
        public List<CharacterTemplateClass> Classes;
        public string Name;
        public string Description;
        public byte Level;
    }
}
