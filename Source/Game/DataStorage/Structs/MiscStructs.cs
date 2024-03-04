// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game.DataStorage
{
    public struct WMOAreaTableTripple
    {
        public WMOAreaTableTripple(int r, int a, int g)
        {
            groupId = g;
            rootId = r;
            adtId = a;
        }

        // ordered by entropy; that way memcmp will have a minimal medium runtime
        public int groupId;
        public int rootId;
        public int adtId;
    }

    public class TaxiPathBySourceAndDestination
    {
        public TaxiPathBySourceAndDestination(int _id, uint _price)
        {
            Id = _id;
            price = _price;
        }

        public int Id;
        public uint price;
    }
}
