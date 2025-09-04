using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class OrganizeScene : EditorWindow
{
    private string rootFolder = "Assets/_Carondelet";

    [MenuItem("Tools/Limpiar y Organizar/Organizar 3D-Prefabs en Carpetas")]
    static void ShowWindow()
    {
        GetWindow<OrganizeScene>("Organizar Activos").minSize = new Vector2(450, 120);
    }

    void OnGUI()
    {
        GUILayout.Label("Carpeta raíz de destino", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        rootFolder = EditorGUILayout.TextField(rootFolder);
        if (GUILayout.Button("…", GUILayout.MaxWidth(30)))
        {
            string abs = EditorUtility.OpenFolderPanel("Selecciona carpeta raíz", Application.dataPath, "");
            if (!string.IsNullOrEmpty(abs) && abs.StartsWith(Application.dataPath))
                rootFolder = "Assets" + abs.Substring(Application.dataPath.Length).Replace("\\", "/");
            else
                EditorUtility.DisplayDialog("Error", "La carpeta debe estar dentro de Assets.", "OK");
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);
        if (GUILayout.Button("Ejecutar organización"))
            Organize();
    }

    void Organize()
    {
        if (!AssetDatabase.IsValidFolder(rootFolder))
        {
            Debug.LogError($"La carpeta '{rootFolder}' no existe.");
            return;
        }

        // Escenas cargadas (principal + subescenas)
        var loadedScenes = new List<Scene>();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            if (s.isLoaded && !string.IsNullOrEmpty(s.path))
                loadedScenes.Add(s);
        }
        if (loadedScenes.Count == 0)
        {
            EditorUtility.DisplayDialog("Sin escenas", "No hay escenas cargadas en el Editor.", "OK");
            return;
        }
        string parentScene = Path.GetFileNameWithoutExtension(loadedScenes[0].path);
        var loadedScenePaths = new HashSet<string>(loadedScenes.Select(sc => sc.path));

        var assetGuidToOtherScenes = BuildAssetToOtherScenesMap(loadedScenePaths);

        var meshMap = new Dictionary<string, HashSet<string>>();   
        var prefabMap = new Dictionary<string, HashSet<string>>(); 
        foreach (var scene in loadedScenes)
            foreach (var root in scene.GetRootGameObjects())
                CollectGO(root, meshMap, prefabMap);

        // Conjunto de materiales usados en las escenas cargadas
        var allSceneMaterialPaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var set in meshMap.Values) foreach (var m in set) allSceneMaterialPaths.Add(m);
        foreach (var set in prefabMap.Values) foreach (var m in set) allSceneMaterialPaths.Add(m);

        var texGuidToMatGuids = BuildTextureToMaterialsMap(allSceneMaterialPaths);

        // Bases
        string base3D = $"{rootFolder}/_3D";
        string basePref = $"{rootFolder}/_Prefabs";
        EnsureFolderRecursive(base3D);
        EnsureFolderRecursive(basePref);

    
        string commonBase3D = $"{base3D}/COMMON";
        string commonObjs3D = $"{commonBase3D}/Objetos";
        string commonMats3D = $"{commonBase3D}/Materiales";
        string commonTexs3D = $"{commonBase3D}/Texturas";
        EnsureFolderRecursive(commonBase3D);
        EnsureFolderRecursive(commonObjs3D);
        EnsureFolderRecursive(commonMats3D);
        EnsureFolderRecursive(commonTexs3D);

        string scene3D = $"{base3D}/{parentScene}";
        string scenePref = $"{basePref}/{parentScene}";
        string commonPref = $"{basePref}/COMMON";
        EnsureFolderRecursive(scene3D);
        EnsureFolderRecursive(scenePref);
        EnsureFolderRecursive(commonPref);

        var touchedMaterialGuids = new HashSet<string>();

        foreach (var kv in meshMap)
        {
            string meshPath = kv.Key;
            if (IsFont(meshPath)) continue;

            string meshGuid = AssetDatabase.AssetPathToGUID(meshPath);
            bool isCommonObj = IsCommonByOtherScenes(meshGuid, assetGuidToOtherScenes);

            if (isCommonObj)
            {
                MoveAssetSmart(meshPath, commonObjs3D);

                foreach (var matPath in kv.Value)
                    ProcessMaterialSmart(
                        matPath,
                        objectFolderForLocal: scene3D,                   
                        isCommon: IsCommonByOtherScenes(AssetDatabase.AssetPathToGUID(matPath), assetGuidToOtherScenes),
                        commonMaterialsFolder: commonMats3D,
                        commonTexturesFolder: commonTexs3D,
                        texGuidToMatGuids: texGuidToMatGuids,
                        touchedMaterialGuids: touchedMaterialGuids
                    );
            }
            else
            {
                string name = Path.GetFileNameWithoutExtension(meshPath);
                string objectFolder = $"{scene3D}/{MakeSafe(name)}";
                EnsureFolderRecursive(objectFolder);
                MoveAssetSmart(meshPath, objectFolder);

                foreach (var matPath in kv.Value)
                    ProcessMaterialSmart(
                        matPath,
                        objectFolderForLocal: objectFolder,
                        isCommon: IsCommonByOtherScenes(AssetDatabase.AssetPathToGUID(matPath), assetGuidToOtherScenes),
                        commonMaterialsFolder: commonMats3D,
                        commonTexturesFolder: commonTexs3D,
                        texGuidToMatGuids: texGuidToMatGuids,
                        touchedMaterialGuids: touchedMaterialGuids
                    );
            }
        }

        foreach (var kv in prefabMap)
        {
            string prefabPath = kv.Key;
            if (IsFont(prefabPath)) continue;

            string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            bool isCommonPrefab = IsCommonByOtherScenes(prefabGuid, assetGuidToOtherScenes);

            string targetFolder = isCommonPrefab ? commonPref : scenePref;
            EnsureFolderRecursive(targetFolder);
            MoveAssetSmart(prefabPath, targetFolder);

            foreach (var matPath in kv.Value)
                ProcessMaterialSmart(
                    matPath,
                    objectFolderForLocal: scene3D, 
                    isCommon: IsCommonByOtherScenes(AssetDatabase.AssetPathToGUID(matPath), assetGuidToOtherScenes),
                    commonMaterialsFolder: commonMats3D,
                    commonTexturesFolder: commonTexs3D,
                    texGuidToMatGuids: texGuidToMatGuids,
                    touchedMaterialGuids: touchedMaterialGuids
                );
        }

        VerifyTexturesPlacement(
            materialGuids: touchedMaterialGuids,
            commonMaterialsFolder: commonMats3D,
            commonTexturesFolder: commonTexs3D,
            texGuidToMatGuids: texGuidToMatGuids
        );

        CleanupEmptyFolders(rootFolder, new[]
        {
            rootFolder, base3D, basePref,
            commonBase3D, commonObjs3D, commonMats3D, commonTexs3D,
            scene3D, scenePref, commonPref
        });

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("¡Listo!",
            "Operacion Terminada✓",
            "OK");
    }
    static void CollectGO(GameObject go,
        Dictionary<string, HashSet<string>> meshMap,
        Dictionary<string, HashSet<string>> prefabMap)
    {
        var mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            RegisterAsset(AssetDatabase.GetAssetPath(mf.sharedMesh), go, meshMap, null);

        var smr = go.GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
            RegisterAsset(AssetDatabase.GetAssetPath(smr.sharedMesh), go, meshMap, smr.sharedMaterials);

        string pPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        if (!string.IsNullOrEmpty(pPath))
            RegisterAsset(pPath, go, prefabMap, null);

        foreach (Transform c in go.transform)
            CollectGO(c.gameObject, meshMap, prefabMap);
    }

    static void RegisterAsset(string assetPath, GameObject go,
        Dictionary<string, HashSet<string>> map,
        Material[] overrideMats)
    {
        if (string.IsNullOrEmpty(assetPath) || IsFont(assetPath)) return;
        if (!map.ContainsKey(assetPath))
            map[assetPath] = new HashSet<string>();

        Material[] mats;
        if (overrideMats != null)
        {
            mats = overrideMats;
        }
        else
        {
            var rend = go.GetComponent<Renderer>();
            mats = rend != null ? rend.sharedMaterials : new Material[0];
        }

        foreach (var m in mats)
        {
            if (m == null) continue;
            string mp = AssetDatabase.GetAssetPath(m);
            if (!string.IsNullOrEmpty(mp) && !IsFont(mp))
                map[assetPath].Add(mp);
        }
    }

    static Dictionary<string, HashSet<string>> BuildAssetToOtherScenesMap(HashSet<string> loadedScenePaths)
    {
        var dict = new Dictionary<string, HashSet<string>>();
        foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            if (loadedScenePaths.Contains(scenePath)) continue; 

            var deps = AssetDatabase.GetDependencies(scenePath, true);
            foreach (var dep in deps)
            {
                string depGuid = AssetDatabase.AssetPathToGUID(dep);
                if (string.IsNullOrEmpty(depGuid)) continue;

                if (!dict.TryGetValue(depGuid, out var set))
                {
                    set = new HashSet<string>();
                    dict[depGuid] = set;
                }
                set.Add(scenePath);
            }
        }
        return dict;
    }

    static bool IsCommonByOtherScenes(string assetGuid, Dictionary<string, HashSet<string>> map)
    {
        return !string.IsNullOrEmpty(assetGuid)
            && map.TryGetValue(assetGuid, out var scenes)
            && scenes != null
            && scenes.Count >= 2; 
    }

    static readonly HashSet<string> textureExts = new HashSet<string>(new[]
    {
        ".png",".jpg",".jpeg",".tga",".psd",".tif",".tiff",".bmp",".exr",".hdr",".dds",".ktx",".ktx2"
    });

    static Dictionary<string, HashSet<string>> BuildTextureToMaterialsMap(HashSet<string> materialPaths)
    {
        var map = new Dictionary<string, HashSet<string>>();
        foreach (var matPath in materialPaths)
        {
            if (string.IsNullOrEmpty(matPath)) continue;
            string matGuid = AssetDatabase.AssetPathToGUID(matPath);
            if (string.IsNullOrEmpty(matGuid)) continue;

            foreach (var texGuid in GetMaterialTextureGuids(matPath))
            {
                if (!map.TryGetValue(texGuid, out var set))
                {
                    set = new HashSet<string>();
                    map[texGuid] = set;
                }
                set.Add(matGuid);
            }
        }
        return map;
    }

    static IEnumerable<string> GetMaterialTextureGuids(string matPath)
    {
        foreach (var dep in AssetDatabase.GetDependencies(matPath, true))
        {
            string ext = Path.GetExtension(dep).ToLowerInvariant();
            if (textureExts.Contains(ext))
            {
                string g = AssetDatabase.AssetPathToGUID(dep);
                if (!string.IsNullOrEmpty(g))
                    yield return g;
            }
        }
    }
    static void ProcessMaterialSmart(
        string matPath,
        string objectFolderForLocal,
        bool isCommon,
        string commonMaterialsFolder,
        string commonTexturesFolder,
        Dictionary<string, HashSet<string>> texGuidToMatGuids,
        HashSet<string> touchedMaterialGuids)
    {
        if (IsFont(matPath)) return;

        string matGuid = AssetDatabase.AssetPathToGUID(matPath);
        if (string.IsNullOrEmpty(matGuid)) return;

        var texGuids = GetMaterialTextureGuids(matPath).ToList();

        if (isCommon)
        {
            var sharedTexGuids = texGuids.Where(g => texGuidToMatGuids.TryGetValue(g, out var set) && set.Count >= 2).ToList();
            var uniqueTexGuids = texGuids.Except(sharedTexGuids).ToList();

            if (uniqueTexGuids.Count > 0)
            {
                string matName = Path.GetFileNameWithoutExtension(matPath);
                string matFolder = $"{commonMaterialsFolder}/{MakeSafe(matName)}";
                EnsureFolderRecursive(matFolder);

                string finalMatPath = MoveAssetSmart(matPath, matFolder);

                foreach (var texGuid in uniqueTexGuids)
                {
                    string texPath = AssetDatabase.GUIDToAssetPath(texGuid);
                    if (!string.IsNullOrEmpty(texPath))
                        MoveAssetSmart(texPath, matFolder);
                }
                foreach (var texGuid in sharedTexGuids)
                {
                    string texPath = AssetDatabase.GUIDToAssetPath(texGuid);
                    if (!string.IsNullOrEmpty(texPath))
                        MoveAssetSmart(texPath, commonTexturesFolder);
                }

                if (!string.IsNullOrEmpty(finalMatPath))
                    touchedMaterialGuids.Add(matGuid);
            }
            else
            {
                string finalMatPath = MoveAssetSmart(matPath, commonMaterialsFolder);

                foreach (var texGuid in texGuids)
                {
                    string texPath = AssetDatabase.GUIDToAssetPath(texGuid);
                    if (!string.IsNullOrEmpty(texPath))
                        MoveAssetSmart(texPath, commonTexturesFolder);
                }

                if (!string.IsNullOrEmpty(finalMatPath))
                    touchedMaterialGuids.Add(matGuid);
            }
        }
        else
        {
            string matName = Path.GetFileNameWithoutExtension(matPath);
            string matFolder = $"{objectFolderForLocal}/{MakeSafe(matName)}";
            EnsureFolderRecursive(matFolder);

            string finalMatPath = MoveAssetSmart(matPath, matFolder);

            foreach (var texGuid in texGuids)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(texGuid);
                if (!string.IsNullOrEmpty(texPath))
                    MoveAssetSmart(texPath, matFolder);
            }

            if (!string.IsNullOrEmpty(finalMatPath))
                touchedMaterialGuids.Add(matGuid);
        }
    }
    static void VerifyTexturesPlacement(
        IEnumerable<string> materialGuids,
        string commonMaterialsFolder,
        string commonTexturesFolder,
        Dictionary<string, HashSet<string>> texGuidToMatGuids)
    {
        if (materialGuids == null) return;

        foreach (var matGuid in materialGuids)
        {
            if (string.IsNullOrEmpty(matGuid)) continue;
            string matPath = AssetDatabase.GUIDToAssetPath(matGuid);
            if (string.IsNullOrEmpty(matPath)) continue;

            string matDir = Path.GetDirectoryName(matPath).Replace("\\", "/");
            if (string.IsNullOrEmpty(matDir)) continue;

            bool isCommonRoot = matDir.Equals(commonMaterialsFolder, System.StringComparison.OrdinalIgnoreCase);
            bool isCommonSub = matDir.StartsWith(commonMaterialsFolder + "/", System.StringComparison.OrdinalIgnoreCase);

            foreach (var texGuid in GetMaterialTextureGuids(matPath))
            {
                string texPath = AssetDatabase.GUIDToAssetPath(texGuid);
                if (string.IsNullOrEmpty(texPath)) continue;

                bool shared = texGuidToMatGuids.TryGetValue(texGuid, out var mats) && mats.Count >= 2;

                string targetDir;
                if (isCommonRoot)
                {
                    targetDir = commonTexturesFolder;
                }
                else if (isCommonSub)
                {
                    targetDir = shared ? commonTexturesFolder : matDir;
                }
                else
                {
                    targetDir = matDir;
                }

                string texDir = Path.GetDirectoryName(texPath).Replace("\\", "/");
                if (!texDir.Equals(targetDir, System.StringComparison.OrdinalIgnoreCase))
                {
                    MoveAssetSmart(texPath, targetDir);
                }
            }
        }
    }
    static string MoveAssetSmart(string assetPath, string folder)
    {
        string fn = Path.GetFileName(assetPath);
        string destCandidate = $"{folder}/{fn}";
        if (assetPath.Equals(destCandidate, System.StringComparison.OrdinalIgnoreCase)) return assetPath;

        EnsureFolderRecursive(folder);
        string dest = AssetDatabase.GenerateUniqueAssetPath(destCandidate);
        string err = AssetDatabase.MoveAsset(assetPath, dest);
        if (!string.IsNullOrEmpty(err))
        {
            Debug.LogWarning($"No pude mover '{assetPath}' → '{dest}': {err}");
            return assetPath;
        }
        return dest;
    }

    static void EnsureFolderRecursive(string fullPath)
    {
        if (AssetDatabase.IsValidFolder(fullPath)) return;
        string parent = Path.GetDirectoryName(fullPath).Replace("\\", "/");
        if (!string.IsNullOrEmpty(parent))
            EnsureFolderRecursive(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(fullPath));
    }

    static void CleanupEmptyFolders(string root, IEnumerable<string> protectedFolders)
    {
        var protect = new HashSet<string>(protectedFolders ?? Enumerable.Empty<string>()) { root };
        var all = GetAllFolders(root).OrderByDescending(p => p.Count(c => c == '/')).ToList();

        bool deleted;
        int guard = 0;
        do
        {
            deleted = false;
            foreach (var folder in all)
            {
                if (protect.Contains(folder)) continue;

                var assets = AssetDatabase.FindAssets("", new[] { folder });
                if (assets.Length == 0)
                {
                    var subs = AssetDatabase.GetSubFolders(folder);
                    if (subs == null || subs.Length == 0)
                    {
                        if (AssetDatabase.DeleteAsset(folder))
                            deleted = true;
                    }
                }
            }
            guard++;
        } while (deleted && guard < 50);
    }

    static IEnumerable<string> GetAllFolders(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var f = stack.Pop();
            yield return f;
            var subs = AssetDatabase.GetSubFolders(f);
            if (subs != null)
                foreach (var s in subs) stack.Push(s);
        }
    }

    static bool IsFont(string p)
    {
        var e = Path.GetExtension(p).ToLowerInvariant();
        return e == ".ttf" || e == ".otf" || e == ".fnt" || e == ".fon";
    }

    static string MakeSafe(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}
