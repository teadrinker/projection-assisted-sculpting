using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeaImgTest : MonoBehaviour
{
    // Start is called before the first frame update
    void OnEnable()
    {
        var ctx = new teadrinker.ImgContext();
        ctx.RunTests();
    }


}
