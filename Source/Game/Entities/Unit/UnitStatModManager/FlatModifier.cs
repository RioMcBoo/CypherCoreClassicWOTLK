// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Game.Maps;

namespace Game.Entities
{
    public record struct FlatModifier
    {
        public static int IdleModifier = 0;
        public int Positive => _positive;
        public int Negative => _negative;
        public int TotalValue => _positive + _negative;

        private int _positive = IdleModifier;
        private int _negative = IdleModifier;

        public static bool IsPositive(int modifier) => modifier > IdleModifier;
        public static bool IsNegative(int modifier) => modifier < IdleModifier;
        public bool IsIdle => Positive == IdleModifier && Negative == IdleModifier;

        public FlatModifier(int modifier)
        {
            Modify(modifier, true);
        }

        public FlatModifier(FlatModifier modifier)
        {
            _positive = modifier.Positive;
            _negative = modifier.Negative;
        }

        public static implicit operator FlatModifier(int modifier)
        {
            return new(modifier);
        }

        public static implicit operator int(FlatModifier mod)
        {
            return mod.TotalValue;
        }

        public void Modify(int modifier, bool apply)
        {
            bool isPositive = IsPositive(modifier);
            
            modifier = GetFinalModifier(modifier, apply);            

            if (isPositive)
            {
                _positive += modifier;
            }
            else
            {
                _negative += modifier;
            }
        }

        public void Modify(FlatModifier flatModifier, bool apply)
        {
            if (apply)
            {
                _positive += flatModifier._positive;
                _negative += flatModifier._negative;
            }
            else
            {
                _positive -= flatModifier._positive;
                _negative -= flatModifier._negative;
            }
        }

        private static int GetFinalModifier(int modifier, bool apply)
        {
            if (!apply)
            {
                modifier = IdleModifier - modifier;
            }

            return modifier;
        }

        public static void Modify(ref int value, int modifier, bool apply)
        {
            value += GetFinalModifier(modifier, apply);
        }
    }
}
