using UnityEngine;

// Enum for body part types
public enum BodyPartType
{
    Head,
    Body,
    LeftGlove,
    RightGlove
}

// Component for collision detection on body parts
public class BodyPartCollision : MonoBehaviour
{
    private BoxingAgent parentAgent;
    private BodyPartType bodyPartType;
    private bool isInitialized = false;

    public void Initialize(BoxingAgent agent, BodyPartType type)
    {
        parentAgent = agent;
        bodyPartType = type;
        isInitialized = true;

        Debug.Log($"🔧 {gameObject.name}: BodyPartCollision initialized - Agent: {agent.gameObject.name}, PartType: {type}");

        // Ensure we have a collider set as trigger
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"⚠️ {gameObject.name}: Collider was not set as trigger! Setting isTrigger = true");
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isInitialized || parentAgent == null)
        {
            Debug.LogWarning($"⚠️ {gameObject.name}: BodyPartCollision not properly initialized");
            return;
        }

        Debug.Log($"🔥 {gameObject.name}: OnTriggerEnter - My type: {bodyPartType}, Other: {other.gameObject.name}");

        // ONLY handle glove collisions (simplified approach)
        // Only gloves should initiate hit detection to avoid double calls
        if (bodyPartType == BodyPartType.LeftGlove || bodyPartType == BodyPartType.RightGlove)
        {
            HandleGloveCollision(other);
        }

        // Body parts just log the collision but don't process it
        else if (bodyPartType == BodyPartType.Head || bodyPartType == BodyPartType.Body)
        {
            Debug.Log($"📝 {gameObject.name}: Body part {bodyPartType} was hit by {other.gameObject.name} (processed by glove)");
        }
    }

    private void HandleGloveCollision(Collider other)
    {
        Debug.Log($"👊 {gameObject.name}: HandleGloveCollision - My glove: {bodyPartType}, Hit object: {other.gameObject.name}");

        // Check if the other object is a body part
        BodyPartCollision otherBodyPart = other.GetComponent<BodyPartCollision>();
        if (otherBodyPart == null)
        {
            Debug.Log($"❌ {gameObject.name}: Other object has no BodyPartCollision component");
            return;
        }

        // Only process if hitting head or body (not other gloves)
        if (otherBodyPart.bodyPartType != BodyPartType.Head && otherBodyPart.bodyPartType != BodyPartType.Body)
        {
            Debug.Log($"❌ {gameObject.name}: Hit object is not head or body: {otherBodyPart.bodyPartType}");
            return;
        }

        // Check if this is hitting an opponent (not ourselves)
        BoxingAgent otherAgent = other.GetComponentInParent<BoxingAgent>();
        if (otherAgent == null)
        {
            Debug.Log($"❌ {gameObject.name}: No BoxingAgent found on target {other.gameObject.name}");
            return;
        }

        if (otherAgent == parentAgent)
        {
            Debug.Log($"❌ {gameObject.name}: Hit our own body part, ignoring");
            return;
        }

        Debug.Log($"✅ {gameObject.name}: Valid glove hit! {bodyPartType} -> {otherBodyPart.bodyPartType} on {otherAgent.gameObject.name}");

        // Notify our parent agent about the hit
        parentAgent.OnGloveTriggerHit(bodyPartType, otherBodyPart.bodyPartType, other.gameObject);
    }

    // Public getters for debugging
    public BoxingAgent GetParentAgent() => parentAgent;
    public BodyPartType GetBodyPartType() => bodyPartType;
    public bool IsInitialized() => isInitialized;
}