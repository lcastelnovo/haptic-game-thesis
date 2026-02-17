using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class AbacusManager : MonoBehaviour
{
    [SerializeField] List<WeArtSlider> _sliders= new List<WeArtSlider>();
    [SerializeField] ParticleSystem _particleSystem;
    [SerializeField] AudioSource _audioSource;
    private bool _isPuzzleCorrect = false;
    private void Start()
    {
        
    }


    void Update()
    {
        if(!_isPuzzleCorrect)
        {
            if(CheckPuzzle())
            {
                _isPuzzleCorrect = true;
                FinishPuzzle();
            }
        }
        else
        {
            if (!CheckPuzzle())
            {
                _isPuzzleCorrect = false;
            }
        }
    }

    private bool CheckPuzzle()
    {
        if(
            _sliders[0].GetValue() > 0.4f&&
            _sliders[1].GetValue() < -0.4f &&
            _sliders[2].GetValue() < -0.4f &&
            _sliders[3].GetValue() > 0.4f &&
            _sliders[4].GetValue() < -0.4f &&
            _sliders[5].GetValue() > -0.4f &&
            _sliders[5].GetValue() < 0.4f &&
            _sliders[6].GetValue() < -0.4f &&
            _sliders[7].GetValue() > 0.4f &&
            _sliders[8].GetValue() > 0.4f &&
            _sliders[9].GetValue() > -0.4f &&
            _sliders[9].GetValue() < 0.4f &&
            _sliders[10].GetValue() > 0.4f &&
            _sliders[11].GetValue() > 0.4f
            )
            return true;
        else
            return false;

    }

    private void FinishPuzzle()
    {
        _particleSystem.Play();
        _audioSource.Play();
    }
}
