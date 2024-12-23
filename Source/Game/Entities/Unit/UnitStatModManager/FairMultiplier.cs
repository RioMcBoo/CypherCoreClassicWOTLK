// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game.Entities
{
    /// <summary>
    /// The idea of ​​the FairMultiplier is that both positive and negative modifiers, when applied, give a minimal effect.<br/>
    /// This is achieved by summing up positive multipliers(each modifier only affects the initial value),<br/>
    /// and multiplying negative multipliers (it also makes it be impossible to achieve a 100% negative effect).<br/>
    /// <br/>
    /// For example, positive:<br/>
    /// 1000 * 1.1 * 1.5 * 1.7 = 2805 [not fair] <br/>
    /// 1000 * (1 + 0.1 + 0.5 + 0.7) = 2300 <br/>
    /// <br/>
    /// For example, negative (case:1):<br/>
    /// 1000 * 0.9 * 0.7 * 0.5 = 315 <br/>
    /// 1000 * (1 - 0.1 - 0.3 - 0.5) = 100 [not fair] <br/>
    /// <br/>
    /// For example, negative (case:2 - possible to achieve a 100% negative effect):<br/>
    /// 1000 * (1 - 0.1 - 0.4 - 0.5) = 0 [not fair] <br/>
    /// <br/>
    /// For example, negative (case:3 - impossible to achieve a 100% negative effect):<br/>
    /// 1000 * 0.1 * 0.1 * 0.1 * 0.1 * 0.1 * 0.1 * 0.1 = 0.001 <br/>
    /// <br/>
    /// The problem with a fair multiplier is that in order to preserve its fairness, <br/>
    /// it is necessary to follow its rules further beyond its structure.
    /// </summary>
    /*
    public record struct FairMultiplier
    {
        public float Positive => MathFunctions.PercentageToMultiplier(_positivePercentage);
        public float Negative => _negativeMultiplier;

        private int _positivePercentage; // used integer to avoid errors when summing floating point numbers 
        private float _negativeMultiplier;
    }
    */
}
