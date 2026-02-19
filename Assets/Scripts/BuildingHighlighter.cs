using System.Collections.Generic;
using UnityEngine;

public class BuildingHighlighter : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 0.85f, 0.2f);
    [Min(0f)] public float emissionIntensity = 1.5f;

    private bool isHighlighted;
    private bool cached;
    private Renderer[] cachedRenderers;
    private readonly Dictionary<Material, MaterialState> materialStates = new Dictionary<Material, MaterialState>();

    public void Configure(Color color, float intensity)
    {
        highlightColor = color;
        emissionIntensity = Mathf.Max(0f, intensity);
    }

    public void SetHighlighted(bool value)
    {
        if (value == isHighlighted)
        {
            return;
        }

        EnsureCached();
        if (value)
        {
            ApplyHighlight();
        }
        else
        {
            RestoreOriginals();
        }

        isHighlighted = value;
    }

    private void EnsureCached()
    {
        if (cached)
        {
            return;
        }

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in cachedRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            foreach (Material material in materials)
            {
                if (material == null || materialStates.ContainsKey(material))
                {
                    continue;
                }

                MaterialState state = new MaterialState
                {
                    hasBaseColor = material.HasProperty("_BaseColor"),
                    hasColor = material.HasProperty("_Color"),
                    hasEmission = material.HasProperty("_EmissionColor"),
                    emissionEnabled = material.IsKeywordEnabled("_EMISSION")
                };

                if (state.hasBaseColor)
                {
                    state.baseColor = material.GetColor("_BaseColor");
                }

                if (state.hasColor)
                {
                    state.color = material.GetColor("_Color");
                }

                if (state.hasEmission)
                {
                    state.emissionColor = material.GetColor("_EmissionColor");
                }

                materialStates.Add(material, state);
            }
        }

        cached = true;
    }

    private void ApplyHighlight()
    {
        Color emissionColor = highlightColor * emissionIntensity;
        foreach (KeyValuePair<Material, MaterialState> kvp in materialStates)
        {
            Material material = kvp.Key;
            MaterialState state = kvp.Value;

            if (state.hasBaseColor)
            {
                material.SetColor("_BaseColor", highlightColor);
            }

            if (state.hasColor)
            {
                material.SetColor("_Color", highlightColor);
            }

            if (state.hasEmission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emissionColor);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }
    }

    private void RestoreOriginals()
    {
        foreach (KeyValuePair<Material, MaterialState> kvp in materialStates)
        {
            Material material = kvp.Key;
            MaterialState state = kvp.Value;

            if (state.hasBaseColor)
            {
                material.SetColor("_BaseColor", state.baseColor);
            }

            if (state.hasColor)
            {
                material.SetColor("_Color", state.color);
            }

            if (state.hasEmission)
            {
                material.SetColor("_EmissionColor", state.emissionColor);
                if (state.emissionEnabled)
                {
                    material.EnableKeyword("_EMISSION");
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                }
            }
        }
    }

    private struct MaterialState
    {
        public bool hasBaseColor;
        public Color baseColor;
        public bool hasColor;
        public Color color;
        public bool hasEmission;
        public Color emissionColor;
        public bool emissionEnabled;
    }
}
