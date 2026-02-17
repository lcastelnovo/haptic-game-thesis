using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Linq;

namespace WeArt.Core
{
    /// <summary>
    /// Utility class for networking operations
    /// </summary>
    public static class WeArtNetwork
    {

        private static string _localIpAddress;

        /// <summary>
        /// The local ip address of the main network interface
        /// </summary>
        public static string LocalIPAddress
        {
            get
            {
                return WeArtConstants.ipLocalHost;
            }
        }
    }
}