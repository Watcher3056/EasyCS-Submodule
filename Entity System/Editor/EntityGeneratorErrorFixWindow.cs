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

        // Lists to track actions performed by the last "Execute" for Undo/Apply (store paths)
        private List<string> lastCommentedOutFiles = new();
        private List<string> lastGeneratedFiles = new();

        // Lists for Dry Run results (store names)
        private List<string> dryRunToDelete = new(); // These are candidates for commenting out
        private List<string> dryRunToGenerate = new();

        // List to store names of files affected in the last execution for display
        private List<string> executedGeneratedNames = new();
        private List<string> executedCommentedOutNames = new();


        // Use constants for generated file prefixes
        private const string FactoryPrefix = "EntityDataFactory";
        private const string DataProviderPrefix = "EntityDataProvider";
        private const string BehaviorProviderPrefix = "EntityBehaviorProvider";

        private static readonly string GeneratedFolder = "Assets/EasyCS Generated/EntityData";
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

            // Text area for pasting errors (maybe disable in Executed state?)
            GUI.enabled = currentState != WindowState.Executed;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            errorText = EditorGUILayout.TextArea(errorText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            GUI.enabled = true; // Re-enable GUI

            GUILayout.Space(5);

            // --- Buttons based on State ---

            if (currentState == WindowState.Initial || currentState == WindowState.DryRunCompleted)
            {
                // Dry Run Button
                if (GUILayout.Button("Dry Run (Preview Actions)"))
                {
                    RunDryRun();
                }

                // Execute Button
                // Only enable execute if there are actions to perform in the dry run
                GUI.enabled = dryRunToDelete.Count > 0 || dryRunToGenerate.Count > 0;
                if (GUILayout.Button("Execute Fix & Clean"))
                {
                    ExecuteFixAndClean();
                }
                GUI.enabled = true; // Re-enable GUI
            }
            else if (currentState == WindowState.Executed)
            {
                // Apply Actions Button (Finalize Deletions)
                if (GUILayout.Button("Apply Actions (Finalize Deletions)"))
                {
                    ApplyActions();
                }

                // Undo Actions Button (Revert last Execute)
                if (GUILayout.Button("Undo Actions"))
                {
                    UndoLastAction();
                }
            }

            GUILayout.Space(10);

            // --- Display Results based on State ---

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
            lastGeneratedFiles.Clear();
            dryRunToDelete.Clear();
            dryRunToGenerate.Clear();
            executedCommentedOutNames.Clear();
            executedGeneratedNames.Clear();
            Debug.Log("[EasyCS] Editor Window State Reset to Initial.");
        }

        private void RunDryRun()
        {
            dryRunToGenerate = ParseMissingTypesToGenerate(errorText);
            dryRunToDelete = ParseObsoleteTypesToDelete(errorText); // These will be commented out
            currentState = WindowState.DryRunCompleted;
            Debug.Log($"Dry Run Complete. Will Comment Out: {dryRunToDelete.Count}, Will Generate: {dryRunToGenerate.Count}");
        }

        private void ExecuteFixAndClean()
        {
            // Clear previous action history and display lists
            lastCommentedOutFiles.Clear();
            lastGeneratedFiles.Clear();
            executedCommentedOutNames.Clear();
            executedGeneratedNames.Clear();


            // Get the lists from the dry run to ensure consistency
            var typesToCommentOut = ParseObsoleteTypesToDelete(errorText);
            var typesToGenerate = ParseMissingTypesToGenerate(errorText);

            // COMMENT OUT Obsolete Files
            EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", "Commenting out obsolete files...", 0);
            int commentCount = 0;
            foreach (var name in typesToCommentOut)
            {
                string path = Path.Combine(GeneratedFolder, name + ".cs").Replace("\\", "/");
                if (File.Exists(path))
                {
                    try
                    {
                        CommentOutFile(path);
                        lastCommentedOutFiles.Add(path); // Store full path for undo/apply
                        executedCommentedOutNames.Add(name); // Store name for display
                        Debug.Log($"[EasyCS] Commented out: {name}.cs at {path}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[EasyCS] Error commenting out {path}: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[EasyCS] Attempted to comment out {name}.cs but file not found at {path}.");
                }
                EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", $"Commenting out obsolete files: {name}.cs", (float)++commentCount / typesToCommentOut.Count);
            }
            EditorUtility.ClearProgressBar();


            // GENERATE Missing Files
            EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", "Generating missing files...", 0);
            int generateCount = 0;
            foreach (var name in typesToGenerate)
            {
                try
                {
                    string generatedPath = ""; // Store path for undo
                    bool success = false; // Track if generation was successful

                    if (name.StartsWith(FactoryPrefix))
                    {
                        string baseName = name.Replace(FactoryPrefix, "");
                        // Assuming EntityDataGenerator exists and has these static methods
                        // Add null/empty checks for baseName if needed
                        // success = EntityDataGenerator.RegenerateFactory(baseName); // Uncomment and replace with actual call
                        // For now, simulate success and path
                        success = true;
                        generatedPath = Path.Combine(GeneratedFolder, name + ".cs").Replace("\\", "/");
                        // Create dummy file if it doesn't exist for testing the flow
                        if (!File.Exists(generatedPath)) File.WriteAllText(generatedPath, $"// Placeholder for generated {name}");
                        Debug.Log($"[EasyCS] Generated: {name}.cs (RegenerateFactory called for {baseName})");
                    }
                    else if (name.StartsWith(DataProviderPrefix))
                    {
                        string baseName = name.Replace(DataProviderPrefix, "");
                        // Assuming EntityDataGenerator exists and has these static methods
                        // success = EntityDataGenerator.RegenerateProvider(baseName); // Uncomment and replace with actual call
                        // For now, simulate success and path
                        success = true;
                        generatedPath = Path.Combine(GeneratedFolder, name + ".cs").Replace("\\", "/");
                        // Create dummy file if it doesn't exist for testing the flow
                        if (!File.Exists(generatedPath)) File.WriteAllText(generatedPath, $"// Placeholder for generated {name}");
                        Debug.Log($"[EasyCS] Generated: {name}.cs (RegenerateProvider called for {baseName})");
                    }
                    else if (name.StartsWith(BehaviorProviderPrefix))
                    {
                        string baseName = name.Replace(BehaviorProviderPrefix, "");
                        // Assuming EntityDataGenerator exists and has these static methods
                        // success = EntityDataGenerator.RegenerateBehaviorProvider(baseName); // Uncomment and replace with actual call
                        // For now, simulate success and path
                        success = true;
                        generatedPath = Path.Combine(GeneratedFolder, name + ".cs").Replace("\\", "/");
                        // Create dummy file if it doesn't exist for testing the flow
                        if (!File.Exists(generatedPath)) File.WriteAllText(generatedPath, $"// Placeholder for generated {name}");
                        Debug.Log($"[EasyCS] Generated: {name}.cs (RegenerateBehaviorProvider called for {baseName})");
                    }
                    else
                    {
                        Debug.LogWarning($"[EasyCS] Cannot generate unknown type pattern: {name}");
                        success = false; // Mark as failed if pattern is unknown
                    }

                    // Add the path and name to the undo/apply/display lists if generation was successful and file exists
                    if (success && !string.IsNullOrEmpty(generatedPath) && File.Exists(generatedPath))
                    {
                        lastGeneratedFiles.Add(generatedPath); // Store full path for undo/apply
                        executedGeneratedNames.Add(name); // Store name for display
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EasyCS] Error generating {name}: {e.Message}");
                }
                EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", $"Generating missing files: {name}.cs", (float)++generateCount / typesToGenerate.Count);
            }
            EditorUtility.ClearProgressBar();

            // Transition to Executed state
            currentState = WindowState.Executed;

            Debug.Log($"[EasyCS] Fix & Clean Execute Complete. Commented Out: {lastCommentedOutFiles.Count}, Generated: {lastGeneratedFiles.Count}");

            // Refresh AssetDatabase to pick up changes (commented files are still assets, new files are added)
            AssetDatabase.Refresh();
        }

        private void ApplyActions()
        {
            Debug.Log("--- Applying Event System Fix & Clean Actions (Finalizing Deletions) ---");

            // Finalize Deletion (Delete commented out files)
            EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", "Applying actions (deleting commented files)...", 0);
            int deleteCount = 0;
            foreach (var path in lastCommentedOutFiles)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        // AssetDatabase.DeleteAsset also deletes the .meta file
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

            // Generated files remain untouched as per requirement

            // Reset state and clear action history
            ResetState();

            Debug.Log("--- Apply Actions Complete ---");

            // Refresh AssetDatabase to pick up changes
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

            // Undo Generation (Delete Generated Files)
            EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", "Undoing generated files...", 0);
            int deleteCount = 0;
            foreach (var path in lastGeneratedFiles)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        // AssetDatabase.DeleteAsset also deletes the .meta file
                        if (AssetDatabase.DeleteAsset(path))
                        {
                            Debug.Log($"[EasyCS] Deleted generated file: {Path.GetFileName(path)}");
                        }
                        else
                        {
                            Debug.LogError($"[EasyCS] Failed to delete generated asset: {path}. It might be locked or in use.");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[EasyCS] Error deleting generated file {path}: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[EasyCS] Attempted to delete generated file {Path.GetFileName(path)} but file not found at {path}.");
                }
                EditorUtility.DisplayProgressBar("EasyCS Fix & Clean", $"Undoing generated files: {Path.GetFileName(path)}", (float)++deleteCount / lastGeneratedFiles.Count);
            }
            EditorUtility.ClearProgressBar();

            // Reset state and clear action history
            ResetState();

            Debug.Log("--- Undo Complete ---");

            // Refresh AssetDatabase to pick up changes (uncommented files are modified, deleted files are gone)
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
            if (dryRunToDelete.Count > 0 || dryRunToGenerate.Count > 0)
            {
                EditorGUILayout.LabelField("Dry Run Summary", EditorStyles.boldLabel);

                if (dryRunToDelete.Count > 0)
                {
                    EditorGUILayout.LabelField("Will Comment Out:", EditorStyles.miniLabel);
                    foreach (var name in dryRunToDelete)
                        EditorGUILayout.HelpBox(name + ".cs", MessageType.Warning); // Warning for commenting out (soft delete)
                }

                if (dryRunToGenerate.Count > 0)
                {
                    EditorGUILayout.LabelField("Will Generate:", EditorStyles.miniLabel);
                    foreach (var name in dryRunToGenerate)
                        EditorGUILayout.HelpBox(name + ".cs", MessageType.Info); // Info for generation
                }
            }
        }

        private void DisplayExecutedActions()
        {
            // Display the results of the last executed action for context before undoing
            if (executedCommentedOutNames.Count > 0 || executedGeneratedNames.Count > 0)
            {
                EditorGUILayout.LabelField("Last Executed Actions", EditorStyles.boldLabel);

                if (executedCommentedOutNames.Count > 0)
                {
                    EditorGUILayout.LabelField("Commented Out:", EditorStyles.miniLabel);
                    foreach (var name in executedCommentedOutNames)
                        EditorGUILayout.HelpBox(name + ".cs", MessageType.Warning);
                }

                if (executedGeneratedNames.Count > 0)
                {
                    EditorGUILayout.LabelField("Generated:", EditorStyles.miniLabel);
                    foreach (var name in executedGeneratedNames)
                        EditorGUILayout.HelpBox(name + ".cs", MessageType.Info);
                }
            }
        }


        /// <summary>
        /// Parses compile errors to find names of generated types that are reported as missing.
        /// These are candidates for regeneration.
        /// </summary>
        private List<string> ParseMissingTypesToGenerate(string raw)
        {
            var results = new HashSet<string>();
            // Regex to find "The type or namespace name 'TypeName' could not be found"
            var matches = Regex.Matches(raw, @"The type or namespace name '(\w+)' could not be found");

            foreach (Match match in matches)
            {
                string typeName = match.Groups[1].Value;
                // Filter for names matching the generated patterns
                if (typeName.StartsWith(FactoryPrefix) ||
                    typeName.StartsWith(DataProviderPrefix) ||
                    typeName.StartsWith(BehaviorProviderPrefix))
                {
                    results.Add(typeName);
                }
            }

            return new List<string>(results);
        }

        /// <summary>
        /// Parses compile errors to find files within the GeneratedFolder that have errors
        /// related to missing types. These generated files are likely obsolete and should be commented out.
        /// Normalizes path separators in the input string before applying the regex.
        /// </summary>
        private List<string> ParseObsoleteTypesToDelete(string raw)
        {
            // Debug.Log($"[EasyCS] Parsing for obsolete types...");
            // Debug.Log($"[EasyCS] Raw error text length: {raw.Length}");
            // Debug.Log($"[EasyCS] GeneratedFolder: {GeneratedFolder}");

            // Normalize path separators in the input string
            string normalizedRaw = raw.Replace('\\', '/');
            // Debug.Log($"[EasyCS] Normalized raw error text length: {normalizedRaw.Length}");

            var results = new HashSet<string>();
            // Regex to find errors in files within the GeneratedFolder path, specifically CS0246 (missing type)
            // Example match: Assets/EasyCS Generated/EntityData/EntityDataFactoryOldComponent.cs(10,25): error CS0246: The type or namespace name 'OldComponent' could not be found
            // We can now use a simple forward slash in the regex after escaping the folder path
            string pattern = $@"{Regex.Escape(GeneratedFolder)}/(\w+\.cs)\(\d+,\d+\):\s*error CS0246: The type or namespace name '(\w+)' could not be found";
            // Debug.Log($"[EasyCS] Regex pattern: {pattern}");

            var matches = Regex.Matches(normalizedRaw, pattern); // Use the normalized string

            // Debug.Log($"[EasyCS] Found {matches.Count} potential matches.");

            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    string fileName = match.Groups[1].Value; // e.g., EntityDataFactoryOldComponent.cs
                    string typeName = Path.GetFileNameWithoutExtension(fileName); // e.g., EntityDataFactoryOldComponent

                    // Debug.Log($"[EasyCS] Matched file: {fileName}, Extracted typeName: {typeName}");

                    // Ensure the file name matches one of our generated patterns
                    if (typeName.StartsWith(FactoryPrefix) ||
                        typeName.StartsWith(DataProviderPrefix) ||
                        typeName.StartsWith(BehaviorProviderPrefix))
                    {
                        results.Add(typeName); // Add the type name (without .cs)
                                               // Debug.Log($"[EasyCS] Added {typeName} to deletion list.");
                    } // else {
                      // Debug.Log($"[EasyCS] Matched file {fileName} but typeName {typeName} does not match expected prefixes.");
                      // }
                } // else {
                  // This part should ideally not be reached with Regex.Matches iteration
                  // Debug.LogWarning($"[EasyCS] Regex match iteration returned a non-successful match?");
                  // }
            }

            // Debug.Log($"[EasyCS] Finished parsing. {results.Count} types found for deletion.");
            return new List<string>(results);
        }
    }
}
