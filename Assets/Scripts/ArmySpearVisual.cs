using UnityEngine;

public class ArmySpearVisual : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject spearPrefab;
    [SerializeField] private Vector3 localPosition = new Vector3(0.02f, 0.02f, 0.12f);
    [SerializeField] private Vector3 localEulerAngles = new Vector3(10f, 90f, 0f);
    [SerializeField] private Vector3 localScale = new Vector3(0.35f, 0.35f, 0.35f);

    private Transform handBone;
    private GameObject spawnedSpear;

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    void OnEnable()
    {
        EnsureAttached();
    }

    public void EnsureAttached()
    {
        if (animator == null || spearPrefab == null)
        {
            return;
        }

        handBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (handBone == null)
        {
            return;
        }

        if (spawnedSpear != null)
        {
            spawnedSpear.transform.SetParent(handBone, false);
            ApplyLocalPose();
            return;
        }

        spawnedSpear = Instantiate(spearPrefab, handBone);
        spawnedSpear.name = "EquippedSpear";
        ApplyLocalPose();
        DisablePhysicsBits(spawnedSpear);
    }

    private void ApplyLocalPose()
    {
        if (spawnedSpear == null)
        {
            return;
        }

        Transform t = spawnedSpear.transform;
        t.localPosition = localPosition;
        t.localRotation = Quaternion.Euler(localEulerAngles);
        t.localScale = localScale;
    }

    private static void DisablePhysicsBits(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }
    }
}
