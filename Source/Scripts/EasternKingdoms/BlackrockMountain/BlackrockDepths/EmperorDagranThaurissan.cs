// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using Game.AI;
using Game.Entities;
using Game.Maps;
using Game.Scripting;
using System;

namespace Scripts.EasternKingdoms.BlackrockMountain.BlackrockDepths.Draganthaurissan
{
    struct SpellIds
    {
        public const int Handofthaurissan = 17492;
        public const int Avatarofflame = 15636;
    }

    struct TextIds
    {
        public const int SayAggro = 0;
        public const int SaySlay = 1;

        public const int EmoteShaken = 0;
    }

    [Script]
    class boss_draganthaurissan : ScriptedAI
    {
        InstanceScript _instance;

        public boss_draganthaurissan(Creature creature) : base(creature)
        {
            _instance = me.GetInstanceScript();
        }

        public override void Reset()
        {
            _scheduler.CancelAll();
        }

        public override void JustEngagedWith(Unit who)
        {
            Talk(TextIds.SayAggro);
            me.CallForHelp(166.0f);
            _scheduler.Schedule((Seconds)4, task =>
            {
                Unit target = SelectTarget(SelectTargetMethod.Random, 0);
                if (target != null)
                    DoCast(target, SpellIds.Handofthaurissan);
                task.Repeat((Seconds)5);
            });
            _scheduler.Schedule((Seconds)25, task =>
            {
                DoCastVictim(SpellIds.Avatarofflame);
                task.Repeat((Seconds)18);
            });
        }

        public override void KilledUnit(Unit who)
        {
            if (who.IsPlayer())
                Talk(TextIds.SaySlay);
        }

        public override void JustDied(Unit killer)
        {
            Creature moira = ObjectAccessor.GetCreature(me, _instance.GetGuidData(DataTypes.DataMoira));
            if (moira != null)
            {
                moira.GetAI().EnterEvadeMode();
                moira.SetFaction(FactionTemplates.Friendly);
                moira.GetAI().Talk(TextIds.EmoteShaken);
            }
        }

        public override void UpdateAI(TimeSpan diff)
        {
            if (!UpdateVictim())
                return;

            _scheduler.Update(diff);
        }
    }
}

