using Basis.Scripts.Device_Management.Devices;
using Basis.Scripts.Device_Management.Devices.Simulation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Animations;
using UnityEngine.LowLevelPhysics;
using UnityEngine.ResourceManagement.AsyncOperations;

public class PickupInteractable : InteractableObject
{
    // public BasisObjectSyncNetworking syncNetworking;

    

    [Header("Reparent Settings")]
    public bool KinematicWhileInteracting = false;
    
    [SerializeField]
    private bool LocalOnly = true;

    [Header("References")]
    public Collider ColliderRef;
    public Rigidbody RigidRef;
    public ParentConstraint ConstraintRef;

    // internal values
    private GameObject HighlightClone;
    private AsyncOperationHandle<Material> asyncOperationHighlightMat;
    private Material ColliderHighlightMat;
    private bool previousKinematicValue = true;
    
    // constants
    const string k_LoadMaterialAddress = "Assets/Interactable/InteractHighlightMat.mat";
    const string k_CloneName = "HighlightClone";
    private static Quaternion rotCapsuleX;
    private static Quaternion rotCapsuleZ;

    public PickupInteractable()
    {
        rotCapsuleX = Quaternion.Euler(new Vector3(0, 0, 90));
        rotCapsuleZ = Quaternion.Euler(new Vector3(90, 0, 0));
    }

    void Start()
    {
        InputSources = new CachedList<InputSource>
        {
            new InputSource(null, false)
        };

        if (RigidRef == null)
        {
            TryGetComponent(out RigidRef);
        }
        if (ColliderRef == null)
        {
            TryGetComponent(out ColliderRef);
        }
        if (ConstraintRef == null)
        {
            if (TryGetComponent(out ConstraintRef))
            {
                var nullSource = new ConstraintSource() {
                    sourceTransform = null,
                    weight = 1,
                };
                ConstraintRef.AddSource(nullSource);
            }
        }

        // TODO: netsync
        if (!LocalOnly)
        {
            // syncNetworking = GetComponent<BasisObjectSyncNetworking>();
        }

        AsyncOperationHandle<Material> op = Addressables.LoadAssetAsync<Material>(k_LoadMaterialAddress);
        ColliderHighlightMat = op.WaitForCompletion();
        asyncOperationHighlightMat = op;

        // NOTE: Collider mesh highlight position and size is only updated on Start(). 
        //      If you wish to have the highlight update at runtime do that elsewhere or make a different InteractableObject Script
        HighlightClone = CloneColliderMesh(ColliderRef, gameObject.transform);

        if (HighlightClone != null)
        {
            HighlightClone.name = k_CloneName;
            // generated meshes at this point should always have a meshrenderer

            if (HighlightClone.TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer.material = ColliderHighlightMat;
            }
            else 
            {
                BasisDebug.LogWarning("Pickup Interactable could not find MeshRenderer component on mesh clone. Highlights will be broken");
            }
        }

    }

    private GameObject CloneColliderMesh(Collider collider, Transform parent) {
        GameObject primitive = null;
        switch (collider.GeometryHolder.Type)
        {
            case GeometryType.Sphere:
                var sphere = (SphereCollider)collider;
                primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                if(primitive.TryGetComponent(out SphereCollider sCol))
                {
                    Destroy(sCol);
                }
                else
                {
                    BasisDebug.LogError("Primitive Sphere did not have a sphere collider?!");
                }
                primitive.transform.parent = parent;

                primitive.transform.localPosition = sphere.center;
                primitive.transform.localScale = Vector3.one * sphere.radius * 2;
                break;
            case GeometryType.Capsule:
                var capsule = (CapsuleCollider)collider;
                primitive = new GameObject(k_CloneName);
                MeshFilter mFilter = primitive.AddComponent<MeshFilter>();
                primitive.AddComponent<MeshRenderer>();
                primitive.transform.parent = parent;

                // generate mesh since we cant just scale the capsule primitve (sadly)
                Mesh newMesh = CapsuleMeshGenerator.CreateCapsuleMesh(capsule.radius, capsule.height, 8);
                mFilter.mesh = newMesh;

                primitive.transform.localPosition = capsule.center;
                
                switch (capsule.direction)
                {
                    // X, Y (no change), Z
                    case 0:
                        primitive.transform.localRotation = rotCapsuleX;
                        break;
                    case 2:
                        primitive.transform.localRotation = rotCapsuleZ;
                        break;
                    default:
                        break;
                }
                primitive.transform.localScale = Vector3.one;

                break;
            case GeometryType.Box:
                var box = (BoxCollider)collider;
                primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if(primitive.TryGetComponent(out BoxCollider boxCol))
                {
                    Destroy(boxCol);
                }
                else
                {
                    BasisDebug.LogError("Cube Primitve did not have a box collider?!");
                }
                primitive.transform.parent = parent;

                primitive.transform.SetLocalPositionAndRotation(box.center, Quaternion.identity);
                primitive.transform.localScale = box.size;
                break;
            case GeometryType.ConvexMesh:
            case GeometryType.TriangleMesh:

                if (!collider.TryGetComponent(out MeshFilter meshFilter))
                {
                    BasisDebug.LogWarning("Pickup Interactable with Mesh collider must have MeshFilter on same object to generate Highlight box");
                    return null;
                }
                // mesh bounds, not collider bounds- as mesh bounds is in local space and not axis aligned
                Bounds objectBounds = meshFilter.mesh.bounds;

                primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if(primitive.TryGetComponent(out BoxCollider _col))
                {
                    Destroy(_col);
                }
                else
                {
                    BasisDebug.LogError("Cube Primitve did not have a box collider?!");
                }
                primitive.transform.parent = parent;

                primitive.transform.localPosition = objectBounds.center;
                primitive.transform.localRotation = Quaternion.identity;
                primitive.transform.localScale = objectBounds.size;
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
        if (ColliderRef && HighlightClone)
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
        return InputSources[0].Source != null && 
            !InputSources[0].IsInteracting && 
            InputSources[0].Source.UniqueDeviceIdentifier == input.UniqueDeviceIdentifier && 
            IsWithinRange(input.transform.position);
    }

    public override void OnHoverStart(BasisInput input)
    {
        InputSources[0] = new InputSource(input, false);
        HighlightObject(true);
    }

    public override void OnHoverEnd(BasisInput input, bool willInteract)
    {
        if (InputSources[0].Source.UniqueDeviceIdentifier == input.UniqueDeviceIdentifier)
        {
            if (!willInteract)
            {
                InputSources[0] = new InputSource(null, false);
            }
            HighlightObject(false);
        }
    }

    public override void OnInteractStart(BasisInput input)
    {
        // same input that was highlighting previously
        if (InputSources[0].Source != null && 
            InputSources[0].Source.UniqueDeviceIdentifier == input.UniqueDeviceIdentifier && 
            !InputSources[0].IsInteracting
        ) {
            SetParentConstraint(input.transform);

            if(RigidRef != null && KinematicWhileInteracting)
            {
                previousKinematicValue = RigidRef.isKinematic;
                RigidRef.isKinematic = true;
            }

            // Set ownership to the local player
            // syncNetworking.IsOwner = true;
            InputSources[0] = new InputSource(input, true);
        }
        else
        {
            Debug.LogWarning("Input source interacted with ReparentInteractable without highlighting first.");
        }
    }

    public override void OnInteractEnd(BasisInput input)
    {
        if (InputSources[0].IsInteracting && InputSources[0].Source != null && InputSources[0].Source.UniqueDeviceIdentifier == input.UniqueDeviceIdentifier)
        {
            SetParentConstraint(null);

            InputSources[0] = new InputSource(null, false);

            if(KinematicWhileInteracting && RigidRef != null)
            {
                RigidRef.isKinematic = previousKinematicValue;
            }


            // syncNetworking.IsOwner = false;
        }
    }

    public void SetParentConstraint(Transform source) {
        if (ConstraintRef != null)
        {
            // ignore source count, only modify the 0 index
            var newSource = new ConstraintSource()
            {
                sourceTransform = source,
                weight = 1,
            };
            ConstraintRef.SetSource(0, newSource);

            if (CanEquip) 
            {
                ConstraintRef.SetTranslationOffset(0, equipPos);
                ConstraintRef.SetRotationOffset(0, equipRot.eulerAngles);
            }
            else if (source != null)
            {
                ConstraintRef.SetTranslationOffset(0, source.InverseTransformPoint(transform.position));
                ConstraintRef.SetRotationOffset(0, (Quaternion.Inverse(source.rotation) * transform.rotation).eulerAngles);
            }
            

            // force constraint weight
            ConstraintRef.weight = 1;
            ConstraintRef.constraintActive = source != null;
        }
        else
        {
            Debug.LogError("ReparentInteractable lost its parent constraint component!", gameObject);
        }
    }

    public override void InputUpdate()
    {
        if (InputSources[0].IsInteracting && InputSources[0].Source != null)
        {
            // transform updated by transform heirarchy already

            // Update the networked data (Storeddata) to reflect the position, rotation, and scale
            if (!LocalOnly)
            {
                // syncNetworking.Storeddata.Position = transform.position;
                // syncNetworking.Storeddata.Rotation = transform.rotation;
                // syncNetworking.Storeddata.Scale = transform.localScale;
            }
        }
    }


    public override bool IsInteractingWith(BasisInput input)
    {
        return InputSources[0].IsInteracting &&
            InputSources[0].Source != null && 
            InputSources[0].Source.UniqueDeviceIdentifier == input.UniqueDeviceIdentifier;
    }

    public override bool IsHoveredBy(BasisInput input)
    {
        return !InputSources[0].IsInteracting && 
            InputSources[0].Source != null && 
            InputSources[0].Source.UniqueDeviceIdentifier == input.UniqueDeviceIdentifier;
    }

    // this is cached, use it
    public override Collider GetCollider()
    {
        return ColliderRef;
    }

    // TODO: netsync
    // public void OnOwnershipTransfer(bool isOwner)
    // {
    //     // remove ourselves from influece
    //     if (!isOwner)
    //     {
    //         transform.SetParent(null);
    //         InputSources[0] = new InputSource(null, true);
    //     }
    //     // dont care otherwise, wait for hover/interact
    // }

    void OnDestroy() 
    {
        Destroy(HighlightClone);
        if (asyncOperationHighlightMat.IsValid())
        {
            asyncOperationHighlightMat.Release();
        }
    }

    public void OnValidate()
    {
        string errPrefix = "ReparentInteractable needs component defined on self or given a reference for ";
        if (RigidRef == null && !TryGetComponent(out Rigidbody _))
        {
            Debug.LogError(errPrefix + "Rigidbody", gameObject);
        }
        if (ColliderRef == null && !TryGetComponent(out Collider _))
        {
            Debug.LogError(errPrefix + "Collider", gameObject);
        }
        if (ConstraintRef == null && !TryGetComponent(out ParentConstraint _))
        {
            Debug.LogError(errPrefix + "ParentConstraint", gameObject);
        }
    }
}