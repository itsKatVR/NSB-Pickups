

using UnityEngine;
using Basis.Scripts.UI;
using Basis.Scripts.Networking.NetworkedPlayer;
using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Device_Management;
using System.Collections.Generic;
using System.Linq;
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
                lineRenderer.positionCount = 2;
                lineRenderer.numCapVertices = 1;
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                PickupDevice pickupDevice = new PickupDevice();
                pickupDevice.input = device;
                pickupDevice.lineRenderer = lineRenderer;

                pickupDevice.state = InteractableObjectState.Empty;
                
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
                if (hitInteractable != null)
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
                                    if(hitInteractable.GetInstanceID() != pickupDevice.targetObject.GetInstanceID()) {
                                        pickupDevice.targetObject.HighlightObject(false);
                                    }
                                    break;
                                case InteractableObjectState.Holding:
                                    break;
                                default:
                                    pickupDevice.targetObject = null;
                                    pickupDevice.state = InteractableObjectState.Empty;
                                    break;
                            }
                        }


                        // pickup only highlighted
                        if (pickupDevice.state == InteractableObjectState.Highlighting)
                        {
                            bool alreadyHeld = pickupDevices.Any(x => x.Value.targetObject != null && x.Value.targetObject.GetInstanceID() == hitInteractable.GetInstanceID()) || hitInteractable.IsHeld();
                            bool sameTarget = pickupDevice.targetObject != null && pickupDevice.targetObject.GetInstanceID() == hitInteractable.GetInstanceID();
                            
                            if (!alreadyHeld && hitInteractable.IsWithinRange(transform) || sameTarget)
                            {
                                pickupDevice.rayHit = hit;
                                pickupDevice.targetObject = hitInteractable;
                                pickupDevice.state = InteractableObjectState.Holding;
                                pickupDevice.targetObject.PickUp(pickupDevice.input.transform);
                            }
                        }
                        

                    }
                    // not holding
                    // highlight if we arent holding, drop any held
                    else
                    {
                        // handle previous states
                        if(pickupDevice.targetObject != null) {
                            // hit a different interactable

                            bool sameTarget = hitInteractable.GetInstanceID() == pickupDevice.targetObject.GetInstanceID();
                            switch (pickupDevice.state)
                            {
                                case InteractableObjectState.Highlighting:
                                    if(!sameTarget) {
                                        pickupDevice.targetObject.HighlightObject(false);
                                    }
                                    break;
                                case InteractableObjectState.Holding:
                                    // no longer holding
                                    pickupDevice.targetObject.Drop();
                                    pickupDevice.state = InteractableObjectState.Empty;
                                    break;
                                default:
                                    pickupDevice.targetObject = null;
                                    pickupDevice.state = InteractableObjectState.Empty;
                                    break;
                            }
                        }
                        
                        // TODO: self steal
                        if (!hitInteractable.IsHeld() && hitInteractable.IsWithinRange(transform)) {
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
                            pickupDevice.state = InteractableObjectState.Empty;
                            pickupDevice.targetObject.HighlightObject(false);
                            break;
                        case InteractableObjectState.Holding:
                        // Debug.LogError("missed raycast while holding, trigger: " + pickupDevice.input.InputState.Trigger);
                            // keep holding anyway
                            if (pickupDevice.input.InputState.Trigger == 1) {
                                pickupDevice.state = InteractableObjectState.Holding;
                                pickupDevice.targetObject.PickUp(pickupDevice.input.transform);
                                
                            } 
                            // missed and dropped
                            else {
                                pickupDevice.state = InteractableObjectState.Empty;
                                pickupDevice.targetObject.Drop();
                            }
                            
                            break;
                        default:
                            pickupDevice.state = InteractableObjectState.Empty;
                            pickupDevice.targetObject = null;
                            break;
                    }
                }
            }
            
            // write changes back into dictionary
            pickupDevices[pickupDevices.ElementAt(Index).Key] = pickupDevice;
        }

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
