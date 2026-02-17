using UnityEngine;

namespace WeArt.TouchUI
{
    /// <summary>
    /// StartPanel — defines which WeArt Panel will be shown at the start of session.
    /// </summary>
    public class StartPanel : MonoBehaviour
    {
        [SerializeField] internal WristPanel wristPanel;
        
        public bool BluetoothPanel;
        public bool StatusDisplay;
        public bool ActuationPanel;
        public bool WristPanel;
        
        private void Start()
        {
            if (!BluetoothPanel) wristPanel.BluetoothButton.onClick.Invoke();
            if (!ActuationPanel) wristPanel.ActuationButton.onClick.Invoke();
            if (!StatusDisplay) wristPanel.StatusButton.onClick.Invoke();
            if (!WristPanel) wristPanel.gameObject.SetActive(false);
        }
    }
}
