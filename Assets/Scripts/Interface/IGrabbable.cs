using UnityEngine;

public interface IGrabbable
{
    void OnGrab(Transform grabPoint);
    void OnRelease(Vector3 releasePosition);
    bool CanBeInteractedWith { get; }
}
