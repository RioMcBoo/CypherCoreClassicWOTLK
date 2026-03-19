// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.using Framework.Constants;

using Framework.Constants;
using System;

namespace Game.Entities
{
    [Flags]
    public enum ActiveStates : int
    {
        Passive = 0x001,
        Disabled = 0x101,
        Enabled = 0x181,
        Command = 0x007,
        Reaction = 0x006,

        Decide = 0x00   // custom
    }

    public enum CommandStates : byte
    {
        Stay = 0,
        Follow = 1,
        Attack = 2,
        Abandon = 3,
        MoveTo = 4
    }

    public struct CharmActionButton
    {
        WowActionButton _packedData;

        public CharmActionButton(uint packedData)
        {
            _packedData = new(packedData);
        }

        public CharmActionButton(ActiveStates type, int value)
        {
            _packedData = new((int)type, value);
        }

        public WowActionButton PackedData => _packedData;

        public ActiveStates State
        {
            get => (ActiveStates)_packedData.Type;
            set => _packedData.Type = (int)value;
        }

        public int Action
        {
            get => _packedData.Value;
            set => _packedData.Value = value;
        }

        public bool IsSpell
        {
            get
            {
                ActiveStates state = State;
                return state == ActiveStates.Disabled || state == ActiveStates.Enabled || state == ActiveStates.Passive;
            }
        }

        public bool IsCommand
        {
            get
            {
                ActiveStates state = State;
                return state == ActiveStates.Command || state == ActiveStates.Reaction;
            }
        }

        public CommandStates Command
        {
            get => (CommandStates)_packedData.Value;
            set => _packedData.Value = (int)value;
        }

        public ReactStates Reaction
        {
            get => (ReactStates)_packedData.Value;
            set => _packedData.Value = (int)value;
        }

        public int SpellId
        {
            get => _packedData.Value;
            set => _packedData.Value = value;
        }

        public static implicit operator uint(CharmActionButton button)
        {
            return button._packedData;
        }

        public static bool operator ==(CharmActionButton left, CharmActionButton right)
        {
            return left._packedData == right._packedData;
        }

        public static bool operator !=(CharmActionButton left, CharmActionButton right)
        {
            return !(left == right);
        }

        public bool Equals(CharmActionButton other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            return obj is CharmActionButton other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _packedData.GetHashCode();
        }
    }
}
