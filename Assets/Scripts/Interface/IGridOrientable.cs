using UnityEngine;

public interface IGridOrientable
{
    Quaternion GetGridRotation();

    Vector3 GetCellOffset();
}
