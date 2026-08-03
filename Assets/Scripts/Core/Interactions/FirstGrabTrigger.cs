using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class FirstGrabTrigger : MonoBehaviour
{
    [SerializeField]
    private UnityEvent onFirstGrab = new UnityEvent();

    private XRGrabInteractable grabInteractable;
    private bool hasTriggered;

    public UnityEvent OnFirstGrab => onFirstGrab;

    public bool HasTriggered => hasTriggered;

    private void Awake()
    {
        CacheInteractable();
    }

    private void OnEnable()
    {
        if (!CacheInteractable())
            return;

        // Remove first so repeated enable cycles can never accumulate listeners.
        grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
        grabInteractable.selectEntered.AddListener(HandleSelectEntered);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
    }

    private bool CacheInteractable()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null)
            return true;

        Debug.LogError(
            "FirstGrabTrigger requires an XRGrabInteractable on the same GameObject.",
            this);
        enabled = false;
        return false;
    }

    private void HandleSelectEntered(SelectEnterEventArgs _)
    {
        if (hasTriggered)
            return;

        hasTriggered = true;
        onFirstGrab.Invoke();
    }

    /// <summary>Allows an isolated component test to arm the trigger again.</summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}
