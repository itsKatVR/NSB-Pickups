using System.Collections.Generic;
using System.Linq;
using Basis.Scripts.Device_Management.Devices;
using UnityEngine;

public class ReparentInteractable : InteractableObject
{
    [Header("References")]
    public Renderer objectRenderer;
    public Material highlightMaterial;
    public Material originalMaterial;
    private Rigidbody rb;

    public BasisObjectSyncNetworking syncNetworking;

    void Start()
    {
        InputSources = new List<InputSource>
        {
            new InputSource(null, false)
        };

        rb = GetComponent<Rigidbody>();
        if (objectRenderer == null)
        {
            objectRenderer = GetComponent<Renderer>();
        }
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.sharedMaterial;
        }

        syncNetworking = GetComponent<BasisObjectSyncNetworking>();
    }


    public void HighlightObject(bool highlight)
    {
        if (objectRenderer && highlightMaterial && originalMaterial)
        {
            objectRenderer.sharedMaterial = highlight ? highlightMaterial : originalMaterial;
        }
    }

    public override bool CanHover(BasisInput input)
    {
        // must be dropped to hover
        return InputSources[0].Source == null && IsWithinRange(input.transform);
    }
    public override bool CanInteract(BasisInput input)
    {
        // currently hovering with this input
        return InputSources[0].Source != null && !InputSources[0].IsInteracting && InputSources[0].Source.GetInstanceID() == input.GetInstanceID() && IsWithinRange(input.transform);
    }

    public override void OnHoverStart(BasisInput input)
    {
        InputSources[0] = new InputSource(input, false);
        HighlightObject(true);
        // Debug.LogError("HoverStart");
    }

    public override void OnHoverEnd(BasisInput input, bool willInteract)
    {
        if (InputSources[0].Source.GetInstanceID() == input.GetInstanceID())
        {
            if (!willInteract)
            {
                InputSources[0] = new InputSource(null, false);
            }
            HighlightObject(false);
            // Debug.LogError("HoverEnd");
        }
    }

    public override void OnInteractStart(BasisInput input)
    {
        // same input that was highlighting previously
        if (InputSources[0].Source != null && InputSources[0].Source.GetInstanceID() == input.GetInstanceID() && !InputSources[0].IsInteracting)
        {

            transform.SetParent(input.transform);

            // Set ownership to the local player
            syncNetworking.IsOwner = true;
            InputSources[0] = new InputSource(input, true);
            // Debug.LogError("InteractStart");
        }
        else
        {
            Debug.LogWarning("Input source interacted with ReparentInteractable without highlighting first.");
        }
    }

    public override void OnInteractEnd(BasisInput input)
    {
        if (InputSources[0].Source != null && InputSources[0].Source.GetInstanceID() == input.GetInstanceID() && InputSources[0].IsInteracting)
        {
            transform.SetParent(null);
            InputSources[0] = new InputSource(null, false);

            syncNetworking.IsOwner = false;
            Debug.LogError("InteractEnd");
        }
    }

    public override void InputUpdate()
    {
        if (InputSources[0].Source != null && InputSources[0].IsInteracting)
        {
            // transform updated by transform heirarchy already
            // TODO: try to force transform update here so its updated after move?
            // transform.position = transform.position;
            // transform.rotation = transform.rotation;

            // Update the networked data (Storeddata) to reflect the position, rotation, and scale
            syncNetworking.Storeddata.Position = transform.position;
            syncNetworking.Storeddata.Rotation = transform.rotation;
            syncNetworking.Storeddata.Scale = transform.localScale;
        }
    }

    public override bool IsInteracting()
    {
        return InputSources[0].Source != null && InputSources[0].IsInteracting;
    }

    public override void OnOwnershipTransfer(bool isOwner)
    {
        // remove ourselves from influece
        if (!isOwner)
        {
            transform.SetParent(null);
            InputSources[0] = new InputSource(null, true);
        }
        // dont care otherwise, wait for hover/interact
    }

    public override bool IsInteractingWith(BasisInput input)
    {
        return InputSources[0].Source != null && InputSources[0].Source.GetInstanceID() == input.GetInstanceID() && InputSources[0].IsInteracting;
    }

    public override bool IsHoveredBy(BasisInput input)
    {
        return InputSources[0].Source != null && InputSources[0].Source.GetInstanceID() == input.GetInstanceID() && !InputSources[0].IsInteracting;
    }
}