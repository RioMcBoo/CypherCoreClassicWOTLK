-- Change column `active` from Unsigned TINYINT TO Unsigned INT
ALTER TABLE `pet_spell` MODIFY COLUMN `active` INT UNSIGNED NOT NULL DEFAULT '0';