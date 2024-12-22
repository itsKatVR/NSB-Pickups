using Basis.Scripts.Addressable_Driver;
using Basis.Scripts.Addressable_Driver.Enums;
using Basis.Scripts.Addressable_Driver.Factory;
using Basis.Scripts.Addressable_Driver.Resource;

using UnityEngine;
using UnityEngine.InputSystem;
using Basis.Scripts.UI;
using Basis.Scripts.Networking.NetworkedPlayer;
using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Device_Management;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEditor.VersionControl;
using Basis.Scripts.Device_Management.Devices;

public class PlayerInteract : MonoBehaviour
{
    [Tooltip("How far the player can interact with objects.")]
    public float raycastDistance = 1.0f;

    private BasisInputModuleHandler inputActions;

    public struct PickupDevice
    {
        public BasisInput input { get; set; }
        public RaycastHit rayHit { get; set; }
        public LineRenderer lineRenderer { get; set; }
        public InteractableObject targetObject { get; set; }
        public InteractableObjectState state {get; set;}
    }

    public enum InteractableObjectState {
        Highlighting,
        Holding,
        Empty,
    }
    public Dictionary<int, PickupDevice> pickupDevices = new Dictionary<int, PickupDevice>();



    public Material lineMaterial;
    public float lineWidth = 0.015f;
    public bool renderInteractLine = true;

    // TODO: Load material in, either thru addressable or otherwise.  
    public static string LoadMaterialAddress = "Assets/Interactable/Material/RayCastMaterial.mat";


    private void Start()
    {
        inputActions = new BasisInputModuleHandler();
        BasisLocalPlayer.Instance.LocalBoneDriver.OnSimulate += Simulate;

        Debug.LogError("PlayerInteract", gameObject);
    }
    public void OnDestroy()
    {
        BasisLocalPlayer.Instance.LocalBoneDriver.OnSimulate -= Simulate;
        // TODO: cleanup line renderers
    }

    private void Simulate()
    {
        int count = BasisDeviceManagement.Instance.AllInputDevices.Count;
        // TODO: replace this with a OnTrackersChanged event or equevilent 
        for (int Index = 0; Index < count; Index++)
        {
            BasisInput device = BasisDeviceManagement.Instance.AllInputDevices[Index];
            // if we can raycast retain in our devices
            if (device.BasisDeviceMatchableNames.HasRayCastSupport)
            {
                device.gameObject.AddComponent(typeof(LineRenderer));
                LineRenderer lineRenderer = device.gameObject.GetComponent<LineRenderer>();
                lineRenderer.material = lineMaterial;
                lineRenderer.startWidth = lineWidth;
                lineRenderer.endWidth = lineWidth;
                lineRenderer.enabled = false;
                lineRenderer.useWorldSpace = true;
                lineRenderer.positionCount = 2;
                lineRenderer.numCapVertices = 6;
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                PickupDevice pickupDevice = new PickupDevice();
                pickupDevice.input = device;
                pickupDevice.lineRenderer = lineRenderer;
                pickupDevices[Index] = pickupDevice;
            }
            else
            {
                Destroy(pickupDevices[Index].lineRenderer);
                // remove any old devices if our input devices has changed, above will overwrite existing.
                pickupDevices.Remove(Index);
            }
        }

        /// do a raycast pass on all our input devices
        for (int Index = 0; Index < pickupDevices.Count; Index++)
        {

            PickupDevice pickupDevice = pickupDevices.ElementAt(Index).Value;

            if (pickupDevice.input == null)
            {
                Debug.LogWarning("Pickup input device unexpectedly null, input devices likely changed");
                continue;
            }

            // TODO: is this palm or pointing?
            Ray ray = new Ray(pickupDevice.input.transform.position, pickupDevice.input.transform.forward);
            RaycastHit hit;


            // TODO: Interact layer mayhaps?
            // TODO: Ignore layers (player ect.)
            if (Physics.Raycast(ray, out hit, raycastDistance))
            {

                if (hit.collider == null)
                {
                    continue;
                }
                InteractableObject hitInteractable = hit.collider.GetComponent<InteractableObject>();


                // TODO: self and remote stealing
                if (hitInteractable != null && hitInteractable.IsWithinRange(transform))
                {


                    // TODO: middle finger grab instead of full trigger
                    // pickup
                    if (pickupDevice.input.InputState.Trigger == 1)
                    {
                        // handle previous states
                        if(pickupDevice.targetObject != null) {
                            // hit a different interactable
                            switch (pickupDevice.state)
                            {
                                case InteractableObjectState.Highlighting:
                                    pickupDevice.targetObject.HighlightObject(false);
                                    break;
                                case InteractableObjectState.Holding:
                                    if(hitInteractable.GetInstanceID() != pickupDevice.targetObject.GetInstanceID()) {
                                        pickupDevice.targetObject.Drop();
                                    }
                                    break;
                                default:
                                    pickupDevice.targetObject = null;
                                    break;
                            }
                        }


                        // pickup highlighted
                        if (pickupDevice.state == InteractableObjectState.Highlighting)
                        {
                            bool skipSameObject = !pickupDevices.Any(x => x.Value.targetObject != null && x.Value.targetObject.GetInstanceID() == hitInteractable.GetInstanceID());
                            // no self steal
                            if (skipSameObject || (!pickupDevice.targetObject.IsHeld() || pickupDevice.targetObject.GetInstanceID() == hitInteractable.GetInstanceID()))
                            {
                                pickupDevice.rayHit = hit;
                                pickupDevice.targetObject = hitInteractable;
                                pickupDevice.state = InteractableObjectState.Holding;
                                pickupDevice.targetObject.PickUp(pickupDevice.input.transform);
                            }
                        }
                        

                    }
                    // highlight if we arent holding, drop any held
                    else
                    {
                        // handle previous states
                        if(pickupDevice.targetObject != null) {
                            // hit a different interactable
                            if(hitInteractable.GetInstanceID() != pickupDevice.targetObject.GetInstanceID()) {
                                switch (pickupDevice.state)
                                {
                                    case InteractableObjectState.Highlighting:
                                        pickupDevice.targetObject.HighlightObject(false);
                                        break;
                                    case InteractableObjectState.Holding:
                                        pickupDevice.targetObject.Drop();
                                        break;
                                    default:
                                        pickupDevice.targetObject = null;
                                        break;
                                }
                            } else {
                                if (pickupDevice.state == InteractableObjectState.Holding) {
                                    pickupDevice.targetObject.Drop();
                                }
                            }
                        }
                        
                        // TODO: self steal
                        if (!hitInteractable.IsHeld()) {
                            pickupDevice.rayHit = hit;
                            pickupDevice.targetObject = hitInteractable;
                            pickupDevice.state = InteractableObjectState.Highlighting;
                            pickupDevice.targetObject.HighlightObject(true);
                        }
                    }
                }
            }
            else
            {
                // raycast missed, dont highlight anything
                if (pickupDevice.targetObject != null) {
                    switch (pickupDevice.state)
                    {
                        case InteractableObjectState.Highlighting:
                            pickupDevice.targetObject.HighlightObject(false);
                            break;
                        case InteractableObjectState.Holding:
                            pickupDevice.targetObject.Drop();
                            break;
                        default:
                            pickupDevice.targetObject = null;
                            break;
                    }
                }
                pickupDevice.targetObject = null;
                pickupDevice.state = InteractableObjectState.Empty;
            }
            
            // write changes back into dictionary
            pickupDevices[pickupDevices.ElementAt(Index).Key] = pickupDevice;
        }

        // // apply grab
        // List<InteractableObject> toRemoveHeld = new List<InteractableObject>();
        // foreach ((InteractableObject, BasisInput) held in heldObjects)
        // {
        //     Transform activelyHeldBy = null;
        //     foreach (KeyValuePair<int, PickupDevice> kv in pickupDevices)
        //     {
        //         if (kv.Value.targetObject != null && kv.Value.targetObject.GetInstanceID() == held.Item1.GetInstanceID() && kv.Value.input.GetInstanceID() == held.Item2.GetInstanceID())
        //         {
        //             activelyHeldBy = kv.Value.input.transform;
        //         }
        //     }
        //     if (activelyHeldBy != null)
        //     {
        //         held.Item1.PickUp(activelyHeldBy.transform);
        //     }
        //     else
        //     {
        //         // no longer actively held
        //         held.Item1.Drop();
        //         toRemoveHeld.Add(held.Item1);
        //     }
        // }
        // foreach (InteractableObject interactable in toRemoveHeld)
        // {
        //     heldObjects.RemoveAll(x => x.Item1.GetInstanceID() == interactable.GetInstanceID());
        // }

        // // apply highlighting
        // List<InteractableObject> toRemoveHighlight = new List<InteractableObject>();
        // foreach (InteractableObject interactable in highlightedObjects)
        // {
        //     if (interactable == null) { continue; }

        //     bool isActivelyHighlighed = false;
        //     foreach (KeyValuePair<int, PickupDevice> kv in pickupDevices)
        //     {
        //         if (kv.Value.highlightedObject != null && kv.Value.highlightedObject.GetInstanceID() == interactable.GetInstanceID())
        //         {
        //             isActivelyHighlighed = true;
        //         }
        //     }
        //     if (isActivelyHighlighed)
        //     {
        //         interactable.HighlightObject(true);
        //     }
        //     else
        //     {
        //         interactable.HighlightObject(false);
        //         toRemoveHighlight.Add(interactable);
        //     }
        // }
        // foreach (InteractableObject interactable in toRemoveHighlight)
        // {
        //     highlightedObjects.Remove(interactable);
        // }


        // apply line renderer
        if (renderInteractLine)
        {
            foreach (KeyValuePair<int, PickupDevice> kv in pickupDevices)
            {
                PickupDevice pickupDevice = kv.Value;
                if (pickupDevice.targetObject != null && pickupDevice.state == InteractableObjectState.Highlighting)
                {
                    Basis.Scripts.TransformBinders.BoneControl.BasisBoneTrackedRole role;

                    Vector3 start = pickupDevice.input.transform.position;
                    // desktop offset for center eye (a little to the bottom right)
                    if (pickupDevice.input.TryGetRole(out role) && role == Basis.Scripts.TransformBinders.BoneControl.BasisBoneTrackedRole.CenterEye)
                    {

                        start = pickupDevice.input.transform.position + (pickupDevice.input.transform.forward * 0.1f) + Vector3.down * 0.1f + (pickupDevice.input.transform.right * 0.1f);
                    }
                    pickupDevice.lineRenderer.SetPosition(0, start);
                    pickupDevice.lineRenderer.SetPosition(1, pickupDevice.rayHit.point);
                    pickupDevice.lineRenderer.enabled = true;
                }
                else
                {
                    pickupDevice.lineRenderer.enabled = false;
                }
            }
        }


    }

    // private void OnDrawGizmos()
    // {
    //     Gizmos.DrawLine(transform.position, transform.position + transform.forward * raycastDistance);
    //     Gizmos.DrawWireSphere(transform.position + transform.forward * raycastDistance, 0.05f);
    // }
}
