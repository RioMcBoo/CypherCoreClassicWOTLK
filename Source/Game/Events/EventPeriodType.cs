// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game
{
    public enum EventPeriodType : sbyte
    {
        Yearly = -1,
        Weekly = 0,
        /// <summary>
        /// Defined dates only (Darkmoon Faire)
        /// </summary>
        Defined = 1,
        /// <summary>
        /// Only used for looping events <br/>
        /// without defined length (Call to Arms)
        /// </summary>
        Cyclical = 2,
    }
}
