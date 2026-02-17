using UnityEngine;

public class TriangleGridOrientation : MonoBehaviour, IGridOrientable
{
    private float cumulativeZ = 360f;

    // Chiamato da HandGrabController ogni volta che l'oggetto ruota in mano
    public void AddRotation(float zDelta)
    {
        cumulativeZ -= zDelta;
        cumulativeZ %= 360f;
        if (cumulativeZ < 0f) cumulativeZ += 360f;
    }

    public Quaternion GetGridRotation()
    {
        float snappedZ = SnapToNearest90(cumulativeZ);
        return Quaternion.Euler(270f, 135f, snappedZ);
    }

    public Vector3 GetCellOffset()
    {
        float zRot = SnapToNearest90(cumulativeZ);

        if (Mathf.Approximately(zRot, 0f)) return new Vector3(-0.025f, 0f, 0.025f);
        if (Mathf.Approximately(zRot, 90f)) return new Vector3(0.025f, 0f, 0.025f);
        if (Mathf.Approximately(zRot, 180f)) return new Vector3(0.025f, 0f, -0.025f);
        if (Mathf.Approximately(zRot, 270f)) return new Vector3(-0.025f, 0f, -0.025f);
        if (Mathf.Approximately(zRot, 360f)) return new Vector3(-0.025f, 0f, 0.025f);

        return Vector3.zero;
    }

    private float SnapToNearest90(float angle)
    {
        angle = angle % 360;
        if (angle < 0) angle += 360;
        return Mathf.Round(angle / 90f) * 90f;
    }
}
