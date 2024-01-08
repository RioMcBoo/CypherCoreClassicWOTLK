// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game.DataStorage
{
    public sealed class KeyChainRecord
    {
        public static int KEYCHAIN_SIZE = 32;

        public uint Id;
        public byte[] Key = new byte[KEYCHAIN_SIZE];
    }

    public sealed class KeystoneAffixRecord
    {
        public LocalizedString Name;
        public LocalizedString Description;
        public uint Id;
        public int FiledataID;
    }
}