using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadingUiController : MonoBehaviour
{
    [SerializeField] private float _loadingTime = 2;
    [SerializeField] private GameObject _gO;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void StartLoading()
    {
        StartCoroutine(LoadingCourotine());
    }

    IEnumerator LoadingCourotine()
    {
       _gO.SetActive(true);

        yield return new WaitForSeconds(_loadingTime);

        _gO.SetActive(false);
    }
}
