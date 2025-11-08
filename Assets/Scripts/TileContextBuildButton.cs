using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

[RequireComponent(typeof(Button))]
public class TileContextBuildButton : MonoBehaviour
{
    [SerializeField] private TileContextPanel panel;
    [SerializeField] private TileBuildAction action = TileBuildAction.Grass;

    private Button button;
    private bool runtimeListenerAdded;

    private void Awake()
    {
        CacheReferences();
        EnsurePanelReference();

        if (Application.isPlaying)
            TryRegisterRuntimeListener();
    }

    private void Reset()
    {
        CacheReferences();
        EnsurePanelReference();

        if (TryInferAction(out var inferred))
            action = inferred;
    }

    private void OnEnable()
    {
        CacheReferences();
        EnsurePanelReference();

        if (Application.isPlaying)
            TryRegisterRuntimeListener();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            UnregisterRuntimeListener();
    }

    private void OnDestroy()
    {

        if (Application.isPlaying)
            UnregisterRuntimeListener();
    }

    private void OnClicked()
    {
        if (panel == null)
        {
            Debug.LogWarning($"[{nameof(TileContextBuildButton)}] Brak przypisanego panelu dla przycisku '{name}'.", this);
            return;
        }

        panel.RequestBuild(action);
    }

    private void CacheReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    private void EnsurePanelReference()
    {
        if (panel != null)
            return;

        panel = GetComponentInParent<TileContextPanel>(true);

        if (panel == null)
            panel = FindObjectOfType<TileContextPanel>(true);
    }

    private void TryRegisterRuntimeListener()
    {
        if (button == null || HasPersistentListener())
            return;

        if (!runtimeListenerAdded)
        {
            button.onClick.AddListener(OnClicked);
            runtimeListenerAdded = true;
        }
    }

    private void UnregisterRuntimeListener()
    {
        if (button == null || !runtimeListenerAdded)
            return;

        button.onClick.RemoveListener(OnClicked);
        runtimeListenerAdded = false;
    }

    private bool HasPersistentListener()
    {
        if (button == null)
            return false;

        var onClick = button.onClick;
        int count = onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (onClick.GetPersistentTarget(i) == this && onClick.GetPersistentMethodName(i) == nameof(OnClicked))
                return true;
        }

        return false;
    }

    private bool TryInferAction(out TileBuildAction inferred)
    {
        var lowerName = name.ToLowerInvariant();

        if (lowerName.Contains("tree"))
        {
            inferred = TileBuildAction.Tree;
            return true;
        }

        if (lowerName.Contains("bush") || lowerName.Contains("krzak"))
        {
            inferred = TileBuildAction.Bush;
            return true;
        }

        if (lowerName.Contains("grass") || lowerName.Contains("trawa"))
        {
            inferred = TileBuildAction.Grass;
            return true;
        }

        inferred = action;
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheReferences();
        EnsurePanelReference();

        if (button == null)
            return;

        var onClick = button.onClick;
        if (!HasPersistentListener())
        {
            UnityEventTools.AddPersistentListener(onClick, OnClicked);
            EditorUtility.SetDirty(button);
            EditorUtility.SetDirty(this);
        }
    }
#endif
}