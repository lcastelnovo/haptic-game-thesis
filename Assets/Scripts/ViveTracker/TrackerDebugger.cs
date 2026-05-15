using UnityEngine;
using Valve.VR;
using System.Text;
using System.Collections.Generic;

public class TrackerDebugger : MonoBehaviour
{
    private CVRSystem vrSystem;
    private TrackedDevicePose_t[] poses;
    public bool ShowDebugObjects = false;
    public List<Renderer> renderersToDisable;

    void Start()
    {
        foreach (var r in renderersToDisable)
        {
            r.enabled = ShowDebugObjects;
        }

        EVRInitError initError = EVRInitError.None;
        vrSystem = OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Other);

        Debug.Log($"OpenVR init result: {initError}");

        if (initError != EVRInitError.None)
        {
            Debug.LogError("OpenVR failed to initialize.");
            return;
        }

        poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
    }

    void Update()
    {
        if (vrSystem == null)
            return;

        vrSystem.GetDeviceToAbsoluteTrackingPose(
            ETrackingUniverseOrigin.TrackingUniverseStanding,
            0,
            poses
        );

        for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
        {
            if (!vrSystem.IsTrackedDeviceConnected(i))
                continue;

            if (vrSystem.GetTrackedDeviceClass(i) != ETrackedDeviceClass.GenericTracker)
                continue;

            string serial = GetDeviceSerial(i);

            if (!poses[i].bPoseIsValid)
            {
                Debug.Log($"Tracker {i} | {serial} | pose invalid");
                continue;
            }

            var m = poses[i].mDeviceToAbsoluteTracking;

            Vector3 position = new Vector3(m.m3, m.m7, -m.m11);

            Debug.Log($"Tracker {i} | {serial} | {position}");
        }
    }

    string GetDeviceSerial(uint index)
    {
        var error = ETrackedPropertyError.TrackedProp_Success;

        uint capacity = vrSystem.GetStringTrackedDeviceProperty(
            index,
            ETrackedDeviceProperty.Prop_SerialNumber_String,
            null,
            0,
            ref error
        );

        if (capacity == 0)
            return "<no serial>";

        StringBuilder sb = new StringBuilder((int)capacity);

        vrSystem.GetStringTrackedDeviceProperty(
            index,
            ETrackedDeviceProperty.Prop_SerialNumber_String,
            sb,
            capacity,
            ref error
        );

        return sb.ToString();
    }

    void OnDestroy()
    {
        if (vrSystem != null)
        {
            OpenVR.Shutdown();
            vrSystem = null;
        }
    }
}