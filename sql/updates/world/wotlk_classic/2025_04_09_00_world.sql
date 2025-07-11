-- fix  HealthScalingExpansion
UPDATE creature_template_difficulty SET HealthScalingExpansion = 2 WHERE HealthScalingExpansion IN (9,3);
UPDATE creature_template_difficulty SET HealthScalingExpansion= 0; 
UPDATE creature_template_difficulty SET HealthScalingExpansion= 2 WHERE entry IN (34031, 27017); 
UPDATE creature_template_difficulty SET HealthScalingExpansion= 1 WHERE entry IN (28470, 25062, 28267, 28513); 
UPDATE creature_template_difficulty SET MinLevel= 61, MaxLevel= 61 WHERE entry=34031; 
UPDATE creature_template_difficulty SET MinLevel= 75, MaxLevel= 75 WHERE entry=27017; 
UPDATE creature_template_difficulty SET MinLevel= 70, MaxLevel= 70 WHERE entry=28267;

-- remove unnecessary data by locales
DELETE FROM creature_template_locale WHERE entry BETWEEN 42079+0 AND 42078+170814;

-- clean spell_totem from non-existent races
DELETE FROM `spell_totem_model` WHERE `RaceID`=36 AND `SpellID` IN (2484, 5394, 8143, 8512, 16191, 51485, 98008, 108280, 157153, 192058, 192077, 192222,
198838, 204330, 204331, 204336, 207399, 324386, 355580);
DELETE FROM `spell_totem_model` WHERE `RaceID`=35 AND `SpellID` IN (2484, 5394, 8143, 8512, 16191, 51485, 98008, 108280, 157153, 192058, 192077, 192222,
198838, 204330, 204331, 204336, 207399, 324386, 355580);
DELETE FROM `spell_totem_model` WHERE `RaceID`=34 AND `SpellID` IN (2484, 5394, 8143, 8512, 16191, 51485, 98008, 108280, 157153, 192058, 192077, 192222,
198838, 204330, 204331, 204336, 207399, 324386, 355580);
DELETE FROM `spell_totem_model` WHERE `RaceID`=32 AND `SpellID` IN (2484, 5394, 8143, 8512, 16191, 51485, 98008, 108280, 157153, 192058, 192077, 192222,
198838, 204330, 204331, 204336, 207399, 324386, 355580);
DELETE FROM `spell_totem_model` WHERE `RaceID`=31 AND `SpellID` IN (2484, 5394, 8143, 8512, 16191, 51485, 98008, 108280, 157153, 192058, 192077, 192222,
198838, 204330, 204331, 204336, 207399, 324386, 355580);
DELETE FROM `spell_totem_model` WHERE `RaceID`=28 AND `SpellID` IN (2484, 5394, 8143, 8512, 16191, 51485, 98008, 108280, 157153, 192058, 192077, 192222,
198838, 204330, 204331, 204336, 207399, 324386, 355580);
DELETE FROM `spell_totem_model` WHERE `RaceID`=26 AND `SpellID` IN (2484, 5394, 8143, 8512, 16191, 51485, 98008, 108280, 157153, 192058, 192077, 192222,
198838, 204330, 204331, 204336, 207399, 324386, 355580);
DELETE FROM `spell_totem_model` WHERE `RaceID`=25 AND `SpellID` IN (2484, 5394, 8143, 8512, 16191, 51485, 98008, 108280, 157153, 192058, 192077, 192222,
198838, 204330, 204331, 204336, 207399, 324386, 355580);
DELETE FROM `spell_totem_model` WHERE `RaceID`=24 AND `SpellID` IN (2484, 5394, 8143, 8512, 16191, 51485, 98008, 108280, 157153, 192058, 192077, 192222,
198838, 204330, 204331, 204336, 207399, 324386, 355580);
DELETE FROM `spell_totem_model` WHERE `RaceID`=9 AND `SpellID` IN (2484, 5394, 8143, 8512, 16191, 51485, 98008, 108280, 157153, 192058, 192077, 192222,
198838, 204330, 204331, 204336, 207399, 324386, 355580);
DELETE FROM `spell_totem_model` WHERE `RaceID`=28 AND `SpellID` IN (196932, 202188, 204332, 210651, 210657, 210660);
DELETE FROM `spell_totem_model` WHERE `RaceID`=26 AND `SpellID` IN (196932, 202188, 204332, 210651, 210657, 210660);
DELETE FROM `spell_totem_model` WHERE `RaceID`=25 AND `SpellID` IN (196932, 202188, 204332, 210651, 210657, 210660);
DELETE FROM `spell_totem_model` WHERE `RaceID`=24 AND `SpellID` IN (196932, 202188, 204332, 210651, 210657, 210660);
DELETE FROM `spell_totem_model` WHERE `RaceID`=9 AND `SpellID` IN (196932, 202188, 204332, 210651, 210657, 210660);

-- remove totem spells above 3.4.3 addon
DELETE FROM spell_totem_model WHERE RaceID=11 AND SpellID IN (355580, 324386, 210660, 210657, 210651, 207399, 204336, 204332, 204331, 204330, 202188, 198838,
196932, 192222, 192077, 192058, 188616, 188592, 157153, 108280, 98008);
DELETE FROM spell_totem_model WHERE RaceID=8 AND SpellID IN (355580, 324386, 210660, 210657, 210651, 207399, 204336, 204332, 204331, 204330, 202188, 198838,
196932, 192222, 192077, 192058, 188616, 188592, 157153, 108280, 98008);
DELETE FROM spell_totem_model WHERE RaceID=6 AND SpellID IN (355580, 324386, 210660, 210657, 210651, 207399, 204336, 204332, 204331, 204330, 202188, 198838,
196932, 192222, 192077, 192058, 188616, 188592, 157153, 108280, 98008);
DELETE FROM spell_totem_model WHERE RaceID=3 AND SpellID IN (355580, 324386, 210660, 210657, 210651, 207399, 204336, 204332, 204331, 204330, 202188, 198838,
196932, 192222, 192077, 192058, 188616, 188592, 157153, 108280, 98008);
DELETE FROM spell_totem_model WHERE RaceID=2 AND SpellID IN (355580, 324386, 210660, 210657, 210651, 207399, 204336, 204332, 204331, 204330, 202188, 198838,
196932, 192222, 192077, 192058, 188616, 188592, 157153, 108280, 98008);

-- remove cataclysm disenchant data
DELETE FROM `disenchant_loot_template` WHERE `Item` IN (52722, 52721, 52720, 52719, 52718, 52555);