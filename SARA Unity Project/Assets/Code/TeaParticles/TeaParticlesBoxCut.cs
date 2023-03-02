
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace teadrinker
{

    public class TeaParticlesBoxCut : MonoBehaviour
    {
        public TeaParticles target;
        public float fadeDistance = 0.1f;
        public Vector3 fadeDistanceMul = Vector3.one;

        void OnDisable() { 
            if (target != null)
                target.DisableBoxCut();
        }
        // Update is called once per frame
        void Update()
        {
            if (target != null)
                target.SetBoxCut(transform.position, transform.rotation, transform.localScale * 0.5f, fadeDistanceMul * fadeDistance);
            else
                Debug.LogError("TeaParticlesBoxCut, target not set");
        }
    }
}
