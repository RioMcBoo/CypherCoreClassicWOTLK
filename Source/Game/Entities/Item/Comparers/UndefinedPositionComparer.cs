// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Game.Entities
{
    public class UndefinedPositionComparer<T> : IComparer<Item> where T : Item
    {
        // Summary:
        //     Compares two objects and returns a value indicating whether one is less than,
        //     equal to, or greater than the other.
        //
        // Parameters:
        //   x:
        //     The first object to compare.
        //
        //   y:
        //     The second object to compare.
        //
        // Returns:
        //     A signed integer that indicates the relative values of x and y, as shown in the
        //     following table.
        //
        //     Value – Meaning
        //     Less than zero –x is less than y.
        //     Zero –x equals y.
        //     Greater than zero –x is greater than y.
        private enum CompareResult
        {
            Equal = 0,
            Y_up = 1,
            X_up = -1,

            Y_down = X_up,
            X_down = Y_up,
        }

        public int Compare(Item x, Item y)
        {
            if (x == null && y == null)
                return (int)CompareResult.Equal;

            if (x == null)
                return (int)CompareResult.X_up;

            if (y == null)
                return (int)CompareResult.Y_up;

            if (CompareByBagFamily(x, y) is CompareResult BagFamilyResult && BagFamilyResult != CompareResult.Equal)
                return (int)BagFamilyResult;

            if (CompareByMergeability(x, y) is CompareResult MergeabilityResult1 && MergeabilityResult1 != CompareResult.Equal)
                return (int)MergeabilityResult1;

            if (CompareByAmount(x, y) is CompareResult StackSizeResult && StackSizeResult != CompareResult.Equal)
                return (int)StackSizeResult;

            return (int)CompareResult.Equal;
        }

        private CompareResult CompareByBagFamily(Item x, Item y)
        {
            int x_rating = x.GetTemplate().GetBagFamily().GetBagFamilyRating();
            int y_rating = y.GetTemplate().GetBagFamily().GetBagFamilyRating();

            if (x_rating < y_rating)
                return CompareResult.X_down;

            if (y_rating < x_rating)
                return CompareResult.Y_down;

            return CompareResult.Equal;
        }

        /// <summary>
        /// Try to storage immergeable first<br/>
        /// (to quickly identify a lack of space)
        /// </summary>
        private CompareResult CompareByMergeability(Item x, Item y)
        {
            bool x_isMergeable = x.GetTemplate().IsMergeable;
            bool y_isMergeable = y.GetTemplate().IsMergeable;

            if (x_isMergeable != y_isMergeable)
            {
                if (x_isMergeable)
                    return CompareResult.X_down;

                if (y_isMergeable)
                    return CompareResult.Y_down;
            }

            return CompareResult.Equal;
        }

        /// <summary>
        /// Try to storage big amount first<br/>
        /// (to quickly identify a lack of space)
        /// </summary>
        private CompareResult CompareByAmount(Item x, Item y)
        {
            int x_amount = x.GetCount();
            int y_amount = x.GetCount();

            if (x_amount > y_amount)
                return CompareResult.X_up;

            if (y_amount > x_amount)
                return CompareResult.Y_up;

            return CompareResult.Equal;
        }
    }
}
