-- Fix spawns of Mage trainers in Stormind
UPDATE `creature` SET `position_x` = -9012, `position_y` = 867.595, `position_z` = 29.621, `orientation` = 0.776 WHERE `id` = 331;
UPDATE `creature` SET `position_x` = -8991, `position_y` = 845.411, `position_z` = 29.621, `orientation` = 1.752 WHERE `id` = 2485;
UPDATE `creature` SET `position_x` = -8990, `position_y` = 862.929, `position_z` = 29.621, `orientation` = 4.858 WHERE `id` = 5497;
UPDATE `creature` SET `position_x` = -9007, `position_y` = 885.181, `position_z` = 29.621, `orientation` = 3.938 WHERE `id` = 5498;

-- Delete non blizzlike NPC
DELETE FROM `creature` WHERE `guid` IN (850236, 850237);