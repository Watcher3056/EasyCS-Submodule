using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;

namespace EasyCS.Editor
{
    public static class EasyCSCodeGeneratorReflection
    {
        // Root folder for all generated EasyCS code
        private const string GeneratedRootFolder = "Assets/EasyCS Generated";

        // Subfolders for different generated types
        private const string EntityDataProvidersFolder = GeneratedRootFolder + "/Entity Data Providers";
        private const string ActorDataSharedProvidersFolder = GeneratedRootFolder + "/Actor Data Shared Providers";
        private const string EntityBehaviorProvidersFolder = GeneratedRootFolder + "/Entity Behavior Providers";
        private const string EntityDataFactoriesFolder = GeneratedRootFolder + "/Entity Data Factories";
        private const string ActorDataSharedFactoriesFolder = GeneratedRootFolder + "/Actor Data Shared Factories";
        // List of all new subfolders for easier iteration
        private static readonly string[] NewGeneratedSubfolders = {
            EntityDataProvidersFolder,
            ActorDataSharedProvidersFolder,
            EntityBehaviorProvidersFolder,
            EntityDataFactoriesFolder,
            ActorDataSharedFactoriesFolder
        };

        // Old generated folders that need cleanup (though current logic handles moving from anywhere)
        private const string OldEntityDataFolder = GeneratedRootFolder + "/EntityData";
        private const string OldActorDataFolder = GeneratedRootFolder + "/ActorData";
        private static readonly string[] OldGeneratedFolders = {
            OldEntityDataFolder,
            OldActorDataFolder
        };


        // Assembly Definition details
        private const string AsmdefPath = GeneratedRootFolder + "/EasyCS.Generated.asmdef";
        private const string OldEntityDataAsmdefPath = OldEntityDataFolder + "/EntityData.Generated.asmdef"; // Path to the old asmdef
        private const string AsmdefName = "EasyCS.Generated";

        // Use constants for generated file prefixes
        private const string EntityDataPrefix = "EntityData"; // Prefix for user-defined EntityData types
        private const string EntityBehaviorPrefix = "EntityBehavior"; // Prefix for user-defined EntityBehavior types
        private const string ActorDataSharedPrefix = "ActorDataShared"; // Prefix for user-defined ActorDataShared types

        private const string FactorySuffix = "Factory"; // Suffix for generated Factory types
        private const string ProviderSuffix = "Provider"; // Suffix for generated Provider types

        // Full generated type name prefixes
        private const string EntityDataFactoryPrefix = EntityDataPrefix + FactorySuffix;
        private const string EntityDataProviderPrefix = EntityDataPrefix + ProviderSuffix;
        private const string EntityBehaviorProviderPrefix = EntityBehaviorPrefix + ProviderSuffix;
        private const string ActorDataSharedFactoryPrefix = ActorDataSharedPrefix + FactorySuffix;
        private const string ActorDataSharedProviderPrefix = ActorDataSharedPrefix + ProviderSuffix;

        // Old ActorData Factory prefix (from original generator)
        private const string OldActorDataFactoryPrefix = "DataActorDataFactory";

        // New: TriInspector Assembly File
        private const string TriInspectorAssemblyFileName = "EasyCS.Generated.TriInspectorAssembly.cs";
        private const string TriInspectorAssemblyFilePath = GeneratedRootFolder + "/" + TriInspectorAssemblyFileName;


        // Menu Items
        [MenuItem("EasyCS/Generate (Reflection)/Regenerate All Generated Scripts", false, 10)]
        public static void RegenerateAll()
        {
            Debug.Log("[EasyCS] Starting full regeneration (Reflection)...");
            // Ensure folders exist and are known to Unity immediately
            EnsureGeneratedFolders();
            // Perform generation and cleanup in a single pass
            GenerateAndCleanup();
            GenerateAsmdef(); // Generate the new asmdef
            AssetDatabase.Refresh(); // Final refresh
            Debug.Log("[EasyCS] Full regeneration (Reflection) complete.");
        }

        [MenuItem("EasyCS/Generate (Reflection)/Generate Missing Scripts", false, 11)]
        public static void GenerateMissing()
        {
            Debug.Log("[EasyCS] Starting generation of missing scripts (Reflection)...");
            // Ensure folders exist and are known to Unity immediately
            EnsureGeneratedFolders();
            // Perform generation and cleanup in a single pass
            GenerateAndCleanup(generateOnlyMissing: true); // Indicate to only generate missing
            GenerateAsmdef(); // Generate the new asmdef
            AssetDatabase.Refresh(); // Final refresh
            Debug.Log("[EasyCS] Generation of missing scripts (Reflection) complete.");
        }

        [MenuItem("EasyCS/Generate (Reflection)/Cleanup Obsolete Scripts", false, 12)]
        public static void CleanupObsolete()
        {
            Debug.Log("[EasyCS] Starting cleanup of obsolete scripts (Reflection)...");
            // Ensure folders exist and are known to Unity immediately
            EnsureGeneratedFolders();
            // Perform cleanup only (by generating expected and deleting non-matching)
            GenerateAndCleanup(cleanupOnly: true); // Indicate to only cleanup
            GenerateAsmdef(); // Regenerate asmdef as files might have been deleted
            AssetDatabase.Refresh(); // Final refresh
            Debug.Log("[EasyCS] Cleanup of obsolete scripts (Reflection) complete.");
        }


        // --- Combined Generation and Cleanup Logic ---

        private static void GenerateAndCleanup(bool generateOnlyMissing = false, bool cleanupOnly = false)
        {
            Debug.Log("[EasyCS] Starting combined generation and cleanup...");

            // Get current valid user-defined base types using reflection
            var entityDataTypes = GetAllTypesDerivedFrom(typeof(IEntityData)).ToList();
            var entityBehaviorTypes = GetAllTypesDerivedFrom(typeof(IEntityBehavior)).ToList();
            var actorDataSharedTypes = GetAllTypesDerivedFrom(typeof(ActorDataSharedBase)).ToList();

            // Filter out EntityData types marked with RuntimeOnlyAttribute
            entityDataTypes = entityDataTypes.Where(t => t.GetCustomAttribute<RuntimeOnlyAttribute>() == null).ToList();

            // Build a list of all expected generated files with their content and expected path
            var expectedFiles = new List<ExpectedGeneratedFileInfo>();

            // Expected Entity Data Factories and Providers
            foreach (var entityType in entityDataTypes)
            {
                string baseName = entityType.Name.Replace(EntityDataPrefix, "");
                string factoryName = $"{EntityDataFactoryPrefix}{baseName}";
                string providerName = $"{EntityDataProviderPrefix}{baseName}";
                string namespaceName = GetNamespaceOf(entityType) ?? "EasyCS.Generated.EntityData";

                // Add Factory
                expectedFiles.Add(new ExpectedGeneratedFileInfo
                {
                    ExpectedPath = Path.Combine(EntityDataFactoriesFolder, factoryName + ".cs").Replace("\\", "/"),
                    ExpectedContent = GenerateEntityDataFactoryContent(factoryName, entityType.Name, namespaceName),
                    InferredBaseName = baseName, // Store inferred base name
                    GeneratedFilePrefix = EntityDataFactoryPrefix // Store prefix
                });

                // Add Provider
                expectedFiles.Add(new ExpectedGeneratedFileInfo
                {
                    ExpectedPath = Path.Combine(EntityDataProvidersFolder, providerName + ".cs").Replace("\\", "/"),
                    ExpectedContent = GenerateEntityDataProviderContent(providerName, factoryName, entityType.Name, namespaceName),
                    InferredBaseName = baseName, // Store inferred base name
                    GeneratedFilePrefix = EntityDataProviderPrefix // Store prefix
                });
            }

            // Expected Entity Behavior Providers
            foreach (var behaviorType in entityBehaviorTypes)
            {
                string baseName = behaviorType.Name.Replace(EntityBehaviorPrefix, "");
                string providerName = $"{EntityBehaviorProviderPrefix}{baseName}";
                string namespaceName = GetNamespaceOf(behaviorType) ?? "EasyCS.Generated.EntityBehaviors";

                expectedFiles.Add(new ExpectedGeneratedFileInfo
                {
                    ExpectedPath = Path.Combine(EntityBehaviorProvidersFolder, providerName + ".cs").Replace("\\", "/"),
                    ExpectedContent = GenerateEntityBehaviorProviderContent(providerName, behaviorType.Name, namespaceName),
                    InferredBaseName = baseName, // Store inferred base name
                    GeneratedFilePrefix = EntityBehaviorProviderPrefix // Store prefix
                });
            }

            // Expected Actor Data Shared Factories and Providers
            foreach (var actorDataType in actorDataSharedTypes)
            {
                string baseName = actorDataType.Name.Replace(ActorDataSharedPrefix, "");
                string factoryName = $"{ActorDataSharedFactoryPrefix}{baseName}";
                string providerName = $"{ActorDataSharedProviderPrefix}{baseName}";
                string namespaceName = GetNamespaceOf(actorDataType) ?? "EasyCS.Generated.ActorDataShared";

                // Add Factory
                expectedFiles.Add(new ExpectedGeneratedFileInfo
                {
                    ExpectedPath = Path.Combine(ActorDataSharedFactoriesFolder, factoryName + ".cs").Replace("\\", "/"),
                    ExpectedContent = GenerateActorDataSharedFactoryContent(factoryName, actorDataType.Name, namespaceName),
                    InferredBaseName = baseName, // Store inferred base name
                    GeneratedFilePrefix = ActorDataSharedFactoryPrefix // Store prefix
                });

                // Add Provider
                expectedFiles.Add(new ExpectedGeneratedFileInfo
                {
                    ExpectedPath = Path.Combine(ActorDataSharedProvidersFolder, providerName + ".cs").Replace("\\", "/"),
                    ExpectedContent = GenerateActorDataSharedProviderContent(providerName, actorDataType.Name, factoryName, namespaceName),
                    InferredBaseName = baseName, // Store inferred base name
                    GeneratedFilePrefix = ActorDataSharedProviderPrefix // Store prefix
                });
            }

            // NEW: Expected TriInspector Assembly file
            expectedFiles.Add(new ExpectedGeneratedFileInfo
            {
                ExpectedPath = TriInspectorAssemblyFilePath,
                ExpectedContent = GenerateTriInspectorAssemblyContent(),
                InferredBaseName = "TriInspector", // A logical base name for identification
                GeneratedFilePrefix = "TriInspectorAssembly" // A unique prefix for this file type
            });


            // Get all existing .cs files in the generated root folder and its subdirectories
            string[] existingGeneratedAssetPaths = Directory.GetFiles(GeneratedRootFolder, "*.cs", SearchOption.AllDirectories)
                                                    .Select(p => p.Replace("\\", "/")) // Ensure Unity path format
                                                    .ToArray();

            // Create a list of existing file information
            var existingFiles = new List<ExistingGeneratedFileInfo>();
            foreach(var existingPath in existingGeneratedAssetPaths)
            {
                 var fileInfo = TryInferGeneratedFileInfo(existingPath);
                 if(fileInfo != null)
                 {
                     existingFiles.Add(fileInfo);
                 }
                 else
                 {
                     // Log files in the generated folder that we don't recognize
                     if (!existingPath.Contains("EasyCS Generated/EasyCSCodeGeneratorReflection.cs")) // Don't log for the generator script itself
                     {
                          Debug.Log($"[EasyCS] Found unrecognized file in generated folder: {existingPath}. It will be considered for deletion if cleanup is enabled.");
                     }
                 }
            }

            // Keep track of which existing files have been successfully processed (moved/updated)
            var successfullyProcessedExistingPaths = new HashSet<string>();

            // PHASE 1: Process existing files - attempt to move/rename and update content
            foreach (var existingFile in existingFiles)
            {
                // Find the corresponding expected file info
                var correspondingExpectedFile = expectedFiles.FirstOrDefault(ef =>
                    ef.InferredBaseName == existingFile.InferredBaseName &&
                    // Handle old ActorDataFactory mapping to new ActorDataSharedFactory
                    ((existingFile.GeneratedFilePrefix == OldActorDataFactoryPrefix && ef.GeneratedFilePrefix == ActorDataSharedFactoryPrefix) ||
                     (existingFile.GeneratedFilePrefix != OldActorDataFactoryPrefix && existingFile.GeneratedFilePrefix == ef.GeneratedFilePrefix))
                );

                if (correspondingExpectedFile != null)
                {
                    string currentAssetPath = existingFile.CurrentPath;
                    string targetAssetPath = correspondingExpectedFile.ExpectedPath;

                    bool needsMove = !currentAssetPath.Equals(targetAssetPath, StringComparison.OrdinalIgnoreCase);

                    if (needsMove)
                    {
                        bool targetFileExists = File.Exists(targetAssetPath);

                        if (targetFileExists)
                        {
                            string currentFileGUID = AssetDatabase.AssetPathToGUID(currentAssetPath);
                            string targetFileGUID = AssetDatabase.AssetPathToGUID(targetAssetPath);

                            if (currentFileGUID == targetFileGUID)
                            {
                                // The asset is already at the target path (or Unity thinks it is).
                                Debug.Log($"[EasyCS] Asset '{Path.GetFileName(currentAssetPath)}' already exists at target path '{targetAssetPath}' with matching GUID. Skipping move.");
                                // Update the path in the existingFiles list to the target path
                                existingFile.CurrentPath = targetAssetPath;
                                // Mark as successfully processed
                                successfullyProcessedExistingPaths.Add(targetAssetPath);
                                currentAssetPath = targetAssetPath; // Use the new path for content update
                            }
                            else
                            {
                                // A different file exists at the target path. This is a conflict.
                                Debug.LogWarning($"[EasyCS] Conflict detected: A different file exists at target path '{targetAssetPath}'. Deleting conflicting file before move.");
                                AssetDatabase.DeleteAsset(targetAssetPath);
                                // Force save and refresh to help Unity process the deletion
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                                // After deleting, the target path is now clear, proceed with move attempt below.
                            }
                        }

                        // Attempt the move if it's still needed after potential conflict resolution
                        if (!currentAssetPath.Equals(targetAssetPath, StringComparison.OrdinalIgnoreCase))
                        {
                             Debug.Log($"[EasyCS] Moving/Renaming file: {currentAssetPath} -> {targetAssetPath}");
                             string moveError = AssetDatabase.MoveAsset(currentAssetPath, targetAssetPath); // Get the error message

                             // FIX: Correctly check if the move failed (non-empty string)
                             if (!string.IsNullOrEmpty(moveError))
                             {
                                 Debug.LogError($"[EasyCS] Failed to move asset from {currentAssetPath} to {targetAssetPath}. Error: {moveError}. This might leave a broken file or orphaned meta file. Skipping content update for this file.");
                                 // Do NOT add to successfullyProcessedExistingPaths if move failed
                                 continue; // Skip content update for this file
                             }
                             // Update the path in the existingFiles list if the move was successful
                             existingFile.CurrentPath = targetAssetPath; // AssetDatabase.MoveAsset returns empty on success, use targetPath
                             // Mark as successfully processed
                             successfullyProcessedExistingPaths.Add(targetAssetPath);
                             currentAssetPath = targetAssetPath; // Use the new path for content update
                        }
                        else
                        {
                             // If needsMove was true but currentAssetPath now equals targetAssetPath,
                             // it means the GUID match logic above handled it. Mark as processed.
                            successfullyProcessedExistingPaths.Add(currentAssetPath);
                        }
                    }
                    else
                    {
                        // File is already at the correct location. Mark as successfully processed.
                        successfullyProcessedExistingPaths.Add(currentAssetPath);
                    }

                    // Now, update its content if necessary (if not in cleanup-only mode)
                    if (!cleanupOnly && successfullyProcessedExistingPaths.Contains(currentAssetPath)) // Only update if successfully processed
                    {
                        try
                        {
                            string existingContent = File.ReadAllText(currentAssetPath); // Read from the potentially new location
                            if (existingContent != correspondingExpectedFile.ExpectedContent)
                            {
                                File.WriteAllText(currentAssetPath, correspondingExpectedFile.ExpectedContent);
                                Debug.Log($"[EasyCS] Updated content for generated file: {Path.GetFileName(currentAssetPath)}");
                            }
                            else
                            {
                                Debug.Log($"[EasyCS] Existing generated file content unchanged: {Path.GetFileName(currentAssetPath)}");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[EasyCS] Error reading/writing generated file {Path.GetFileName(currentAssetPath)}: {e.Message}");
                        }
                    }
                }
                // If correspondingExpectedFile is null, this existing file is obsolete and will be handled in Phase 3.
            }

            // PHASE 2: Generate any files that were NOT successfully processed in Phase 1 (i.e., truly missing)
            if (!cleanupOnly) // Only generate if not in cleanup-only mode
            {
                foreach (var expectedFile in expectedFiles)
                {
                    // Check if an existing file corresponding to this expected file was successfully processed.
                    // We need to check against the *final* path it should be at.
                    bool wasProcessed = successfullyProcessedExistingPaths.Contains(expectedFile.ExpectedPath);

                    if (!wasProcessed)
                    {
                        // This expected file does not exist at its correct location, nor was a corresponding
                        // existing file successfully moved/updated to this location. So, it needs to be generated.
                        try
                        {
                            string targetDirectory = Path.GetDirectoryName(expectedFile.ExpectedPath);
                            if (!Directory.Exists(targetDirectory))
                            {
                                Directory.CreateDirectory(targetDirectory);
                                // Crucial to refresh after creating directories so Unity recognizes them and creates .meta files
                                AssetDatabase.Refresh();
                            }
                             // Double-check if a file *still* exists at the target path after refreshes, just in case.
                            if (!File.Exists(expectedFile.ExpectedPath))
                            {
                                File.WriteAllText(expectedFile.ExpectedPath, expectedFile.ExpectedContent);
                                Debug.Log($"[EasyCS] Generated missing file: {Path.GetFileName(expectedFile.ExpectedPath)}");
                            }
                            else
                            {
                                Debug.LogWarning($"[EasyCS] Expected file '{Path.GetFileName(expectedFile.ExpectedPath)}' still exists at target path after checks. Skipping generation to avoid overwrite issues.");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[EasyCS] Error generating missing file {Path.GetFileName(expectedFile.ExpectedPath)}: {e.Message}");
                        }
                    }
                }
            }


            // PHASE 3: Clean up obsolete existing files (those not successfully processed)
            if (!generateOnlyMissing) // Only delete if not in "generate only missing" mode
            {
                foreach (var existingFile in existingFiles)
                {
                    if (!successfullyProcessedExistingPaths.Contains(existingFile.CurrentPath))
                    {
                        // This existing file was not successfully moved/updated or matched with any expected file, so it's obsolete.
                        Debug.Log($"[EasyCS] Deleting obsolete generated file: {existingFile.CurrentPath}");
                        AssetDatabase.DeleteAsset(existingFile.CurrentPath);
                    }
                }
            }


            // Clean up old directories and the old asmdef file (always run cleanup for old folders/asmdef)
             CleanupEmptyDirectories(GeneratedRootFolder);
            if (File.Exists(OldEntityDataAsmdefPath))
            {
                Debug.Log($"[EasyCS] Deleting old asmdef: {OldEntityDataAsmdefPath}");
                AssetDatabase.DeleteAsset(OldEntityDataAsmdefPath);
            }

            Debug.Log("[EasyCS] Combined generation and cleanup complete.");
        }


        // --- Individual Script Content Generation ---

        private static string GenerateEntityDataFactoryContent(string factoryName, string entityDataTypeName, string namespaceName)
        {
            return string.Format(
@"using EasyCS;
using EasyCS.EntityFactorySystem;
using UnityEngine; // Required for CreateAssetMenu

namespace {2}
{{
    [CreateAssetMenu(fileName = ""{0}"", menuName = ""EasyCS/Entity Data Factories/{1}"")] // Menu name based on base name
    public partial class {0} : EntityDataFactory<{1}> // Use partial
    {{
    }}
}}", factoryName, entityDataTypeName, namespaceName); // Use full type name for generic argument
        }

        private static string GenerateEntityDataProviderContent(string providerName, string factoryName, string entityDataTypeName, string namespaceName)
        {
            return string.Format(
@"using EasyCS;
using EasyCS.EntityFactorySystem;

namespace {3}
{{
    public partial class {0} : EntityDataProvider<{1}, {2}> // Use partial
    {{
    }}
}}", providerName, factoryName, entityDataTypeName, namespaceName); // TFactory, TData
        }

        private static string GenerateEntityBehaviorProviderContent(string providerName, string behaviorTypeName, string namespaceName)
        {
            return string.Format(
        @"using EasyCS;
using EasyCS.EntityFactorySystem; // Assuming EntityBehaviorProvider is in this namespace

namespace {2}
{{
    public partial class {0} : EntityBehaviorProvider<{1}> // Use partial
    {{
    }}
}}", providerName, behaviorTypeName, namespaceName);
        }

        private static string GenerateActorDataSharedFactoryContent(string factoryName, string actorDataTypeName, string namespaceName)
        {
            // FIX: Removed '$' before @" to correctly use string.Format
            return string.Format(
@"using EasyCS;
using UnityEngine; // Required for CreateAssetMenu

namespace {2}
{{
    [CreateAssetMenu(fileName = ""{0}"", menuName = ""EasyCS/Actor Data Shared Factories/{1}"")] // Menu name based on base name
    public partial class {0} : ActorDataSharedFactory<{1}> // Use partial
    {{
    }}
}}", factoryName, actorDataTypeName, namespaceName); // Use full type name for generic argument
        }

        private static string GenerateActorDataSharedProviderContent(string providerName, string actorDataTypeName, string factoryName, string namespaceName)
        {
            // FIX: Removed '$' before @" to correctly use string.Format
            return string.Format(
@"using EasyCS;

namespace {3}
{{
    public partial class {0} : ActorDataSharedProviderBase<{1}, {2}> // *** Corrected type name here ***
    {{
    }}
}}", providerName, actorDataTypeName, factoryName, namespaceName); // TData, TFactory
        }

        // NEW: Generates the content for the TriInspector assembly attribute file
        private static string GenerateTriInspectorAssemblyContent()
        {
            // Add using TriInspector;
            return @"
using TriInspector; // Ensure TriInspector namespace is included

[assembly: DrawWithTriInspector]
";
        }


        // --- Assembly Definition Generation ---

        private static void GenerateAsmdef()
        {
            Debug.Log("[EasyCS] Generating Assembly Definition...");
            // Ensure root folder exists (already done, but good practice)
            EnsureGeneratedFolders(); // Note: This will call AssetDatabase.Refresh()

            var referencedAssemblies = new HashSet<string> { "EasyCS.Runtime" }; // Always reference EasyCS.Runtime
            referencedAssemblies.Add("TriInspector"); // NEW: Add TriInspector assembly reference

            // Find all assemblies containing the user-defined base types and their derived types using reflection
            var baseTypes = new Type[] {
                typeof(IEntityData),
                typeof(IEntityBehavior),
                typeof(ActorDataSharedBase)
            }.Where(t => t != null).ToList(); // Filter out null if a base type is missing

            foreach (var baseType in baseTypes)
            {
                var derivedTypes = GetAllTypesDerivedFrom(baseType);
                foreach (var type in derivedTypes)
                {
                    if (type?.Assembly != null) // Added null check for Assembly
                    {
                        referencedAssemblies.Add(type.Assembly.GetName().Name);
                    }
                }
                // Also add the assembly of the base type itself
                if (baseType?.Assembly != null) // Added null check for Assembly
                {
                    referencedAssemblies.Add(baseType.Assembly.GetName().Name);
                }
            }

            // Also add assemblies containing the generic base classes for generated types using reflection
            // NOTE: Ensure the assembly containing these types is referenced by the assembly
            // containing this generator script (e.g., EasyCS.Editor.asmdef -> EasyCS.Runtime.asmdef).
            var genericBaseTypes = new Type[] {
                typeof(EntityDataFactory<>),
                typeof(EntityDataProvider<,>),
                typeof(EntityBehaviorProvider<>),
                typeof(ActorDataSharedFactory<>),
                typeof(ActorDataSharedProviderBase<,>)
            }.Where(t => t != null).ToList(); // Filter out null if a generic base type is missing

            foreach (var genericBaseType in genericBaseTypes)
            {
                if (genericBaseType?.Assembly != null) // Added null check for Assembly
                {
                    referencedAssemblies.Add(genericBaseType.Assembly.GetName().Name);
                }
            }


            string[] references = referencedAssemblies
                .Distinct()
                .OrderBy(r => r) // Order for consistency
                .Select(r => $"\"{r}\"")
                .ToArray();

            string asmdefContent =
        $@"{{
    ""name"": ""{AsmdefName}"",
    ""references"": [
        {string.Join(",\n        ", references)}
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";

            File.WriteAllText(AsmdefPath, asmdefContent);
            Debug.Log($"[EasyCS] Generated Assembly Definition: {AsmdefPath}");
        }


        // --- Helper Methods ---

        private static void EnsureGeneratedFolders()
        {
            Debug.Log("[EasyCS] Ensuring generated folders exist...");
            if (!Directory.Exists(GeneratedRootFolder))
            {
                Directory.CreateDirectory(GeneratedRootFolder);
            }
            foreach (var folder in NewGeneratedSubfolders)
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }
            // Refresh AssetDatabase to ensure Unity is aware of the new folders and creates .meta files
            // This refresh is crucial before attempting to move assets into these folders.
            AssetDatabase.Refresh();
            Debug.Log("[EasyCS] Generated folders ensured.");
        }

        private static void OverwriteIfChanged(string path, string newContent)
        {
            bool fileExists = File.Exists(path);

            if (!fileExists || File.ReadAllText(path) != newContent)
            {
                File.WriteAllText(path, newContent);
                Debug.Log($"[EasyCS] {(fileExists ? "Updated" : "Generated")} {Path.GetFileName(path)}");
            }
            else
            {
                Debug.Log($"[EasyCS] File content unchanged, skipping write: {Path.GetFileName(path)}");
            }
        }

        // Helper to get the namespace of a type, returning null if not found or global
        private static string GetNamespaceOf(Type type)
        {
            if (type == null || string.IsNullOrEmpty(type.Namespace))
            {
                return null; // Or return a default namespace if preferred
            }
            return type.Namespace;
        }

        // Helper to determine the expected subfolder for a given generated file prefix
        private static string GetExpectedSubfolderForPrefix(string prefix)
        {
            if (prefix.StartsWith(EntityDataFactoryPrefix)) return EntityDataFactoriesFolder;
            if (prefix.StartsWith(EntityDataProviderPrefix)) return EntityDataProvidersFolder;
            if (prefix.StartsWith(EntityBehaviorProviderPrefix)) return EntityBehaviorProvidersFolder;
            if (prefix.StartsWith(ActorDataSharedFactoryPrefix)) return ActorDataSharedFactoriesFolder;
            if (prefix.StartsWith(ActorDataSharedProviderPrefix)) return ActorDataSharedProvidersFolder;
            return GeneratedRootFolder; // Fallback, should not happen with correct prefixes
        }

        // Helper to try and infer generated file info from an existing path and name
        private static ExistingGeneratedFileInfo TryInferGeneratedFileInfo(string assetPath)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assetPath);

            string inferredBaseName = null;
            string generatedFilePrefix = null;

            if (fileNameWithoutExtension.StartsWith(EntityDataFactoryPrefix))
            {
                inferredBaseName = fileNameWithoutExtension.Replace(EntityDataFactoryPrefix, "");
                generatedFilePrefix = EntityDataFactoryPrefix;
            }
            else if (fileNameWithoutExtension.StartsWith(EntityDataProviderPrefix))
            {
                inferredBaseName = fileNameWithoutExtension.Replace(EntityDataProviderPrefix, "");
                generatedFilePrefix = EntityDataProviderPrefix;
            }
            else if (fileNameWithoutExtension.StartsWith(EntityBehaviorProviderPrefix))
            {
                inferredBaseName = fileNameWithoutExtension.Replace(EntityBehaviorProviderPrefix, "");
                generatedFilePrefix = EntityBehaviorProviderPrefix;
            }
            else if (fileNameWithoutExtension.StartsWith(ActorDataSharedFactoryPrefix))
            {
                inferredBaseName = fileNameWithoutExtension.Replace(ActorDataSharedFactoryPrefix, "");
                generatedFilePrefix = ActorDataSharedFactoryPrefix;
            }
            else if (fileNameWithoutExtension.StartsWith(ActorDataSharedProviderPrefix))
            {
                inferredBaseName = fileNameWithoutExtension.Replace(ActorDataSharedProviderPrefix, "");
                generatedFilePrefix = ActorDataSharedProviderPrefix;
            }
            // Special handling for old ActorData factories with the old prefix
            else if (fileNameWithoutExtension.StartsWith(OldActorDataFactoryPrefix))
            {
                inferredBaseName = fileNameWithoutExtension.Replace(OldActorDataFactoryPrefix, "");
                generatedFilePrefix = OldActorDataFactoryPrefix; // Keep old prefix for identification
            }
            // NEW: Handle TriInspector assembly file
            else if (fileNameWithoutExtension.Equals(Path.GetFileNameWithoutExtension(TriInspectorAssemblyFileName)))
            {
                inferredBaseName = "TriInspector"; // Consistent base name
                generatedFilePrefix = "TriInspectorAssembly"; // Unique prefix
            }


            if (inferredBaseName != null && generatedFilePrefix != null)
            {
                return new ExistingGeneratedFileInfo
                {
                    CurrentPath = assetPath,
                    CurrentFileName = Path.GetFileName(assetPath),
                    InferredBaseName = inferredBaseName,
                    GeneratedFilePrefix = generatedFilePrefix
                };
            }

            return null; // Could not infer generated file info
        }


        // --- Reflection Helpers (Copied and adapted from original generators) ---

        private static List<Type> GetAllTypesDerivedFrom(Type baseType)
        {
            if (baseType == null) return new List<Type>(); // Handle case if baseType is null

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try { return assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // Log loader exceptions to help diagnose issues
                        foreach (var loaderEx in ex.LoaderExceptions)
                        {
                            Debug.LogError($"[EasyCS] Loader Exception: {loaderEx.Message}");
                        }
                        return Array.Empty<Type>();
                    }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t =>
                    t != null &&
                    t.IsClass &&
                    !t.IsAbstract &&
                    (
                        baseType.IsInterface
                            ? baseType.IsAssignableFrom(t) // Check for interface implementation
                            : (t.IsSubclassOf(baseType) || (baseType.IsGenericTypeDefinition && InheritsFromRawGeneric(t, baseType))) // Check for concrete class inheritance or generic inheritance
                    )
                ).ToList();
        }

        // Helper to check if a type inherits from a raw generic type definition (e.g., EntityDataFactory<>)
        private static bool InheritsFromRawGeneric(Type toCheck, Type generic)
        {
            if (toCheck == null || generic == null || !generic.IsGenericTypeDefinition) return false;

            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }

        // Helper to detect the class name from file content using regex (basic parsing)
        // This method is not directly used in the revised GenerateAndCleanup logic
        // for determining target paths, as file name conventions are assumed.
        private static string DetectClassName(string content)
        {
            // Regex to find 'class ClassName' or 'public class ClassName' etc.
            var match = Regex.Match(content, @"\b(public|private|protected|internal|\s+)\s+class\s+(\w+)\b");
            if (match.Success && match.Groups.Count > 2)
            {
                return match.Groups[2].Value; // Capture group 2 is the class name
            }
            return null; // Class name not found or pattern not matched
        }

        // Helper to check if a file name matches one of the expected generated patterns
        private static bool IsExpectedGeneratedFileName(string fileName)
        {
            return fileName.StartsWith(EntityDataFactoryPrefix) ||
                   fileName.StartsWith(EntityDataProviderPrefix) ||
                   fileName.StartsWith(EntityBehaviorProviderPrefix) ||
                   fileName.StartsWith(ActorDataSharedFactoryPrefix) ||
                   fileName.StartsWith(ActorDataSharedProviderPrefix) ||
                   fileName.Equals(Path.GetFileNameWithoutExtension(TriInspectorAssemblyFileName)) || // NEW: Check for TriInspector assembly file
                   // Also include the old ActorData Factory prefix for cleanup/migration
                   fileName.StartsWith(OldActorDataFactoryPrefix);
        }

        // Helper to clean up empty directories recursively
        private static void CleanupEmptyDirectories(string startDirectory)
        {
            if (!Directory.Exists(startDirectory))
                return;

            foreach (var directory in Directory.GetDirectories(startDirectory))
            {
                CleanupEmptyDirectories(directory); // Recurse first
                if (!Directory.GetFiles(directory).Any() && !Directory.GetDirectories(directory).Any())
                {
                    Debug.Log($"[EasyCS] Deleting empty directory: {directory}");
                    // Use AssetDatabase.DeleteAsset to delete the directory and its .meta file
                    AssetDatabase.DeleteAsset(directory.Replace("\\", "/"));
                }
            }
        }


        // Helper class to hold information about an expected generated file
        private class ExpectedGeneratedFileInfo
        {
            public string ExpectedPath { get; set; }
            public string ExpectedContent { get; set; }
            public string InferredBaseName { get; set; } // Added to store inferred base name
            public string GeneratedFilePrefix { get; set; } // Added to store the generated file prefix
        }

        // Helper class to hold information about an existing generated file
        private class ExistingGeneratedFileInfo
        {
            public string CurrentPath { get; set; }
            public string CurrentFileName { get; set; }
            public string InferredBaseName { get; set; } // Inferred base name from file name
            public string GeneratedFilePrefix { get; set; } // Inferred prefix from file name
        }
    }
}
