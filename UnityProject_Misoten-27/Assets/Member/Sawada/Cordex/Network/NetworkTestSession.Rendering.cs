using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sawada.Cordex
{
    public sealed partial class NetworkTestSession
    {
        [Header("Local view culling")]
        [Tooltip("各端末のPlayerCameraの視界外にあるメッシュの描画だけを省略します。通信・物理・NPCは継続します。視界外の影も省略される場合があります。")]
        [SerializeField] bool cullOutsideLocalView = true;
        [Tooltip("視界の境界に残す余白（メートル）。画面端の影や大きなメッシュの消失が気になる場合は増やしてください。")]
        [SerializeField, Min(0f)] float localViewCullingPadding = 8f;

        readonly Plane[] localViewPlanes = new Plane[6];
        readonly List<Renderer> localViewRenderers = new List<Renderer>();
        readonly List<Renderer> localViewSuppressed = new List<Renderer>();
        float localViewRefreshAt;
        Camera localViewRenderingCamera;

        // These are local diagnostics, never synchronized to other players.
        public int ViewCullingCandidates { get; private set; }
        public int ViewCullingHidden { get; private set; }

        void OnEnable()
        {
            localViewRefreshAt = 0f;
            RenderPipelineManager.beginCameraRendering += BeginLocalViewRendering;
            RenderPipelineManager.endCameraRendering += EndLocalViewRendering;
            RenderPipelineManager.endContextRendering += EndLocalViewContext;
        }

        void StopLocalViewCulling()
        {
            RenderPipelineManager.beginCameraRendering -= BeginLocalViewRendering;
            RenderPipelineManager.endCameraRendering -= EndLocalViewRendering;
            RenderPipelineManager.endContextRendering -= EndLocalViewContext;
            RestoreLocalViewRendering();
            localViewRenderers.Clear();
            ViewCullingCandidates = ViewCullingHidden = 0;
        }

        void BeginLocalViewRendering(ScriptableRenderContext context, Camera camera)
        {
            // Restore before any other camera, including previews, reflections and Scene view.
            RestoreLocalViewRendering();
            if (!cullOutsideLocalView || camera != view || !camera.isActiveAndEnabled
                || localId <= 0 || m_miniGameViewActive || camera.cameraType != CameraType.Game)
                return;

            if (Time.unscaledTime >= localViewRefreshAt)
            {
                localViewRenderers.Clear();
                foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    // Exclude UI, particles, trails and preview objects. Runtime spawns are
                    // discovered within one second; until then Unity's normal culling applies.
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                    if (renderer.gameObject.scene != gameObject.scene
                        || renderer.GetComponentInParent<Canvas>(true) != null) continue;
                    localViewRenderers.Add(renderer);
                }
                localViewRefreshAt = Time.unscaledTime + 1f;
            }

            GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix, localViewPlanes);
            float padding = Mathf.Max(0f, localViewCullingPadding);
            for (int i = 0; i < localViewPlanes.Length; i++)
            {
                var plane = localViewPlanes[i];
                plane.distance += padding;
                localViewPlanes[i] = plane;
            }

            localViewRenderingCamera = camera;
            ViewCullingCandidates = ViewCullingHidden = 0;
            foreach (var renderer in localViewRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                    || renderer.forceRenderingOff || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                    continue;
                ViewCullingCandidates++;
                if (GeometryUtility.TestPlanesAABB(localViewPlanes, renderer.bounds)) continue;
                // Never deactivate GameObjects or disable their simulation/animation.
                // Only flags changed by us are restored; existing hidden objects stay hidden.
                localViewSuppressed.Add(renderer);
                renderer.forceRenderingOff = true;
                ViewCullingHidden++;
            }
        }

        void EndLocalViewRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == localViewRenderingCamera) RestoreLocalViewRendering();
        }

        void EndLocalViewContext(ScriptableRenderContext context, List<Camera> cameras)
        {
            RestoreLocalViewRendering();
        }

        void RestoreLocalViewRendering()
        {
            foreach (var renderer in localViewSuppressed)
                if (renderer != null) renderer.forceRenderingOff = false;
            localViewSuppressed.Clear();
            localViewRenderingCamera = null;
        }
    }
}
