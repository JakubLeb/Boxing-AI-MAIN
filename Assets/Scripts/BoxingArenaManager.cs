using UnityEngine;
using Unity.MLAgents;

public class BoxingArenaManager : MonoBehaviour
{
    [Header("Arena Settings")]
    public Transform arenaCenter;
    public Vector2 arenaSize = new Vector2(4f, 4f);

    [Header("Agents")]
    public BoxingAgent agent1;
    public BoxingAgent agent2;

    [Header("Spawn Settings")]
    public float minDistanceBetweenAgents = 1.5f;
    public bool randomizeRotation = true;
    public bool faceEachOther = true;
    [Range(0f, 45f)]
    public float rotationRandomness = 15f;

    [Header("Visual Debug")]
    public bool showArenaGizmos = true;
    public Color arenaColor = Color.green;
    public Color spawnAreaColor = Color.yellow;

    private void Start()
    {
        if (agent1 != null && agent2 != null)
        {
            agent1.opponent = agent2.transform;
            agent2.opponent = agent1.transform;
        }
        PositionAgents();
    }

    public void PositionAgents()
    {
        if (agent1 == null || agent2 == null) return;
        Vector3 centerPos = arenaCenter != null ? arenaCenter.position : Vector3.zero;
        Vector3 pos1, pos2;
        GenerateValidPositions(centerPos, out pos1, out pos2);
        agent1.transform.position = pos1;
        agent2.transform.position = pos2;

        if (randomizeRotation) SetRandomRotations();
        else if (faceEachOther) SetFacingRotations();

        Debug.Log($"Arena: Positioned agents at {pos1} and {pos2}");
    }

    private void GenerateValidPositions(Vector3 center, out Vector3 pos1, out Vector3 pos2)
    {
        int maxAttempts = 100, attempts = 0;
        float safeZoneX = (arenaSize.x * 0.5f) - 0.5f;
        float safeZoneZ = (arenaSize.y * 0.5f) - 0.5f;

        do
        {
            pos1 = new Vector3(center.x + Random.Range(-safeZoneX, safeZoneX), center.y, center.z + Random.Range(-safeZoneZ, safeZoneZ));
            pos2 = new Vector3(center.x + Random.Range(-safeZoneX, safeZoneX), center.y, center.z + Random.Range(-safeZoneZ, safeZoneZ));
            attempts++;
        }
        while (Vector3.Distance(pos1, pos2) < minDistanceBetweenAgents && attempts < maxAttempts);

        if (attempts >= maxAttempts)
        {
            pos1 = center + new Vector3(-safeZoneX * 0.7f, 0, 0);
            pos2 = center + new Vector3(safeZoneX * 0.7f, 0, 0);
            Debug.LogWarning("Arena: Used fallback positioning after max attempts");
        }
    }

    private void SetRandomRotations()
    {
        agent1.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        agent2.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
    }

    private void SetFacingRotations()
    {
        Vector3 direction1 = (agent2.transform.position - agent1.transform.position).normalized;
        Quaternion lookRotation1 = Quaternion.LookRotation(direction1 * agent1.directionMultiplier);
        float randomOffset1 = Random.Range(-rotationRandomness, rotationRandomness);
        agent1.transform.rotation = lookRotation1 * Quaternion.Euler(0, randomOffset1, 0);

        Vector3 direction2 = (agent1.transform.position - agent2.transform.position).normalized;
        Quaternion lookRotation2 = Quaternion.LookRotation(direction2 * agent2.directionMultiplier);
        float randomOffset2 = Random.Range(-rotationRandomness, rotationRandomness);
        agent2.transform.rotation = lookRotation2 * Quaternion.Euler(0, randomOffset2, 0);
    }

    public bool IsInArena(Vector3 position)
    {
        Vector3 center = arenaCenter != null ? arenaCenter.position : Vector3.zero;
        Vector3 localPos = position - center;
        return Mathf.Abs(localPos.x) <= arenaSize.x * 0.5f && Mathf.Abs(localPos.z) <= arenaSize.y * 0.5f;
    }

    public void OnEpisodeEnd()
    {
        PositionAgents();
    }

    public void ResetArena()
    {
        PositionAgents();
    }

    #region Gizmos
    private void OnDrawGizmos()
    {
        if (!showArenaGizmos) return;

        Vector3 center = arenaCenter != null ? arenaCenter.position : Vector3.zero;
        Gizmos.color = arenaColor;
        Gizmos.DrawWireCube(center, new Vector3(arenaSize.x, 0.1f, arenaSize.y));

        Gizmos.color = spawnAreaColor;
        Vector3 safeZoneSize = new Vector3(arenaSize.x - 1f, 0.05f, arenaSize.y - 1f);
        Gizmos.DrawWireCube(center, safeZoneSize);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, 0.1f);
    }

    private void OnDrawGizmosSelected()
    {
        OnDrawGizmos();

        if (agent1 != null && agent2 != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(agent1.transform.position, minDistanceBetweenAgents * 0.5f);
            Gizmos.DrawWireSphere(agent2.transform.position, minDistanceBetweenAgents * 0.5f);
        }
    }
    #endregion
}
