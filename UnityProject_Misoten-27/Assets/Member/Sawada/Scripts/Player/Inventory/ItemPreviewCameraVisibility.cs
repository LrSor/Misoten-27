
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class ItemPreviewCameraVisibility : MonoBehaviour
{
    [Header("表示するカメラ")]
    [SerializeField]
    private Camera m_targetCamera;

    private Renderer[] m_renderers;

    private void Awake()
    {
        m_renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        if (m_renderers == null)
        {
            m_renderers = GetComponentsInChildren<Renderer>(true);
        }

        // Built-in Render Pipeline
        Camera.onPreCull += HandleCameraPreCull;

        // URP / HDRP
        RenderPipelineManager.beginCameraRendering +=
            OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        Camera.onPreCull -= HandleCameraPreCull;

        RenderPipelineManager.beginCameraRendering -=
            OnBeginCameraRendering;

        foreach (Renderer renderer in m_renderers)
        {
            if (renderer != null)
            {
                renderer.forceRenderingOff = false;
            }
        }
    }

    private void HandleCameraPreCull(Camera camera)
    {
        SetVisibility(camera);
    }

    private void OnBeginCameraRendering(
        ScriptableRenderContext context,
        Camera camera)
    {
        SetVisibility(camera);
    }

    private void SetVisibility(Camera camera)
    {
        bool isVisible = camera == m_targetCamera;

        foreach (Renderer renderer in m_renderers)
        {
            if (renderer != null)
            {
                renderer.forceRenderingOff = !isVisible;
            }
        }
    }

    public void SetTargetCamera(Camera camera)
    {
        m_targetCamera = camera;
    }
}
