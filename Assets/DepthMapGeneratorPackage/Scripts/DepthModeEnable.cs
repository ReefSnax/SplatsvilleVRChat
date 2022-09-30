using UnityEngine;

public class DepthModeEnable : MonoBehaviour
{
    // References
    public Camera TargetCam;

    // Fields
    public DepthTextureMode Mode;

    void Start()
    {
        if(TargetCam != null)
        {
            TargetCam.depthTextureMode = Mode;
        }
    }
}
