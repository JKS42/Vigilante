using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Draws selected world meshes above the tutorial dim so cracked walls
/// and a broken piece stay readable while the rest of the screen is dark.
/// </summary>
public class TutorialWorldHighlight : MonoBehaviour
{
    const int HighlightLayer = 6;

    struct SavedObject
    {
        public GameObject gameObject;
        public int layer;
    }

    static TutorialWorldHighlight instance;

    readonly List<SavedObject> saved = new List<SavedObject>();
    Camera mainCamera;
    int savedMask;
    Camera highlightCamera;
    RenderTexture texture;
    RawImage view;
    Material compositeMaterial;

    public static void ShowIntactWalls()
    {
        Break[] breaks = Object.FindObjectsByType<Break>(FindObjectsSortMode.None);
        List<Renderer> renderers = new List<Renderer>();
        for (int i = 0; i < breaks.Length; i++)
        {
            Break piece = breaks[i];
            if (piece == null || !piece.IsIntactWall)
                continue;
            AddRenderers(piece.gameObject, renderers);
        }

        Show(renderers);
    }

    public static void ShowPiece(Break piece)
    {
        List<Renderer> renderers = new List<Renderer>();
        if (piece != null)
            AddRenderers(piece.gameObject, renderers);
        Show(renderers);
    }

    public static void ClearHighlight()
    {
        if (instance != null)
            instance.Clear();
    }

    static void Show(List<Renderer> renderers)
    {
        if (renderers == null || renderers.Count == 0)
        {
            ClearHighlight();
            return;
        }

        Ensure().Present(renderers);
    }

    static TutorialWorldHighlight Ensure()
    {
        if (instance != null)
            return instance;

        GameObject host = new GameObject("TutorialWorldHighlight");
        instance = host.AddComponent<TutorialWorldHighlight>();
        return instance;
    }

    static void AddRenderers(GameObject root, List<Renderer> renderers)
    {
        if (root == null)
            return;

        Renderer[] found = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                renderers.Add(found[i]);
        }
    }

    void Present(List<Renderer> renderers)
    {
        Clear();
        if (Shader.Find("Vigilante/TutorialComposite") == null)
            return;

        mainCamera = Camera.main;
        if (mainCamera == null || renderers.Count == 0)
            return;

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            saved.Add(new SavedObject
            {
                gameObject = renderer.gameObject,
                layer = renderer.gameObject.layer
            });
            renderer.gameObject.layer = HighlightLayer;
        }

        savedMask = mainCamera.cullingMask;
        mainCamera.cullingMask = savedMask & ~(1 << HighlightLayer);

        int width = Mathf.Max(16, Screen.width);
        int height = Mathf.Max(16, Screen.height);
        texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
        texture.Create();

        GameObject cameraObject = new GameObject("TutorialHighlightCamera");
        cameraObject.transform.SetParent(mainCamera.transform, false);
        highlightCamera = cameraObject.AddComponent<Camera>();
        highlightCamera.CopyFrom(mainCamera);
        highlightCamera.cullingMask = 1 << HighlightLayer;
        highlightCamera.clearFlags = CameraClearFlags.SolidColor;
        highlightCamera.backgroundColor = Color.magenta;
        highlightCamera.allowMSAA = false;
        highlightCamera.targetTexture = texture;
        highlightCamera.depth = mainCamera.depth + 1f;

        UniversalAdditionalCameraData extra = highlightCamera.GetComponent<UniversalAdditionalCameraData>();
        if (extra == null)
            extra = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        extra.renderType = CameraRenderType.Base;
        extra.renderPostProcessing = false;
        extra.cameraStack.Clear();

        view = CreateView();
    }

    RawImage CreateView()
    {
        GameObject canvasGo = new GameObject("TutorialHighlightView");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject imageGo = new GameObject("Image");
        imageGo.transform.SetParent(canvasGo.transform, false);
        RawImage image = imageGo.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        image.uvRect = new Rect(0f, 1f, 1f, -1f);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Shader composite = Shader.Find("Vigilante/TutorialComposite");
        if (composite != null)
        {
            compositeMaterial = new Material(composite);
            image.material = compositeMaterial;
        }

        return image;
    }

    void LateUpdate()
    {
        if (highlightCamera == null || mainCamera == null)
            return;

        highlightCamera.fieldOfView = mainCamera.fieldOfView;
        highlightCamera.cullingMask = 1 << HighlightLayer;
    }

    void OnDestroy()
    {
        Clear();
        if (instance == this)
            instance = null;
    }

    void Clear()
    {
        for (int i = 0; i < saved.Count; i++)
        {
            SavedObject item = saved[i];
            if (item.gameObject != null)
                item.gameObject.layer = item.layer;
        }
        saved.Clear();

        if (mainCamera != null)
            mainCamera.cullingMask = savedMask;
        mainCamera = null;

        if (highlightCamera != null)
            Destroy(highlightCamera.gameObject);
        highlightCamera = null;

        if (view != null)
            Destroy(view.canvas.gameObject);
        view = null;

        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        if (compositeMaterial != null)
            Destroy(compositeMaterial);
        compositeMaterial = null;
    }
}
