using WeArt.Core;
using System.Collections.Generic;

namespace WeArt.Bluetooth
{
   /// <summary>
   /// Utility class to keep the info about remembered TD devices.
   /// </summary>
   [System.Serializable]
   public class RememberedDevices
   {
      public List<string> DeviceMacAddresses;
      public List<HandSide> HandSides;

      public RememberedDevices()
      {
         DeviceMacAddresses = new List<string>();
         HandSides = new List<HandSide>();
      }

      /// <summary>
      /// Add device to remember for next sessions.
      /// </summary>
      /// <param name="deviceMacAddress"></param>
      /// <param name="handSide"></param>
      public void AddDevice(string deviceMacAddress, HandSide handSide)
      {
         DeviceMacAddresses.Add(deviceMacAddress);
         HandSides.Add(handSide);
      }

      /// <summary>
      /// Removes all devices from storage.
      /// </summary>
      public void Clear()
      {
         DeviceMacAddresses.Clear();
         HandSides.Clear();
      }
   }

}
