using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WindowShadowView : MonoBehaviour
{
    [SerializeField]
    private Renderer[] controlledRenderers = Array.Empty<Renderer>();

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        ResetView();
    }

    public void Show()
    {
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public void ResetView()
    {
        Hide();
    }

    private void SetVisible(bool visible)
    {
        bool hasValidRenderer = false;
        if (controlledRenderers != null)
        {
            foreach (Renderer controlledRenderer in controlledRenderers)
            {
                if (controlledRenderer == null)
                    continue;

                controlledRenderer.enabled = visible;
                hasValidRenderer = true;
            }
        }

        IsVisible = visible && hasValidRenderer;
    }
}
