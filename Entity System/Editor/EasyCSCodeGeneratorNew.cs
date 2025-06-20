﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using System.Text.RegularExpressions;

namespace EasyCS.Editor
{
    public static class EasyCSCodeGeneratorReflection
    {
        // --- Static Flags for Automation and Reload Management ---
        private static bool IsProcessingGeneration { get; set; } = false;
        private static bool DidGenerateFilesThisCycle
        {
            get; set;
        }
            = false;
        private static bool IsBlockingReloads { get; set; } = false;

        private const string AutoGeneratePrefsKey = "EasyCS.AutoGenerateEnabled";
        private static bool EnableAutoGeneration
        {
            get => EditorPrefs.GetBool(AutoGeneratePrefsKey, true);
            set => EditorPrefs.SetBool(AutoGeneratePrefsKey, value);
        }


        // Initialize method called by Unity after scripts are loaded in the editor.
        // This is a more robust way to subscribe to editor events than a static constructor.
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            Debug.Log("[EasyCS] EasyCSCodeGeneratorReflection initialized and subscribed to afterAssemblyReload.");
        }

        /// <summary>
        /// This method is called by Unity after all assemblies have been reloaded and the new AppDomain is active.
        /// It orchestrates the automatic generation and cleanup process.
        /// </summary>
        private static void OnAfterAssemblyReload()
        {
            // Only proceed if auto-generation is enabled via the menu flag.
            if (!EnableAutoGeneration)
            {
                Debug.Log("[EasyCS] Automatic code generation is disabled by user settings. Skipping run.");
                return;
            }

            // If already processing, return to prevent re-entry.
            if (IsProcessingGeneration)
            {
                Debug.Log("[EasyCS] Already processing generation, skipping redundant call from OnAfterAssemblyReload.");
                return;
            }

            IsProcessingGeneration = true;
            DidGenerateFilesThisCycle = false;

            // Lock assembly reloads to prevent Unity from immediately recompiling
            // if our generation process creates new files. This delays the second compilation.
            if (!IsBlockingReloads)
            {
                EditorApplication.LockReloadAssemblies();
                IsBlockingReloads = true;
                Debug.Log("[EasyCS] Assembly reloads locked for automatic generation check (afterAssemblyReload).");
            }

            try
            {
                // At this point, AppDomain.CurrentDomain reflects the newly loaded assemblies.
                // We can directly use them for reflection.
                IEnumerable<System.Reflection.Assembly> loadedSystemAssemblies = AppDomain.CurrentDomain.GetAssemblies();

                // Execute the full regeneration process, passing the freshly loaded System.Reflection.Assembly objects.
                // This will set DidGenerateFilesThisCycle to true if any changes are made.
                RegenerateAll(loadedSystemAssemblies);
            }
            finally
            {
                IsProcessingGeneration = false;

                // If files were generated during this run, Unity has a pending compilation.
                // Unlock assemblies to allow Unity to perform the next compilation.
                if (DidGenerateFilesThisCycle)
                {
                    Debug.Log("[EasyCS] Files generated. Unlocking assembly reloads to trigger next compilation.");
                    // Unity will automatically trigger a recompile because asset changes were made.
                    EditorApplication.UnlockReloadAssemblies();
                    IsBlockingReloads = false;
                }
                else if (IsBlockingReloads)
                {
                    // If no files were generated, but we locked, unlock assemblies.
                    // This means no further compilation is needed from our side for this cycle.
                    Debug.Log("[EasyCS] No files generated. Unlocking assembly reloads. No further compilation needed.");
                    EditorApplication.UnlockReloadAssemblies();
                    IsBlockingReloads = false;
                }
            }
        }


        // --- Core Generated Folders ---
        public const string GeneratedRootFolder = "Assets/EasyCS Generated";

        public const string PartialsRootFolder = GeneratedRootFolder + "/Partials";

        // Subfolders for different generated types (directly under GeneratedRootFolder)
        public const string EntityDataProvidersFolder = GeneratedRootFolder + "/Entity Data Providers";
        public const string ActorDataSharedProvidersFolder = GeneratedRootFolder + "/Actor Data Shared Providers";
        public const string EntityBehaviorProvidersFolder = GeneratedRootFolder + "/Entity Behavior Providers";
        public const string EntityDataFactoriesFolder = GeneratedRootFolder + "/Entity Data Factories";
        public const string ActorDataSharedFactoriesFolder = GeneratedRootFolder + "/Actor Data Shared Factories";

        // List of all top-level subfolders for easier iteration when ensuring existence
        // Note: Assembly-specific folders under PartialsRootFolder are created dynamically.
        private static readonly string[] TopLevelGeneratedSubfolders = {
            PartialsRootFolder,
            EntityDataProvidersFolder,
            ActorDataSharedProvidersFolder,
            EntityBehaviorProvidersFolder,
            EntityDataFactoriesFolder,
            ActorDataSharedFactoriesFolder
        };

        // --- Old/Obsolete Folders for Cleanup ---
        private const string OldEntityDataFolder = GeneratedRootFolder + "/EntityData";
        private const string OldActorDataFolder = GeneratedRootFolder + "/ActorData";
        private static readonly string[] OldGeneratedFolders = {
            OldEntityDataFolder,
            OldActorDataFolder
        };

        // --- Assembly Definition Details ---
        public const string AsmdefPath = GeneratedRootFolder + "/EasyCS.Generated.asmdef";
        private const string OldEntityDataAsmdefPath = OldEntityDataFolder + "/EntityData.Generated.asmdef";
        public const string AsmdefName = "EasyCS.Generated";

        // --- Type Prefix/Suffix Constants ---
        public const string EntityDataPrefix = "EntityData";
        public const string EntityBehaviorPrefix = "EntityBehavior";
        public const string ActorDataSharedPrefix = "ActorDataShared";
        public const string ActorDataPlainPrefix = "ActorData";
        public const string ActorBehaviorPlainPrefix = "ActorBehavior";

        public const string FactorySuffix = "Factory";
        public const string ProviderSuffix = "Provider";

        public const string EntityDataFactoryPrefix = EntityDataPrefix + FactorySuffix;
        public const string EntityDataProviderPrefix = EntityDataPrefix + ProviderSuffix;
        public const string EntityBehaviorProviderPrefix = EntityBehaviorPrefix + ProviderSuffix;
        public const string ActorDataSharedFactoryPrefix = ActorDataSharedPrefix + FactorySuffix;
        public const string ActorDataSharedProviderPrefix = ActorDataSharedPrefix + ProviderSuffix;

        public const string OldActorDataFactoryPrefix = "DataActorDataFactory";

        public const string GeneratedPartialPrefix = "Partial";

        private static readonly string[] FoldersWithSpecificAsmref = {
            EntityDataProvidersFolder,
            ActorDataSharedProvidersFolder,
            EntityBehaviorProvidersFolder,
            EntityDataFactoriesFolder,
            ActorDataSharedFactoriesFolder
        };


        // --- Unity Menu Items ---
        [MenuItem("EasyCS/Generate (Reflection)/Regenerate All Generated Scripts", false, 10)]
        public static void RegenerateAll()
        {
            RegenerateAll(AppDomain.CurrentDomain.GetAssemblies());
        }

        [MenuItem("EasyCS/Generate (Reflection)/Generate Missing Scripts", false, 11)]
        public static void GenerateMissing()
        {
            Debug.Log("[EasyCS] Starting generation of missing scripts (Reflection)...");
            EnsureGeneratedFolders();
            GenerateAndCleanup(generateOnlyMissing: true, assembliesToScan: AppDomain.CurrentDomain.GetAssemblies());
            HandlePartialClassGenerationAndCleanup(generateOnlyMissing: true, assembliesToScan: AppDomain.CurrentDomain.GetAssemblies());
            HandleAsmdefGeneration();
            HandleAsmrefGenerationForProvidersAndFactories();
            AssetDatabase.Refresh();
            Debug.Log("[EasyCS] Generation of missing scripts (Reflection) complete.");
        }

        [MenuItem("EasyCS/Generate (Reflection)/Cleanup Obsolete Scripts", false, 12)]
        public static void CleanupObsolete()
        {
            Debug.Log("[EasyCS] Starting cleanup of obsolete scripts (Reflection)...");
            EnsureGeneratedFolders();
            GenerateAndCleanup(cleanupOnly: true, assembliesToScan: AppDomain.CurrentDomain.GetAssemblies());
            HandlePartialClassGenerationAndCleanup(cleanupOnly: true, assembliesToScan: AppDomain.CurrentDomain.GetAssemblies());
            HandleAsmdefGeneration();
            HandleAsmrefGenerationForProvidersAndFactories();
            AssetDatabase.Refresh();
            Debug.Log("[EasyCS] Cleanup of obsolete scripts (Reflection) complete.");
        }

        // New menu item to toggle auto-generation
        [MenuItem("EasyCS/Generate (Reflection)/Toggle Auto-Generation", false, 0)]
        private static void ToggleAutoGeneration()
        {
            EnableAutoGeneration = !EnableAutoGeneration;
            Debug.Log($"[EasyCS] Automatic code generation {(EnableAutoGeneration ? "ENABLED" : "DISABLED")}.");
        }

        // Validation method to show checkmark in the menu
        [MenuItem("EasyCS/Generate (Reflection)/Toggle Auto-Generation", true)]
        private static bool ToggleAutoGenerationValidate()
        {
            Menu.SetChecked("EasyCS/Generate (Reflection)/Toggle Auto-Generation", EnableAutoGeneration);
            return true;
        }

        /// <summary>
        /// Main entry point for the regeneration logic.
        /// </summary>
        /// <param name="assembliesToScan">Optional collection of System.Reflection.Assembly to scan for types. If null, AppDomain.CurrentDomain.GetAssemblies() is used.</param>
        public static void RegenerateAll(IEnumerable<System.Reflection.Assembly> assembliesToScan = null)
        {
            Debug.Log("[EasyCS] Starting full regeneration (Reflection)...");
            EnsureGeneratedFolders();
            GenerateAndCleanup(assembliesToScan: assembliesToScan);
            HandlePartialClassGenerationAndCleanup(assembliesToScan: assembliesToScan);
            HandleAsmdefGeneration();
            HandleAsmrefGenerationForProvidersAndFactories();
            AssetDatabase.Refresh();
            Debug.Log("[EasyCS] Full regeneration (Reflection) complete.");
        }


        /// <summary>
        /// Orchestrates the process of generating new files, updating existing ones,
        /// and cleaning up obsolete or misplaced generated files for Factories and Providers.
        /// </summary>
        private static void GenerateAndCleanup(bool generateOnlyMissing = false, bool cleanupOnly = false, IEnumerable<System.Reflection.Assembly> assembliesToScan = null)
        {
            Debug.Log("[EasyCS] Starting combined generation and cleanup for Factories and Providers...");

            // Get current valid user-defined base types using reflection
            var entityDataTypes = GetAllTypesDerivedFrom(typeof(IEntityData), assembliesToScan).ToList();
            var entityBehaviorTypes = GetAllTypesDerivedFrom(typeof(IEntityBehavior), assembliesToScan).ToList();
            var actorDataSharedTypes = GetAllTypesDerivedFrom(typeof(ActorDataSharedBase), assembliesToScan).ToList();

            // Filter out EntityData types marked with RuntimeOnlyAttribute
            // (Assuming RuntimeOnlyAttribute is defined elsewhere and accessible, e.g., in EasyCS.Runtime)
            entityDataTypes = entityDataTypes.Where(t => t.GetCustomAttribute<EasyCS.RuntimeOnlyAttribute>() == null).ToList();

            // Build a list of all expected generated files with their content and expected path
            var expectedFiles = new List<ExpectedGeneratedFileInfo>();

            // Populate expected files for Entity Data Factories and Providers
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

            // Populate expected files for Entity Behavior Providers
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

            // Populate expected files for Actor Data Shared Factories and Providers
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

            // Get all existing .cs files in the GeneratedRootFolder and its subdirectories (excluding Partials)
            // We need to exclude PartialsRootFolder from this scan for Factories/Providers
            string[] existingGeneratedAssetPaths = Directory.GetFiles(GeneratedRootFolder, "*.cs", SearchOption.AllDirectories)
                                                    .Where(p => !p.StartsWith(PartialsRootFolder + "/", StringComparison.OrdinalIgnoreCase))
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
                    // Ensure the generator script itself is not flagged
                    if (!existingPath.EndsWith("EasyCSCodeGeneratorReflection.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"Found unrecognized file in generated folder: {existingPath}. It will be considered for deletion if cleanup is enabled.");
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
                                Debug.Log($"Asset '{Path.GetFileName(currentAssetPath)}' already exists at target path '{targetAssetPath}' with matching GUID. Skipping move.");
                                // Update the path in the existingFiles list to reflect its canonical location
                                existingFile.CurrentPath = targetAssetPath;
                                // Mark as successfully processed
                                successfullyProcessedExistingPaths.Add(targetAssetPath);
                                // Use the new path for subsequent content update
                                currentAssetPath = targetAssetPath;
                            }
                            else
                            {
                                Debug.LogWarning($"Conflict detected: A different file exists at target path '{targetAssetPath}'. Deleting conflicting file before move.");
                                AssetDatabase.DeleteAsset(targetAssetPath);
                                DidGenerateFilesThisCycle = true;
                                // Force save and refresh to help Unity process the deletion immediately
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                                // After deleting, the target path is now clear, proceed with move attempt below.
                            }
                        }

                        // Attempt the move if it's still needed after potential conflict resolution
                        if (!currentAssetPath.Equals(targetAssetPath, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"Moving/Renaming file: {currentAssetPath} -> {targetAssetPath}");
                            string moveError = AssetDatabase.MoveAsset(currentAssetPath, targetAssetPath);

                            // Check if the move operation failed
                            if (!string.IsNullOrEmpty(moveError))
                            {
                                Debug.LogError($"Failed to move asset from {currentAssetPath} to {targetAssetPath}. Error: {moveError}. This might leave a broken file or orphaned meta file. Skipping content update for this file.");
                                // Do NOT add to successfullyProcessedExistingPaths if move failed, as it wasn't moved
                                continue;
                            }
                            DidGenerateFilesThisCycle = true;
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
                                DidGenerateFilesThisCycle = true;
                                Debug.Log($"Updated content for generated file: {Path.GetFileName(currentAssetPath)}");
                            }
                            else
                            {
                                Debug.Log($"Existing generated file content unchanged: {Path.GetFileName(currentAssetPath)}");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Error reading/writing generated file {Path.GetFileName(currentAssetPath)}: {e.Message}");
                        }
                    }
                }
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
                        try
                        {
                            string targetDirectory = Path.GetDirectoryName(expectedFile.ExpectedPath);
                            // Create directory if it doesn't exist
                            if (!Directory.Exists(targetDirectory))
                            {
                                Directory.CreateDirectory(targetDirectory);
                                DidGenerateFilesThisCycle = true;
                                // Crucial to refresh after creating directories so Unity recognizes them and creates .meta files
                                AssetDatabase.Refresh();
                            }
                            // Double-check if a file *still* exists at the target path after refreshes, just in case.
                            if (!File.Exists(expectedFile.ExpectedPath))
                            {
                                File.WriteAllText(expectedFile.ExpectedPath, expectedFile.ExpectedContent);
                                DidGenerateFilesThisCycle = true;
                                Debug.Log($"Generated missing file: {Path.GetFileName(expectedFile.ExpectedPath)}");
                            }
                            else
                            {
                                Debug.LogWarning($"Expected file '{Path.GetFileName(expectedFile.ExpectedPath)}' still exists at target path. Skipping generation to avoid overwrite issues.");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Error generating missing file {Path.GetFileName(expectedFile.ExpectedPath)}: {e.Message}");
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
                        Debug.Log($"Deleting obsolete generated file: {existingFile.CurrentPath}");
                        AssetDatabase.DeleteAsset(existingFile.CurrentPath);
                        DidGenerateFilesThisCycle = true;
                    }
                }
            }

            // Clean up old directories and the old asmdef file (always attempt cleanup)
            CleanupEmptyDirectories(GeneratedRootFolder);
            if (File.Exists(OldEntityDataAsmdefPath))
            {
                Debug.Log($"Deleting old asmdef: {OldEntityDataAsmdefPath}");
                AssetDatabase.DeleteAsset(OldEntityDataAsmdefPath);
                DidGenerateFilesThisCycle = true;
            }

            Debug.Log("[EasyCS] Combined generation and cleanup for Factories and Providers complete.");
        }


        /// <summary>
        /// Orchestrates the process of generating new partial class files for ActorData and ActorBehavior implementors,
        /// updating existing ones, and cleaning up obsolete or misplaced partial class files.
        /// </summary>
        private static void HandlePartialClassGenerationAndCleanup(bool generateOnlyMissing = false, bool cleanupOnly = false, IEnumerable<System.Reflection.Assembly> assembliesToScan = null)
        {
            Debug.Log("[EasyCS] Starting partial class generation and cleanup for ActorData and ActorBehavior...");

            // Ensure the PartialsRootFolder exists before attempting to list files within it.
            EnsureGeneratedFolders();

            // Discover user-defined ActorData and ActorBehavior types
            // Importantly, this will now only find the *original* user-defined types, not the generated partials.
            var actorDataPlainTypes = GetAllTypesDerivedFrom(typeof(ActorData), assembliesToScan)
                                        .Where(t => !t.IsSubclassOf(typeof(ActorDataSharedBase)))
                                        .ToList();
            var actorBehaviorPlainTypes = GetAllTypesDerivedFrom(typeof(ActorBehavior), assembliesToScan).ToList();

            // Store original assembly names for .asmref generation
            var originalAssemblyNames = new HashSet<string>();

            // Build a list of all expected partial class files
            var expectedPartialFiles = new List<ExpectedGeneratedPartialFileInfo>();

            // Populate expected files for ActorData partials
            foreach (var type in actorDataPlainTypes)
            {
                string partialFileName = GeneratedPartialPrefix + type.Name;
                string namespaceName = GetNamespaceOf(type);
                string originalAssemblyName = type.Assembly.GetName().Name;

                originalAssemblyNames.Add(originalAssemblyName);

                string assemblySpecificFolder = Path.Combine(PartialsRootFolder, originalAssemblyName).Replace("\\", "/");
                string actorDataFolder = Path.Combine(assemblySpecificFolder, "ActorData").Replace("\\", "/");
                string expectedPath = Path.Combine(actorDataFolder, partialFileName + ".cs").Replace("\\", "/");

                expectedPartialFiles.Add(new ExpectedGeneratedPartialFileInfo
                {
                    ExpectedPath = expectedPath,
                    ExpectedContent = GenerateActorDataPartialContent(type.Name, namespaceName),
                    OriginalAssemblyName = originalAssemblyName,
                    OriginalTypeName = type.Name
                });
            }

            // Populate expected files for ActorBehavior partials
            foreach (var type in actorBehaviorPlainTypes)
            {
                string partialFileName = GeneratedPartialPrefix + type.Name;
                string namespaceName = GetNamespaceOf(type);
                string originalAssemblyName = type.Assembly.GetName().Name;

                originalAssemblyNames.Add(originalAssemblyName);

                string assemblySpecificFolder = Path.Combine(PartialsRootFolder, originalAssemblyName).Replace("\\", "/");
                string actorBehaviorFolder = Path.Combine(assemblySpecificFolder, "ActorBehavior").Replace("\\", "/");
                string expectedPath = Path.Combine(actorBehaviorFolder, partialFileName + ".cs").Replace("\\", "/");

                expectedPartialFiles.Add(new ExpectedGeneratedPartialFileInfo
                {
                    ExpectedPath = expectedPath,
                    ExpectedContent = GenerateActorBehaviorPartialContent(type.Name, namespaceName),
                    OriginalAssemblyName = originalAssemblyName,
                    OriginalTypeName = type.Name
                });
            }

            // Get all existing .cs files in the PartialsRootFolder and its subdirectories
            string[] existingPartialAssetPaths = Directory.GetFiles(PartialsRootFolder, "*.cs", SearchOption.AllDirectories)
                                                    .Select(p => p.Replace("\\", "/"))
                                                    .ToArray();

            // Create a list of existing partial file information
            var existingPartialFiles = new List<ExistingGeneratedPartialFileInfo>();
            foreach (var existingPath in existingPartialAssetPaths)
            {
                var fileInfo = TryInferGeneratedPartialFileInfo(existingPath);
                if (fileInfo != null)
                {
                    existingPartialFiles.Add(fileInfo);
                }
            }

            // Keep track of which existing partial files have been successfully processed
            var successfullyProcessedExistingPartialPaths = new HashSet<string>();

            // PHASE 1 (Partials): Process existing partial files (move/rename/update content)
            foreach (var existingFile in existingPartialFiles)
            {
                var correspondingExpectedFile = expectedPartialFiles.FirstOrDefault(ef =>
                    ef.OriginalTypeName == existingFile.OriginalTypeName &&
                    ef.OriginalAssemblyName == existingFile.OriginalAssemblyName
                );

                if (correspondingExpectedFile != null)
                {
                    string currentAssetPath = existingFile.CurrentPath;
                    string targetAssetPath = correspondingExpectedFile.ExpectedPath;

                    bool needsMove = !currentAssetPath.Equals(targetAssetPath, StringComparison.OrdinalIgnoreCase);

                    if (needsMove)
                    {
                        if (File.Exists(targetAssetPath))
                        {
                            string currentFileGUID = AssetDatabase.AssetPathToGUID(currentAssetPath);
                            string targetFileGUID = AssetDatabase.AssetPathToGUID(targetAssetPath);

                            if (currentFileGUID == targetFileGUID)
                            {
                                Debug.Log($"Partial asset '{Path.GetFileName(currentAssetPath)}' already exists at target path '{targetAssetPath}' with matching GUID. Skipping move.");
                                existingFile.CurrentPath = targetAssetPath;
                                successfullyProcessedExistingPartialPaths.Add(targetAssetPath);
                                currentAssetPath = targetAssetPath;
                            }
                            else
                            {
                                Debug.LogWarning($"Conflict detected for partial: A different file exists at target path '{targetAssetPath}'. Deleting conflicting file before move.");
                                AssetDatabase.DeleteAsset(targetAssetPath);
                                DidGenerateFilesThisCycle = true;
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                            }
                        }

                        if (!currentAssetPath.Equals(targetAssetPath, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"Moving/Renaming partial file: {currentAssetPath} -> {targetAssetPath}");
                            string moveError = AssetDatabase.MoveAsset(currentAssetPath, targetAssetPath);
                            if (!string.IsNullOrEmpty(moveError))
                            {
                                Debug.LogError($"Failed to move partial asset from {currentAssetPath} to {targetAssetPath}. Error: {moveError}. Skipping content update.");
                                continue;
                            }
                            DidGenerateFilesThisCycle = true;
                            existingFile.CurrentPath = targetAssetPath;
                            successfullyProcessedExistingPartialPaths.Add(targetAssetPath);
                            currentAssetPath = targetAssetPath;
                        }
                        else
                        {
                            successfullyProcessedExistingPartialPaths.Add(currentAssetPath);
                        }
                    }
                    else
                    {
                        successfullyProcessedExistingPartialPaths.Add(currentAssetPath);
                    }

                    if (!cleanupOnly && successfullyProcessedExistingPartialPaths.Contains(currentAssetPath))
                    {
                        try
                        {
                            string existingContent = File.ReadAllText(currentAssetPath);
                            if (existingContent != correspondingExpectedFile.ExpectedContent)
                            {
                                File.WriteAllText(currentAssetPath, correspondingExpectedFile.ExpectedContent);
                                DidGenerateFilesThisCycle = true;
                                Debug.Log($"Updated content for partial file: {Path.GetFileName(currentAssetPath)}");
                            }
                            else
                            {
                                Debug.Log($"Existing partial file content unchanged: {Path.GetFileName(currentAssetPath)}");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Error reading/writing partial file {Path.GetFileName(currentAssetPath)}: {e.Message}");
                        }
                    }
                }
            }

            // PHASE 2 (Partials): Generate any files that were NOT successfully processed
            if (!cleanupOnly)
            {
                foreach (var expectedFile in expectedPartialFiles)
                {
                    bool wasProcessed = successfullyProcessedExistingPartialPaths.Contains(expectedFile.ExpectedPath);

                    if (!wasProcessed)
                    {
                        try
                        {
                            string targetDirectory = Path.GetDirectoryName(expectedFile.ExpectedPath);
                            if (!Directory.Exists(targetDirectory))
                            {
                                Directory.CreateDirectory(targetDirectory);
                                DidGenerateFilesThisCycle = true;
                                AssetDatabase.Refresh();
                            }
                            if (!File.Exists(expectedFile.ExpectedPath))
                            {
                                File.WriteAllText(expectedFile.ExpectedPath, expectedFile.ExpectedContent);
                                DidGenerateFilesThisCycle = true;
                                Debug.Log($"Generated missing partial file: {Path.GetFileName(expectedFile.ExpectedPath)}");
                            }
                            else
                            {
                                Debug.LogWarning($"Expected partial file '{Path.GetFileName(expectedFile.ExpectedPath)}' still exists at target path. Skipping generation.");
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Error generating missing partial file {Path.GetFileName(expectedFile.ExpectedPath)}: {e.Message}");
                        }
                    }
                }
            }

            // PHASE 3 (Partials): Clean up obsolete existing partial files
            if (!generateOnlyMissing)
            {
                foreach (var existingFile in existingPartialFiles)
                {
                    if (!successfullyProcessedExistingPartialPaths.Contains(existingFile.CurrentPath))
                    {
                        Debug.Log($"Deleting obsolete partial file: {existingFile.CurrentPath}");
                        AssetDatabase.DeleteAsset(existingFile.CurrentPath);
                        DidGenerateFilesThisCycle = true;
                    }
                }
            }

            // Handle .asmref files for the generated partial folders
            HandlePartialClassAsmrefs(originalAssemblyNames);

            // NEW CLEANUP STEP: Delete any .cs or .asmref files in PartialsRootFolder not explicitly generated or expected
            if (!generateOnlyMissing)
            {
                string[] allFilesinPartials = Directory.GetFiles(PartialsRootFolder, "*.*", SearchOption.AllDirectories)
                                                        .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase))
                                                        .Select(p => p.Replace("\\", "/"))
                                                        .ToArray();

                var allExpectedPaths = new HashSet<string>(expectedPartialFiles.Select(f => f.ExpectedPath));
                // Add expected .asmref paths to the set
                foreach (var assemblyName in originalAssemblyNames)
                {
                    string assemblySpecificFolder = Path.Combine(PartialsRootFolder, assemblyName).Replace("\\", "/");
                    string asmrefPath = Path.Combine(assemblySpecificFolder, $"{assemblyName}.asmref").Replace("\\", "/");
                    allExpectedPaths.Add(asmrefPath);
                }


                foreach (var fileInPartials in allFilesinPartials)
                {
                    if (!allExpectedPaths.Contains(fileInPartials))
                    {
                        Debug.Log($"Deleting unexpected file in Partials directory: {fileInPartials}");
                        AssetDatabase.DeleteAsset(fileInPartials);
                        DidGenerateFilesThisCycle = true;
                    }
                }
            }


            // Final cleanup of empty directories within the partials root
            CleanupEmptyDirectories(PartialsRootFolder);
            Debug.Log("[EasyCS] Partial class generation and cleanup complete.");
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
            string factoryGenericArgName = $"{EntityDataFactoryPrefix}{baseName}";

            return string.Format(
@"using EasyCS;
using EasyCS.EntityFactorySystem;
using UnityEngine;

namespace {3}
{{
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

namespace {2}
{{
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
            return string.Format(
@"using EasyCS;
using UnityEngine;

namespace {3}
{{
    [AddComponentMenu(""EasyCS/Actor/Data/Data {1}"")]
    public partial class {0} : ActorDataSharedProviderBase<{4}, {2}>
    {{
    }}
}}", providerName, displayBaseName, factoryName, namespaceName, actorDataTypeName);
        }

        /// <summary>
        /// Generates the C# content for a partial ActorData class, adding the AddComponentMenu attribute.
        /// Handles namespaces correctly.
        /// </summary>
        private static string GenerateActorDataPartialContent(string actorDataTypeName, string namespaceName)
        {
            string displayBaseName = AddSpacesToSentence(actorDataTypeName.Replace(ActorDataPlainPrefix, ""));
            string template;

            if (!string.IsNullOrEmpty(namespaceName))
            {
                template =
@"using UnityEngine;

namespace {1}
{{
    [AddComponentMenu(""EasyCS/Actor/Data/Data {0}"")]
    public partial class {2} {{ }}
}}";
                return string.Format(template, displayBaseName, namespaceName, actorDataTypeName);
            }
            else
            {
                // No namespace, class is in the global namespace
                template =
@"using UnityEngine;

[AddComponentMenu(""EasyCS/Actor/Data/Data {0}"")]
public partial class {1} {{ }}";
                return string.Format(template, displayBaseName, actorDataTypeName);
            }
        }

        /// <summary>
        /// Generates the C# content for a partial ActorBehavior class, adding the AddComponentMenu attribute.
        /// Handles namespaces correctly.
        /// </summary>
        private static string GenerateActorBehaviorPartialContent(string actorBehaviorTypeName, string namespaceName)
        {
            string displayBaseName = AddSpacesToSentence(actorBehaviorTypeName.Replace(ActorBehaviorPlainPrefix, ""));
            string template;

            if (!string.IsNullOrEmpty(namespaceName))
            {
                template =
@"using UnityEngine;

namespace {1}
{{
    [AddComponentMenu(""EasyCS/Actor/Behavior/Behavior {0}"")]
    public partial class {2} {{ }}
}}";
                return string.Format(template, displayBaseName, namespaceName, actorBehaviorTypeName);
            }
            else
            {
                // No namespace, class is in the global namespace
                template =
@"using UnityEngine;

[AddComponentMenu(""EasyCS/Actor/Behavior/Behavior {0}"")]
public partial class {1} {{ }}";
                return string.Format(template, displayBaseName, actorBehaviorTypeName);
            }
        }


        /// <summary>
        /// Handles the conditional generation or deletion of the EasyCS.Generated.asmdef file.
        /// The .asmdef is skipped if user-defined types are found in 'Assembly-CSharp.dll'.
        /// </summary>
        private static void HandleAsmdefGeneration()
        {
            Debug.Log("[EasyCS] Generating Assembly Definition...");
            EnsureGeneratedFolders();

            if (!ShouldGenerateAsmdef())
            {
                Debug.LogWarning("Skipping EasyCS.Generated.asmdef generation due to unwrapped dependencies in Assembly-CSharp. Deleting existing asmdef if found.");
                if (File.Exists(AsmdefPath))
                {
                    AssetDatabase.DeleteAsset(AsmdefPath);
                    DidGenerateFilesThisCycle = true;
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                return;
            }

            // Only EasyCS.Runtime and TriInspector are consistently required here
            var referencedAssemblies = new HashSet<string> { "EasyCS.Runtime", "TriInspector" };

            var baseTypes = new Type[] {
                typeof(IEntityData),
                typeof(IEntityBehavior),
                typeof(ActorDataSharedBase)
            }.Where(t => t != null).ToList();

            foreach (var baseType in baseTypes)
            {
                // This uses default reflection, which is fine for getting types to reference
                var derivedTypes = GetAllTypesDerivedFrom(baseType, AppDomain.CurrentDomain.GetAssemblies());
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

            // Check if content has changed before writing
            if (!File.Exists(AsmdefPath) || File.ReadAllText(AsmdefPath) != asmdefContent)
            {
                File.WriteAllText(AsmdefPath, asmdefContent);
                DidGenerateFilesThisCycle = true;
                Debug.Log($"[EasyCS] Generated Assembly Definition: {AsmdefPath}");
            }
            else
            {
                Debug.Log($"[EasyCS] Assembly Definition unchanged: {AsmdefPath}");
            }
        }

        /// <summary>
        /// Handles the generation and cleanup of .asmref files for specific provider and factory folders.
        /// These .asmref files reference the main EasyCS.Generated.asmdef, or Assembly-CSharp if the main asmdef doesn't exist.
        /// </summary>
        private static void HandleAsmrefGenerationForProvidersAndFactories()
        {
            Debug.Log("[EasyCS] Handling .asmref generation for provider and factory folders...");
            // Determine if the main EasyCS.Generated.asmdef exists
            bool mainAsmdefExists = File.Exists(AsmdefPath);
            string mainAsmdefGuid = mainAsmdefExists ? AssetDatabase.AssetPathToGUID(AsmdefPath) : null;

            // Reference Assembly-CSharp if main asmdef does not exist, otherwise use GUID reference
            string referenceValue = mainAsmdefExists ? $"GUID:{mainAsmdefGuid}" : "Assembly-CSharp";

            foreach (var folderPath in FoldersWithSpecificAsmref)
            {
                string asmrefPath = Path.Combine(folderPath, $"{Path.GetFileName(folderPath)}.asmref").Replace("\\", "/");

                // Generate/Update .asmref content based on whether main asmdef exists
                string asmrefContent = $@"{{
    ""reference"": ""{referenceValue}""
}}";
                try
                {
                    // Only write if content has changed or file doesn't exist
                    if (!File.Exists(asmrefPath) || File.ReadAllText(asmrefPath) != asmrefContent)
                    {
                        File.WriteAllText(asmrefPath, asmrefContent);
                        DidGenerateFilesThisCycle = true;
                        Debug.Log($"[EasyCS] Generated/Updated .asmref for '{Path.GetFileName(folderPath)}': {asmrefPath}");
                    }
                    else
                    {
                        Debug.Log($"[EasyCS] .asmref for '{Path.GetFileName(folderPath)}' is unchanged: {asmrefPath}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error writing .asmref for '{Path.GetFileName(folderPath)}' at {asmrefPath}: {e.Message}");
                }
            }
            AssetDatabase.Refresh();
            Debug.Log("[EasyCS] .asmref handling for provider and factory folders complete.");
        }

        /// <summary>
        /// Handles the generation and cleanup of .asmref files for the partial class folders.
        /// These .asmref files reference the original assembly of the user-defined class.
        /// It always generates an .asmref, referencing "Assembly-CSharp" if that's the original assembly.
        /// </summary>
        private static void HandlePartialClassAsmrefs(HashSet<string> originalAssemblyNames)
        {
            Debug.Log("[EasyCS] Handling .asmref generation for partial class folders...");

            var expectedAsmrefPaths = new HashSet<string>();

            foreach (var assemblyName in originalAssemblyNames)
            {
                string assemblySpecificFolder = Path.Combine(PartialsRootFolder, assemblyName).Replace("\\", "/");
                string asmrefPath = Path.Combine(assemblySpecificFolder, $"{assemblyName}.asmref").Replace("\\", "/");
                expectedAsmrefPaths.Add(asmrefPath);

                // Generate .asmref content: reference "Assembly-CSharp" if it's the original assembly, otherwise reference the actual assembly name
                string referenceValue = assemblyName.Equals("Assembly-CSharp", StringComparison.OrdinalIgnoreCase) ? "Assembly-CSharp" : assemblyName;

                string asmrefContent = $@"{{
    ""reference"": ""{referenceValue}""
}}";

                try
                {
                    // Ensure the folder exists before writing the .asmref
                    if (!Directory.Exists(assemblySpecificFolder))
                    {
                        Directory.CreateDirectory(assemblySpecificFolder);
                        DidGenerateFilesThisCycle = true;
                        AssetDatabase.Refresh();
                    }

                    // Only write if content has changed or file doesn't exist
                    if (!File.Exists(asmrefPath) || File.ReadAllText(asmrefPath) != asmrefContent)
                    {
                        File.WriteAllText(asmrefPath, asmrefContent);
                        DidGenerateFilesThisCycle = true;
                        Debug.Log($"[EasyCS] Generated/Updated .asmref for '{assemblyName}' partials: {asmrefPath}");
                    }
                    else
                    {
                        Debug.Log($"[EasyCS] .asmref for '{assemblyName}' partials is unchanged: {asmrefPath}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error writing .asmref for '{assemblyName}' at {asmrefPath}: {e.Message}");
                }
            }
            // The cleanup of obsolete .asmref files in partials folder is now handled by the broader partials cleanup below.
            // No specific action needed here as the larger cleanup will catch them.
            AssetDatabase.Refresh();
            Debug.Log("[EasyCS] .asmref handling for partial class folders complete.");
        }


        /// <summary>
        /// Determines if the EasyCS.Generated.asmdef file should be generated.
        /// It returns false if any user-defined types (IEntityData, IEntityBehavior, ActorDataSharedBase, ActorData, ActorBehavior)
        /// are found in the default 'Assembly-CSharp.dll', indicating unwrapped dependencies.
        /// </summary>
        private static bool ShouldGenerateAsmdef()
        {
            // For asmdef generation check, it's safer to use AppDomain.CurrentDomain.GetAssemblies()
            // as this function might be called outside of a CompilationPipeline.compilationFinished context
            // (e.g., if a MenuItem explicitly calls RegenerateAll).
            var userDefinedTypes = new List<Type>();
            userDefinedTypes.AddRange(GetAllTypesDerivedFrom(typeof(IEntityData), AppDomain.CurrentDomain.GetAssemblies()));
            userDefinedTypes.AddRange(GetAllTypesDerivedFrom(typeof(IEntityBehavior), AppDomain.CurrentDomain.GetAssemblies()));
            userDefinedTypes.AddRange(GetAllTypesDerivedFrom(typeof(ActorDataSharedBase), AppDomain.CurrentDomain.GetAssemblies()));
            userDefinedTypes.AddRange(GetAllTypesDerivedFrom(typeof(ActorData), AppDomain.CurrentDomain.GetAssemblies()));
            userDefinedTypes.AddRange(GetAllTypesDerivedFrom(typeof(ActorBehavior), AppDomain.CurrentDomain.GetAssemblies()));


            foreach (var type in userDefinedTypes)
            {
                if (type?.Assembly != null && type.Assembly.GetName().Name == "Assembly-CSharp")
                {
                    Debug.Log($"Detected user-defined type '{type.FullName}' in 'Assembly-CSharp'. Skipping .asmdef generation.");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Ensures all necessary generated folders exist in the Unity project, including the new 'Partials' roots.
        /// </summary>
        private static void EnsureGeneratedFolders()
        {
            Debug.Log("[EasyCS] Ensuring generated folders exist...");
            if (!Directory.Exists(GeneratedRootFolder))
            {
                Directory.CreateDirectory(GeneratedRootFolder);
                DidGenerateFilesThisCycle = true;
            }
            foreach (var folder in TopLevelGeneratedSubfolders)
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    DidGenerateFilesThisCycle = true;
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
                DidGenerateFilesThisCycle = true;
                Debug.Log($"{(fileExists ? "Updated" : "Generated")} {Path.GetFileName(path)}");
            }
            else
            {
                Debug.Log($"File content unchanged, skipping write: {Path.GetFileName(path)}");
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
        /// This method is primarily used for the existing Factory/Provider types.
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
        /// Attempts to infer information about an existing generated Factory/Provider file (base name, prefix, etc.)
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
            // Handle old ActorData Factory prefix for migration/cleanup
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

            // Exclude generated partial files from being detected as other types
            if (fileNameWithoutExtension.StartsWith(GeneratedPartialPrefix))
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// Attempts to infer information about an existing generated Partial file.
        /// This method now expects file names to start with 'Partial'.
        /// </summary>
        private static ExistingGeneratedPartialFileInfo TryInferGeneratedPartialFileInfo(string assetPath)
        {
            // Expected path format: PartialsRootFolder/[Assembly Name]/[ActorData|ActorBehavior]/Partial[OriginalTypeName].cs
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(assetPath);

            // Check if the file name starts with the 'Partial' prefix
            if (!fileNameWithoutExtension.StartsWith(GeneratedPartialPrefix))
            {
                return null;
            }

            string originalTypeName = fileNameWithoutExtension.Substring(GeneratedPartialPrefix.Length);

            string relativePath = Path.GetRelativePath(PartialsRootFolder, assetPath).Replace("\\", "/");
            string[] segments = relativePath.Split('/');

            if (segments.Length >= 3)
            {
                string assemblyName = segments[0];
                string typeFolder = segments[1];

                // Basic validation of the type folder and inferred original type name
                if ((typeFolder == "ActorData" && originalTypeName.StartsWith(ActorDataPlainPrefix)) ||
                    (typeFolder == "ActorBehavior" && originalTypeName.StartsWith(ActorBehaviorPlainPrefix)))
                {
                    return new ExistingGeneratedPartialFileInfo
                    {
                        CurrentPath = assetPath,
                        CurrentFileName = Path.GetFileName(assetPath),
                        OriginalAssemblyName = assemblyName,
                        OriginalTypeName = originalTypeName
                    };
                }
            }
            return null;
        }


        /// <summary>
        /// Retrieves all concrete, non-abstract types derived from a specified base type (class or interface)
        /// from a given set of assemblies.
        /// If no assemblies are specified, it defaults to scanning all assemblies in the current AppDomain.
        /// </summary>
        private static List<Type> GetAllTypesDerivedFrom(Type baseType, IEnumerable<System.Reflection.Assembly> assembliesToScan = null)
        {
            if (baseType == null) return new List<Type>();

            IEnumerable<System.Reflection.Assembly> targetAssemblies = assembliesToScan ?? AppDomain.CurrentDomain.GetAssemblies();

            return targetAssemblies
                .SelectMany(assembly =>
                {
                    try { return assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException ex)
                    {
                        foreach (var loaderEx in ex.LoaderExceptions)
                        {
                            Debug.LogError($"Loader Exception: {loaderEx.Message}");
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
                )
                .Where(t => !t.Name.StartsWith(GeneratedPartialPrefix))
                .ToList();
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
                    Debug.Log($"Deleting empty directory: {directory}");
                    AssetDatabase.DeleteAsset(directory.Replace("\\", "/"));
                    DidGenerateFilesThisCycle = true;
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

        // Helper class to hold information about an expected generated Factory/Provider file
        private class ExpectedGeneratedFileInfo
        {
            public string ExpectedPath { get; set; }
            public string ExpectedContent { get; set; }
            public string InferredBaseName { get; set; }
            public string GeneratedFilePrefix { get; set; }
        }

        // Helper class to hold information about an existing generated Factory/Provider file
        private class ExistingGeneratedFileInfo
        {
            public string CurrentPath { get; set; }
            public string CurrentFileName { get; set; }
            public string InferredBaseName { get; set; }
            public string GeneratedFilePrefix { get; set; }
        }

        // Helper class to hold information about an expected generated Partial file
        private class ExpectedGeneratedPartialFileInfo
        {
            public string ExpectedPath { get; set; }
            public string ExpectedContent { get; set; }
            public string OriginalAssemblyName { get; set; }
            public string OriginalTypeName { get; set; }
        }

        // Helper class to hold information about an existing generated Partial file
        private class ExistingGeneratedPartialFileInfo
        {
            public string CurrentPath { get; set; }
            public string CurrentFileName { get; set; }
            public string OriginalAssemblyName { get; set; }
            public string OriginalTypeName { get; set; }
        }
    }
}
