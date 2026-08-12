using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Adds Level 1 / 2 / 3 buttons under the New Game panel at runtime if missing.
/// Wire MainMenu.StartLevel1/2/3 in the Inspector for a polished menu, or rely on this.
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    public MainMenu mainMenu;
    public Transform buttonParent;

    void Start()
    {
        if (mainMenu == null)
            mainMenu = FindFirstObjectByType<MainMenu>();

        if (mainMenu == null)
            return;

        if (buttonParent == null && mainMenu.NewGamePanel != null)
            buttonParent = mainMenu.NewGamePanel.transform;

        if (buttonParent == null)
            return;

        // If the panel already has multiple buttons, don't clutter it.
        Button[] existing = buttonParent.GetComponentsInChildren<Button>(true);
        if (existing != null && existing.Length >= 3)
            return;

        CreateButton("Level 1 — Tutorial", () => mainMenu.StartLevel1());
        CreateButton("Level 2 — Crossfire", () => mainMenu.StartLevel2());
        CreateButton("Level 3 — Boss", () => mainMenu.StartLevel3());
    }

    void CreateButton(string label, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(buttonParent, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.14f, 0.9f);

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(action);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(420f, 48f);

        VerticalLayoutGroup layout = buttonParent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = buttonParent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 12f;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childControlHeight = false;
            layout.childControlWidth = false;
        }

        ContentSizeFitter fitter = buttonParent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = buttonParent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 26f;
        tmp.color = Color.white;
        RectTransform trt = tmp.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }
}
