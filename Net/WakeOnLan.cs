using System;
using System.Collections.Generic;
using System.Text;

namespace Tsukikage.Net
{
    public static class WakeOnLan
    {
        /// <summary>
        /// マジックパケットを作る
        /// </summary>
        /// <param name="macAddress">Length must be 6.</param>
        /// <returns>102 bytes Magic Packet</returns>
        public static byte[] MakeMagicPacket(params byte[] macAddress)
        {
            // Make MagicPacket
            if (macAddress.Length != 6)
                throw new ArgumentException("macAddress.Length != 6");

            List<byte> packet = new List<byte>(102);

            packet.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, });
            for (int i = 0; i < 16; i++)
                packet.AddRange(macAddress);

            return packet.ToArray();
        }

        /// <summary>
        /// Broadcast MagicPacket to Local Domain.
        /// </summary>
        /// <param name="macAddress">Length must be 6.</param>
        public static void BroadcastMagicPacket(params byte[] macAddress)
        {
            byte[] packet = MakeMagicPacket(macAddress);
            using (var socket = new System.Net.Sockets.UdpClient(0))
                socket.Send(packet, 102, new System.Net.IPEndPoint(0xFFFFFFFF, 7));
        }
    }
}
