using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class HoverInteractSphere : MonoBehaviour
{

    public InteractableObject HoverTarget;
    public float TargetDistance = float.PositiveInfinity;

    public Vector3 TargetClosestPoint = Vector3.zero;

    private SphereCollider sphereColliderRef;

    void Start()
    {
        gameObject.TryGetComponent(out sphereColliderRef);
        ResetTarget();
    }

    // trigger stay does our updating so we dont need to manage a list of everything in bounds
    private void OnTriggerStay(Collider other)
    {
        if (sphereColliderRef != null && other.TryGetComponent(out InteractableObject otherInteractable))
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
        // current target left
        if (other.TryGetComponent(out InteractableObject otherInteractable) && HoverTarget.GetInstanceID() == otherInteractable.GetInstanceID())
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
