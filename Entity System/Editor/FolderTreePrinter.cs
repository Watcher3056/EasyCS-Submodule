using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class FolderTreePrinter
{
    [MenuItem("Assets/EasyCS/Print Folder Tree to Console")]
    private static void PrintFolderTree()
    {
        string selectedPath = GetSelectedFolderPath();
        if (string.IsNullOrEmpty(selectedPath))
        {
            Debug.LogWarning("[EasyCS] Please select a folder in the Project window.");
            return;
        }

        string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), selectedPath);
        StringBuilder builder = new StringBuilder();

        builder.AppendLine($"📂 Folder Tree: {selectedPath}\n");
        AppendTreeRecursive(builder, absolutePath, 0);

        Debug.Log(builder.ToString());
    }

    private static void AppendTreeRecursive(StringBuilder builder, string path, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 2);
        string folderName = Path.GetFileName(path);
        builder.AppendLine($"{indent}- {folderName}/");

        foreach (var file in Directory.GetFiles(path))
        {
            string fileName = Path.GetFileName(file);
            if (!fileName.StartsWith(".")) // skip hidden/system files
                builder.AppendLine($"{indent}  • {fileName}");
        }

        foreach (var dir in Directory.GetDirectories(path))
        {
            AppendTreeRecursive(builder, dir, indentLevel + 1);
        }
    }

    private static string GetSelectedFolderPath()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return AssetDatabase.IsValidFolder(path) ? path : null;
    }

    [MenuItem("Assets/EasyCS/Print Folder Tree to Console", true)]
    private static bool ValidatePrintFolderTree()
    {
        return AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
    }
}
