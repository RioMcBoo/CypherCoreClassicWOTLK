// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game.Entities
{
    public record struct MultModifier
    {
        public static float IdleModifier = 1.0f;
        public float Positive => _positive;
        public float Negative => _negative;
        public float TotalValue => _positive * _negative;

        private float _positive = IdleModifier;
        private float _negative = IdleModifier;

        public static bool IsPositive(float multiplier) => multiplier > IdleModifier;
        public static bool IsNegative(float multiplier) => multiplier < IdleModifier;
        public static bool IsPositive(int percentage) => IsPositive(MathFunctions.PercentageToMultiplier(percentage));
        public static bool IsNegative(int percentage) => IsNegative(MathFunctions.PercentageToMultiplier(percentage));
        public bool IsIdle => Positive == IdleModifier && Negative == IdleModifier;

        public MultModifier() { }

        public MultModifier(float multiplier)
        {
            Modify(multiplier, true);
        }

        public MultModifier(int percentage)
        {
            ModifyPercentage(percentage, true);
        }

        public MultModifier(MultModifier modifier)
        {
            _positive = modifier.Positive;
            _negative = modifier.Negative;
        }

        public static implicit operator MultModifier(float multiplier)
        {
            return new(multiplier);
        }

        public static implicit operator MultModifier(int percentage)
        {
            return new(percentage);
        }

        public static implicit operator float(MultModifier mod)
        {
            return mod.TotalValue;
        }

        public void Modify(float multiplier, bool apply)
        {
            bool isPositive = IsPositive(multiplier);

            if (!apply)
            {
                multiplier = IdleModifier / multiplier;
            }

            if (isPositive)
            {
                _positive *= multiplier;
            }
            else
            {
                _negative *= multiplier;
            }
        }

        public void ModifyPercentage(int percentage, bool apply)
        {
            Modify(MathFunctions.PercentageToMultiplier(percentage), apply);
        }

        public void Modify(MultModifier multModifier, bool apply)
        {
            if (apply)
            {
                _positive *= multModifier._positive;
                _negative *=  multModifier._negative;
            }
            else
            {
                _positive /= multModifier._positive;
                _negative /= multModifier._negative;
            }
        }        
    }
}
