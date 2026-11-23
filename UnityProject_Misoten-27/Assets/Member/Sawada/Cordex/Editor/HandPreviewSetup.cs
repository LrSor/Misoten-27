using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Sawada.Cordex
{
    public static class HandPreviewSetup
    {
        const string ScenePath = "Assets/Member/Sawada/Scenes/GameUI.unity";
        const string TexturePath = "Assets/Member/Sawada/Cordex/Camera/RT_HandPreview.renderTexture";

        public static void VerifyDisplayClearBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
            var display = cameras.Single(camera => camera.name == "GameViewCamera");
            var preview = cameras.Single(camera => camera.name == "HandPreviewCamera");
            if (display.targetTexture != null || !display.enabled || display.clearFlags != CameraClearFlags.SolidColor ||
                display.backgroundColor.a != 1 || display.targetDisplay != 0 || display.cullingMask != 0 ||
                preview.targetTexture == null || preview.clearFlags != CameraClearFlags.SolidColor)
                throw new Exception("Display/RT clear configuration is invalid");

            // Seed a previous frame, then check that the display camera overwrites it completely.
            var scratch = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
            scratch.Create();
            var previous = RenderTexture.active;
            RenderTexture.active = scratch;
            GL.Clear(true, true, Color.magenta);
            display.targetTexture = scratch;
            display.Render();
            var readback = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            RenderTexture.active = scratch;
            readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            readback.Apply();
            var pixels = readback.GetPixels32();
            if (pixels.Any(pixel => pixel.r > 240 && pixel.b > 240 && pixel.g < 10))
                throw new Exception("Previous-frame pixels survived the display camera clear");
            if (pixels.Any(pixel => pixel.a < 250)) throw new Exception("Display background was not cleared opaque");
            display.targetTexture = null;
            RenderTexture.active = previous;
            scratch.Release();
            UnityEngine.Object.DestroyImmediate(scratch);
            UnityEngine.Object.DestroyImmediate(readback);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) > 0)
                        throw new Exception("Missing script: " + item.name);
            Debug.Log("[GameViewClear] PASS: display camera clears previous pixels, opaque background, independent RT camera, no missing scripts");
        }

        public static void RunGameUIBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(TexturePath);
            if (texture == null) throw new Exception("RT_HandPreview is missing");
            if (scene.GetRootGameObjects().Any(item => item.name == "HandPreviewStage"))
                throw new Exception("Preview is already configured");
            var stage = new GameObject("HandPreviewStage");
            stage.transform.position = new Vector3(1000, 1000, 1000);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "HandPreviewCube";
            cube.transform.SetParent(stage.transform, false);
            cube.transform.localRotation = Quaternion.Euler(15, 30, 0);
            var cameraObject = new GameObject("HandPreviewCamera", typeof(Camera));
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -4);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.targetTexture = texture;
            camera.orthographic = true;
            camera.orthographicSize = 1.1f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 10;
            camera.depth = -10;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasObject = new GameObject("HandPreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var imageObject = new GameObject("HandPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            var image = imageObject.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(320, 320);
            EditorSceneManager.SaveScene(scene);
            VerifyGameUIBatch();
        }

        public static void VerifyGameUIBatch()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var stage = scene.GetRootGameObjects().Single(item => item.name == "HandPreviewStage");
            var camera = stage.GetComponentInChildren<Camera>();
            var cube = stage.transform.Find("HandPreviewCube").GetComponent<MeshRenderer>();
            var canvas = scene.GetRootGameObjects().Single(item => item.name == "HandPreviewCanvas").GetComponent<Canvas>();
            var image = canvas.GetComponentInChildren<RawImage>();
            if (camera.targetTexture == null || image.texture != camera.targetTexture || camera.cullingMask != -1 ||
                image.rectTransform.anchorMin != new Vector2(.5f, .5f) || image.rectTransform.anchoredPosition != Vector2.zero ||
                image.raycastTarget || cube.sharedMaterial == null || !canvas.isActiveAndEnabled)
                throw new Exception("Preview camera, cube or centered UI configuration is invalid");
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) > 0)
                        throw new Exception("Missing script: " + item.name);
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = camera.targetTexture;
            var readback = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;
            Directory.CreateDirectory("Logs/HandPreview");
            File.WriteAllBytes("Logs/HandPreview/cube.png", readback.EncodeToPNG());
            var opaque = readback.GetPixels32().Count(pixel => pixel.a > 32);
            UnityEngine.Object.DestroyImmediate(readback);
            if (opaque < 100) throw new Exception("Cube RenderTexture is empty");
            Debug.Log($"[GameUICubePreview] PASS: cube rendered ({opaque} pixels), RT connected, centered RawImage, no missing scripts, no filtering");
        }
    }
}
