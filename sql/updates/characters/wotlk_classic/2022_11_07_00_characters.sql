ALTER TABLE `character_glyphs`
CHANGE COLUMN `glyphId` `glyph1` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0' AFTER `talentGroup`,
ADD COLUMN `glyph2` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0' AFTER `glyph1`,
ADD COLUMN `glyph3` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0' AFTER `glyph2`,
ADD COLUMN `glyph4` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0' AFTER `glyph3`,
ADD COLUMN `glyph5` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0' AFTER `glyph4`,
ADD COLUMN `glyph6` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0' AFTER `glyph5`,
DROP PRIMARY KEY,
ADD PRIMARY KEY (`guid`, `talentGroup`) USING BTREE;

ALTER TABLE `character_talent`
CHANGE COLUMN `talentId` `spell` INT(10) UNSIGNED NOT NULL AFTER `guid`,
DROP PRIMARY KEY,
ADD PRIMARY KEY (`guid`, `spell`, `talentGroup`) USING BTREE;