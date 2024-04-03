using Game.Networking.Packets;

namespace Game
{
    public partial class WorldSession
    {
        //[WorldPacketHandler(ClientOpcodes.BattlePayGetPurchaseList, Processing = PacketProcessing.Inplace)]
        //[WorldPacketHandler(ClientOpcodes.BattlePayGetProductList, Processing = PacketProcessing.Inplace)]
        //[WorldPacketHandler(ClientOpcodes.UpdateVasPurchaseStates, Processing = PacketProcessing.Inplace)]
        //[WorldPacketHandler(ClientOpcodes.Unknown_3743, Processing = PacketProcessing.Inplace)]
        //[WorldPacketHandler(ClientOpcodes.OverrideScreenFlash, Processing = PacketProcessing.Inplace)]
        //[WorldPacketHandler(ClientOpcodes.GetAccountCharacterList, Processing = PacketProcessing.Inplace)]
        //[WorldPacketHandler(ClientOpcodes.QueuedMessagesEnd, Processing = PacketProcessing.Inplace)]
        void HandleUnused(EmptyPaket empty) { }        
    }
}
