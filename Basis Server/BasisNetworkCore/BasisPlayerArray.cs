using LiteNetLib;
using System;

namespace BasisNetworkCore
{
    public class BasisPlayerArray
    {
        private static readonly object PlayerArrayLock = new object();
        private static NetPeer[] PlayerArray = new NetPeer[1024];
        private static int PlayerCount = 0;

        // Reusable snapshot buffer
        private static NetPeer[] SnapshotBuffer = new NetPeer[1024];

        public static void AddPlayer(NetPeer player)
        {
            lock (PlayerArrayLock)
            {
                if (PlayerCount < PlayerArray.Length)
                {
                    PlayerArray[PlayerCount++] = player;
                }
            }
        }

        public static void RemovePlayer(NetPeer player)
        {
            lock (PlayerArrayLock)
            {
                for (int i = 0; i < PlayerCount; i++)
                {
                    if (PlayerArray[i] == player)
                    {
                        PlayerArray[i] = PlayerArray[--PlayerCount]; // Swap with last element
                        PlayerArray[PlayerCount] = null; // Clear the last slot
                        break;
                    }
                }
            }
        }

        public static ReadOnlySpan<NetPeer> GetSnapshot()
        {
            lock (PlayerArrayLock)
            {
                // Copy current players into the reusable snapshot buffer
                Array.Copy(PlayerArray, 0, SnapshotBuffer, 0, PlayerCount);

                // Return a span of the active players
                return new ReadOnlySpan<NetPeer>(SnapshotBuffer, 0, PlayerCount);
            }
        }
    }
}