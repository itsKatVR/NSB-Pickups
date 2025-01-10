using System;
using System.Collections.Generic;
using Basis.Scripts.Device_Management.Devices;
using UnityEditor.UI;
using UnityEngine;


[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public abstract class InteractableObject: MonoBehaviour {
    public List<InputSource> InputSources;

    public Vector3 equipPos;
    public Quaternion equipRot;

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
    [Header("Interaction Settings")]
    public float InteractRange = 1.0f;

    abstract public bool IsInteracting();

    /// <summary>
    /// Check if object is within range based on its transform and Interact Range
    /// </summary>
    /// <param name="inputTransform"></param>
    /// <returns></returns>
    public virtual bool IsWithinRange(Vector3 source) {
        return Vector3.Distance(transform.position, source) <= InteractRange;
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


    abstract public void OnOwnershipTransfer(bool isOwner);
}

