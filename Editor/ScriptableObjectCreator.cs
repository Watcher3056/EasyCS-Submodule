#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ScriptableObjectCreator : OdinMenuEditorWindow
{
    static HashSet<Type> scriptableObjectTypes = new HashSet<Type>(AssemblyUtilities.GetTypes(AssemblyTypeFlags.CustomTypes)
        .Where(t =>
            t.IsClass &&
            typeof(ScriptableObject).IsAssignableFrom(t) &&
            !typeof(EditorWindow).IsAssignableFrom(t) &&
            !typeof(Editor).IsAssignableFrom(t)));

    [MenuItem("Assets/Create Scriptable Object", priority = -1000)]
    private static void ShowDialog()
    {
        var path = "Assets";
        var obj = Selection.activeObject;
        if (obj && AssetDatabase.Contains(obj))
        {
            path = AssetDatabase.GetAssetPath(obj);
            if (!Directory.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }
        }

        var window = CreateInstance<ScriptableObjectCreator>();
        window.ShowUtility();
        window.position = GUIHelper.GetEditorWindowRect().AlignCenter(800, 500);
        window.titleContent = new GUIContent(path);
        window.targetFolder = path.Trim('/');
    }

    private ScriptableObject previewObject;
    private string targetFolder;
    private Vector2 scroll;

    private Type SelectedType
    {
        get
        {
            var m = this.MenuTree.Selection.LastOrDefault();
            return m == null ? null : m.Value as Type;
        }
    }

    protected override OdinMenuTree BuildMenuTree()
    {
        this.MenuWidth = 270;
        this.WindowPadding = Vector4.zero;

        OdinMenuTree tree = new OdinMenuTree(false);
        tree.Config.DrawSearchToolbar = true;
        tree.DefaultMenuStyle = OdinMenuStyle.TreeViewStyle;
        tree.AddRange(scriptableObjectTypes.Where(x => !x.IsAbstract), GetMenuPathForType).AddThumbnailIcons();
        tree.SortMenuItemsByName();
        tree.Selection.SelectionConfirmed += x => this.CreateAsset();
        tree.Selection.SelectionChanged += e =>
        {
            if (this.previewObject && !AssetDatabase.Contains(this.previewObject))
            {
                DestroyImmediate(this.previewObject);
            }

            if (e != SelectionChangedType.ItemAdded)
            {
                return;
            }

            var t = this.SelectedType;
            if (t != null && !t.IsAbstract)
            {
                this.previewObject = CreateInstance(t) as ScriptableObject;
            }
        };

        return tree;
    }

    private string GetMenuPathForType(Type t)
    {
        if (t != null && scriptableObjectTypes.Contains(t))
        {
            var name = t.Name.Split('`').First().SplitPascalCase();
            return GetMenuPathForType(t.BaseType) + "/" + name;
        }

        return "";
    }

    protected override IEnumerable<object> GetTargets()
    {
        yield return this.previewObject;
    }

    protected override void DrawEditor(int index)
    {
        this.scroll = GUILayout.BeginScrollView(this.scroll);
        {
            base.DrawEditor(index);
        }
        GUILayout.EndScrollView();

        if (this.previewObject)
        {
            GUILayout.FlexibleSpace();
            SirenixEditorGUI.HorizontalLineSeparator(1);
            if (GUILayout.Button("Create Asset", GUILayoutOptions.Height(30)))
            {
                this.CreateAsset();
            }
        }
    }

    private void CreateAsset()
    {
        if (this.previewObject)
        {
            var dest = this.targetFolder + "/new " + this.MenuTree.Selection.First().Name.ToLower() + ".asset";
            dest = AssetDatabase.GenerateUniqueAssetPath(dest);
            AssetDatabase.CreateAsset(this.previewObject, dest);
            AssetDatabase.Refresh();
            Selection.activeObject = this.previewObject;
            EditorApplication.delayCall += this.Close;
        }
    }
}

#else

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class ScriptableObjectCreator : EditorWindow
{
    private static HashSet<Type> scriptableObjectTypes = new HashSet<Type>(
        AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(assembly => assembly.GetTypes())
        .Where(t =>
            t.IsClass &&
            !t.IsAbstract &&
            typeof(ScriptableObject).IsAssignableFrom(t) &&
            !typeof(EditorWindow).IsAssignableFrom(t) &&
            !typeof(Editor).IsAssignableFrom(t)));

    [MenuItem("Assets/Create Scriptable Object", priority = -1000)]
    private static void ShowDialog()
    {
        var path = "Assets";
        var obj = Selection.activeObject;
        if (obj && AssetDatabase.Contains(obj))
        {
            path = AssetDatabase.GetAssetPath(obj);
            if (!Directory.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }
        }

        var window = CreateInstance<ScriptableObjectCreator>();
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 800, 500);
        window.titleContent = new GUIContent("Create ScriptableObject");
        window.targetFolder = path.Trim('/');
        window.ShowUtility();
    }

    private ScriptableObject previewObject;
    private string targetFolder;
    private Vector2 scroll;
    private Type selectedType;
    private string searchQuery = "";
    private string assetName = "";
    private Dictionary<string, List<Type>> groupedTypes = new();
    private Dictionary<string, bool> foldouts = new();
    private Texture2D defaultScriptableIcon => EditorGUIUtility.ObjectContent(ScriptableObject.CreateInstance<ScriptableObject>(), typeof(ScriptableObject)).image as Texture2D;
    private static readonly Color SelectedColor = new Color(0.53f, 0.81f, 0.92f); // Sky blue

    private void OnEnable()
    {
        string[] keywords = searchQuery.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        groupedTypes = scriptableObjectTypes
            .Where(t => keywords.Length == 0 || keywords.All(k => t.FullName.ToLower().Contains(k)))
            .GroupBy(t => t.Namespace ?? "No Namespace")
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Name).ToList());

        foreach (var key in groupedTypes.Keys)
        {
            foldouts[key] = string.IsNullOrEmpty(searchQuery) ? false : true;
        }

        var allVisible = groupedTypes.SelectMany(g => g.Value).ToList();
        if (allVisible.Count == 1)
        {
            selectedType = allVisible[0];
            assetName = "new " + selectedType.Name.ToLower();
            if (previewObject && !AssetDatabase.Contains(previewObject))
            {
                DestroyImmediate(previewObject);
            }
            previewObject = CreateInstance(selectedType);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(270));
        GUILayout.Label("ScriptableObject Types", EditorStyles.boldLabel);

        string newSearchQuery = EditorGUILayout.TextField("Search", searchQuery);
        if (newSearchQuery != searchQuery)
        {
            searchQuery = newSearchQuery;
            OnEnable();
        }

        scroll = GUILayout.BeginScrollView(scroll);
        foreach (var group in groupedTypes)
        {
            foldouts[group.Key] = EditorGUILayout.Foldout(foldouts[group.Key], group.Key, true);
            if (foldouts[group.Key])
            {
                foreach (var type in group.Value)
                {
                    Texture icon = EditorGUIUtility.ObjectContent(null, type).image ?? defaultScriptableIcon;
                    GUIContent content = new GUIContent(" " + type.Name, icon);
                    GUIStyle style = new GUIStyle(GUI.skin.button)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        imagePosition = ImagePosition.ImageLeft,
                        fixedHeight = 20,
                        padding = new RectOffset(4, 4, 2, 2),
                    };

                    if (type == selectedType)
                    {
                        var backgroundColor = GUI.backgroundColor;
                        GUI.backgroundColor = SelectedColor;
                        GUILayout.Button(content, style);
                        GUI.backgroundColor = backgroundColor;
                    }
                    else
                    {
                        if (GUILayout.Button(content, style))
                        {
                            selectedType = type;
                            assetName = "new " + selectedType.Name.ToLower();
                            if (previewObject && !AssetDatabase.Contains(previewObject))
                            {
                                DestroyImmediate(previewObject);
                            }
                            previewObject = CreateInstance(selectedType);
                        }
                    }
                }
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        if (previewObject)
        {
            GUILayout.Label("Preview", EditorStyles.boldLabel);
            assetName = EditorGUILayout.TextField("Asset Name", assetName);

            SerializedObject serializedObject = new SerializedObject(previewObject);
            SerializedProperty property = serializedObject.GetIterator();
            property.NextVisible(true);
            while (property.NextVisible(false))
            {
                EditorGUILayout.PropertyField(property, true);
            }
            serializedObject.ApplyModifiedProperties();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create Asset", GUILayout.Height(30)))
            {
                CreateAsset();
            }
        }
        GUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void CreateAsset()
    {
        if (previewObject)
        {
            string path = Path.Combine(targetFolder, assetName + ".asset");
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(previewObject, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = previewObject;
            Close();
        }
    }

    private static Texture2D MakeColorTexture(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}

#endif