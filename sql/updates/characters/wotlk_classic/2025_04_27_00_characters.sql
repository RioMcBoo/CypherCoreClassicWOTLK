-- Add ammoId to saving
ALTER TABLE `characters`   
	ADD COLUMN `ammoId` INT DEFAULT 0 NOT NULL AFTER `power10`,
	ADD CONSTRAINT `ammoId_check` CHECK ((`ammoId` > -1));