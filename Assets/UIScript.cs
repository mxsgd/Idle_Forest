using UnityEngine;
using UnityEngine.UIElements;

public class uiScript : MonoBehaviour
{
    public void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        VisualElement DeckContainer = root.Q<VisualElement>("Container");
        Label score = root.Q<Label>("Label");

        if (DeckContainer is null)
            Debug.Log("deckcontainerbrak");
            
        if (score is null)
            Debug.Log("scorebrak");
    }
}