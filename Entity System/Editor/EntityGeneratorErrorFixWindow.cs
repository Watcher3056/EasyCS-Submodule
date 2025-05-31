using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;

namespace EasyCS.Editor
{
    public class EntityErrorFixAndCleanWindow : EditorWindow
    {
        private string errorText = "";
        private Vector2 scroll;

        // Enum to manage the state of the window
        private enum WindowState
        {
            Initial, // Ready to paste errors, Dry Run/Execute available
            DryRunCompleted, // Dry Run results shown, Execute enabled if actions found
            Executed // Actions performed (commented/generated), Apply/Undo available
        }

        private WindowState currentState = WindowState.Initial;

        private List<string> lastCommentedOutFiles = new();

        private List<string> dryRunToDelete = new();

        private List<string> executedCommentedOutNames = new();

        private const string GeneratedRootFolder = "Assets/EasyCS Generated";

        private const string CommentPrefix = "//"; // Prefix used for commenting out lines

        [MenuItem("EasyCS/Fix & Clean From Compile Errors")]
        public static void ShowWindow()
        {
            var window = GetWindow<EntityErrorFixAndCleanWindow>("Fix & Clean Entity Scripts");
            window.ResetState(); // Ensure initial state on window open
        }

        private void OnGUI()
        {
            GUILayout.Label("Paste Compile Errors from Unity Console", EditorStyles.boldLabel);

            GUI.enabled = currentState != WindowState.Executed;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            errorText = EditorGUILayout.TextArea(errorText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            GUI.enabled = true;

            GUILayout.Space(5);

            if (currentState == WindowState.Initial || currentState == WindowState.DryRunCompleted)
            {
                if (GUILayout.Button("Dry Run (Preview Actions)"))
                {
                    RunDryRun();
                }

                // Only enable execute if there are actions to perform in the dry run
                GUI.enabled = dryRunToDelete.Count > 0; // Removed dryRunToGenerate check
                if (GUILayout.Button("Execute Fix & Clean"))
                {
                    ExecuteFixAndClean();
                }
                GUI.enabled = true;
            }
            else if (currentState == WindowState.Executed)
            {
                if (GUILayout.Button("Apply Actions (Finalize Deletions)"))
                {
                    ApplyActions();
                }

                if (GUILayout.Button("Undo Actions"))
                {
                    UndoLastAction();
                }
            }

            GUILayout.Space(10);

            if (currentState == WindowState.DryRunCompleted)
            {
                DisplayDryRunResults();
            }
            else if (currentState == WindowState.Executed)
            {
                DisplayExecutedActions();
            }
        }

        private void ResetState()
        {
            currentState = WindowState.Initial;
            errorText = "";
            lastCommentedOutFiles.Clear();
            dryRunToDelete.Clear();
            executedCommentedOutNames.Clear();
            Debug.Log("[EasyCS] Editor Window State Reset to Initial.");
        }

        private void RunDryRun()
        {
            // Removed generation logic from here as well
            dryRunToDelete = ParseObsoleteTypesToDelete(errorText);
            currentState = WindowState.DryRunCompleted;
            Debug.Log($"Dry Run Complete. Will Comment Out: {dryRunToDelete.Count}"); // Updated debug message
        }

        private void ExecuteFixAndClean()
        {
            lastCommentedOutFiles.Clear();
            executedCommentedOutNames.Clear();

            var filesToCommentOut = ParseObsoleteTypesToDelete(errorText);

            // COMMENT OUT Obsolete Files
            EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", "Commenting out obsolete files...", 0);
            int commentCount = 0;
            foreach (var path in filesToCommentOut)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        CommentOutFile(path);
                        lastCommentedOutFiles.Add(path);
                        executedCommentedOutNames.Add(Path.GetFileNameWithoutExtension(path));
                        Debug.Log($"[EasyCS] Commented out: {Path.GetFileName(path)}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[EasyCS] Error commenting out {path}: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[EasyCS] Attempted to comment out {Path.GetFileName(path)} but file not found at {path}.");
                }
                EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", $"Commenting out obsolete files: {Path.GetFileName(path)}", (float)++commentCount / filesToCommentOut.Count);
            }
            EditorUtility.ClearProgressBar();

            currentState = WindowState.Executed;

            Debug.Log($"[EasyCS] Fix & Clean Execute Complete. Commented Out: {lastCommentedOutFiles.Count}"); // Updated debug message

            AssetDatabase.Refresh();
        }

        private void ApplyActions()
        {
            Debug.Log("--- Applying Event System Fix & Clean Actions (Finalizing Deletions) ---");

            EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", "Applying actions (deleting commented files)...", 0);
            int deleteCount = 0;
            foreach (var path in lastCommentedOutFiles)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        if (AssetDatabase.DeleteAsset(path))
                        {
                            Debug.Log($"[EasyCS] Deleted commented file: {Path.GetFileName(path)}");
                        }
                        else
                        {
                            Debug.LogError($"[EasyCS] Failed to delete commented asset: {path}. It might be locked or in use.");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[EasyCS] Error deleting commented file {path}: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[EasyCS] Attempted to delete commented file {Path.GetFileName(path)} but file not found at {path}.");
                }
                EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", $"Applying actions (deleting {Path.GetFileName(path)})...", (float)++deleteCount / lastCommentedOutFiles.Count);
            }
            EditorUtility.ClearProgressBar();

            ResetState();

            Debug.Log("--- Apply Actions Complete ---");

            AssetDatabase.Refresh();
        }


        private void UndoLastAction()
        {
            Debug.Log("--- Undoing Last Event System Fix & Clean Action ---");

            // Undo Commenting Out (Uncomment files)
            EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", "Undoing commented out files...", 0);
            int uncommentCount = 0;
            foreach (var path in lastCommentedOutFiles)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        UncommentFile(path);
                        Debug.Log($"[EasyCS] Uncommented: {Path.GetFileName(path)}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[EasyCS] Error uncommenting {path}: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[EasyCS] Attempted to uncomment {Path.GetFileName(path)} but file not found at {path}.");
                }
                EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", $"Undoing commented out files: {Path.GetFileName(path)}", (float)++uncommentCount / lastCommentedOutFiles.Count);
            }
            EditorUtility.ClearProgressBar();

            ResetState();

            Debug.Log("--- Undo Complete ---");

            AssetDatabase.Refresh();
        }


        private void CommentOutFile(string path)
        {
            var lines = File.ReadAllLines(path).ToList();
            var commentedLines = lines.Select(line => CommentPrefix + line).ToList();
            File.WriteAllLines(path, commentedLines);
        }

        private void UncommentFile(string path)
        {
            var lines = File.ReadAllLines(path).ToList();
            var uncommentedLines = lines.Select(line =>
            {
                if (line.StartsWith(CommentPrefix))
                {
                    return line.Substring(CommentPrefix.Length);
                }
                return line;
            }).ToList();
            File.WriteAllLines(path, uncommentedLines);
        }


        private void DisplayDryRunResults()
        {
            if (dryRunToDelete.Count > 0) // Removed dryRunToGenerate check
            {
                EditorGUILayout.LabelField("Dry Run Summary", EditorStyles.boldLabel);

                if (dryRunToDelete.Count > 0)
                {
                    EditorGUILayout.LabelField("Will Comment Out (Full Paths):", EditorStyles.miniLabel);
                    foreach (var path in dryRunToDelete)
                        EditorGUILayout.HelpBox(Path.GetFileName(path), MessageType.Warning);
                }
            }
        }

        private void DisplayExecutedActions()
        {
            if (executedCommentedOutNames.Count > 0) // Removed executedGeneratedNames check
            {
                EditorGUILayout.LabelField("Last Executed Actions", EditorStyles.boldLabel);

                if (executedCommentedOutNames.Count > 0)
                {
                    EditorGUILayout.LabelField("Commented Out:", EditorStyles.miniLabel);
                    foreach (var name in executedCommentedOutNames)
                        EditorGUILayout.HelpBox(name + ".cs", MessageType.Warning);
                }
            }
        }


        /// <summary>
        /// Parses compile errors to find names of user-defined types that are reported as missing.
        /// This method is now only used for informational purposes in the UI, as generation is removed.
        /// </summary>
        private List<string> ParseMissingTypesToGenerate(string raw)
        {
            var results = new HashSet<string>();
            // Regex to find "The type or namespace name 'TypeName' could not be found"
            // This 'TypeName' refers to the *user-defined* type that is missing.
            var matches = Regex.Matches(raw, @"The type or namespace name '(\w+)' could not be found");

            foreach (Match match in matches)
            {
                string typeName = match.Groups[1].Value;
                // Filter for names matching our user-defined base types' conventions
                // (e.g., EntityDataSomething, EntityBehaviorSomething, ActorDataSharedSomething)
                if (typeName.StartsWith(EasyCSCodeGeneratorReflection.EntityDataPrefix) ||
                    typeName.StartsWith(EasyCSCodeGeneratorReflection.EntityBehaviorPrefix) ||
                    typeName.StartsWith(EasyCSCodeGeneratorReflection.ActorDataSharedPrefix))
                {
                    // Add the user-defined type name itself, as this is what we'll use to infer factory/provider names
                    results.Add(typeName);
                }
            }

            return new List<string>(results);
        }

        /// <summary>
        /// Parses compile errors to find paths of generated files that have errors.
        /// These generated files are likely obsolete and should be commented out/deleted.
        /// Normalizes path separators in the input string before applying the regex.
        /// </summary>
        private List<string> ParseObsoleteTypesToDelete(string raw)
        {
            string normalizedRaw = raw.Replace('\\', '/');
            var results = new HashSet<string>();

            string pathPattern = @"^(?<filepath>.+?\.cs)\(\d+,\d+\):\s*error CS\d+:";
            var pathMatches = Regex.Matches(normalizedRaw, pathPattern, RegexOptions.Multiline);

            foreach (Match pathMatch in pathMatches)
            {
                if (pathMatch.Success)
                {
                    string fullPath = pathMatch.Groups["filepath"].Value;
                    if (fullPath.StartsWith(GeneratedRootFolder + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);

                        if (fileNameWithoutExtension.StartsWith(EasyCSCodeGeneratorReflection.EntityDataFactoryPrefix) ||
                            fileNameWithoutExtension.StartsWith(EasyCSCodeGeneratorReflection.EntityDataProviderPrefix) ||
                            fileNameWithoutExtension.StartsWith(EasyCSCodeGeneratorReflection.EntityBehaviorProviderPrefix) ||
                            fileNameWithoutExtension.StartsWith(EasyCSCodeGeneratorReflection.ActorDataSharedFactoryPrefix) ||
                            fileNameWithoutExtension.StartsWith(EasyCSCodeGeneratorReflection.ActorDataSharedProviderPrefix) ||
                            fileNameWithoutExtension.StartsWith(EasyCSCodeGeneratorReflection.OldActorDataFactoryPrefix))
                        {
                            results.Add(fullPath);
                        }
                    }
                }
            }
            return new List<string>(results);
        }
    }
}
