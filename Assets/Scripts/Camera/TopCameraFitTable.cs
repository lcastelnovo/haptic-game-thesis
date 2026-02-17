using UnityEngine;

[ExecuteAlways] // Esegue anche in editor
public class TopCameraFitTable : MonoBehaviour
{
    public Camera topCamera;           // Assegna qui la TopCamera
    public float tableWidth = 1.5f;    // X
    public float tableLength = 0.8f;   // Z
    public float cameraHeight = 1f;    // Altezza della camera sopra il tavolo
    public float margin = 0.05f;       // Margine attorno al tavolo (opzionale)

    void Update()
    {
        if (topCamera == null) topCamera = GetComponent<Camera>();

        // Assicurati che sia ortografica
        topCamera.orthographic = true;

        // Calcolo aspect ratio
        float aspect = (float)Screen.width / Screen.height;

        // Dimensioni includendo il margine
        float verticalSize = (tableLength / 2f) + margin;
        float horizontalSize = (tableWidth / (2f * aspect)) + margin;

        // Imposta l'orthographic size per vedere tutto
        topCamera.orthographicSize = Mathf.Max(verticalSize, horizontalSize);

        topCamera.transform.position = new Vector3(0f, cameraHeight, 0f);
        topCamera.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
    }
}
