using UnityEngine;

[ExecuteAlways]
public class SkyCubemapCapture : MonoBehaviour
{
    public Camera captureCamera;
    public RenderTexture cubeRT;
    public bool updateEveryFrame = true;

    void LateUpdate()
    {
        if (!updateEveryFrame) return;
        Capture();
    }

    [ContextMenu("Capture Cubemap")]
    public void Capture()
    {
        if (captureCamera == null || cubeRT == null) return;

        captureCamera.RenderToCubemap(cubeRT);
    }
}