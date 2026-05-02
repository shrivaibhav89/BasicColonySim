using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class EnemyWaveFeedback : MonoBehaviour
{
    [Header("References")]
    public Camera targetCamera;
    public Volume volume;

    [Header("Camera Shake")]
    public float shakeDuration = 1.1f;
    public float shakeStrength = 0.35f;
    public float shakeFrequency = 22f;

    [Header("Post Process")]
    public float arrivalPulseDuration = 1.2f;
    [Range(0f, 1f)] public float pulseVignetteIntensity = 0.48f;
    [Range(0f, 1f)] public float waveVignetteIntensity = 0.22f;
    [Range(0f, 1f)] public float pulseChromaticIntensity = 0.55f;
    [Range(0f, 1f)] public float waveChromaticIntensity = 0.15f;
    public Color waveVignetteColor = new Color(0.9f, 0.05f, 0.02f, 1f);

    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private Color originalVignetteColor;
    private float originalVignetteIntensity;
    private float originalChromaticIntensity;
    private bool hasVignette;
    private bool hasChromaticAberration;
    private Coroutine postProcessRoutine;
    private Coroutine shakeRoutine;
    private Vector3 currentShakeOffset;

    void Awake()
    {
        ResolveReferences();
        CachePostProcessState();
    }

    void OnDisable()
    {
        ApplyShakeOffset(Vector3.zero);
        RestorePostProcess();
    }

    public void PlayWaveStarted()
    {
        ResolveReferences();
        CachePostProcessState();

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        if (postProcessRoutine != null)
        {
            StopCoroutine(postProcessRoutine);
        }

        shakeRoutine = StartCoroutine(ShakeCamera());
        postProcessRoutine = StartCoroutine(PlayArrivalPostProcess());
    }

    public void PlayWaveEnded()
    {
        if (postProcessRoutine != null)
        {
            StopCoroutine(postProcessRoutine);
        }

        postProcessRoutine = StartCoroutine(FadePostProcessBack());
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (volume == null)
        {
            volume = FindObjectOfType<Volume>();
        }
    }

    private void CachePostProcessState()
    {
        hasVignette = false;
        hasChromaticAberration = false;

        if (volume == null || volume.profile == null)
        {
            return;
        }

        if (!volume.profile.TryGet(out vignette))
        {
            vignette = volume.profile.Add<Vignette>(true);
        }

        if (!volume.profile.TryGet(out chromaticAberration))
        {
            chromaticAberration = volume.profile.Add<ChromaticAberration>(true);
        }

        if (vignette != null)
        {
            hasVignette = true;
            originalVignetteColor = vignette.color.value;
            originalVignetteIntensity = vignette.intensity.value;
        }

        if (chromaticAberration != null)
        {
            hasChromaticAberration = true;
            originalChromaticIntensity = chromaticAberration.intensity.value;
        }
    }

    private IEnumerator ShakeCamera()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / shakeDuration);
            float x = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f) * 2f;
            ApplyShakeOffset(new Vector3(x, y, 0f) * shakeStrength * fade);
            yield return null;
        }

        ApplyShakeOffset(Vector3.zero);
        shakeRoutine = null;
    }

    private IEnumerator PlayArrivalPostProcess()
    {
        float elapsed = 0f;
        while (elapsed < arrivalPulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / arrivalPulseDuration);
            float pulse = 1f - t;

            ApplyPostProcess(
                Mathf.Lerp(waveVignetteIntensity, pulseVignetteIntensity, pulse),
                Mathf.Lerp(waveChromaticIntensity, pulseChromaticIntensity, pulse));

            yield return null;
        }

        ApplyPostProcess(waveVignetteIntensity, waveChromaticIntensity);
        postProcessRoutine = null;
    }

    private IEnumerator FadePostProcessBack()
    {
        float startVignette = hasVignette ? vignette.intensity.value : 0f;
        float startChromatic = hasChromaticAberration ? chromaticAberration.intensity.value : 0f;
        float elapsed = 0f;
        const float fadeDuration = 0.8f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            ApplyPostProcess(
                Mathf.Lerp(startVignette, originalVignetteIntensity, t),
                Mathf.Lerp(startChromatic, originalChromaticIntensity, t));
            yield return null;
        }

        RestorePostProcess();
        postProcessRoutine = null;
    }

    private void ApplyPostProcess(float vignetteIntensity, float chromaticIntensity)
    {
        if (hasVignette)
        {
            vignette.active = true;
            vignette.color.overrideState = true;
            vignette.intensity.overrideState = true;
            vignette.color.value = waveVignetteColor;
            vignette.intensity.value = vignetteIntensity;
        }

        if (hasChromaticAberration)
        {
            chromaticAberration.active = true;
            chromaticAberration.intensity.overrideState = true;
            chromaticAberration.intensity.value = chromaticIntensity;
        }
    }

    private void RestorePostProcess()
    {
        if (hasVignette)
        {
            vignette.color.value = originalVignetteColor;
            vignette.intensity.value = originalVignetteIntensity;
        }

        if (hasChromaticAberration)
        {
            chromaticAberration.intensity.value = originalChromaticIntensity;
        }
    }

    private void ApplyShakeOffset(Vector3 offset)
    {
        if (targetCamera == null)
        {
            return;
        }

        targetCamera.transform.localPosition -= currentShakeOffset;
        currentShakeOffset = offset;
        targetCamera.transform.localPosition += currentShakeOffset;
    }
}
