using System;
using System.Collections.Generic;
using UnityEngine;

public class TowerGarrison : MonoBehaviour
{
    private static readonly List<TowerGarrison> allTowers = new List<TowerGarrison>();
    public static IReadOnlyList<TowerGarrison> AllTowers => allTowers;

    [Header("Capacity")]
    [SerializeField] private int capacity = 4;

    [Header("Points")]
    [SerializeField] private Transform[] garrisonSlots;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private Transform hiddenAnchor;
    [SerializeField] private bool autoCollectChildSlots = true;
    [SerializeField] private float perchRadius = 0.85f;
    [SerializeField] private float perchTopOffset = 0.2f;

    [Header("Tower Combat")]
    [SerializeField] private bool useAutoTowerVolley = false;
    [SerializeField] private bool fireOnlyDuringWave = true;
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float volleyCooldown = 1.4f;
    [SerializeField] private int arrowDamage = 10;
    [SerializeField] private float arrowSpeed = 9f;
    [SerializeField] private float arrowArcHeight = 1.8f;
    [SerializeField] private float arrowScale = 0.25f;
    [SerializeField] private GameObject arrowProjectilePrefab;

    private readonly List<ArmyUnit> garrisonedUnits = new List<ArmyUnit>();
    private float nextVolleyTime;
    private readonly List<Transform> runtimeSlotCache = new List<Transform>();

    public int Capacity => Mathf.Max(1, capacity);
    public int OccupiedCount => garrisonedUnits.Count;
    public int AvailableSlots => Mathf.Max(0, Capacity - OccupiedCount);
    public bool HasFreeSlot => AvailableSlots > 0;

    public event Action<TowerGarrison> OnGarrisonChanged;

    private void OnEnable()
    {
        if (!allTowers.Contains(this))
        {
            allTowers.Add(this);
        }

        RebuildSlotCache();
    }

    private void OnDisable()
    {
        allTowers.Remove(this);
    }

    private void OnDestroy()
    {
        for (int i = garrisonedUnits.Count - 1; i >= 0; i--)
        {
            ArmyUnit unit = garrisonedUnits[i];
            if (unit != null)
            {
                unit.HandleTowerDestroyed(this);
            }
        }

        garrisonedUnits.Clear();
        allTowers.Remove(this);
    }

    private void Update()
    {
        if (!useAutoTowerVolley)
        {
            return;
        }

        if (garrisonedUnits.Count == 0)
        {
            return;
        }

        if (fireOnlyDuringWave && !EnemyWaveManager.IsWaveInProgress)
        {
            return;
        }

        if (Time.time < nextVolleyTime)
        {
            return;
        }

        FireVolley();
        nextVolleyTime = Time.time + Mathf.Max(0.1f, volleyCooldown);
    }

    public bool TryGarrison(ArmyUnit unit)
    {
        if (unit == null)
        {
            return false;
        }

        if (garrisonedUnits.Contains(unit))
        {
            return true;
        }

        if (!HasFreeSlot)
        {
            return false;
        }

        garrisonedUnits.Add(unit);
        OnGarrisonChanged?.Invoke(this);
        return true;
    }

    public void RemoveGarrison(ArmyUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        if (garrisonedUnits.Remove(unit))
        {
            OnGarrisonChanged?.Invoke(this);
        }
    }

    public Transform GetSlotTransformFor(ArmyUnit unit)
    {
        RebuildSlotCache();
        int index = Mathf.Max(0, garrisonedUnits.IndexOf(unit));
        if (runtimeSlotCache.Count > 0)
        {
            Transform slot = runtimeSlotCache[index % runtimeSlotCache.Count];
            if (slot != null)
            {
                return slot;
            }
        }

        return null;
    }

    public Vector3 GetHiddenPositionFor(ArmyUnit unit)
    {
        int index = Mathf.Max(0, garrisonedUnits.IndexOf(unit));
        Transform slot = GetSlotTransformFor(unit);
        if (slot != null)
        {
            return slot.position;
        }

        if (hiddenAnchor != null)
        {
            float ring = 0.25f;
            float angle = (index % 8) * 45f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * ring, 0f, Mathf.Sin(angle) * ring);
            return hiddenAnchor.position + offset;
        }

        return transform.position + Vector3.up;
    }

    public Vector3 GetPerchPositionFor(ArmyUnit unit)
    {
        Transform slot = GetSlotTransformFor(unit);
        if (slot != null)
        {
            return slot.position;
        }

        int index = Mathf.Max(0, garrisonedUnits.IndexOf(unit));
        Vector3 center = transform.position;
        float baseY = GetTopY() + perchTopOffset;
        float angleStep = 360f / Mathf.Max(1, Capacity);
        float angle = index * angleStep * Mathf.Deg2Rad;
        Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * perchRadius;
        return new Vector3(center.x, baseY, center.z) + radial;
    }

    public Vector3 GetSlotPositionFor(ArmyUnit unit)
    {
        return GetPerchPositionFor(unit);
    }

    public Vector3 GetUngarrisonPositionFor(ArmyUnit unit)
    {
        Vector3 slotPos = GetSlotPositionFor(unit);
        Vector3 outward = (slotPos - transform.position);
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.001f)
        {
            int index = Mathf.Max(0, garrisonedUnits.IndexOf(unit));
            float angle = (index % 8) * 45f * Mathf.Deg2Rad;
            outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
        else
        {
            outward.Normalize();
        }

        return slotPos + outward * 1.25f;
    }

    private void FireVolley()
    {
        EnemyAI[] enemies = FindObjectsOfType<EnemyAI>();
        if (enemies == null || enemies.Length == 0)
        {
            return;
        }

        int volleyCount = Mathf.Min(garrisonedUnits.Count, Capacity);
        if (volleyCount <= 0)
        {
            return;
        }

        for (int i = 0; i < volleyCount; i++)
        {
            Vector3 spawn = GetArrowSpawnForIndex(i);
            EnemyAI target = FindNearestEnemyInRange(enemies, spawn, attackRange);
            if (target == null)
            {
                continue;
            }

            FireArrowAt(target, spawn);
        }
    }

    private Vector3 GetArrowSpawnForIndex(int index)
    {
        RebuildSlotCache();
        if (runtimeSlotCache.Count > 0)
        {
            Transform slot = runtimeSlotCache[index % runtimeSlotCache.Count];
            if (slot != null)
            {
                return slot.position + Vector3.up * 0.4f;
            }
        }

        if (attackOrigin != null)
        {
            return attackOrigin.position;
        }

        return transform.position + Vector3.up * 2f;
    }

    private EnemyAI FindNearestEnemyInRange(EnemyAI[] enemies, Vector3 origin, float range)
    {
        EnemyAI nearest = null;
        float bestSqr = range * range;
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            float sqr = (enemy.transform.position - origin).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private void FireArrowAt(EnemyAI enemy, Vector3 spawnPosition)
    {
        GameObject projectile = arrowProjectilePrefab != null
            ? Instantiate(arrowProjectilePrefab, spawnPosition, Quaternion.identity)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);

        projectile.transform.localScale = Vector3.one * arrowScale;
        if (arrowProjectilePrefab == null)
        {
            Collider c = projectile.GetComponent<Collider>();
            if (c != null)
            {
                c.enabled = false;
            }
        }

        ArmySpearProjectile arrow = projectile.GetComponent<ArmySpearProjectile>();
        if (arrow == null)
        {
            arrow = projectile.AddComponent<ArmySpearProjectile>();
        }

        arrow.Initialize(enemy.transform, arrowDamage, arrowSpeed, arrowArcHeight);
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySfxAt(SoundId.ProjectileLaunch, spawnPosition);
        }
    }

    public void ConfigureCombat(GameObject arrowPrefab, float newRange, int newCapacity)
    {
        if (arrowPrefab != null)
        {
            arrowProjectilePrefab = arrowPrefab;
        }

        attackRange = Mathf.Max(1f, newRange);
        capacity = Mathf.Max(1, newCapacity);
    }

    private void RebuildSlotCache()
    {
        runtimeSlotCache.Clear();

        if (garrisonSlots != null)
        {
            for (int i = 0; i < garrisonSlots.Length; i++)
            {
                if (garrisonSlots[i] != null && !runtimeSlotCache.Contains(garrisonSlots[i]))
                {
                    runtimeSlotCache.Add(garrisonSlots[i]);
                }
            }
        }

        if (!autoCollectChildSlots || runtimeSlotCache.Count > 0)
        {
            return;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
            {
                continue;
            }

            string lower = child.name.ToLowerInvariant();
            if (lower.Contains("slot"))
            {
                runtimeSlotCache.Add(child);
            }
        }
    }

    private float GetTopY()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return transform.position.y + 2f;
        }

        float topY = float.MinValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
            {
                continue;
            }

            if (r.bounds.max.y > topY)
            {
                topY = r.bounds.max.y;
            }
        }

        return topY > float.MinValue ? topY : transform.position.y + 2f;
    }
}
