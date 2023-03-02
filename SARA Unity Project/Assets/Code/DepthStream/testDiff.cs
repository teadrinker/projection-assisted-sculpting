
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testDiff : MonoBehaviour
{
    // Start is called before the first frame update
    void OnEnable()
    {
        teadrinker.DiffRLE.RunTests();
    }


}
