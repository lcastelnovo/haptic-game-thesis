using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class CalibrationInfoField : MonoBehaviour
{
    [SerializeField] GameObject window;
    [SerializeField] GameObject startWindow, startWindowPC;
    [SerializeField] GameObject startAutomaticCalibrationWindow;
    [SerializeField] Text textInfo;
    [SerializeField] GameObject loadingCircle;
    [SerializeField] GameObject calibrationExample;
    [SerializeField] Text textTime;
    [SerializeField] GameObject icon;
    [SerializeField] Sprite spriteSuccess;
    [SerializeField] Sprite spriteFailed;
    [SerializeField] Sprite spriteWarning;

    private bool loading = false;
    private Image _icon;
    public enum MessageType { None, Warning, Error, Success };

    private void Awake()
    {
        _icon = icon.GetComponent<Image>();
    }

    public void ShowStartAutomaticCalibrationWindow(bool state)
    {
        startAutomaticCalibrationWindow.SetActive(state);
        startWindow.SetActive(!state);
        window.SetActive(!state);
        startWindowPC.SetActive(!state);
    }

    public void ShowStartWindow(bool state)
    {
        startAutomaticCalibrationWindow.SetActive(false);
        startWindow.SetActive(state);
        window.SetActive(!state);
        startWindowPC.SetActive(false);
    }

    public void ShowStartWindowPC(bool state)
    {
        startWindowPC.SetActive(state);
        startAutomaticCalibrationWindow.SetActive(false);
        startWindow.SetActive(false);
        window.SetActive(!state);
    }
  

    public void Show(string message, bool showLoading)
    {
        textInfo.text = message;

        loadingCircle.SetActive(showLoading);
        textTime.gameObject.SetActive(showLoading);
        loading = showLoading;

        calibrationExample.SetActive(false);

        window.SetActive(true);
    }

    public void Show(string message, bool showLoading, bool showTutorial)
    {
        textInfo.text = message;
        loadingCircle.SetActive(showLoading);
        textTime.gameObject.SetActive(showLoading);
        loading = showLoading;

        calibrationExample.SetActive(showTutorial);
        window.SetActive(true);
    }

    public void Show(string message, bool showLoading, float hideAfterSeconds)
    {
        Show(message, showLoading);
        calibrationExample.SetActive(false);
        StartCoroutine(coroutine_DelayHide(hideAfterSeconds));
    }

    public void Show(string message, bool showLoading, bool showTutorial, float hideAfterSeconds)
    {
        Show(message, showLoading, showTutorial);
        StartCoroutine(coroutine_DelayHide(hideAfterSeconds));
    }

    public void Hide()
    {
        startAutomaticCalibrationWindow.SetActive(false);
        window.SetActive(false);
        startWindow.SetActive(false);
        startWindowPC.SetActive(false);
    }

    /// <summary>
    /// Set the time text during the loading
    /// </summary>
    /// <param name="time"></param>
    public void SetTime(float time)
    {
        string result = time.ToString("F0");
        textTime.text = result;
    }

    /// <summary>
    /// Set a specific icon to show in the canvas. Select none to hide it
    /// </summary>
    /// <param name="type"></param>
    public void SetIcon(MessageType type)
    {
        switch (type)
        {
            case MessageType.None:
                icon.SetActive(false);
                break;

            case MessageType.Success:
                icon.SetActive(true);
                _icon.sprite = spriteSuccess;
                break;

            case MessageType.Warning:
                icon.SetActive(true);
                _icon.sprite = spriteWarning;
                break;

            case MessageType.Error:
                icon.SetActive(true);
                _icon.sprite = spriteFailed;
                break;
        }
    }

    private void Update()
    {
        if (loading)
            loadingCircle.transform.Rotate(0f, 0f, -60f * Time.deltaTime);
    }

    IEnumerator coroutine_DelayHide(float time)
    {
        yield return new WaitForSeconds(time);
        Hide();
    }
}
