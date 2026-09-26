using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Adds Level 1 / 2 / 3 buttons under the New Game panel at runtime if missing.
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    public MainMenu mainMenu;
    public Transform buttonParent;
    bool built;

    void Start()
    {
        if (mainMenu == null)
            mainMenu = FindFirstObjectByType<MainMenu>();
    }

    public void EnsureLevelButtons()
    {
        if (mainMenu == null)
            mainMenu = FindFirstObjectByType<MainMenu>();
        if (mainMenu == null)
            return;

        if (buttonParent == null && mainMenu.NewGamePanel != null)
            buttonParent = mainMenu.NewGamePanel.transform;
        if (buttonParent == null)
            return;

        if (HasLevelButtons(buttonParent) || built)
            return;

        CreateButton("Level 1 — Tutorial", () => mainMenu.StartLevel1());
        CreateButton("Level 2 — Crossfire", () => mainMenu.StartLevel2());
        CreateButton("Level 3 — Boss", () => mainMenu.StartLevel3());
        built = true;
    }

    static bool HasLevelButtons(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            string n = parent.GetChild(i).name;
            if (n.StartsWith("Level 1") || n.Contains("Tutorial"))
                return true;
        }
        return false;
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
            layout.padding = new RectOffset(20, 20, 80, 20);
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
        VigilanteUiStyle.StyleInvertedButtonPreserveActions(btn);
    }
}
