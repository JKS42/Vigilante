using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ButtonGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TextMeshProUGUI text;

    [Header("Glow")]
    public float glowOn = 1f;
    public float glowOff = 0f;

    [Header("Size")]
    public float sizeIncrease = 4f; // how much bigger on hover
    private float originalSize;

    private Material material;

    void Start()
    {
        material = text.fontMaterial;

        // Store original size
        originalSize = text.fontSize;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        material.SetFloat("_GlowPower", glowOn);
        text.fontSize = originalSize + sizeIncrease;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        material.SetFloat("_GlowPower", glowOff);
        text.fontSize = originalSize;
    }
}
