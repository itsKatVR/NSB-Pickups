using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Basis.Scripts.Device_Management.Devices;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.LowLevelPhysics;

[RequireComponent(typeof(Collider))]
public class ReparentInteractable : InteractableObject
{
    [Header("References")]
    public Collider colliderRef;
    private Rigidbody rb;

    public GameObject HighlightClone;
    public BasisObjectSyncNetworking syncNetworking;

    const string k_CloneName = "HighlightClone";

    public static string LoadMaterialAddress = "Assets/Interactable/Material/Highlight.mat";

    private Material ColliderHighlightMat;

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

        // NOTE: Collider mesh highlight position and size is only updated on Start(). 
        //      If you wish to have the highlight update at runtime do that elsewhere or make a different InteractableObject Script
        HighlightClone = CloneColliderMesh(GetComponent<Collider>(), gameObject.transform);
        
        UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Material> op = Addressables.LoadAssetAsync<Material>(LoadMaterialAddress);
        ColliderHighlightMat = op.WaitForCompletion();
        if (HighlightClone)
        {
            HighlightClone.name = k_CloneName;
            // generated meshes at this point should always have a meshrenderer
            HighlightClone.GetComponent<MeshRenderer>().material = ColliderHighlightMat;
        }

    }

    private GameObject CloneColliderMesh(Collider collider, Transform parent) {
        GameObject primitive = null;
        switch (collider.GeometryHolder.Type)
        {
            case GeometryType.Sphere:
                var sphere = (SphereCollider)collider;
                primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(primitive.GetComponent<SphereCollider>());
                primitive.transform.parent = parent;

                primitive.transform.localPosition = sphere.center;
                primitive.transform.localScale = new Vector3(sphere.radius * 2, sphere.radius * 2, sphere.radius * 2);
                break;
            case GeometryType.Capsule:
                var capsule = collider.GetComponent<CapsuleCollider>();
                primitive = new GameObject(k_CloneName);
                primitive.AddComponent<MeshFilter>();
                primitive.AddComponent<MeshRenderer>();
                primitive.transform.parent = parent;

                // TODO: does this take in account parent scale properly on generation?
                // generate mesh since we cant just scale

                Mesh newMesh = CapsuleMeshGenerator.CreateCapsuleMesh(capsule.radius, capsule.height, 8);
                var meshFilter = primitive.GetComponent<MeshFilter>();
                // meshFilter.mesh.Clear();
                meshFilter.mesh = newMesh;

                primitive.transform.localPosition = capsule.center;
                switch (capsule.direction)
                {
                    // X, Y (no change), Z
                    case 0:
                        primitive.transform.localRotation = Quaternion.Euler(new Vector3(0, 0, 90));
                        break;
                    case 2:
                        primitive.transform.localRotation = Quaternion.Euler(new Vector3(90, 0, 0));
                        break;
                    default:
                        break;
                }
                primitive.transform.localScale = Vector3.one;

                break;
            case GeometryType.Box:
                var box = collider.GetComponent<BoxCollider>();
                primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(primitive.GetComponent<BoxCollider>());
                primitive.transform.parent = parent;

                primitive.transform.localPosition = box.center;
                primitive.transform.localRotation = Quaternion.Euler(Vector3.zero);
                primitive.transform.localScale = box.size;
                break;
            case GeometryType.ConvexMesh:
            case GeometryType.TriangleMesh:
                MeshFilter sourceMesh = gameObject.GetComponent<MeshFilter>();
                if(sourceMesh == null)
                {
                    return null;
                }
                primitive = new GameObject(k_CloneName);
                primitive.AddComponent<MeshFilter>();
                primitive.GetComponent<MeshFilter>().sharedMesh = sourceMesh.mesh;
                primitive.AddComponent<MeshRenderer>();
                // TODO: setup meshRenderer needed defaults
                primitive.transform.parent = parent;
                primitive.transform.localScale = Vector3.one;
                primitive.transform.localPosition = Vector3.zero;
                primitive.transform.localRotation = Quaternion.Euler(Vector3.zero);
                break;

            // dont know how to handle remaning types 
            case GeometryType.Terrain:
            case GeometryType.Invalid:
            default:
                Debug.LogWarning("Interactable Object could not generate mesh collider highlight due to invalid collider type: " + collider.GeometryHolder.Type);
                break;
        }
        
        primitive.SetActive(false);
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
        return InputSources[0].Source == null && IsWithinRange(input.transform.position);
    }
    public override bool CanInteract(BasisInput input)
    {
        // currently hovering with this input
        return InputSources[0].Source != null && !InputSources[0].IsInteracting && InputSources[0].Source.GetInstanceID() == input.GetInstanceID() && IsWithinRange(input.transform.position);
    }

    public override void OnHoverStart(BasisInput input)
    {
        InputSources[0] = new InputSource(input, false);
        HighlightObject(true);
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

        // AssetDatabase.CreateAsset(HighlightClone.GetComponent<MeshFilter>().mesh, "Assets/fkme.asset");
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

    void OnDestroy() 
    {
        Destroy(HighlightClone);
    }
}