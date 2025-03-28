CREATE TABLE `character_trade_skill_spells` (
  `guid` bigint NOT NULL COMMENT 'Player''s Global Unique Identifier',
  `skill` smallint NOT NULL COMMENT 'Profession Skill Identifier',
  `spell` int NOT NULL COMMENT 'Spell Identifier',
  PRIMARY KEY (`guid`,`skill`,`spell`) USING BTREE,
  CONSTRAINT `guid_check` CHECK ((`guid` > 0)),
  CONSTRAINT `skill_check` CHECK ((`skill` > 0)),
  CONSTRAINT `spell_check` CHECK ((`spell` > 0))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='A copy of ''''character_spell'''' containing only known profession recipes. Only used if the player is offline so that the profession hyperlink works.';