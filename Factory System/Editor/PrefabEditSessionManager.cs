using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EasyCS.EntityFactorySystem.EntityFactory;

namespace EasyCS.EntityFactorySystem.Editor
{

#if UNITY_EDITOR
    public static class PrefabEditSessionManager
    {
        public static bool IsEditing { get; private set; }
        public static GameObject EditNow { get; private set; }

        private static string _currentTempPath;
        private static EntityFactory _editingFactory;

        static PrefabEditSessionManager()
        {
            UnityEditor.SceneManagement.PrefabStage.prefabStageClosing += OnPrefabStageClosing;
        }

        public static void RegisterEditingSession(string tempPath, EntityFactory factory, GameObject editNow)
        {
            IsEditing = true;
            EditNow = editNow;
            _currentTempPath = tempPath;
            _editingFactory = factory;
        }

        public static void ResetEditingSession()
        {
            IsEditing = false;
            EditNow = null;
            _currentTempPath = null;
            _editingFactory = null;
        }

        private static void OnPrefabStageClosing(UnityEditor.SceneManagement.PrefabStage stage)
        {
            if (string.IsNullOrEmpty(_currentTempPath))
                return;

            if (stage.prefabContentsRoot == null || _editingFactory == null)
                return;

            // Important: check if the closed prefab matches the currently registered editing prefab
            if (!string.Equals(stage.assetPath, _currentTempPath, StringComparison.OrdinalIgnoreCase))
                return; // Ignore if another prefab is closing

            EntityData entityData = BuildEntityDataRecursive(stage.prefabContentsRoot.transform);

            _editingFactory.SetEntityData(entityData);

            UnityEditor.EditorUtility.SetDirty(_editingFactory);
            UnityEditor.AssetDatabase.SaveAssets();

            UnityEditor.AssetDatabase.DeleteAsset(_currentTempPath);
            UnityEditor.AssetDatabase.Refresh();

            _currentTempPath = null;
            _editingFactory = null;
            IsEditing = false;
            EditNow = null;
        }

        private static EntityFactory.EntityData BuildEntityDataRecursive(Transform transform)
        {
            var entityObjects = transform.gameObject;

            var entityData = new EntityFactory.EntityData
            {
                Name = entityObjects.name,
                Components = new List<EntityFactory.ComponentData>(),
                NestedFactories = new List<EntityFactory>(),
                ChildEntities = new List<EntityFactory.EntityData>()
            };

            // Handle nested factory provider if exists
            var nestedProvider = transform.GetComponent<EntityNestedProvider>();
            if (nestedProvider != null)
            {
                entityData.NestedFactories.AddRange(nestedProvider.NestedFactories.Where(f => f != null));
            }

            // Handle all component data providers
            var dataProviders = transform.GetComponents<IEntityDataProvider>();
            foreach (var provider in dataProviders)
            {
                var componentData = new EntityFactory.ComponentData
                {
                    dataType = provider.Source == Source.Embedded ? ComponentData.DataType.Data : ComponentData.DataType.DataFactory,
                    dataComponent = provider.Source == Source.Embedded ? provider.EditorGetComponent() : null,
                    behaviorComponent = null,
                    referenceFactory = provider.Source == Source.Asset ? (EntityDataFactoryBase)provider.EditorGetFactory() : null
                };
                entityData.Components.Add(componentData);
            }

            // Handle all component behavior providers
            var behaviorProviders = transform.GetComponents<IEntityBehaviorProvider>();
            foreach (var provider in behaviorProviders)
            {
                var componentData = new EntityFactory.ComponentData
                {
                    dataType = ComponentData.DataType.Behavior,
                    dataComponent = null,
                    behaviorComponent = provider.Behavior,
                    referenceFactory = null
                };
                entityData.Components.Add(componentData);
            }

            // Process child entities recursively
            foreach (Transform child in transform)
            {
                if (nestedProvider != null && nestedProvider.NestedInstancesGameobjects.Contains(child.gameObject))
                    continue;

                var childEntityData = BuildEntityDataRecursive(child);
                entityData.ChildEntities.Add(childEntityData);
            }

            entityData.Template = transform.GetComponent<EntityTemplateProvider>().EntityTemplate;

            return entityData;
        }
    }

#endif
}