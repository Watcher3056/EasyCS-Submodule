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
        public const string GeneratedRootFolder = "Assets/EasyCS Generated";

        // Subfolders for different generated types
        public const string EntityDataProvidersFolder = GeneratedRootFolder + "/Entity Data Providers";
        public const string ActorDataSharedProvidersFolder = GeneratedRootFolder + "/Actor Data Shared Providers";
        public const string EntityBehaviorProvidersFolder = GeneratedRootFolder + "/Entity Behavior Providers";
        public const string EntityDataFactoriesFolder = GeneratedRootFolder + "/Entity Data Factories";
        public const string ActorDataSharedFactoriesFolder = GeneratedRootFolder + "/Actor Data Shared Factories";
        private static readonly string[] NewGeneratedSubfolders = {
            EntityDataProvidersFolder,
            ActorDataSharedProvidersFolder,
            EntityBehaviorProvidersFolder,
            EntityDataFactoriesFolder,
            ActorDataSharedFactoriesFolder
        };

        // Old generated folders that need cleanup
        private const string OldEntityDataFolder = GeneratedRootFolder + "/EntityData";
        private const string OldActorDataFolder = GeneratedRootFolder + "/ActorData";
        private static readonly string[] OldGeneratedFolders = {
            OldEntityDataFolder,
            OldActorDataFolder
        };

        // Assembly Definition details
        public const string AsmdefPath = GeneratedRootFolder + "/EasyCS.Generated.asmdef";
        private const string OldEntityDataAsmdefPath = OldEntityDataFolder + "/EntityData.Generated.asmdef";
        public const string AsmdefName = "EasyCS.Generated";

        // Constants for user-defined type prefixes
        public const string EntityDataPrefix = "EntityData";
        public const string EntityBehaviorPrefix = "EntityBehavior";
        public const string ActorDataSharedPrefix = "ActorDataShared";

        // Constants for generated type suffixes
        public const string FactorySuffix = "Factory";
        public const string ProviderSuffix = "Provider";

        // Full generated type name prefixes
        public const string EntityDataFactoryPrefix = EntityDataPrefix + FactorySuffix;
        public const string EntityDataProviderPrefix = EntityDataPrefix + ProviderSuffix;
        public const string EntityBehaviorProviderPrefix = EntityBehaviorPrefix + ProviderSuffix;
        public const string ActorDataSharedFactoryPrefix = ActorDataSharedPrefix + FactorySuffix;
        public const string ActorDataSharedProviderPrefix = ActorDataSharedPrefix + ProviderSuffix;

        // Old ActorData Factory prefix (from original generator, for cleanup)
        public const string OldActorDataFactoryPrefix = "DataActorDataFactory";


        // Menu Items
        [MenuItem("EasyCS/Generate (Reflection)/Regenerate All Generated Scripts", false, 10)]
        public static void RegenerateAll()
        {
            Debug.Log("[EasyCS] Starting full regeneration (Reflection)...");
            EnsureGeneratedFolders();
            GenerateAndCleanup();
            HandleAsmdefGeneration();
            AssetDatabase.Refresh();
            Debug.Log("[EasyCS] Full regeneration (Reflection) complete.");
        }

        [MenuItem("EasyCS/Generate (Reflection)/Generate Missing Scripts", false, 11)]
        public static void GenerateMissing()
        {
            Debug.Log("[EasyCS] Starting generation of missing scripts (Reflection)...");
            EnsureGeneratedFolders();
            GenerateAndCleanup(generateOnlyMissing: true);
            HandleAsmdefGeneration();
            AssetDatabase.Refresh();
            Debug.Log("[EasyCS] Generation of missing scripts (Reflection) complete.");
        }

        [MenuItem("EasyCS/Generate (Reflection)/Cleanup Obsolete Scripts", false, 12)]
        public static void CleanupObsolete()
        {
            Debug.Log("[EasyCS] Starting cleanup of obsolete scripts (Reflection)...");
            EnsureGeneratedFolders();
            GenerateAndCleanup(cleanupOnly: true);
            HandleAsmdefGeneration();
            AssetDatabase.Refresh();
            Debug.Log("[EasyCS] Cleanup of obsolete scripts (Reflection) complete.");
        }

        /// <summary>
        /// Orchestrates the process of generating new files, updating existing ones,
        /// and cleaning up obsolete or misplaced generated files.
        /// </summary>
        private static void GenerateAndCleanup(bool generateOnlyMissing = false, bool cleanupOnly = false)
        {
            Debug.Log("[EasyCS] Starting combined generation and cleanup...");

            // Get current valid user-defined base types using reflection
            var entityDataTypes = GetAllTypesDerivedFrom(typeof(IEntityData)).ToList();
            var entityBehaviorTypes = GetAllTypesDerivedFrom(typeof(IEntityBehavior)).ToList();
            var actorDataSharedTypes = GetAllTypesDerivedFrom(typeof(ActorDataSharedBase)).ToList();

            // Filter out EntityData types marked with RuntimeOnlyAttribute
            entityDataTypes = entityDataTypes.Where(t => t.GetCustomAttribute<EasyCS.RuntimeOnlyAttribute>() == null).ToList();

            // Build a list of all expected generated files with their content and expected path
            var expectedFiles = new List<ExpectedGeneratedFileInfo>();

            // Populate expected files for Entity Data
            foreach (var entityType in entityDataTypes)
            {
                string baseName = entityType.Name.Replace(EntityDataPrefix, "");
                string factoryName = $"{EntityDataFactoryPrefix}{baseName}";
                string providerName = $"{EntityDataProviderPrefix}{baseName}";
                string namespaceName = GetNamespaceOf(entityType) ?? "EasyCS.Generated.EntityData";

                expectedFiles.Add(new ExpectedGeneratedFileInfo
                {
                    ExpectedPath = Path.Combine(EntityDataFactoriesFolder, factoryName + ".cs").Replace("\\", "/"),
                    ExpectedContent = GenerateEntityDataFactoryContent(factoryName, entityType.Name, namespaceName),
                    InferredBaseName = baseName,
                    GeneratedFilePrefix = EntityDataFactoryPrefix
                });

                expectedFiles.Add(new ExpectedGeneratedFileInfo
                {
                    ExpectedPath = Path.Combine(EntityDataProvidersFolder, providerName + ".cs").Replace("\\", "/"),
                    ExpectedContent = GenerateEntityDataProviderContent(providerName, entityType.Name, namespaceName),
                    InferredBaseName = baseName,
                    GeneratedFilePrefix = EntityDataProviderPrefix
                });
            }

            // Populate expected files for Entity Behavior
            foreach (var behaviorType in entityBehaviorTypes)
            {
                string baseName = behaviorType.Name.Replace(EntityBehaviorPrefix, "");
                string providerName = $"{EntityBehaviorProviderPrefix}{baseName}";
                string namespaceName = GetNamespaceOf(behaviorType) ?? "EasyCS.Generated.EntityBehaviors";

                expectedFiles.Add(new ExpectedGeneratedFileInfo
                {
                    ExpectedPath = Path.Combine(EntityBehaviorProvidersFolder, providerName + ".cs").Replace("\\", "/"),
                    ExpectedContent = GenerateEntityBehaviorProviderContent(providerName, behaviorType.Name, namespaceName),
                    InferredBaseName = baseName,
                    GeneratedFilePrefix = EntityBehaviorProviderPrefix
                });
            }

            // Populate expected files for Actor Data Shared
            foreach (var actorDataType in actorDataSharedTypes)
            {
                string baseName = actorDataType.Name.Replace(ActorDataSharedPrefix, "");
                string factoryName = $"{ActorDataSharedFactoryPrefix}{baseName}";
                string providerName = $"{ActorDataSharedProviderPrefix}{baseName}";
                string namespaceName = GetNamespaceOf(actorDataType) ?? "EasyCS.Generated.ActorDataShared";

                expectedFiles.Add(new ExpectedGeneratedFileInfo
                {
                    ExpectedPath = Path.Combine(ActorDataSharedFactoriesFolder, factoryName + ".cs").Replace("\\", "/"),
                    ExpectedContent = GenerateActorDataSharedFactoryContent(factoryName, actorDataType.Name, namespaceName),
                    InferredBaseName = baseName,
                    GeneratedFilePrefix = ActorDataSharedFactoryPrefix
                });

                expectedFiles.Add(new ExpectedGeneratedFileInfo
                {
                    ExpectedPath = Path.Combine(ActorDataSharedProvidersFolder, providerName + ".cs").Replace("\\", "/"),
                    ExpectedContent = GenerateActorDataSharedProviderContent(providerName, actorDataType.Name, factoryName, namespaceName),
                    InferredBaseName = baseName,
                    GeneratedFilePrefix = ActorDataSharedProviderPrefix
                });
            }

            // Get all existing .cs files in the generated root folder and its subdirectories
            string[] existingGeneratedAssetPaths = Directory.GetFiles(GeneratedRootFolder, "*.cs", SearchOption.AllDirectories)
                                                    .Select(p => p.Replace("\\", "/"))
                                                    .ToArray();

            // Create a list of existing file information
            var existingFiles = new List<ExistingGeneratedFileInfo>();
            foreach (var existingPath in existingGeneratedAssetPaths)
            {
                var fileInfo = TryInferGeneratedFileInfo(existingPath);
                if (fileInfo != null)
                {
                    existingFiles.Add(fileInfo);
                }
                else
                {
                    // Log files in the generated folder that we don't recognize
                    if (!existingPath.Contains("EasyCS Generated/EasyCSCodeGeneratorReflection.cs"))
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
                // Find the corresponding expected file info based on inferred base name and prefix
                var correspondingExpectedFile = expectedFiles.FirstOrDefault(ef =>
                    ef.InferredBaseName == existingFile.InferredBaseName &&
                    // Handle old ActorDataFactory mapping to new ActorDataSharedFactory for compatibility
                    ((existingFile.GeneratedFilePrefix == OldActorDataFactoryPrefix && ef.GeneratedFilePrefix == ActorDataSharedFactoryPrefix) ||
                     (existingFile.GeneratedFilePrefix != OldActorDataFactoryPrefix && existingFile.GeneratedFilePrefix == ef.GeneratedFilePrefix))
                );

                if (correspondingExpectedFile != null)
                {
                    string currentAssetPath = existingFile.CurrentPath;
                    string targetAssetPath = correspondingExpectedFile.ExpectedPath;

                    // Determine if the file needs to be moved to a new location
                    bool needsMove = !currentAssetPath.Equals(targetAssetPath, StringComparison.OrdinalIgnoreCase);

                    if (needsMove)
                    {
                        bool targetFileExists = File.Exists(targetAssetPath);

                        if (targetFileExists)
                        {
                            string currentFileGUID = AssetDatabase.AssetPathToGUID(currentAssetPath);
                            string targetFileGUID = AssetDatabase.AssetPathToGUID(targetAssetPath);

                            // Check if the asset at the target path is actually the same asset
                            if (currentFileGUID == targetFileGUID)
                            {
                                Debug.Log($"[EasyCS] Asset '{Path.GetFileName(currentAssetPath)}' already exists at target path '{targetAssetPath}' with matching GUID. Skipping move.");
                                // Update the path in the existingFiles list to reflect its canonical location
                                existingFile.CurrentPath = targetAssetPath;
                                // Mark as successfully processed
                                successfullyProcessedExistingPaths.Add(targetAssetPath);
                                // Use the new path for subsequent content update
                                currentAssetPath = targetAssetPath;
                            }
                            else
                            {
                                // A different file exists at the target path, indicating a conflict. Delete it.
                                Debug.LogWarning($"[EasyCS] Conflict detected: A different file exists at target path '{targetAssetPath}'. Deleting conflicting file before move.");
                                AssetDatabase.DeleteAsset(targetAssetPath);
                                // Force save and refresh to help Unity process the deletion immediately
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                                // After deleting, the target path is now clear, proceed with move attempt below.
                            }
                        }

                        // Attempt the move if it's still needed after potential conflict resolution
                        if (!currentAssetPath.Equals(targetAssetPath, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"[EasyCS] Moving/Renaming file: {currentAssetPath} -> {targetAssetPath}");
                            string moveError = AssetDatabase.MoveAsset(currentAssetPath, targetAssetPath);

                            // Check if the move operation failed
                            if (!string.IsNullOrEmpty(moveError))
                            {
                                Debug.LogError($"[EasyCS] Failed to move asset from {currentAssetPath} to {targetAssetPath}. Error: {moveError}. This might leave a broken file or orphaned meta file. Skipping content update for this file.");
                                // Do NOT add to successfullyProcessedExistingPaths if move failed, as it wasn't moved
                                continue; // Skip content update for this file if move failed
                            }
                            // Update the path in the existingFiles list if the move was successful
                            existingFile.CurrentPath = targetAssetPath;
                            // Mark as successfully processed
                            successfullyProcessedExistingPaths.Add(targetAssetPath);
                            // Use the new path for subsequent content update
                            currentAssetPath = targetAssetPath;
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
                    // Only update if the file was successfully processed (moved or already in place)
                    if (!cleanupOnly && successfullyProcessedExistingPaths.Contains(currentAssetPath))
                    {
                        try
                        {
                            string existingContent = File.ReadAllText(currentAssetPath);
                            // Only write if content has actually changed to avoid unnecessary asset refreshes
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
            // This phase ensures all expected files exist in their correct location.
            if (!cleanupOnly)
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
                            // Create directory if it doesn't exist
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
            // This phase deletes any generated files that are no longer expected.
            if (!generateOnlyMissing)
            {
                foreach (var existingFile in existingFiles)
                {
                    // If an existing file was not successfully processed (moved or updated) in Phase 1, it's obsolete.
                    if (!successfullyProcessedExistingPaths.Contains(existingFile.CurrentPath))
                    {
                        Debug.Log($"[EasyCS] Deleting obsolete generated file: {existingFile.CurrentPath}");
                        AssetDatabase.DeleteAsset(existingFile.CurrentPath);
                    }
                }
            }

            // Clean up old directories and the old asmdef file (always attempt cleanup)
            CleanupEmptyDirectories(GeneratedRootFolder);
            if (File.Exists(OldEntityDataAsmdefPath))
            {
                Debug.Log($"[EasyCS] Deleting old asmdef: {OldEntityDataAsmdefPath}");
                AssetDatabase.DeleteAsset(OldEntityDataAsmdefPath);
            }

            Debug.Log("[EasyCS] Combined generation and cleanup complete.");
        }

        /// <summary>
        /// Generates the C# content for an EntityDataFactory class.
        /// </summary>
        private static string GenerateEntityDataFactoryContent(string factoryName, string entityDataTypeName, string namespaceName)
        {
            return string.Format(
@"using EasyCS;
using EasyCS.EntityFactorySystem;
using UnityEngine;
using TriInspector;

namespace {2}
{{
    [DrawWithTriInspector]
    [CreateAssetMenu(fileName = ""{0}"", menuName = ""EasyCS/Entity Data Factories/{1}"")]
    public partial class {0} : EntityDataFactory<{1}>
    {{
    }}
}}", factoryName, entityDataTypeName, namespaceName);
        }

        /// <summary>
        /// Generates the C# content for an EntityDataProvider class.
        /// </summary>
        private static string GenerateEntityDataProviderContent(string providerName, string entityDataTypeName, string namespaceName)
        {
            string baseName = entityDataTypeName.Replace(EntityDataPrefix, "");
            string displayBaseName = AddSpacesToSentence(baseName);
            // The factory type name should be constructed from the EntityDataFactoryPrefix and the baseName, not the full entityDataTypeName.
            string factoryGenericArgName = $"{EntityDataFactoryPrefix}{baseName}";

            return string.Format(
@"using EasyCS;
using EasyCS.EntityFactorySystem;
using UnityEngine;
using TriInspector;

namespace {3}
{{
    [DrawWithTriInspector]
    [AddComponentMenu(""EasyCS/Entity/Data/Data {1}"")]
    public partial class {0} : EntityDataProvider<{4}, {2}>
    {{
    }}
}}", providerName, displayBaseName, entityDataTypeName, namespaceName, factoryGenericArgName);
        }

        /// <summary>
        /// Generates the C# content for an EntityBehaviorProvider class.
        /// </summary>
        private static string GenerateEntityBehaviorProviderContent(string providerName, string behaviorTypeName, string namespaceName)
        {
            string baseName = behaviorTypeName.Replace(EntityBehaviorPrefix, "");
            string displayBaseName = AddSpacesToSentence(baseName);
            return string.Format(
        @"using EasyCS;
using EasyCS.EntityFactorySystem;
using UnityEngine;
using TriInspector;

namespace {2}
{{
    [DrawWithTriInspector]
    [AddComponentMenu(""EasyCS/Entity/Behavior/Behavior {1}"")]
    public partial class {0} : EntityBehaviorProvider<{3}>
    {{
    }}
}}", providerName, displayBaseName, namespaceName, behaviorTypeName);
        }

        /// <summary>
        /// Generates the C# content for an ActorDataSharedFactory class.
        /// </summary>
        private static string GenerateActorDataSharedFactoryContent(string factoryName, string actorDataTypeName, string namespaceName)
        {
            return string.Format(
@"using EasyCS;
using UnityEngine;
using TriInspector;

namespace {2}
{{
    [DrawWithTriInspector]
    [CreateAssetMenu(fileName = ""{0}"", menuName = ""EasyCS/Actor Data Shared Factories/{1}"")]
    public partial class {0} : ActorDataSharedFactory<{1}>
    {{
    }}
}}", factoryName, actorDataTypeName, namespaceName);
        }

        /// <summary>
        /// Generates the C# content for an ActorDataSharedProvider class.
        /// </summary>
        private static string GenerateActorDataSharedProviderContent(string providerName, string actorDataTypeName, string factoryName, string namespaceName)
        {
            string baseName = actorDataTypeName.Replace(ActorDataSharedPrefix, "");
            string displayBaseName = AddSpacesToSentence(baseName);
            // The factory type name should be passed as factoryName which already has the correct format like "ActorDataSharedFactoryProjectilePrefab"
            return string.Format(
@"using EasyCS;
using UnityEngine;
using TriInspector;

namespace {3}
{{
    [DrawWithTriInspector]
    [AddComponentMenu(""EasyCS/Actor/Data/Data {1}"")]
    public partial class {0} : ActorDataSharedProviderBase<{4}, {2}>
    {{
    }}
}}", providerName, displayBaseName, factoryName, namespaceName, actorDataTypeName);
        }

        /// <summary>
        /// Handles the conditional generation or deletion of the EasyCS.Generated.asmdef file.
        /// The .asmdef is skipped if user-defined types are found in 'Assembly-CSharp.dll'.
        /// </summary>
        private static void HandleAsmdefGeneration()
        {
            if (ShouldGenerateAsmdef())
            {
                GenerateAsmdefFile();
            }
            else
            {
                Debug.LogWarning("[EasyCS] Skipping EasyCS.Generated.asmdef generation due to unwrapped dependencies in Assembly-CSharp. Deleting existing asmdef if found.");
                if (File.Exists(AsmdefPath))
                {
                    AssetDatabase.DeleteAsset(AsmdefPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
        }

        /// <summary>
        /// Generates the EasyCS.Generated.asmdef file with necessary references.
        /// </summary>
        private static void GenerateAsmdefFile()
        {
            Debug.Log("[EasyCS] Generating Assembly Definition...");
            EnsureGeneratedFolders();

            var referencedAssemblies = new HashSet<string> { "EasyCS.Runtime", "TriInspector" };

            var baseTypes = new Type[] {
                typeof(IEntityData),
                typeof(IEntityBehavior),
                typeof(ActorDataSharedBase)
            }.Where(t => t != null).ToList();

            foreach (var baseType in baseTypes)
            {
                var derivedTypes = GetAllTypesDerivedFrom(baseType);
                foreach (var type in derivedTypes)
                {
                    if (type?.Assembly != null)
                    {
                        referencedAssemblies.Add(type.Assembly.GetName().Name);
                    }
                }
                if (baseType?.Assembly != null)
                {
                    referencedAssemblies.Add(baseType.Assembly.GetName().Name);
                }
            }

            var genericBaseTypes = new Type[] {
                typeof(EntityDataFactory<>),
                typeof(EntityDataProvider<,>),
                typeof(EntityBehaviorProvider<>),
                typeof(ActorDataSharedFactory<>),
                typeof(ActorDataSharedProviderBase<,>)
            }.Where(t => t != null).ToList();

            foreach (var genericBaseType in genericBaseTypes)
            {
                if (genericBaseType?.Assembly != null)
                {
                    referencedAssemblies.Add(genericBaseType.Assembly.GetName().Name);
                }
            }

            string[] references = referencedAssemblies
                .Distinct()
                .OrderBy(r => r)
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

        /// <summary>
        /// Determines if the EasyCS.Generated.asmdef file should be generated.
        /// It returns false if any user-defined types (IEntityData, IEntityBehavior, ActorDataSharedBase)
        /// are found in the default 'Assembly-CSharp.dll', indicating unwrapped dependencies.
        /// </summary>
        private static bool ShouldGenerateAsmdef()
        {
            var userDefinedTypes = new List<Type>();
            userDefinedTypes.AddRange(GetAllTypesDerivedFrom(typeof(IEntityData)));
            userDefinedTypes.AddRange(GetAllTypesDerivedFrom(typeof(IEntityBehavior)));
            userDefinedTypes.AddRange(GetAllTypesDerivedFrom(typeof(ActorDataSharedBase)));

            foreach (var type in userDefinedTypes)
            {
                if (type?.Assembly != null && type.Assembly.GetName().Name == "Assembly-CSharp")
                {
                    Debug.Log($"[EasyCS] Detected user-defined type '{type.FullName}' in 'Assembly-CSharp'. Skipping .asmdef generation.");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Ensures all necessary generated folders exist in the Unity project.
        /// </summary>
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
            AssetDatabase.Refresh();
            Debug.Log("[EasyCS] Generated folders ensured.");
        }

        /// <summary>
        /// Writes content to a file, overwriting it only if the content has changed or the file doesn't exist.
        /// </summary>
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

        /// <summary>
        /// Gets the namespace of a given Type, returns null if the type is null or has no namespace.
        /// </summary>
        private static string GetNamespaceOf(Type type)
        {
            if (type == null || string.IsNullOrEmpty(type.Namespace))
            {
                return null;
            }
            return type.Namespace;
        }

        /// <summary>
        /// Determines the expected subfolder path for a generated file based on its prefix.
        /// </summary>
        private static string GetExpectedSubfolderForPrefix(string prefix)
        {
            if (prefix.StartsWith(EntityDataFactoryPrefix)) return EntityDataFactoriesFolder;
            if (prefix.StartsWith(EntityDataProviderPrefix)) return EntityDataProvidersFolder;
            if (prefix.StartsWith(EntityBehaviorProviderPrefix)) return EntityBehaviorProvidersFolder;
            if (prefix.StartsWith(ActorDataSharedFactoryPrefix)) return ActorDataSharedFactoriesFolder;
            if (prefix.StartsWith(ActorDataSharedProviderPrefix)) return ActorDataSharedProvidersFolder;
            return GeneratedRootFolder;
        }

        /// <summary>
        /// Attempts to infer information about an existing generated file (base name, prefix, etc.)
        /// based on its asset path and naming conventions.
        /// </summary>
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
            else if (fileNameWithoutExtension.StartsWith(OldActorDataFactoryPrefix))
            {
                inferredBaseName = fileNameWithoutExtension.Replace(OldActorDataFactoryPrefix, "");
                generatedFilePrefix = OldActorDataFactoryPrefix;
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

            return null;
        }

        /// <summary>
        /// Retrieves all concrete, non-abstract types derived from a specified base type (class or interface)
        /// across all loaded assemblies in the current AppDomain.
        /// Handles both direct class inheritance and interface implementation, as well as raw generic type definitions.
        /// </summary>
        private static List<Type> GetAllTypesDerivedFrom(Type baseType)
        {
            if (baseType == null) return new List<Type>();

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try { return assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException ex)
                    {
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
                            ? baseType.IsAssignableFrom(t)
                            : (t.IsSubclassOf(baseType) || (baseType.IsGenericTypeDefinition && InheritsFromRawGeneric(t, baseType)))
                    )
                ).ToList();
        }

        /// <summary>
        /// Checks if a given type inherits from a raw generic type definition (e.g., `List<>` or `EntityDataFactory<>`).
        /// </summary>
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

        /// <summary>
        /// Detects the class name from a C# file content using a regex.
        /// This method is not directly used in the main generation logic for path determination,
        /// as file naming conventions are assumed.
        /// </summary>
        private static string DetectClassName(string content)
        {
            var match = Regex.Match(content, @"\b(public|private|protected|internal|\s+)\s+class\s+(\w+)\b");
            if (match.Success && match.Groups.Count > 2)
            {
                return match.Groups[2].Value;
            }
            return null;
        }

        /// <summary>
        /// Checks if a file name matches one of the expected patterns for generated EasyCS files.
        /// </summary>
        private static bool IsExpectedGeneratedFileName(string fileName)
        {
            return fileName.StartsWith(EntityDataFactoryPrefix) ||
                   fileName.StartsWith(EntityDataProviderPrefix) ||
                   fileName.StartsWith(EntityBehaviorProviderPrefix) ||
                   fileName.StartsWith(ActorDataSharedFactoryPrefix) ||
                   fileName.StartsWith(ActorDataSharedProviderPrefix) ||
                   fileName.StartsWith(OldActorDataFactoryPrefix);
        }

        /// <summary>
        /// Recursively cleans up empty directories starting from a specified path.
        /// It deletes empty directories and their .meta files.
        /// </summary>
        private static void CleanupEmptyDirectories(string startDirectory)
        {
            if (!Directory.Exists(startDirectory))
                return;

            foreach (var directory in Directory.GetDirectories(startDirectory))
            {
                CleanupEmptyDirectories(directory);
                if (!Directory.GetFiles(directory).Any() && !Directory.GetDirectories(directory).Any())
                {
                    Debug.Log($"[EasyCS] Deleting empty directory: {directory}");
                    AssetDatabase.DeleteAsset(directory.Replace("\\", "/"));
                }
            }
        }

        /// <summary>
        /// Converts a camel case string to a sentence-cased string by adding spaces.
        /// E.g., "HealthMax" becomes "Health Max".
        /// </summary>
        private static string AddSpacesToSentence(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            return Regex.Replace(text, "(?<!^)([A-Z])", " $1").Trim();
        }

        // Helper class to hold information about an expected generated file
        private class ExpectedGeneratedFileInfo
        {
            public string ExpectedPath { get; set; }
            public string ExpectedContent { get; set; }
            public string InferredBaseName { get; set; }
            public string GeneratedFilePrefix { get; set; }
        }

        // Helper class to hold information about an existing generated file
        private class ExistingGeneratedFileInfo
        {
            public string CurrentPath { get; set; }
            public string CurrentFileName { get; set; }
            public string InferredBaseName { get; set; }
            public string GeneratedFilePrefix { get; set; }
        }
    }
}
