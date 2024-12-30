// using System.Diagnostics;
using UnityEngine;
using UnityEngine.Animations;

public class InteractablePhysicObject : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float pickupRange = 1.0f;
    public bool DisableCollisionsWhileHeld = true;

    [Header("References")]
    public Renderer objectRenderer;
    public Material highlightMaterial;
    public Material originalMaterial;
    public bool isHeld = false;
    private Rigidbody rb;
    private Collider col;
    private ConfigurableJoint joint;
    private bool lastDetectCollisions;

    private Transform anchor;
    private float offsetDistance;
    private Quaternion offsetRot;

    public BasisObjectSyncNetworking syncNetworking;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>(); 
        joint = GetComponent<ConfigurableJoint>();
        
        joint.autoConfigureConnectedAnchor = false;
        joint.configuredInWorldSpace = true;
        LockJoint(false);

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

    public bool IsHeld()
    {
        return isHeld;
    }

    public bool IsWithinRange(Transform playerCamera)
    {
        return Vector3.Distance(transform.position, playerCamera.position) <= pickupRange;
    }

    public void HighlightObject(bool highlight)
    {
        // Debug log to check if HighlightObject is being called correctly
        Debug.Log("Highlighting object: " + (highlight ? "ON" : "OFF"));
        if (objectRenderer && highlightMaterial && originalMaterial)
        {
            // Log the material being applied
            Debug.Log("Applying material: " + (highlight ? highlightMaterial.name : originalMaterial.name));
            objectRenderer.sharedMaterial = highlight ? highlightMaterial : originalMaterial;
        }
    }

    public void PickUp(Transform parent)
    {
        if (!isHeld)
        {
            isHeld = true;

            if (DisableCollisionsWhileHeld)
            {
                // Disable physics (set Rigidbody to ignore collisions) while holding
                lastDetectCollisions = col.isTrigger;
                col.isTrigger = true;
            }

            

            // save object distance and rotation
            anchor = parent;
            offsetDistance = Vector3.Distance(transform.position, parent.position);
            offsetRot = gameObject.transform.rotation;

            // lock all axes of the joint
            LockJoint(true);

            // Update the networked data (Storeddata) to reflect the position, rotation, and scale
            syncNetworking.Storeddata.Position = transform.position;
            syncNetworking.Storeddata.Rotation = transform.rotation;
            syncNetworking.Storeddata.Scale = transform.localScale;

            // Set ownership to the local player when they pick up the object
            syncNetworking.IsOwner = true;

            // Disable object highlight once picked up
            HighlightObject(false);
        }
    }

    public void LockJoint(bool isLocked) {
        if(isLocked){
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;
        } else {
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;

            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;
        }
    }

    public void FixedUpdate() {
        if(isHeld) {
            // set world position and rotation of the joint
            joint.connectedAnchor = anchor.position + anchor.forward * offsetDistance;
            joint.targetRotation = Quaternion.Inverse(anchor.rotation) * offsetRot;
            // TODO: send postition to network while held
        }
    }

    public void Drop()
    {
        if (isHeld)
        {
            isHeld = false;

            // unlock joint so physics can be applied
            LockJoint(false);

            if (DisableCollisionsWhileHeld && col.isTrigger)
            {
                col.isTrigger = lastDetectCollisions; // Re-enable collisions
            }

            // When dropped, update the networked data to reflect the new position and rotation
            syncNetworking.Storeddata.Position = transform.position;
            syncNetworking.Storeddata.Rotation = transform.rotation;
            syncNetworking.Storeddata.Scale = transform.localScale;

            // Transfer ownership back when dropped | Local player no longer owns the object
            syncNetworking.IsOwner = false;
        }
    }
}