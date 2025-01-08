using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class HoverInteract : MonoBehaviour
{

    public InteractableObject HoverTarget;
    public float TargetDistance = float.PositiveInfinity;

    public Vector3 TargetClosestPoint = Vector3.zero;

    private SphereCollider sphereColliderRef;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sphereColliderRef = gameObject.GetComponent<SphereCollider>();
        ResetTarget();
    }

    // trigger stay does our updating so we dont need to manage a list of everything in bounds
    private void OnTriggerStay(Collider other)
    {
        InteractableObject otherInteractable = other.GetComponent<InteractableObject>();

        if (otherInteractable != null && sphereColliderRef != null)
        {
            Vector3 otherClosestPoint = other.ClosestPoint(transform.position);
            float otherDistance = Vector3.Distance(otherClosestPoint, transform.position);

            if (otherDistance < TargetDistance)
            {
                HoverTarget = otherInteractable;
                TargetDistance = otherDistance;
                TargetClosestPoint = otherClosestPoint;
            }
            else if (otherClosestPoint != TargetClosestPoint && otherInteractable.GetInstanceID() == HoverTarget.GetInstanceID())
            {
                TargetClosestPoint = otherClosestPoint;
            }

        }
    }

    private void OnTriggerExit(Collider other)
    {
        InteractableObject otherInteractable = other.GetComponent<InteractableObject>();
        // current target left
        if (otherInteractable != null && HoverTarget.GetInstanceID() == otherInteractable.GetInstanceID())
        {
            ResetTarget();
        }
    }

    private void ResetTarget()
    {
        HoverTarget = null;
        TargetClosestPoint = Vector3.zero;
        TargetDistance = float.PositiveInfinity;
    }
}
