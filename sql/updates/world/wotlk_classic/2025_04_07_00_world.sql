-- Fix Mage portal in Wizzaard Sanctum
UPDATE `world_safe_locs` SET `MapID` = 0, `LocX` = -9014.94, `LocY` = 873.326, `LocZ` = 148.616, `Facing` = 314.621, `Comment` = 'Stormwind - Mage Exit Target' WHERE `ID` = 3630;
UPDATE `world_safe_locs` SET `MapID` = 0, `LocX` = -9016.97, `LocY` = 885.436, `LocZ` = 29.6207, `Facing` = 308.771, `Comment` = 'Stormwind - Mage Entrance Target' WHERE `ID` = 3631;


DELETE FROM `areatrigger_teleport` WHERE `ID` IN (702, 704);
INSERT INTO `areatrigger_teleport` (`ID`, `PortLocID`, `Name`) VALUES 
(702, 3630, 'Stormwind - Mage Exit Target'),
(704, 3631, 'Stormwind - Mage Entrance Target');