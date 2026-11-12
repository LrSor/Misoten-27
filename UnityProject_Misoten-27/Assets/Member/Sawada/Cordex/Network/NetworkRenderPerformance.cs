using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sawada.Cordex
{
    // Runtime-only rendering overrides. Never edit the project's shared URP assets.
    public sealed class NetworkRenderPerformance : IDisposable
    {
        readonly RenderPipelineAsset previousQualityPipeline;
        readonly UniversalRenderPipelineAsset sourcePipeline;
        readonly Dictionary<UniversalAdditionalCameraData, bool> cameraPostProcessing = new Dictionary<UniversalAdditionalCameraData, bool>();
        readonly float[] frameTimes = new float[512];
        readonly float[] sortedFrameTimes = new float[512];
        readonly Sample sample = new Sample();
        UniversalRenderPipelineAsset lightweightPipeline;
        StreamWriter trace;
        int frames, storedFrames, slowFrames;
        float elapsed;
        bool disposed;
        public bool Lightweight { get; private set; }
        public bool CanConfigure => sourcePipeline != null && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        public string Status { get; private set; } = "FPS: measuring...";

        [Serializable]
        sealed class Sample
        {
            public float time, fps, averageFrameMs, p95FrameMs, maxFrameMs, renderScale, shadowDistance;
            public int frames, framesOver33Ms, shadowCascades, width, height;
            public bool lightweight, rendering, cameraPostProcessing;
        }

        public NetworkRenderPerformance(string[] args)
        {
            previousQualityPipeline = QualitySettings.renderPipeline;
            sourcePipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            int profileIndex = Array.IndexOf(args, "--net-render-profile");
            bool standard = profileIndex >= 0 && profileIndex + 1 < args.Length && args[profileIndex + 1] == "standard";
            SetLightweight(!standard);
            int traceIndex = Array.IndexOf(args, "--net-performance-trace");
            if (traceIndex >= 0 && traceIndex + 1 < args.Length)
            {
                try { trace = new StreamWriter(args[traceIndex + 1]) { AutoFlush = true }; }
                catch (Exception e) { Debug.LogWarning("[NetworkRenderPerformance] Trace: " + e.Message); }
            }
        }

        public void RegisterCamera(Camera camera)
        {
            if (camera == null || !camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) return;
            if (!cameraPostProcessing.ContainsKey(data)) cameraPostProcessing.Add(data, data.renderPostProcessing);
            data.renderPostProcessing = Lightweight ? false : cameraPostProcessing[data];
        }

        public void UnregisterCamera(Camera camera)
        {
            if (camera == null || !camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) return;
            if (!cameraPostProcessing.TryGetValue(data, out bool previous)) return;
            data.renderPostProcessing = previous;
            cameraPostProcessing.Remove(data);
        }

        public void SetLightweight(bool enabled)
        {
            if (disposed) return;
            Lightweight = enabled && CanConfigure;
            if (Lightweight)
            {
                if (lightweightPipeline == null)
                {
                    lightweightPipeline = UnityEngine.Object.Instantiate(sourcePipeline);
                    lightweightPipeline.name = sourcePipeline.name + " (network runtime lightweight)";
                    lightweightPipeline.hideFlags = HideFlags.HideAndDontSave;
                    // Keep the window/UI resolution and all gameplay geometry unchanged.
                    lightweightPipeline.renderScale = Mathf.Min(sourcePipeline.renderScale, .8f);
                    lightweightPipeline.msaaSampleCount = 1;
                    lightweightPipeline.shadowDistance = Mathf.Min(sourcePipeline.shadowDistance, 20f);
                    lightweightPipeline.shadowCascadeCount = 1;
                }
                QualitySettings.renderPipeline = lightweightPipeline;
            }
            else if (lightweightPipeline != null && QualitySettings.renderPipeline == lightweightPipeline)
                QualitySettings.renderPipeline = previousQualityPipeline;
            foreach (var entry in cameraPostProcessing)
                if (entry.Key != null) entry.Key.renderPostProcessing = Lightweight ? false : entry.Value;
            // Do not mix frames collected under different profiles.
            frames = storedFrames = slowFrames = 0; elapsed = 0;
            sample.maxFrameMs = 0;
            Status = "FPS: measuring...";
            Debug.Log("[NetworkRenderPerformance] Profile: " + (Lightweight ? "lightweight" : "standard") +
                "; rendering=" + (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null));
        }

        public void Tick(float deltaTime)
        {
            if (disposed || deltaTime <= 0) return;
            float ms = deltaTime * 1000;
            frames++; elapsed += deltaTime;
            if (ms > 1000f / 30) slowFrames++;
            if (storedFrames < frameTimes.Length) frameTimes[storedFrames++] = ms;
            sample.maxFrameMs = Mathf.Max(sample.maxFrameMs, ms);
            if (elapsed < 1 || storedFrames == 0) return;
            Array.Copy(frameTimes, sortedFrameTimes, storedFrames);
            Array.Sort(sortedFrameTimes, 0, storedFrames);
            sample.time = Time.realtimeSinceStartup;
            sample.fps = frames / elapsed; sample.averageFrameMs = elapsed * 1000 / frames;
            sample.p95FrameMs = sortedFrameTimes[Mathf.Clamp(Mathf.CeilToInt(storedFrames * .95f) - 1, 0, storedFrames - 1)];
            sample.frames = frames; sample.framesOver33Ms = slowFrames;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            sample.renderScale = pipeline != null ? pipeline.renderScale : 1;
            sample.shadowDistance = pipeline != null ? pipeline.shadowDistance : 0;
            sample.shadowCascades = pipeline != null ? pipeline.shadowCascadeCount : 0;
            sample.width = Screen.width; sample.height = Screen.height;
            sample.lightweight = Lightweight; sample.rendering = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            sample.cameraPostProcessing = false;
            foreach (var camera in cameraPostProcessing.Keys)
                if (camera != null && camera.GetComponent<Camera>().isActiveAndEnabled && camera.renderPostProcessing)
                    sample.cameraPostProcessing = true;
            Status = $"FPS: {sample.fps:F1} | frame P95: {sample.p95FrameMs:F1} ms | >33ms: {slowFrames}/{frames}";
            trace?.WriteLine(JsonUtility.ToJson(sample));
            frames = storedFrames = slowFrames = 0; elapsed = 0; sample.maxFrameMs = 0;
        }

        public void Dispose()
        {
            if (disposed) return;
            SetLightweight(false);
            disposed = true;
            if (lightweightPipeline != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(lightweightPipeline);
                else UnityEngine.Object.DestroyImmediate(lightweightPipeline);
            }
            cameraPostProcessing.Clear();
            trace?.Dispose(); trace = null;
        }
    }
}
