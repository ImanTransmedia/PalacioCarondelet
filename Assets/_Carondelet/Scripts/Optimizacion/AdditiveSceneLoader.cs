using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.ResourceManagement.ResourceProviders;

public class AdditiveSceneLoader : MonoBehaviour
{
    public static bool AllScenesLoaded = false;
    public bool IsDone = false;

    [Tooltip("Lista de nombres Addressables de las subescenas")]
    public List<string> subSceneKeys = new List<string>();

    public float Progress { get; private set; }

    public List<AsyncOperationHandle<SceneInstance>> loadedSceneHandles = new();
    private int totalSubScenes = 0;

    void Start()
    {
        totalSubScenes = subSceneKeys != null ? subSceneKeys.Count : 0;
        StartCoroutine(LoadSubScenesSequentially());
    }

    IEnumerator LoadSubScenesSequentially()
    {
        yield return new WaitForSeconds(1f);

        if (subSceneKeys == null || subSceneKeys.Count == 0)
        {
            Debug.Log("No hay subescenas que cargar. Continuando...");
            Progress = 1f;
            IsDone = true;
            SceneLoadingTracker.NotifyLoadingComplete();
            yield break;
        }

        for (int i = 0; i < subSceneKeys.Count; i++)
        {
            string key = subSceneKeys[i];
            Debug.Log($"Liberando memoria antes de: {key}");

            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();

            var clearCacheHandle = Addressables.ClearDependencyCacheAsync(key, false);
            yield return clearCacheHandle;
            if (!clearCacheHandle.Result)
                Debug.LogWarning($"No se limpio cache de: {key}");
            Addressables.Release(clearCacheHandle);

            Debug.Log($"Cargando subescena ({i + 1}/{totalSubScenes}): {key}");

            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(
                key, LoadSceneMode.Additive, true);

            while (!handle.IsDone)
            {
                float partial = handle.PercentComplete; // 0..1
                Progress = ((float)i + partial) / totalSubScenes;
                yield return null;
            }

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"Subescena cargada: {key}");
                loadedSceneHandles.Add(handle);
                Progress = (float)(i + 1) / totalSubScenes;
                yield return new WaitForSeconds(0.2f);
            }
            else
            {
                Debug.LogError($"Error cargando subescena: {key}");
            }
        }

        Debug.Log("Todas las subescenas estan listas.");
        IsDone = true;
        AllScenesLoaded = true;
        SceneLoadingTracker.NotifyLoadingComplete();
    }

    public void UnloadAllSubScenes()
    {
        StartCoroutine(UnloadAllScenesRoutine());
    }

    IEnumerator UnloadAllScenesRoutine()
    {
        foreach (var handle in loadedSceneHandles)
        {
            yield return Addressables.UnloadSceneAsync(handle, UnloadSceneOptions.None);
        }

        loadedSceneHandles.Clear();
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }
}
