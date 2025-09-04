using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ItemDetector : MonoBehaviour
{
    [Header("Configuración")]
    public Collider triggerCollider; 
    public LayerMask detectionLayers;
    public GameObject displayPrefab;

    private Dictionary<Transform, GameObject> activeDisplays = new Dictionary<Transform, GameObject>();
    private Dictionary<Transform, Coroutine> fadeCoroutines = new Dictionary<Transform, Coroutine>();

    private void Start()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();
    }

  private void OnTriggerEnter(Collider other)
{
    if (!IsInLayerMask(other.gameObject.layer, detectionLayers))
        return;

    Transform eyeOffsetTransform = GetEyeOffset(other.gameObject, out Vector3 offsetValue);
    if (eyeOffsetTransform != null)
    {
        if (activeDisplays.ContainsKey(eyeOffsetTransform))
        {
            if (fadeCoroutines.TryGetValue(eyeOffsetTransform, out Coroutine oldCoroutine))
                StopCoroutine(oldCoroutine);

            CanvasGroup cg = activeDisplays[eyeOffsetTransform].GetComponentInChildren<CanvasGroup>();
            fadeCoroutines[eyeOffsetTransform] = StartCoroutine(FadeCanvasGroup(cg, 1f, 0.2f));
        }
        else
        {
         
            Vector3 centerPosition = other.bounds.center;
            Vector3 finalPosition = centerPosition + offsetValue;

            GameObject instance = Instantiate(displayPrefab, finalPosition, Quaternion.identity);

            CanvasGroup cg = instance.GetComponentInChildren<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                fadeCoroutines[eyeOffsetTransform] = StartCoroutine(FadeCanvasGroup(cg, 1f, 0.2f));
            }

            activeDisplays.Add(eyeOffsetTransform, instance);
        }
    }
}

    private void OnTriggerExit(Collider other)
    {
        Transform eyeOffset = GetEyeOffset(other.gameObject, out _);
        if (eyeOffset != null && activeDisplays.ContainsKey(eyeOffset))
        {
            GameObject instance = activeDisplays[eyeOffset];
            CanvasGroup cg = instance.GetComponentInChildren<CanvasGroup>();

            if (cg != null)
            {
               
                if (fadeCoroutines.TryGetValue(eyeOffset, out Coroutine oldCoroutine))
                    StopCoroutine(oldCoroutine);

                fadeCoroutines[eyeOffset] = StartCoroutine(FadeAndDestroy(eyeOffset, cg, 0f, 0.2f));
            }
            else
            {
                Destroy(instance);
                activeDisplays.Remove(eyeOffset);
            }
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
    {
        float startAlpha = cg.alpha;
        float time = 0f;

        while (time < duration)
        {
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        cg.alpha = targetAlpha;
    }

    private IEnumerator FadeAndDestroy(Transform key, CanvasGroup cg, float targetAlpha, float duration)
    {
        yield return FadeCanvasGroup(cg, targetAlpha, duration);

        if (activeDisplays.TryGetValue(key, out GameObject obj))
        {
            Destroy(obj);
            activeDisplays.Remove(key);
            fadeCoroutines.Remove(key);
        }
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return ((mask.value & (1 << layer)) != 0);
    }

    private Transform GetEyeOffset(GameObject obj, out Vector3 offset)
    {
        offset = Vector3.zero;

        if (obj.TryGetComponent<ItemDisplay>(out var item))
        {
            offset = item.eyeOffset;
            return item.transform;
        }
        if (obj.TryGetComponent<paintingDisplay>(out var painting))
        {
            offset = painting.eyeOffset;
            return painting.transform;
        }
        if (obj.TryGetComponent<textureDisplay>(out var texture))
        {
            offset = texture.eyeOffset;
            return texture.transform;
        }

        return null;
    }

    private Bounds GetBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);

        foreach (Renderer rend in renderers)
        {
            bounds.Encapsulate(rend.bounds);
        }

        return bounds;
    }
}
