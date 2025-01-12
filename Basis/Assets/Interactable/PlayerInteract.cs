

using UnityEngine;
using Basis.Scripts.UI;
using Basis.Scripts.Networking.NetworkedPlayer;
using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Device_Management;
using System.Collections.Generic;
using System.Linq;
using Basis.Scripts.Device_Management.Devices;
using Basis.Scripts.Addressable_Driver;
using UnityEngine.AddressableAssets;
using Unity.Burst;
using System;
using Unity.Collections;
using UnityEngine.ResourceManagement.AsyncOperations;
using Basis.Scripts.TransformBinders.BoneControl;

public class PlayerInteract : MonoBehaviour
{

    [Tooltip("How far the player can interact with objects. Must > hoverDistance")]
    public float raycastDistance = 1.0f;
    [Tooltip("How far the player Hover.")]
    public float hoverRadius = 0.5f;
    [Tooltip("Both of the above are relative to object transforms, objects with larger colliders may have issues")]

    public struct InteractInput
    {
        public string deviceUid { get; set; }
        public BasisInput input { get; set; }
        public GameObject interactOrigin { get; set; }
        // TODO: use this ref
        public LineRenderer lineRenderer { get; set; }
        public HoverInteractSphere hoverInteract { get; set; }
        public InteractableObject lastTarget { get; set; }

        public bool IsInput(BasisInput input)
        {
            return deviceUid == input.UniqueDeviceIdentifier;
        }
    }

    public CachedList<InteractInput> InteractInputs = new();
    
    // private UniqueCounterList<InteractableObject> ActiveObjects = new();

    public Material LineMaterial;
    private AsyncOperationHandle<Material> asyncOperationLineMaterial;
    public float interactLineWidth = 0.015f;
    public bool renderInteractLine = true;

    // TODO: load with addressable.  
    public static string LoadMaterialAddress = "Assets/Interactable/InteractLineMat.mat";


    void Start()
    {
        BasisLocalPlayer.Instance.LocalBoneDriver.OnSimulate += Simulate;
        BasisDeviceManagement.Instance.AllInputDevices.OnListChanged += OnInputChanged;
        BasisDeviceManagement.Instance.AllInputDevices.OnListItemRemoved += OnInputRemoved;

        AsyncOperationHandle<Material> op = Addressables.LoadAssetAsync<Material>(LoadMaterialAddress);
        LineMaterial = op.WaitForCompletion();
        asyncOperationLineMaterial = op;
    }
    void OnDestroy()
    {
        if (asyncOperationLineMaterial.IsValid())
        {
            asyncOperationLineMaterial.Release();
        }
        BasisLocalPlayer.Instance.LocalBoneDriver.OnSimulate -= Simulate;
        BasisDeviceManagement.Instance.AllInputDevices.OnListChanged -= OnInputChanged;
        BasisDeviceManagement.Instance.AllInputDevices.OnListItemRemoved -= OnInputRemoved;

        foreach (InteractInput input in InteractInputs)
        {
            Destroy(input.interactOrigin);
        }
    }

    private void OnInputChanged()
    {
        int count = BasisDeviceManagement.Instance.AllInputDevices.Count;
        for (int Index = 0; Index < count; Index++)
        {
            BasisInput device = BasisDeviceManagement.Instance.AllInputDevices[Index];
            // skip invalid or already included
            if (device == null || device != null && InteractInputs.Any(x => x.IsInput(device)))
            {
                continue;
            }

            // TODO: need a different config value for can interact/pickup/grab. Mainly input action/trigger values
            if (device.BasisDeviceMatchableNames != null && 
                device.BasisDeviceMatchableNames.HasRayCastSupport
            ) {
                AddInput(device);
            }
            // TODO: what if it has no matchable name?
            // device removed handled elsewhere
        }
    }

    private void OnInputRemoved(BasisInput input)
    {
        RemoveInput(input.UniqueDeviceIdentifier);
    }

    // simulate after IK update
    [BurstCompile]
    private void Simulate()
    {
        var count = InteractInputs.Count;
        for (int Index = 0; Index < count; Index++)
        {
            InteractInput interactInput = InteractInputs[Index];
            if (interactInput.input == null)
            {
                Debug.LogWarning("Pickup input device unexpectedly null, input devices likely changed");
                continue;
            }

            HoverInteractSphere hoverSphere = interactInput.hoverInteract;

            Vector3 originPos = interactInput.interactOrigin.transform.position;
            Ray ray;
            if (hoverSphere.HoverTarget != null)
            {
                Vector3 direction = (originPos - hoverSphere.TargetClosestPoint).normalized;
                ray = new Ray(originPos, direction);
            }
            else
            {
                if (IsDesktopCenterEye(interactInput.input))
                {
                    ray = new Ray(interactInput.input.transform.position, interactInput.input.transform.forward);
                }
                else
                {
                    Vector3 origin = originPos;
                    Vector3 direction = interactInput.interactOrigin.transform.forward;
                    ray = new Ray(origin, direction);
                }
            }

            RaycastHit rayHit;
            InteractableObject hitInteractable = null;
            // TODO: Interact layer
            bool isValidRayHit = Physics.Raycast(ray, out rayHit, raycastDistance) && 
                rayHit.collider != null && 
                rayHit.collider.TryGetComponent(out hitInteractable);

            
            if (isValidRayHit || hoverSphere.HoverTarget != null)
            {
                // prioritize hover
                if (hoverSphere.HoverTarget != null)
                {
                    hitInteractable = hoverSphere.HoverTarget;
                }

                if (hitInteractable != null)
                {
                    // NOTE: this will skip a frame of hover after stopping interact
                    interactInput = UpdatePickupState(hitInteractable, interactInput);


                }
            }
            // hover misssed entirely 
            else
            {
                if (interactInput.lastTarget != null)
                {
                    // seperate if blocks in case implementation allows for hovering and holding of the same object

                    // TODO: proximity check so we dont keep interacting with objects out side of player's reach. Needs an impl that wont break under lag though. `|| !interactInput.targetObject.IsWithinRange(interactInput.input.transform)`
                    // only drop if trigger was released
                    if (!IsInputGrabbing(interactInput.input) && interactInput.lastTarget.IsInteractingWith(interactInput.input))
                    {
                        interactInput.lastTarget.OnInteractEnd(interactInput.input);
                    }

                    if (interactInput.lastTarget.IsHoveredBy(interactInput.input))
                    {
                        interactInput.lastTarget.OnHoverEnd(interactInput.input, false);
                    }
                }
            }

            // write changes back
            InteractInputs[Index] = interactInput;
        }

        // update objects, seperate list to ensure each target only gets one update
        List<InteractableObject> updateList = new();
        foreach (InteractInput input in InteractInputs)
        {
            if (input.lastTarget != null && !updateList.Any(x => x.GetInstanceID() == input.lastTarget.GetInstanceID()))
            {
                updateList.Add(input.lastTarget);
            }
        }
        foreach (InteractableObject interactable in updateList)
        {
            interactable.InputUpdate();
        }


        // apply line renderer
        if (renderInteractLine)
        {
            foreach (InteractInput input in InteractInputs)
            {
                if (input.lastTarget != null && input.lastTarget.IsHoveredBy(input.input))
                {
                    Vector3 origin = input.interactOrigin.transform.position;
                    Vector3 start;
                    // desktop offset for center eye (a little to the bottom right)
                    if (IsDesktopCenterEye(input.input))
                    {
                        start = input.interactOrigin.transform.position + (input.interactOrigin.transform.forward * 0.1f) + Vector3.down * 0.1f + (input.interactOrigin.transform.right * 0.1f);
                    }
                    else
                    {
                        start = origin;
                    }
                    if (input.lineRenderer != null)
                    {
                        Vector3 endPos = input.lastTarget.GetCollider().ClosestPoint(origin);
                        input.lineRenderer.SetPosition(0, start);
                        input.lineRenderer.SetPosition(1, endPos);
                        input.lineRenderer.enabled = true;
                    }
                }
                else
                {
                    if (input.lineRenderer)
                    {
                        input.lineRenderer.enabled = false;
                    }
                }
            }
        }
        // turn all the lines off
        else
        {
            foreach (InteractInput input in InteractInputs)
            {
                input.lineRenderer.enabled = false;
            }
        }
    }

    private InteractInput UpdatePickupState(InteractableObject hitInteractable, InteractInput interactInput)
    {
        // hit a different target than last time
        if (interactInput.lastTarget != null && interactInput.lastTarget.GetInstanceID() != hitInteractable.GetInstanceID())
        {
            // TODO: grab button instead of full trigger
            // Holding Logic: 
            if (IsInputGrabbing(interactInput.input))
            {
                // clear hover (unlikely to happen since last frame, but possible)
                if (interactInput.lastTarget.IsHoveredBy(interactInput.input))
                {
                    interactInput.lastTarget.OnHoverEnd(interactInput.input, false);
                }

                // interacted with new hit since last frame & we arent holding (in which case do nothing)
                if (hitInteractable.CanInteract(interactInput.input) && !interactInput.lastTarget.IsInteractingWith(interactInput.input))
                {
                    hitInteractable.OnInteractStart(interactInput.input);
                    interactInput.lastTarget = hitInteractable;
                }
            }
            // No trigger
            else
            {
                bool removeTarget = false;
                // end iteract of hit (unlikely since we just hit it this update)
                if (hitInteractable.IsInteractingWith(interactInput.input))
                {
                    hitInteractable.OnInteractEnd(interactInput.input);
                }
                // end interact of previous object
                if (interactInput.lastTarget.IsInteractingWith(interactInput.input))
                {
                    interactInput.lastTarget.OnInteractEnd(interactInput.input);
                    removeTarget = true;
                }

                // hover missed previous object
                if (interactInput.lastTarget.IsHoveredBy(interactInput.input))
                {
                    interactInput.lastTarget.OnHoverEnd(interactInput.input, false);
                    removeTarget = true;
                }

                // remove here in case both hover and interact ended
                if (removeTarget)
                {
                    interactInput.lastTarget = null;
                }

                // try hovering new interactable
                if (hitInteractable.CanHover(interactInput.input))
                {
                    hitInteractable.OnHoverStart(interactInput.input);
                    interactInput.lastTarget = hitInteractable;
                }
            }
        }
        // hitting same interactable
        else
        {
            // TODO: middle finger grab instead of full trigger
            // Pickup logic: 
            // per input an object can be either held or hovered, not both. Objects can ignore this by purposfully modifying IsHovered/IsInteracted.
            if (IsInputGrabbing(interactInput.input))
            {
                // first clear hover...
                if (hitInteractable.IsHoveredBy(interactInput.input))
                {
                    // will interact this frame
                    hitInteractable.OnHoverEnd(interactInput.input, hitInteractable.CanInteract(interactInput.input));
                }

                // then try to interact
                // TODO: hand set pickup limitations
                if (hitInteractable.CanInteract(interactInput.input))
                {
                    hitInteractable.OnInteractStart(interactInput.input);
                    interactInput.lastTarget = hitInteractable;
                }
            }
            // not holding
            // hover if we arent holding, drop any held
            else
            {
                // first end interact...
                if (hitInteractable.IsInteractingWith(interactInput.input))
                {
                    hitInteractable.OnInteractEnd(interactInput.input);
                }

                // then hover
                if (hitInteractable.CanHover(interactInput.input))
                {
                    hitInteractable.OnHoverStart(interactInput.input);
                    interactInput.lastTarget = hitInteractable;
                }
            }
        }
        return interactInput;
    }

    private bool IsInputGrabbing(BasisInput input)
    {
        return input.InputState.Trigger > 0.5f;
    }

    private void RemoveInput(string uid)
    {
        var inputs = InteractInputs.Where(x => x.deviceUid == uid).ToArray();
        if (inputs.Length > 1) 
        {
            BasisDebug.LogError("Interact Inputs has multiple inputs of the same UID. Please report this bug.");
        }

        if (inputs.Length > 0)
        {
            InteractInput input = inputs[0];
            if (input.lastTarget != null)
            {
                if (input.lastTarget.IsHoveredBy(input.input))
                {
                    input.lastTarget.OnHoverEnd(input.input, false);
                }

                if (input.lastTarget.IsInteractingWith(input.input))
                {
                    input.lastTarget.OnInteractEnd(input.input);
                }
            }
            

            Destroy(input.interactOrigin);
            InteractInputs.Remove(input);
        }
    }

    private void AddInput(BasisInput input)
    {
        GameObject interactOrigin = new GameObject("Interact Origin");

        LineRenderer lineRenderer = interactOrigin.AddComponent<LineRenderer>();
        SphereCollider sphereCollider = interactOrigin.AddComponent<SphereCollider>();
        HoverInteractSphere interactSphere = interactOrigin.AddComponent<HoverInteractSphere>();

        interactOrigin.transform.SetParent(input.transform);
        interactOrigin.layer = LayerMask.NameToLayer("Interactable");
        // TODO: custom config to use center of palm instead of raycast offset (IK palm? but that breaks input on a bad avi upload, no?)
        interactOrigin.transform.SetLocalPositionAndRotation(input.BasisDeviceMatchableNames.PositionRayCastOffset, Quaternion.Euler(input.BasisDeviceMatchableNames.RotationRaycastOffset));
        
        lineRenderer.enabled = false;
        lineRenderer.material = LineMaterial;
        lineRenderer.startWidth = interactLineWidth;
        lineRenderer.endWidth = interactLineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.textureMode = LineTextureMode.Tile;
        lineRenderer.positionCount = 2;
        lineRenderer.numCapVertices = 0;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        sphereCollider.isTrigger = true;
        sphereCollider.center = Vector3.zero;
        sphereCollider.radius = hoverRadius;
        // deskies cant hover grab :)
        sphereCollider.enabled = !IsDesktopCenterEye(input);


        InteractInput interactInput = new()
        {
            deviceUid = input.UniqueDeviceIdentifier,
            input = input,
            interactOrigin = interactOrigin,
            lineRenderer = lineRenderer,
            hoverInteract = interactSphere,
        };

        InteractInputs.Add(interactInput);
    }


    private void OnDrawGizmos()
    {
        foreach (var device in InteractInputs)
        {
            // pointer line
            Gizmos.DrawLine(device.interactOrigin.transform.position, device.interactOrigin.transform.position + device.interactOrigin.transform.forward * raycastDistance);

            // hover target line
            if (device.hoverInteract != null && device.hoverInteract.HoverTarget != null)
            {
                Gizmos.DrawLine(device.interactOrigin.transform.position, device.hoverInteract.TargetClosestPoint);
            }

            // hover sphere
            if (!IsDesktopCenterEye(device.input))
            {
                Gizmos.DrawWireSphere(device.interactOrigin.transform.position, hoverRadius);
            }
        }
    }

    public bool IsDesktopCenterEye(BasisInput input)
    {
        return input.TryGetRole(out BasisBoneTrackedRole role) && role == BasisBoneTrackedRole.CenterEye;
    }
}
