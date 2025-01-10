

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

public class PlayerInteract : MonoBehaviour
{

    [Tooltip("How far the player can interact with objects. Must > hoverDistance")]
    public float raycastDistance = 1.0f;
    [Tooltip("How far the player Hover.")]
    public float hoverRadius = 0.5f;
    [Tooltip("Both of the above are relative to object transforms, objects with larger colliders may have issues")]

    public struct PickupDevice
    {
        public BasisInput input { get; set; }
        public Vector3 interactHitPoint { get; set; }
        public GameObject interactOrigin { get; set; }
        public InteractableObject lastTarget { get; set; }
    }

    public Dictionary<int, PickupDevice> pickupDevices = new Dictionary<int, PickupDevice>();

    public Material lineMaterial;
    private AsyncOperationHandle<Material> asyncOperationLineMaterial;
    public float interactLineWidth = 0.015f;
    public bool renderInteractLine = true;

    // TODO: load with addressable.  
    public static string LoadMaterialAddress = "Assets/Interactable/Material/RayCastMaterial.mat";


    private async void Start()
    {
        BasisLocalPlayer.Instance.LocalBoneDriver.OnSimulate += Simulate;
        BasisDeviceManagement.Instance.AllInputDevices.OnListChanged += OnInputChanged;
        BasisDeviceManagement.Instance.AllInputDevices.OnListItemRemoved += OnInputRemoved;

        AsyncOperationHandle<Material> op = Addressables.LoadAssetAsync<Material>(LoadMaterialAddress);
        lineMaterial = op.WaitForCompletion();
        op.Release();
        // Debug.LogError(gameObject);
    }
    public void OnDestroy()
    {
        if(asyncOperationLineMaterial.IsValid())
        {
            asyncOperationLineMaterial.Release();
        }
        BasisLocalPlayer.Instance.LocalBoneDriver.OnSimulate -= Simulate;
        BasisDeviceManagement.Instance.AllInputDevices.OnListChanged -= OnInputChanged;
        BasisDeviceManagement.Instance.AllInputDevices.OnListItemRemoved -= OnInputRemoved;
        // TODO: cleanup line renderers
    }

    private void OnInputChanged()
    {

        int count = BasisDeviceManagement.Instance.AllInputDevices.Count;
        for (int Index = 0; Index < count; Index++)
        {
            BasisInput device = BasisDeviceManagement.Instance.AllInputDevices[Index];
            if (!device)
            {
                return;
            }
                
            // if we can raycast retain in our devices.
            if (device.BasisDeviceMatchableNames != null && device.BasisDeviceMatchableNames.HasRayCastSupport && !pickupDevices.ContainsKey(Index))
            {
                AddInput(device, Index);
            }
            // new device at the same index
            else if (device.BasisDeviceMatchableNames != null && device.BasisDeviceMatchableNames.HasRayCastSupport && pickupDevices.ContainsKey(Index) && pickupDevices[Index].input.GetInstanceID() != device.GetInstanceID())
            {
                RemoveInput(Index);
                AddInput(device, Index);
            }
            // TODO: what if it has no matchable name?
            // device removed handled elsewhere
        }
    }

    private void OnInputRemoved(BasisInput input)
    {
        var matching = pickupDevices.Where(x => x.Value.input.GetInstanceID() == input.GetInstanceID()).ToList();
        Debug.Assert(matching.Count <= 1, "Player Interact has multiple inputs of the same reference");
        if (matching.Count > 0)
        {
            pickupDevices.Remove(matching[0].Key);
        }
    }

    // simulate after IK update
    [BurstCompile]
    private void Simulate()
    {
        for (int Index = 0; Index < pickupDevices.Count; Index++)
        {
            PickupDevice pickupDevice = pickupDevices.ElementAt(Index).Value;
            if (pickupDevice.input == null)
            {
                Debug.LogWarning("Pickup input device unexpectedly null, input devices likely changed");
                continue;
            }

            HoverInteract hoverSphere = pickupDevice.interactOrigin.GetComponent<HoverInteract>();

            Ray ray;
            if (hoverSphere.HoverTarget != null)
            {
                Vector3 direction = (pickupDevice.interactOrigin.transform.position - hoverSphere.TargetClosestPoint).normalized;
                ray = new Ray(pickupDevice.interactOrigin.transform.position, direction);
            }
            else
            {
                if (pickupDevice.input.IsDesktopCenterEye())
                {
                    ray = new Ray(pickupDevice.input.transform.position, pickupDevice.input.transform.forward);
                }
                else
                {
                    Vector3 origin = pickupDevice.interactOrigin.transform.position;
                    Vector3 direction = pickupDevice.interactOrigin.transform.forward;
                    ray = new Ray(origin, direction);
                }
            }


            RaycastHit rayHit;
            // TODO: Interact layer
            if (Physics.Raycast(ray, out rayHit, raycastDistance) || hoverSphere.HoverTarget != null)
            {

                if (rayHit.collider == null && hoverSphere.HoverTarget == null)
                {
                    continue;
                }


                InteractableObject hitInteractable;
                if (hoverSphere.HoverTarget != null)
                {
                    hitInteractable = hoverSphere.HoverTarget;
                    pickupDevice.interactHitPoint = hoverSphere.TargetClosestPoint;
                }
                // TODO: some sort of type iteration to get one and only one interactable script of the included set (since we should be getting any classes extending InteractableObject)
                else
                {
                    hitInteractable = rayHit.collider.GetComponent<ReparentInteractable>();
                    pickupDevice.interactHitPoint = rayHit.point;
                }

                if (hitInteractable != null)
                {
                    // NOTE: this will skip a frame of hover after stopping interact
                    pickupDevice = UpdatePickupState(hitInteractable, pickupDevice);
                }
            }
            // hover misssed entirely 
            else
            {
                if (pickupDevice.lastTarget != null)
                {
                    // seperate if blocks in case implementation allows for hovering and holding of the same object

                    // TODO: proximity check so we dont keep interacting with objects out side of player's reach. Needs an impl that wont break under lag though. `|| !pickupDevice.targetObject.IsWithinRange(pickupDevice.input.transform)`
                    // only drop if trigger was released
                    if (!IsInputGrabbing(pickupDevice.input) && pickupDevice.lastTarget.IsInteractingWith(pickupDevice.input))
                    {
                        pickupDevice.lastTarget.OnInteractEnd(pickupDevice.input);
                    }

                    if (pickupDevice.lastTarget.IsHoveredBy(pickupDevice.input))
                    {
                        pickupDevice.lastTarget.OnHoverEnd(pickupDevice.input, false);
                    }
                }
            }

            // write changes back into dictionary
            pickupDevices[pickupDevices.ElementAt(Index).Key] = pickupDevice;
        }


        // update objects, seperate list to ensure each target only gets one update
        List<InteractableObject> updateList = new List<InteractableObject>();
        foreach (KeyValuePair<int, PickupDevice> kv in pickupDevices)
        {
            if (kv.Value.lastTarget != null && !updateList.Any(x => x.GetInstanceID() == kv.Value.lastTarget.GetInstanceID()))
            {
                updateList.Add(kv.Value.lastTarget);
            }
        }
        foreach (InteractableObject interactable in updateList)
        {
            interactable.InputUpdate();
        }


        // apply line renderer
        if (renderInteractLine)
        {
            foreach (KeyValuePair<int, PickupDevice> kv in pickupDevices)
            {
                PickupDevice pickupDevice = kv.Value;
                if (pickupDevice.lastTarget != null && pickupDevice.lastTarget.IsHoveredBy(pickupDevice.input))
                {
                    Vector3 start = pickupDevice.interactOrigin.transform.position;

                    // desktop offset for center eye (a little to the bottom right)
                    if (pickupDevice.input.IsDesktopCenterEye())
                    {
                        start = pickupDevice.interactOrigin.transform.position + (pickupDevice.interactOrigin.transform.forward * 0.1f) + Vector3.down * 0.1f + (pickupDevice.interactOrigin.transform.right * 0.1f);
                    }
                    LineRenderer lineRenderer = pickupDevice.interactOrigin.GetComponent<LineRenderer>();
                    lineRenderer.SetPosition(0, start);
                    lineRenderer.SetPosition(1, pickupDevice.interactHitPoint);
                    lineRenderer.enabled = true;
                }
                else
                {
                    if (pickupDevice.interactOrigin != null)
                    {
                        pickupDevice.interactOrigin.GetComponent<LineRenderer>().enabled = false;
                    }
                }
            }
        }
        // turn all the lines off
        else
        {
            foreach (KeyValuePair<int, PickupDevice> kv in pickupDevices)
            {
                PickupDevice pickupDevice = kv.Value;
                pickupDevice.interactOrigin.GetComponent<LineRenderer>().enabled = false;
            }
        }
    }

    private PickupDevice UpdatePickupState(InteractableObject hitInteractable, PickupDevice pickupDevice)
    {
        // hit a different target than last time
        if (pickupDevice.lastTarget != null && pickupDevice.lastTarget.GetInstanceID() != hitInteractable.GetInstanceID())
        {
            // TODO: grab button instead of full trigger
            // Holding Logic: 
            if (IsInputGrabbing(pickupDevice.input))
            {
                // clear hover (unlikely to happen since last frame, but possible)
                if (pickupDevice.lastTarget.IsHoveredBy(pickupDevice.input))
                {
                    pickupDevice.lastTarget.OnHoverEnd(pickupDevice.input, false);
                }

                // interacted with new hit since last frame & we arent holding (in which case do nothing)
                if (hitInteractable.CanInteract(pickupDevice.input) && !pickupDevice.lastTarget.IsInteractingWith(pickupDevice.input))
                {
                    hitInteractable.OnInteractStart(pickupDevice.input);
                    pickupDevice.lastTarget = hitInteractable;
                }
            }
            // No trigger
            else
            {
                bool removeTarget = false;
                // end iteract of hit (unlikely since we just hit it this update)
                if (hitInteractable.IsInteractingWith(pickupDevice.input))
                {
                    hitInteractable.OnInteractEnd(pickupDevice.input);
                }
                // end interact of previous object
                if (pickupDevice.lastTarget.IsInteractingWith(pickupDevice.input))
                {
                    pickupDevice.lastTarget.OnInteractEnd(pickupDevice.input);
                    removeTarget = true;
                }

                // hover missed previous object
                if (pickupDevice.lastTarget.IsHoveredBy(pickupDevice.input))
                {
                    pickupDevice.lastTarget.OnHoverEnd(pickupDevice.input, false);
                    removeTarget = true;
                }

                // remove here in case both hover and interact ended
                if (removeTarget)
                {
                    pickupDevice.lastTarget = null;
                }

                // try hovering new interactable
                if (hitInteractable.CanHover(pickupDevice.input))
                {
                    hitInteractable.OnHoverStart(pickupDevice.input);
                    pickupDevice.lastTarget = hitInteractable;
                }
            }

        }
        // hitting same interactable
        else
        {

            // TODO: middle finger grab instead of full trigger
            // Pickup logic: 
            // per input an object can be either held or hovered, not both. Objects can ignore this by purposfully modifying IsHovered/IsInteracted.
            if (IsInputGrabbing(pickupDevice.input))
            {
                // first clear hover...
                if (hitInteractable.IsHoveredBy(pickupDevice.input))
                {
                    hitInteractable.OnHoverEnd(pickupDevice.input, hitInteractable.CanInteract(pickupDevice.input));
                }

                // then try to interact
                // TODO: hand set pickup limitations
                if (hitInteractable.CanInteract(pickupDevice.input))
                {
                    hitInteractable.OnInteractStart(pickupDevice.input);
                    pickupDevice.lastTarget = hitInteractable;
                }
            }
            // not holding
            // hover if we arent holding, drop any held
            else
            {
                // first end interact...
                if (hitInteractable.IsInteractingWith(pickupDevice.input))
                {
                    hitInteractable.OnInteractEnd(pickupDevice.input);
                }

                // then hover
                if (hitInteractable.CanHover(pickupDevice.input))
                {
                    hitInteractable.OnHoverStart(pickupDevice.input);
                    pickupDevice.lastTarget = hitInteractable;
                }
            }
        }
        return pickupDevice;
    }

    private bool IsInputGrabbing(BasisInput input)
    {
        return input.InputState.Trigger > 0.5f;
    }

    private void RemoveInput(int Index)
    {
        if (pickupDevices.TryGetValue(Index, out PickupDevice device))
        {
            if (device.lastTarget.IsHoveredBy(device.input))
            {
                device.lastTarget.OnHoverEnd(device.input, false);
            }

            if (device.lastTarget.IsInteractingWith(device.input))
            {
                device.lastTarget.OnInteractEnd(device.input);
            }

            Destroy(device.interactOrigin);
            pickupDevices.Remove(Index);
        }
    }

    private void AddInput(BasisInput input, int Index)
    {
        var components = new Type[] { typeof(LineRenderer), typeof(SphereCollider), typeof(HoverInteract) };

        GameObject interactOrigin = new GameObject("Interact Origin", components);
        interactOrigin.transform.SetParent(input.transform);
        interactOrigin.layer = LayerMask.NameToLayer("Ignore Raycast");
        // TODO: custom config to use center of palm instead of raycast offset (IK palm? but that breaks input on a bad avi upload, no?)
        interactOrigin.transform.localPosition = input.BasisDeviceMatchableNames.PositionRayCastOffset;
        interactOrigin.transform.localRotation = Quaternion.Euler(input.BasisDeviceMatchableNames.RotationRaycastOffset);


        LineRenderer lineRenderer = interactOrigin.GetComponent<LineRenderer>();
        lineRenderer.enabled = false;
        lineRenderer.material = lineMaterial;
        lineRenderer.startWidth = interactLineWidth;
        lineRenderer.endWidth = interactLineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.textureMode = LineTextureMode.Tile;
        lineRenderer.positionCount = 2;
        lineRenderer.numCapVertices = 0;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        SphereCollider sphereCollider = interactOrigin.GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.center = Vector3.zero;
        sphereCollider.radius = hoverRadius;
        // deskies cant hover grab :)
        sphereCollider.enabled = !input.IsDesktopCenterEye();

        PickupDevice pickupDevice = new PickupDevice();
        pickupDevice.input = input;
        pickupDevice.interactOrigin = interactOrigin;

        pickupDevices[Index] = pickupDevice;
    }

    private void OnDrawGizmos()
    {
        foreach (var device in pickupDevices.Values)
        {
            // pointer line
            Gizmos.DrawLine(device.interactOrigin.transform.position, device.interactOrigin.transform.position + device.interactOrigin.transform.forward * raycastDistance);

            // hover target line
            HoverInteract hover = device.interactOrigin.GetComponent<HoverInteract>();
            if (hover != null && hover.HoverTarget != null)
            {
                Gizmos.DrawLine(device.interactOrigin.transform.position, hover.TargetClosestPoint);
            }

            // hover sphere
            if (!device.input.IsDesktopCenterEye())
            {
                Gizmos.DrawWireSphere(device.interactOrigin.transform.position, hoverRadius);
            }
        }
    }
}
