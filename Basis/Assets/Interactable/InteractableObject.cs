using System;
using System.Collections.Generic;
using Basis.Scripts.Device_Management.Devices;
using UnityEditor.UI;
using UnityEngine;


// needs rigidbody for hover sphere `OnTriggerStay`
public abstract class InteractableObject: MonoBehaviour {
    public CachedList<InputSource> InputSources;

    [Header("Interactable Settings")]
    public float InteractRange = 1.0f;
    public bool CanEquip = false;
    public Vector3 equipPos;
    public Quaternion equipRot;


    /// <summary>
    /// Check if object is within range based on its transform and Interact Range
    /// </summary>
    /// <param name="inputTransform"></param>
    /// <returns></returns>
    public virtual bool IsWithinRange(Vector3 source) 
    {
        Collider collider = GetCollider();
        if (collider != null)
        {
            return Vector3.Distance(collider.ClosestPoint(source), source) <= InteractRange;
        }
        // fall back to object transform distance
        return Vector3.Distance(transform.position, source) <= InteractRange;
    }

    /// <summary>
    /// Gets collider on self, override with cached get whenever possible 
    /// </summary>
    public virtual Collider GetCollider()
    {
        if (TryGetComponent(out Collider col))
        {
            return col;
        }
        return null;
    }
    abstract public bool CanHover(BasisInput input);
    abstract public bool IsHoveredBy(BasisInput input);

    abstract public bool CanInteract(BasisInput input);
    abstract public bool IsInteractingWith(BasisInput input);

    abstract public void OnInteractStart(BasisInput input);


    abstract public void OnInteractEnd(BasisInput input);

    abstract public void OnHoverStart(BasisInput input);

    abstract public void OnHoverEnd(BasisInput input, bool willInteract);

    
    abstract public void InputUpdate();

    public struct InputSource
    {
        public InputSource(BasisInput source, bool isInteracting)
        {
            Source = source;
            IsInteracting = isInteracting;
        }
        public BasisInput Source {get; set;}
        /// <summary>
        /// - true: source interacting with object
        /// - false: source hovering
        /// If not either this source should not be in the list!
        /// </summary>
        public bool IsInteracting {get; set;}
    }
}

