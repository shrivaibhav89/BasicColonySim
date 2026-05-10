using System.Collections.Generic;
using UnityEngine;

public class VillagerModelRandomizer : MonoBehaviour
{
    [SerializeField] private Villager villager;
    [SerializeField] private VillagerAnimationVariant animationVariant;
    [SerializeField] private List<GameObject> modelVariants = new List<GameObject>();

    void Awake()
    {
        CacheReferences();
    }

    void OnEnable()
    {
        ChooseRandomVariant();
    }

    private void CacheReferences()
    {
        if (villager == null)
        {
            villager = GetComponent<Villager>();
        }

        if (animationVariant == null)
        {
            animationVariant = GetComponent<VillagerAnimationVariant>();
        }

        if (transform.Find("VisualVariants") is Transform variantsRoot)
        {
            if (modelVariants == null)
            {
                modelVariants = new List<GameObject>();
            }
            modelVariants.Clear();
            for (int i = 0; i < variantsRoot.childCount; i++)
            {
                GameObject child = variantsRoot.GetChild(i).gameObject;
                if (child != null)
                {
                    modelVariants.Add(child);
                }
            }
        }
    }

    public void ChooseRandomVariant()
    {
        CacheReferences();
        if (modelVariants == null || modelVariants.Count == 0)
        {
            return;
        }

        List<GameObject> validVariants = new List<GameObject>();
        for (int i = 0; i < modelVariants.Count; i++)
        {
            if (modelVariants[i] != null)
            {
                validVariants.Add(modelVariants[i]);
            }
        }

        if (validVariants.Count == 0)
        {
            return;
        }

        int selectedIndex = Random.Range(0, validVariants.Count);
        GameObject selected = validVariants[selectedIndex];
        Animator selectedAnimator = selected.GetComponentInChildren<Animator>(true);

        for (int i = 0; i < validVariants.Count; i++)
        {
            GameObject model = validVariants[i];
            bool active = model == selected;
            model.SetActive(active);
        }

        if (selectedAnimator == null)
        {
            // fallback: first model with Animator
            for (int i = 0; i < validVariants.Count; i++)
            {
                Animator candidate = validVariants[i].GetComponentInChildren<Animator>(true);
                if (candidate != null)
                {
                    selected = validVariants[i];
                    selectedAnimator = candidate;
                    for (int j = 0; j < validVariants.Count; j++)
                    {
                        validVariants[j].SetActive(validVariants[j] == selected);
                    }
                    break;
                }
            }
        }

        if (selectedAnimator == null)
        {
            return;
        }

        if (villager != null)
        {
            villager.SetAnimator(selectedAnimator);
        }

        if (animationVariant != null)
        {
            animationVariant.SetAnimator(selectedAnimator);
        }
    }
}
