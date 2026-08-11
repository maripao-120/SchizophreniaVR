using UnityEngine;

[DisallowMultipleComponent]
public sealed class CeilingArmsView : MonoBehaviour
{
    [SerializeField]
    private Renderer[] controlledRenderers = new Renderer[0];

    private void Awake()
    {
        ResetView();
    }

    private void OnDisable()
    {
        Hide();
    }

    public void Show()
    {
        SetVisibility(true);
    }

    public void Hide()
    {
        SetVisibility(false);
    }

    public void ResetView()
    {
        Hide();
    }

    private void SetVisibility(bool visible)
    {
        foreach (Renderer controlledRenderer in controlledRenderers)
        {
            if (controlledRenderer != null)
                controlledRenderer.enabled = visible;
        }
    }
}
