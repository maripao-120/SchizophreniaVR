using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NoteView : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("References")]
    [SerializeField]
    private TMP_Text noteText;

    [SerializeField]
    private Renderer backgroundRenderer;

    [Header("Content")]
    [SerializeField, TextArea(2, 4)]
    private string initialText =
        "NO OLVIDES TOMAR LA PASTILLA\nSI EMPIEZAS A SENTIRTE MAL.";

    [SerializeField, TextArea(2, 4)]
    private string alteredText =
        "NO CONFÍES EN ELLOS.\nNO TOMES LA PASTILLA.";

    [Header("Colors")]
    [SerializeField]
    private Color initialTextColor = new Color(0.12f, 0.10f, 0.08f, 1f);

    [SerializeField]
    private Color alteredTextColor = new Color(0.55f, 0.08f, 0.08f, 1f);

    [SerializeField]
    private Color initialBackgroundColor = new Color(0.92f, 0.87f, 0.72f, 1f);

    [SerializeField]
    private Color alteredBackgroundColor = new Color(0.72f, 0.48f, 0.43f, 1f);

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        ShowInitialState();
    }

    public void ShowInitialState()
    {
        ApplyState(initialText, initialTextColor, initialBackgroundColor);
    }

    public void ShowAlteredState()
    {
        ApplyState(alteredText, alteredTextColor, alteredBackgroundColor);
    }

    public void ResetView()
    {
        ShowInitialState();
    }

    private void ApplyState(string content, Color textColor, Color backgroundColor)
    {
        if (noteText == null)
        {
            Debug.LogError("NoteView has no TMP text reference assigned.", this);
        }
        else
        {
            noteText.text = content;
            noteText.color = textColor;
        }

        ApplyBackgroundColor(backgroundColor);
    }

    private void ApplyBackgroundColor(Color color)
    {
        if (backgroundRenderer == null)
        {
            Debug.LogError("NoteView has no background Renderer assigned.", this);
            return;
        }

        Material sharedMaterial = backgroundRenderer.sharedMaterial;
        if (sharedMaterial == null)
        {
            Debug.LogError("NoteView background Renderer has no material.", this);
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        backgroundRenderer.GetPropertyBlock(propertyBlock);

        if (sharedMaterial.HasProperty(BaseColorId))
            propertyBlock.SetColor(BaseColorId, color);
        else if (sharedMaterial.HasProperty(ColorId))
            propertyBlock.SetColor(ColorId, color);
        else
        {
            Debug.LogWarning(
                "NoteView background material has no supported color property; text still changes.",
                this);
            return;
        }

        // A property block changes only this renderer, never the shared material asset.
        backgroundRenderer.SetPropertyBlock(propertyBlock);
    }
}
