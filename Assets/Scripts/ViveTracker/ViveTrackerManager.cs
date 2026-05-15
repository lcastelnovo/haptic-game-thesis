using UnityEngine;
using Valve.VR;
using System.Text;
using WeArt.Core;

public class ViveTrackerManager : MonoBehaviour
{
    [Header("Tracker Serials")]
    public string leftTrackerSerial;
    public string rightTrackerSerial;

    [Header("Targets")]
    public Transform leftTrackerTarget;
    public Transform rightTrackerTarget;

    [Header("Options")]
    public Transform trackingRoot;
    public ETrackingUniverseOrigin trackingOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding;

    private CVRSystem vrSystem;
    private TrackedDevicePose_t[] poses;

    private int leftIndex = -1;
    private int rightIndex = -1;

    void Start()
    {
        EVRInitError initError = EVRInitError.None;
        vrSystem = OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Other);

        Debug.Log($"OpenVR init result: {initError}");

        if (initError != EVRInitError.None)
        {
            Debug.LogError("OpenVR failed to initialize.");
            return;
        }

        poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        RefreshTrackerAssignments();
    }

    void Update()
    {
        if (vrSystem == null)
            return;

        vrSystem.GetDeviceToAbsoluteTrackingPose(trackingOrigin, 0, poses);

        if (leftIndex >= 0)
            ApplyPose(leftIndex, leftTrackerTarget, HandSide.Left);

        if (rightIndex >= 0)
            ApplyPose(rightIndex, rightTrackerTarget, HandSide.Right);
    }

    [ContextMenu("Refresh Tracker Assignments")]
    public void RefreshTrackerAssignments()
    {
        leftIndex = -1;
        rightIndex = -1;

        for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
        {
            if (!vrSystem.IsTrackedDeviceConnected(i))
                continue;

            if (vrSystem.GetTrackedDeviceClass(i) != ETrackedDeviceClass.GenericTracker)
                continue;

            string serial = GetDeviceSerial(i);

            if (serial == leftTrackerSerial)
                leftIndex = (int)i;

            if (serial == rightTrackerSerial)
                rightIndex = (int)i;
        }

        Debug.Log($"Left Vive tracker index: {leftIndex} | Right Vive tracker index: {rightIndex}");
    }

    private void ApplyPose(int deviceIndex, Transform target, HandSide handSide)
    {
        if (target == null)
            return;

        if (deviceIndex < 0 || deviceIndex >= poses.Length)
            return;

        if (!poses[deviceIndex].bPoseIsValid)
            return;

        var m = poses[deviceIndex].mDeviceToAbsoluteTracking;

        Vector3 worldPosition = new Vector3(
            m.m3,
            m.m7,
            -m.m11
        );


        Matrix4x4 matrix = new Matrix4x4();
        matrix.m00 = m.m0; matrix.m01 = m.m1; matrix.m02 = -m.m2; matrix.m03 = m.m3;
        matrix.m10 = m.m4; matrix.m11 = m.m5; matrix.m12 = -m.m6; matrix.m13 = m.m7;
        matrix.m20 = -m.m8; matrix.m21 = -m.m9; matrix.m22 = m.m10; matrix.m23 = -m.m11;
        matrix.m30 = 0f; matrix.m31 = 0f; matrix.m32 = 0f; matrix.m33 = 1f;

        Quaternion worldRotation = matrix.rotation;

        if (trackingRoot != null)
        {
            target.localPosition = trackingRoot.InverseTransformPoint(worldPosition);
            target.localRotation = Quaternion.Inverse(trackingRoot.rotation) * worldRotation;
            if(handSide == HandSide.Left)
            {
                target.rotation = target.rotation * Quaternion.Euler(0,0,180);
            }

        }
        else
        {
            target.position = worldPosition;
            target.rotation = worldRotation;
        }
    }

    private string GetDeviceSerial(uint index)
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
            return string.Empty;

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