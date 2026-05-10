using UnityEngine;

public class VillagerAnimationVariant : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AnimatorOverrideController overrideController;

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    public void SetAnimator(Animator targetAnimator)
    {
        animator = targetAnimator;
    }

    public void SetOverrideController(AnimatorOverrideController controller)
    {
        overrideController = controller;
    }
}
