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
    }

    private void OnEnable()
    {
        CacheReferences();
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

    private void UnregisterRuntimeListener()
    {
        if (button == null || !runtimeListenerAdded)
            return;

        button.onClick.RemoveListener(OnClicked);
        runtimeListenerAdded = false;
    }

}