using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WEART
{
    public class BunsenActivation : MonoBehaviour
    {
        [SerializeField]
        private GameObject _objectActivation;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.P))
            {
                _objectActivation.SetActive(false);
            }
        }

        public void ActivateEffect()
        {
            _objectActivation.SetActive(!_objectActivation.activeSelf);
        }
    }

}