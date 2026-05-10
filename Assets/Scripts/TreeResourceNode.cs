using System.Collections.Generic;
using UnityEngine;

public class TreeResourceNode : MonoBehaviour
{
    public static readonly List<TreeResourceNode> All = new List<TreeResourceNode>();

    [Header("Resource")]
    [SerializeField] private int maxDurability = 30;
    [SerializeField] private int woodPerChop = 1;
    [SerializeField] private bool destroyWhenDepleted = true;

    [Header("Feedback")]
    [SerializeField] private float wobbleStrength = 4f;
    [SerializeField] private float wobbleDuration = 0.12f;
    [SerializeField] private bool playFallAndFadeOnDepleted = true;
    [SerializeField] private float fallDuration = 0.9f;
    [SerializeField] private float fadeDelay = 0.6f;
    [SerializeField] private float fadeDuration = 1.2f;
    [SerializeField] private Vector3 fallEuler = new Vector3(0f, 0f, 85f);

    private int currentDurability;
    private Villager reservedBy;
    private float wobbleTimer;
    private Quaternion originalRotation;
    private bool isDepletionSequenceRunning;
    private readonly List<Renderer> cachedRenderers = new List<Renderer>();
    private readonly List<Color> baseColors = new List<Color>();

    public bool IsDepleted => currentDurability <= 0;
    public bool IsReserved => reservedBy != null;
    public Villager ReservedBy => reservedBy;

    void Awake()
    {
        currentDurability = Mathf.Max(1, maxDurability);
        originalRotation = transform.rotation;
        CacheRenderers();
    }

    void OnEnable()
    {
        if (!All.Contains(this))
        {
            All.Add(this);
        }
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (wobbleTimer <= 0f)
        {
            return;
        }

        wobbleTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(wobbleTimer / Mathf.Max(0.01f, wobbleDuration));
        float angle = Mathf.Sin((1f - t) * Mathf.PI * 4f) * wobbleStrength * t;
        transform.rotation = originalRotation * Quaternion.Euler(0f, angle, 0f);
        if (wobbleTimer <= 0f)
        {
            transform.rotation = originalRotation;
        }
    }

    public bool CanBeReservedBy(Villager villager)
    {
        if (villager == null || IsDepleted || !gameObject.activeInHierarchy)
        {
            return false;
        }

        return reservedBy == null || reservedBy == villager;
    }

    public bool TryReserve(Villager villager)
    {
        if (!CanBeReservedBy(villager))
        {
            return false;
        }

        reservedBy = villager;
        return true;
    }

    public void Release(Villager villager)
    {
        if (reservedBy == villager)
        {
            reservedBy = null;
        }
    }

    public bool TryChop(Villager villager, int chopAmount, out int harvestedWood)
    {
        harvestedWood = 0;
        if (villager == null || reservedBy != villager || IsDepleted || chopAmount <= 0)
        {
            return false;
        }

        int consume = Mathf.Min(chopAmount, currentDurability);
        currentDurability -= consume;
        harvestedWood = consume * Mathf.Max(1, woodPerChop);

        wobbleTimer = wobbleDuration;
        originalRotation = transform.rotation;

        if (currentDurability <= 0)
        {
            currentDurability = 0;
            HandleDepleted();
        }

        return harvestedWood > 0;
    }

    private void HandleDepleted()
    {
        reservedBy = null;

        if (playFallAndFadeOnDepleted && destroyWhenDepleted && !isDepletionSequenceRunning)
        {
            StartCoroutine(PlayFallAndFadeSequence());
            return;
        }

        if (destroyWhenDepleted)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void CacheRenderers()
    {
        cachedRenderers.Clear();
        baseColors.Clear();
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
            {
                continue;
            }

            cachedRenderers.Add(r);
            Color c = Color.white;
            if (r.material != null)
            {
                if (r.material.HasProperty("_BaseColor"))
                {
                    c = r.material.GetColor("_BaseColor");
                }
                else if (r.material.HasProperty("_Color"))
                {
                    c = r.material.color;
                }
            }
            baseColors.Add(c);
        }
    }

    private System.Collections.IEnumerator PlayFallAndFadeSequence()
    {
        isDepletionSequenceRunning = true;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(fallEuler);

        float t = 0f;
        float dur = Mathf.Max(0.05f, fallDuration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            transform.rotation = Quaternion.Slerp(startRot, endRot, eased);
            yield return null;
        }

        transform.rotation = endRot;

        if (fadeDelay > 0f)
        {
            yield return new WaitForSeconds(fadeDelay);
        }

        float fadeT = 0f;
        float fadeDur = Mathf.Max(0.05f, fadeDuration);
        while (fadeT < fadeDur)
        {
            fadeT += Time.deltaTime;
            float p = Mathf.Clamp01(fadeT / fadeDur);
            float alpha = Mathf.Lerp(1f, 0f, p);
            ApplyAlpha(alpha);
            yield return null;
        }

        ApplyAlpha(0f);
        Destroy(gameObject);
    }

    private void ApplyAlpha(float alpha)
    {
        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            Renderer r = cachedRenderers[i];
            if (r == null || r.material == null)
            {
                continue;
            }

            Material m = r.material;
            Color baseColor = i < baseColors.Count ? baseColors[i] : Color.white;
            Color c = baseColor;
            c.a = alpha;

            if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", c);
            }
            if (m.HasProperty("_Color"))
            {
                m.color = c;
            }

            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
