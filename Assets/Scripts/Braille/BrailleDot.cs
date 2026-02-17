using UnityEngine;

public class BrailleDot : MonoBehaviour
{
    public int dotIndex;

    private float raiseHeight = 0.01f;
    private Vector3 basePosition;

    private void Awake()
    {
        basePosition = transform.localPosition;
    }

    public void SetBasePosition(Vector3 pos)
    {
        basePosition = pos;
        transform.localPosition = pos;
    }

    public void SetRaiseHeight(float height)
    {
        raiseHeight = height;
        basePosition = transform.localPosition;
    }

    public void SetState(bool raised)
    {
        if (raised)
        {
            // puntino attivo e alzato
            gameObject.SetActive(true);
            transform.localPosition = basePosition + Vector3.up * raiseHeight;
        }
        else
        {
            // puntino disattivato (non visibile)
            gameObject.SetActive(false);
        }
    }
}
