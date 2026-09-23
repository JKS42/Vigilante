using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Press feedback for inverted Vigilante UI buttons: darken + slight shrink.
/// Optional onRelease runs when the pointer is released over the button.
/// </summary>
public class VigilanteUiPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] float pressedScale = 0.94f;
    [SerializeField] float darken = 0.72f;

    Image fill;
    TextMeshProUGUI[] labels;
    Image[] frameImages;
    Color[] frameColors;
    Color[] labelColors;
    Color fillColor = Color.white;
    Vector3 baseScale = Vector3.one;
    bool pressed;
    bool pointerInside;
    System.Action onRelease;

    public void Configure(Image fillImage, TextMeshProUGUI[] labelList, System.Action releaseAction = null)
    {
        fill = fillImage;
        labels = labelList;
        onRelease = releaseAction;
        CacheFrame();
        if (fill != null)
            fillColor = fill.color;
        baseScale = transform.localScale;
        if (baseScale.sqrMagnitude < 0.0001f)
            baseScale = Vector3.one;
    }

    void Awake()
    {
        if (fill == null)
            fill = GetComponent<Image>();
        if (labels == null || labels.Length == 0)
            labels = GetComponentsInChildren<TextMeshProUGUI>(true);
        CacheFrame();
        if (fill != null)
            fillColor = fill.color;
        baseScale = transform.localScale;
    }

    void CacheFrame()
    {
        Transform frame = transform.Find("UiInvertedFrame");
        if (frame == null)
        {
            frameImages = new Image[0];
            frameColors = new Color[0];
            return;
        }

        frameImages = frame.GetComponentsInChildren<Image>(true);
        frameColors = new Color[frameImages.Length];
        for (int i = 0; i < frameImages.Length; i++)
            frameColors[i] = frameImages[i] != null ? frameImages[i].color : Color.white;

        if (labels != null)
        {
            labelColors = new Color[labels.Length];
            for (int i = 0; i < labels.Length; i++)
                labelColors[i] = labels[i] != null ? labels[i].color : Color.black;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerInside = true;
        SetPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        bool wasPressed = pressed;
        SetPressed(false);
        if (wasPressed && pointerInside && onRelease != null)
            onRelease.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        SetPressed(false);
    }

    void OnDisable()
    {
        pointerInside = false;
        SetPressed(false);
    }

    void SetPressed(bool on)
    {
        if (pressed == on)
            return;
        pressed = on;

        transform.localScale = on ? baseScale * pressedScale : baseScale;

        if (fill != null)
            fill.color = on ? fillColor * darken : fillColor;

        if (frameImages != null)
        {
            for (int i = 0; i < frameImages.Length; i++)
            {
                if (frameImages[i] == null)
                    continue;
                frameImages[i].color = on ? frameColors[i] * darken : frameColors[i];
            }
        }

        if (labels != null && labelColors != null)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null)
                    continue;
                labels[i].color = on ? labelColors[i] * darken : labelColors[i];
            }
        }
    }
}
