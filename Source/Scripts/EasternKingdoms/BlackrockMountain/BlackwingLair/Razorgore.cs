// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using Game.Spells;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackwingLair.Razorgore
{
    struct SpellIds
    {
        // @todo orb uses the wrong spell, this needs sniffs
        public const int Mindcontrol = 42013;
        public const int Channel = 45537;
        public const int EggDestroy = 19873;

        public const int Cleave = 22540;
        public const int Warstomp = 24375;
        public const int Fireballvolley = 22425;
        public const int Conflagration = 23023;
    }

    struct TextIds
    {
        public const int SayEggsBroken1 = 0;
        public const int SayEggsBroken2 = 1;
        public const int SayEggsBroken3 = 2;
        public const int SayDeath = 3;
    }

    struct CreatureIds
    {
        public const int EliteDrachkin = 12422;
        public const int EliteWarrior = 12458;
        public const int Warrior = 12416;
        public const int Mage = 12420;
        public const int Warlock = 12459;
    }

    struct GameObjectIds
    {
        public const int Egg = 177807;
    }

    [Script]
    class boss_razorgore : BossAI
    {
        bool secondPhase;

        public boss_razorgore(Creature creature) : base(creature, DataTypes.RazorgoreTheUntamed)
        {
            Initialize();
        }

        void Initialize()
        {
            secondPhase = false;
        }

        public override void Reset()
        {
            _Reset();

            Initialize();
            instance.SetData(BWLMisc.DataEggEvent, (int)EncounterState.NotStarted);
        }

        public override void JustDied(Unit killer)
        {
            _JustDied();
            Talk(TextIds.SayDeath);

            instance.SetData(BWLMisc.DataEggEvent, (int)EncounterState.NotStarted);
        }

        void DoChangePhase()
        {
            _scheduler.Schedule(TimeSpan.FromSeconds(15), task =>
            {
                DoCastVictim(SpellIds.Cleave);
                task.Repeat(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(10));
            });
            _scheduler.Schedule(TimeSpan.FromSeconds(35), task =>
            {
                DoCastVictim(SpellIds.Warstomp);
                task.Repeat(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(25));
            });
            _scheduler.Schedule(TimeSpan.FromSeconds(7), task =>
            {
                DoCastVictim(SpellIds.Fireballvolley);
                task.Repeat(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(15));
            });
            _scheduler.Schedule(TimeSpan.FromSeconds(12), task =>
            {
                DoCastVictim(SpellIds.Conflagration);
                task.Repeat(TimeSpan.FromSeconds(30));
            });

            secondPhase = true;
            me.RemoveAllAuras();
            me.SetFullHealth();
        }

        public override void DoAction(int action)
        {
            if (action == BWLMisc.ActionPhaseTwo)
                DoChangePhase();
        }

        public override void DamageTaken(Unit who, ref int damage, DamageEffectType damageType, SpellInfo spellInfo = null)
        {
            // @todo this is wrong - razorgore should still take damage,
            // he should just nuke the whole room and respawn if he dies during P1
            if (!secondPhase)
                damage = 0;
        }

        public override void UpdateAI(uint diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }

    [Script]
    class go_orb_of_domination : GameObjectAI
    {
        InstanceScript instance;

        public go_orb_of_domination(GameObject go) : base(go)
        {
            instance = go.GetInstanceScript();
        }

        public override bool OnGossipHello(Player player)
        {
            if (instance.GetData(BWLMisc.DataEggEvent) != (int)EncounterState.Done)
            {
                Creature razorgore = instance.GetCreature(DataTypes.RazorgoreTheUntamed);
                if (razorgore != null)
                {
                    razorgore.Attack(player, true);
                    player.CastSpell(razorgore, SpellIds.Mindcontrol);
                }
            }
            return true;
        }
    }

    [Script] // 19873 - Destroy Egg
    class spell_egg_event : SpellScript
    {
        void HandleOnHit()
        {
            InstanceScript instance = GetCaster().GetInstanceScript();
            if (instance != null)
                instance.SetData(BWLMisc.DataEggEvent, (int)EncounterState.Special);
        }

        public override void Register()
        {
            OnHit.Add(new HitHandler(HandleOnHit));
        }
    }
}

