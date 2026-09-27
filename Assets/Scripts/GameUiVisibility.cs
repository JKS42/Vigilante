using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Visibility switch for gameplay HUD elements, excluding menus and tutorials.</summary>
public static class GameUiVisibility
{
    const string PreferenceKey = "Vigilante.GameUiVisible";

    sealed class Target
    {
        public GameObject gameObject;
        public CanvasGroup group;
        public float visibleAlpha;
        public bool wasInteractable;
        public bool wasBlockingRaycasts;
    }

    static readonly List<Target> targets = new List<Target>();
    static bool initialized;
    static bool visible = true;

    public static event Action<bool> VisibilityChanged;

    public static bool IsVisible
    {
        get
        {
            EnsureInitialized();
            return visible;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        targets.Clear();
        initialized = false;
        visible = true;
        VisibilityChanged = null;
    }

    static void EnsureInitialized()
    {
        if (initialized)
            return;
        initialized = true;
        visible = PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
    }

    public static void SetVisible(bool show)
    {
        EnsureInitialized();
        if (visible == show)
        {
            ApplyToTargets();
            return;
        }

        visible = show;
        PlayerPrefs.SetInt(PreferenceKey, visible ? 1 : 0);
        PlayerPrefs.Save();
        ApplyToTargets();
        VisibilityChanged?.Invoke(visible);
    }

    public static void Toggle()
    {
        SetVisible(!IsVisible);
    }

    public static void Register(GameObject targetObject)
    {
        EnsureInitialized();
        if (targetObject == null)
            return;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].gameObject == targetObject)
                return;
        }

        CanvasGroup group = targetObject.GetComponent<CanvasGroup>();
        if (group == null)
            group = targetObject.AddComponent<CanvasGroup>();

        Target target = new Target
        {
            gameObject = targetObject,
            group = group,
            visibleAlpha = group.alpha,
            wasInteractable = group.interactable,
            wasBlockingRaycasts = group.blocksRaycasts
        };
        targets.Add(target);
        Apply(target);
    }

    static void ApplyToTargets()
    {
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            Target target = targets[i];
            if (target == null || target.gameObject == null || target.group == null)
            {
                targets.RemoveAt(i);
                continue;
            }
            Apply(target);
        }
    }

    static void Apply(Target target)
    {
        target.group.alpha = visible ? target.visibleAlpha : 0f;
        target.group.interactable = visible && target.wasInteractable;
        target.group.blocksRaycasts = visible && target.wasBlockingRaycasts;
    }
}
