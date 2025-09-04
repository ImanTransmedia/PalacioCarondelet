using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class OrganizeUnused : EditorWindow
{
    private string rootFolder = "Assets/_Carondelet";
    private string eliminarFolderName = "_ELIMINAR";

    private List<string> sceneList = new List<string>();
    private List<string> unusedModels = new List<string>();
    private List<string> unusedTextures = new List<string>();
    private List<string> unusedMaterials = new List<string>();
    private List<string> unusedAssets = new List<string>();

    private Vector2 scrollScenesPos;
    private Vector2 scrollModelsPos;
    private Vector2 scrollTexturesPos;
    private Vector2 scrollMaterialsPos;

    [MenuItem("Tools/Limpiar y Organizar/Mover Activos No Usados")]
    static void Init()
    {
        var window = GetWindow<OrganizeUnused>("Mover Activos No Usados");
        window.minSize = new Vector2(480, 600);
    }

    void OnGUI()
    {
        GUILayout.Label("Configuración", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        rootFolder = EditorGUILayout.TextField("Carpeta raíz", rootFolder);
        if (GUILayout.Button("…", GUILayout.MaxWidth(30)))
        {
            string abs = EditorUtility.OpenFolderPanel("Selecciona carpeta raíz", Application.dataPath, "");
            if (!string.IsNullOrEmpty(abs) && abs.StartsWith(Application.dataPath))
                rootFolder = "Assets" + abs.Substring(Application.dataPath.Length).Replace("\\", "/");
            else
                EditorUtility.DisplayDialog("Error", "La carpeta debe estar dentro de Assets.", "OK");
        }
        EditorGUILayout.EndHorizontal();

        eliminarFolderName = EditorGUILayout.TextField("Nombre carpeta eliminar", eliminarFolderName);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Escanear activos no usados"))
            ScanUnusedAssets();
        GUI.enabled = unusedAssets.Count > 0;
        if (GUILayout.Button("Exportar lista a TXT"))
            ExportListToTxt();
        if (GUILayout.Button("Mover a ELIMINAR"))
            MoveUnusedAssets();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUILayout.Label($"Escenas a analizar ({sceneList.Count}):", EditorStyles.boldLabel);
        scrollScenesPos = EditorGUILayout.BeginScrollView(scrollScenesPos, GUILayout.ExpandHeight(true));
        foreach (var s in sceneList)
            EditorGUILayout.LabelField(s, EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUILayout.Label($"Modelos no usados ({unusedModels.Count}):", EditorStyles.boldLabel);
        scrollModelsPos = EditorGUILayout.BeginScrollView(scrollModelsPos, GUILayout.ExpandHeight(true));
        foreach (var p in unusedModels)
            EditorGUILayout.LabelField(p, EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUILayout.Label($"Texturas no usadas ({unusedTextures.Count}):", EditorStyles.boldLabel);
        scrollTexturesPos = EditorGUILayout.BeginScrollView(scrollTexturesPos, GUILayout.ExpandHeight(true));
        foreach (var p in unusedTextures)
            EditorGUILayout.LabelField(p, EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUILayout.Label($"Materiales no usados ({unusedMaterials.Count}):", EditorStyles.boldLabel);
        scrollMaterialsPos = EditorGUILayout.BeginScrollView(scrollMaterialsPos, GUILayout.ExpandHeight(true));
        foreach (var p in unusedMaterials)
            EditorGUILayout.LabelField(p, EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private bool IsIgnored(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        return ext == ".ttf" || ext == ".otf" ||
               ext == ".fnt" || ext == ".fon" ||
               ext == ".asset" ||
               ext == ".shader" || ext == ".shadergraph" ||
               ext == ".rendertexture";
    }

    private void ScanUnusedAssets()
    {
        sceneList.Clear();
        unusedModels.Clear();
        unusedTextures.Clear();
        unusedMaterials.Clear();
        unusedAssets.Clear();

        var analyzePaths = new List<string>();
        foreach (var g in AssetDatabase.FindAssets("t:Scene", new[] { rootFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            sceneList.Add(path);
            analyzePaths.Add(path);
        }
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { rootFolder }))
            analyzePaths.Add(AssetDatabase.GUIDToAssetPath(g));

        var used = new HashSet<string>();
        foreach (var ap in analyzePaths)
        {
            foreach (var dep in AssetDatabase.GetDependencies(ap, true))
            {
                string ext = Path.GetExtension(dep).ToLower();
                if ((ext == ".fbx" || ext == ".obj") ||
                    (ext == ".png" || ext == ".jpg" || ext == ".tga" ||
                     ext == ".psd" || ext == ".tif" || ext == ".tiff" ||
                     ext == ".bmp" || ext == ".gif" || ext == ".exr" || ext == ".hdr") ||
                    ext == ".mat")
                {
                    used.Add(dep);
                }
            }
        }

        foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { rootFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (!used.Contains(path) && !IsIgnored(path))
                unusedModels.Add(path);
        }
        foreach (var g in AssetDatabase.FindAssets("t:Texture", new[] { rootFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (!used.Contains(path) && !IsIgnored(path))
                unusedTextures.Add(path);
        }
        foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { rootFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (!used.Contains(path) && !IsIgnored(path))
                unusedMaterials.Add(path);
        }

        unusedAssets.AddRange(unusedModels);
        unusedAssets.AddRange(unusedTextures);
        unusedAssets.AddRange(unusedMaterials);

        if (unusedAssets.Count == 0)
            EditorUtility.DisplayDialog("Escaneo completado", "No se encontraron activos sin usar.", "OK");
    }

    private void ExportListToTxt()
    {
        string defaultName = "lista_activos_no_usados.txt";
        string folderPath = Application.dataPath + "/" + rootFolder.Substring("Assets/".Length);
        string savePath = EditorUtility.SaveFilePanel("Guardar lista de activos", folderPath, defaultName, "txt");
        if (string.IsNullOrEmpty(savePath)) return;

        using (var w = new StreamWriter(savePath))
        {
            w.WriteLine("=== Modelos no usados ===");
            foreach (var p in unusedModels) w.WriteLine(p);
            w.WriteLine();
            w.WriteLine("=== Texturas no usadas ===");
            foreach (var p in unusedTextures) w.WriteLine(p);
            w.WriteLine();
            w.WriteLine("=== Materiales no usados ===");
            foreach (var p in unusedMaterials) w.WriteLine(p);
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Exportada", $"Lista guardada:\n{savePath}", "OK");
        EditorUtility.RevealInFinder(savePath);
    }

    private void MoveUnusedAssets()
    {
        string eliminarFolder = $"{rootFolder}/{eliminarFolderName}";
        if (!AssetDatabase.IsValidFolder(eliminarFolder))
            AssetDatabase.CreateFolder(rootFolder, eliminarFolderName);

        int moved = 0;
        foreach (var a in unusedAssets)
        {
            if (IsIgnored(a)) continue;
            string fn = Path.GetFileName(a);
            string dest = $"{eliminarFolder}/{fn}";
            string err = AssetDatabase.MoveAsset(a, dest);
            if (string.IsNullOrEmpty(err)) moved++;
            else Debug.LogWarning($"No se pudo mover '{a}' → '{dest}': {err}");
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Movimiento completado", $"Movidos {moved} activos a «{eliminarFolder}»", "OK");
        EditorUtility.RevealInFinder(eliminarFolder);

        unusedModels.Clear();
        unusedTextures.Clear();
        unusedMaterials.Clear();
        unusedAssets.Clear();
        sceneList.Clear();
    }
}