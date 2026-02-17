using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CalibrationPhrases : MonoBehaviour
{
    public static CalibrationPhrases instance;

    public string OnStart = "Please put your hand here for calibration";
    public string OnHandEnter = "Keep your hands still!";
    public string OnHandExit = "You moved you hand. Repositioning...";
    public string OnSuccess = "Great! Calibration is finished";
    public string OnCalibrationError = "Calibration was failed. Retry to repositioning...";
    public string OnRequestTwoHands = "Please hold both hands simultaneously for calibration";
    public string OnTimeout = "Timeout error. Please retry";

    private void Awake()
    {
        if(instance == null)
            instance = this;
    }
}
