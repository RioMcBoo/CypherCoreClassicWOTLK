-- Fix dungeons/instance entrance
UPDATE `world_safe_locs` SET `MapID` = 229, `LocX` = 78.3534, `LocY` = -226.841, `LocZ` = 49.7662, `Facing` = 4.71238, `Comment` = 'Upper Blackrock Spire Entrance' WHERE `ID` = 4501;
UPDATE `world_safe_locs` SET `MapID` = 289, `LocX` = 196.37, `LocY` = 127.05, `LocZ` = 134.91, `Facing` = 6.09, `Comment` = 'Scholomance Entrance' WHERE `ID` = 3671;
INSERT INTO `areatrigger_teleport` (`ID`, `PortLocID`, `Name`) VALUES (1470, 3701, 'Blackrock Spire - Searing Gorge Instance');