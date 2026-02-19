using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class JobPriorityUI : MonoBehaviour
{
    [Header("References")]
    public PopulationManager populationManager;
    public Transform listRoot;
    public JobPriorityRowUI rowPrefab;

    [Header("Settings")]
    [Tooltip("Higher value = higher priority. Top row gets this value.")]
    public int topPriorityValue = 3;

    private readonly List<JobPriorityRowUI> rows = new List<JobPriorityRowUI>();

    void Awake()
    {
        if (populationManager == null)
        {
            populationManager = PopulationManager.Instance;
        }
    }

    void OnEnable()
    {
        BuildRows();
        if (populationManager != null)
        {
            populationManager.OnJobPrioritiesChanged += RefreshRows;
        }
    }

    void OnDisable()
    {
        if (populationManager != null)
        {
            populationManager.OnJobPrioritiesChanged -= RefreshRows;
        }
    }

    private void BuildRows()
    {
        if (listRoot == null || rowPrefab == null)
        {
            return;
        }

        ClearRows();

        Array values = Enum.GetValues(typeof(JobType));
        foreach (JobType jobType in values)
        {
            if (jobType == JobType.None)
            {
                continue;
            }

            JobPriorityRowUI row = Instantiate(rowPrefab, listRoot);
            row.Initialize(jobType, populationManager, this);
            rows.Add(row);
        }

        ApplyOrderToPriorities();
    }

    private void ClearRows()
    {
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i] != null)
            {
                Destroy(rows[i].gameObject);
            }
        }
        rows.Clear();
    }

    private void RefreshRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null)
            {
                rows[i].Refresh();
            }
        }
    }

    public void HandleRowDrag(JobPriorityRowUI row)
    {
        if (row == null || listRoot == null)
        {
            return;
        }

        int newIndex = GetIndexFromPosition(row.transform as RectTransform);
        int currentIndex = row.transform.GetSiblingIndex();
        if (newIndex != currentIndex)
        {
            row.transform.SetSiblingIndex(newIndex);
        }
    }

    public void HandleRowDrop(JobPriorityRowUI row)
    {
        ApplyOrderToPriorities();
        RebuildLayout();
    }

    private int GetIndexFromPosition(RectTransform dragged)
    {
        if (dragged == null)
        {
            return 0;
        }

        float draggedY = dragged.position.y;
        int childCount = listRoot.childCount;
        int targetIndex = childCount - 1;
        for (int i = 0; i < childCount; i++)
        {
            RectTransform child = listRoot.GetChild(i) as RectTransform;
            if (child == null || child == dragged)
            {
                continue;
            }

            if (draggedY > child.position.y)
            {
                targetIndex = i;
                break;
            }
        }

        return Mathf.Clamp(targetIndex, 0, childCount - 1);
    }

    private void ApplyOrderToPriorities()
    {
        if (populationManager == null || listRoot == null)
        {
            return;
        }

        int childCount = listRoot.childCount;
        int topValue = Mathf.Max(0, topPriorityValue);
        for (int i = 0; i < childCount; i++)
        {
            JobPriorityRowUI row = listRoot.GetChild(i).GetComponent<JobPriorityRowUI>();
            if (row == null)
            {
                continue;
            }

            int value = Mathf.Max(0, topValue - i);
            populationManager.SetJobPriority(row.JobType, value);
        }

        RefreshRows();
    }

    private void RebuildLayout()
    {
        RectTransform root = listRoot as RectTransform;
        if (root == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }
}
