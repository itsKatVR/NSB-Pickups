

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

public class PlayerInteract : MonoBehaviour
{

    [Tooltip("How far the player can interact with objects.")]
    public float raycastDistance = 1.0f;
    [Tooltip("How far the player Hover.")]
    public float hoverDistance = 1.0f;
    [Tooltip("Both of the above are relative to object transforms, objects with larger colliders may have issues")]

    public struct PickupDevice
    {
        public BasisInput input { get; set; }
        public RaycastHit rayHit { get; set; }
        public SphereCollider hoverSphere { get; set; }
        public LineRenderer lineRenderer { get; set; }
        public InteractableObject lastTarget { get; set; }
    }

    public Dictionary<int, PickupDevice> pickupDevices = new Dictionary<int, PickupDevice>();



    public Material lineMaterial;
    public float lineWidth = 0.015f;
    public bool renderInteractLine = true;

    // TODO: load with addressable.  
    public static string LoadMaterialAddress = "Assets/Interactable/Material/RayCastMaterial.mat";


    private async void Start()
    {       
        BasisLocalPlayer.Instance.LocalBoneDriver.OnSimulate += Simulate;

        UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Material> op =  Addressables.LoadAssetAsync<Material>(LoadMaterialAddress);
        lineMaterial = op.WaitForCompletion();
    }
    public void OnDestroy()
    {
        BasisLocalPlayer.Instance.LocalBoneDriver.OnSimulate -= Simulate;
        // TODO: cleanup line renderers
    }

    [BurstCompile]
    private void Simulate()
    {
        int count = BasisDeviceManagement.Instance.AllInputDevices.Count;
        // TODO: replace this with a OnTrackersChanged event or equevilent 
        for (int Index = 0; Index < count; Index++)
        {
            BasisInput device = BasisDeviceManagement.Instance.AllInputDevices[Index];
            // if we can raycast retain in our devices.
            if (device.BasisDeviceMatchableNames.HasRayCastSupport && !pickupDevices.ContainsKey(Index))
            {
                device.gameObject.AddComponent(typeof(LineRenderer));
                LineRenderer lineRenderer = device.gameObject.GetComponent<LineRenderer>();
                lineRenderer.material = lineMaterial;
                lineRenderer.startWidth = lineWidth;
                lineRenderer.endWidth = lineWidth;
                lineRenderer.enabled = false;
                lineRenderer.useWorldSpace = true;
                lineRenderer.textureMode = LineTextureMode.Tile;
                lineRenderer.positionCount = 2;
                lineRenderer.numCapVertices = 0;
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                PickupDevice pickupDevice = new PickupDevice();
                pickupDevice.input = device;
                pickupDevice.lineRenderer = lineRenderer;

                pickupDevices[Index] = pickupDevice;
            }
            else
            {
                // TODO: remove other devices (needs VR testing)
                // Destroy(pickupDevices[Index].lineRenderer);
                // // remove any old devices if our input devices has changed, above will overwrite existing.
                // pickupDevices.Remove(Index);
            }
        }


        for (int Index = 0; Index < pickupDevices.Count; Index++)
        {
            PickupDevice pickupDevice = pickupDevices.ElementAt(Index).Value;
            if (pickupDevice.input == null)
            {
                Debug.LogWarning("Pickup input device unexpectedly null, input devices likely changed");
                continue;
            }

            Ray ray;
            if (pickupDevice.input.IsDesktopCenterEye()) {
                ray = new Ray(pickupDevice.input.transform.position, pickupDevice.input.transform.forward);
            }
            else
            {
                Vector3 origin = pickupDevice.input.transform.TransformPoint(pickupDevice.input.BasisDeviceMatchableNames.RotationRaycastOffset);
                // TODO: device rotation offset
                Vector3 direction = pickupDevice.input.transform.forward;
                ray = new Ray(origin, direction);
            }



            // TODO: sphere proximity (closest hit used)

            RaycastHit rayHit;
            // TODO: Interact layer mayhaps?
            // TODO: Ignore layers (player ect.)
            if (Physics.Raycast(ray, out rayHit, raycastDistance))
            {

                if (rayHit.collider == null)
                {
                    continue;
                }
                pickupDevice.rayHit = rayHit;

                InteractableObject hitInteractable;
                // TODO: some sort of type iteration to get one and only one interactable script of the included set (since we should be getting any classes extending InteractableObject)
                hitInteractable = rayHit.collider.GetComponent<ReparentInteractable>();

                // NOTE: this will skip a frame of hover after stopping interact
                if (hitInteractable != null)
                {
                    // hit a different target than last time
                    if (pickupDevice.lastTarget != null && pickupDevice.lastTarget.GetInstanceID() != hitInteractable.GetInstanceID())
                    {
                        // TODO: grab button instead of full trigger
                        // Holding Logic: 
                        if (pickupDevice.input.InputState.Trigger == 1)
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
                        //
                        if (pickupDevice.input.InputState.Trigger == 1)
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
                    if (pickupDevice.input.InputState.Trigger != 1 && pickupDevice.lastTarget.IsInteractingWith(pickupDevice.input))
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

        // update objects
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
                    Vector3 start = pickupDevice.input.transform.position;

                    // desktop offset for center eye (a little to the bottom right)
                    if (pickupDevice.input.IsDesktopCenterEye())
                    {

                        start = pickupDevice.input.transform.position + (pickupDevice.input.transform.forward * 0.1f) + Vector3.down * 0.1f + (pickupDevice.input.transform.right * 0.1f);
                    }
                    pickupDevice.lineRenderer.SetPosition(0, start);
                    pickupDevice.lineRenderer.SetPosition(1, pickupDevice.rayHit.point);
                    pickupDevice.lineRenderer.enabled = true;
                }
                else
                {
                    if(pickupDevice.lineRenderer != null) {
                        pickupDevice.lineRenderer.enabled = false;
                    }
                }
            }
        } 
        // turn all the lines off
        else {
            foreach (KeyValuePair<int, PickupDevice> kv in pickupDevices)
            {
                PickupDevice pickupDevice = kv.Value;
                pickupDevice.lineRenderer.enabled = false;
            }
        }


    }

    // TODO: add this back in...
    private void OnDrawGizmos()
    {
        // Gizmos.DrawLine(transform.position, transform.position + transform.forward * raycastDistance);
        // Gizmos.DrawWireSphere(transform.position + transform.forward * raycastDistance, 0.05f);
    }
}
