-- Fix dungeon entrace for Scarlet Monastery
DELETE FROM  `areatrigger_teleport` WHERE `ID` IN (45, 610, 612, 614);
INSERT INTO `areatrigger_teleport` (`ID`, `PortLocID`, `Name`) VALUES 
(45, 3597, 'Scarlet Monastery - Graveyard (Entrance)'),
(610, 3627, 'Scarlet Monastery - Cathedral (Entrance)'),
(612, 3628, 'Scarlet Monastery - Armory (Entrance)'),
(614, 3629, 'Scarlet Monastery - Library (Entrance)');
