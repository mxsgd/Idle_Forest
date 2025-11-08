using UnityEngine;
using UnityEngine.UI;

public class HoldProgressIndicator : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        if (fillImage == null)
            fillImage = GetComponentInChildren<Image>();

        if (baseScale == Vector3.one)
            baseScale = transform.localScale;
    }

    private void LateUpdate()
    {
        if (!faceCamera)
            return;

        var camera = Camera.main;
        if (camera == null)
            return;

        var direction = transform.position - camera.transform.position;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    public void SetProgress(float normalizedProgress)
    {
        normalizedProgress = Mathf.Clamp01(normalizedProgress);

        if (fillImage != null)
        {
            fillImage.fillAmount = normalizedProgress;
            return;
        }

        transform.localScale = baseScale * Mathf.Max(0.0001f, normalizedProgress);
    }
}