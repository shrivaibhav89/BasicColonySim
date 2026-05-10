using UnityEngine;

public static class ArmyThreatTracker
{
    public static Vector3 LastThreatPosition { get; private set; }
    public static float LastThreatTime { get; private set; }

    public static void ReportBuildingUnderAttack(Vector3 worldPosition)
    {
        LastThreatPosition = worldPosition;
        LastThreatTime = Time.time;
    }

    public static bool TryGetRecentThreat(float maxAgeSeconds, out Vector3 position)
    {
        position = LastThreatPosition;
        return Time.time - LastThreatTime <= Mathf.Max(0.01f, maxAgeSeconds);
    }
}
