using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Basis.Scripts.Device_Management.Devices;
using UnityEngine;
using UnityEngine.LowLevelPhysics;

[RequireComponent(typeof(Collider))]
public class ReparentInteractable : InteractableObject
{
    [Header("References")]
    public Collider colliderRef;
    private Rigidbody rb;

    private GameObject HighlightClone;
    public BasisObjectSyncNetworking syncNetworking;

    public static string LoadMaterialAddress = "Assets/Interactable/Material/Highlight.mat";

    void Start()
    {
        InputSources = new List<InputSource>
        {
            new InputSource(null, false)
        };

        rb = GetComponent<Rigidbody>();
        if (colliderRef == null)
        {
            colliderRef = GetComponent<Collider>();
        }

        syncNetworking = GetComponent<BasisObjectSyncNetworking>();


        var components = new Type[] {typeof(MeshRenderer), typeof(MeshFilter)};
        HighlightClone = CloneMesh(gameObject.GetComponent<Collider>());
        HighlightClone.name = "HighlightClone";

    }

    private GameObject CloneMesh(Collider collider) {
        GameObject primitive = null;
        switch (collider.GeometryHolder.Type)
        {
            case GeometryType.Sphere:
                var sphere = collider.GetComponent<SphereCollider>();
                primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var newSphere = primitive.GetComponent<SphereCollider>();
                newSphere.transform.localPosition = sphere.center;
                newSphere.transform.localScale = new Vector3(sphere.radius * 2, sphere.radius * 2, sphere.radius * 2);
                break;
            case GeometryType.Capsule:
                var capsule = collider.GetComponent<CapsuleCollider>();
                primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                var newCapsule = primitive.GetComponent<CapsuleCollider>();
                // TODO
                break;
            case GeometryType.Box:
                primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                break;
            // not sure how to get the hull generated for this, just use the triangle mesh
            case GeometryType.ConvexMesh:
            case GeometryType.TriangleMesh:
                MeshFilter sourceMesh = gameObject.GetComponent<MeshFilter>();
                if(sourceMesh == null)
                {
                    return null;
                }
                var meshClone = new GameObject();
                meshClone.AddComponent<MeshFilter>();
                meshClone.GetComponent<MeshFilter>().mesh = sourceMesh.mesh;
                // return immediately, mesh should be the same size that we are using
                return meshClone;
            // dont know how to handle remaning types 
            case GeometryType.Terrain:
            case GeometryType.Invalid:
            default:
                break;
        }
        
        return primitive;
    }


    public void HighlightObject(bool highlight)
    {
        if (colliderRef && HighlightClone)
        {
            HighlightClone.SetActive(highlight);
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
            // Debug.LogError("InteractEnd");
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