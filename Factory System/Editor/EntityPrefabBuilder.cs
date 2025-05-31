using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static EasyCS.EntityFactorySystem.EntityFactory;

namespace EasyCS.EntityFactorySystem.Editor
{

#if UNITY_EDITOR
    public static class EntityPrefabBuilder
    {
        public static void StartEditing(EntityFactory factory)
        {
            try
            {
                StartEditingInternal(factory);
            }
            catch (Exception ex)
            {
                PrefabEditSessionManager.ResetEditingSession();

                Debug.LogException(ex);
            }
        }

        private static void StartEditingInternal(EntityFactory factory)
        {
            string sessionId = Guid.NewGuid().ToString().Substring(0, 8);
            string tempPathFolderParent = "Assets/Temp";
            string tempFolderName = "EntityPrefabEditing";
            string tempPathFolder = $"{tempPathFolderParent}/{tempFolderName}";
            string prefabName = $"EntityFactoryEdit_{factory.name}_{sessionId}";
            string tempPath = $"{tempPathFolder}/{prefabName}.prefab";

            EnsureFolderExists(tempPathFolder);

            GameObject root = new GameObject(prefabName);

            PrefabUtility.SaveAsPrefabAsset(root, tempPath);
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.Refresh();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(tempPath);
            if (prefab != null)
            {
                AssetDatabase.OpenAsset(prefab);
            }

            root = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot;

            BuildHierarchyRecursive(factory, root.transform, null);
            root.name = prefabName;

            PrefabEditSessionManager.RegisterEditingSession(tempPath, factory, root);

            List<EntityProvider> entityProviders = new List<EntityProvider>(root.GetComponentsInChildren<EntityProvider>(true));
            List<Actor> actors = new List<Actor>(root.GetComponentsInChildren<Actor>(true));

            entityProviders.ForEach(provider => GameObject.DestroyImmediate(provider));
            actors.ForEach(actor => actor.EditorSetEntityProvider(null));

            PrefabUtility.SaveAsPrefabAsset(root, tempPath);
            AssetDatabase.Refresh();
        }

        public static void BuildNestedFactoryHierarchy(EntityFactory factory, Transform parent)
        {
            GameObject entityGO = new GameObject(factory.name);

            var nestedProvider = entityGO.AddComponent<EntityNestedProvider>();
            nestedProvider.SetNestedFactoriesEditorOnly(new List<EntityFactory> { factory });

            entityGO.transform.SetParent(parent);
        }

        public static GameObject BuildHierarchyRecursive(EntityFactory factory, Transform current, Transform parent)
        {
            GameObject go = BuildHierarchyRecursive(factory.Entity, null, current);
            go.transform.SetParent(parent);

            go.name = factory.name + "_Root";

            return go;
        }

        public static GameObject BuildHierarchyRecursive(EntityData entityData, EntityData entityDataParent, Transform current)
        {
            Dictionary<EntityData, EntityData> selfDataToParentData = new Dictionary<EntityData, EntityData>();
            Dictionary<GameObject, EntityData> selfGoToSelfData = new Dictionary<GameObject, EntityData>();
            Dictionary<EntityData, GameObject> selfDataToSelfGo = new Dictionary<EntityData, GameObject>();
            List<GameObject> entityObjects = new List<GameObject>();

            void BuildChildsRecursive(EntityData entityData2, EntityData entityDataParent2, Transform current)
            {
                GameObject go = BuildEntityObject(entityData2, current);
                entityObjects.Add(go);

                selfDataToSelfGo.Add(entityData2, go);
                selfGoToSelfData.Add(go, entityData2);
                selfDataToParentData.Add(entityData2, entityDataParent2);

                foreach (EntityData child in entityData2.ChildEntities)
                    BuildChildsRecursive(child, entityData2, null);
            }

            BuildChildsRecursive(entityData, entityDataParent, current);


            // Parenting
            foreach (GameObject go in entityObjects)
            {
                EntityData entity = selfGoToSelfData[go];
                EntityData entityParent = selfDataToParentData[entity];

                if (entityParent == null)
                    continue;

                GameObject goParent = selfDataToSelfGo[entityParent];
                go.transform.SetParent(goParent?.transform);
            }

            GameObject root = entityObjects.First().transform.root.gameObject;

            return entityObjects.First();
        }

        public static GameObject BuildEntityObject(EntityData entityData, Transform current)
        {
            GameObject entityGO = current == null ? new GameObject() : current.gameObject;
            entityGO.AddComponent<EntityEditorComponentFlag>().hideFlags = HideFlags.HideInInspector;
            Actor actor= entityGO.AddComponent<Actor>();

            if (string.IsNullOrEmpty(entityData.Name) == false)
                entityGO.name = entityData.Name;
            else
                entityGO.name = "New Entity";

            //Actor actor = entityGO.AddComponent<Actor>();

            if (entityData.NestedFactories != null && entityData.NestedFactories.Count > 0)
            {
                var nestedProvider = entityGO.AddComponent<EntityNestedProvider>();
                nestedProvider.SetNestedFactoriesEditorOnly(entityData.NestedFactories);
            }

            foreach (var component in entityData.Components)
            {
                Type typeEntityComponent = null;
                Type typeEntityProvider = null;

                if (component.dataType == ComponentData.DataType.Behavior)
                {
                    typeEntityComponent = component.behaviorComponent.GetType();
                    typeEntityProvider = EntityBehaviorProviderFinder.FindEntityBehaviorProviderMatching(typeEntityComponent);

                    IEntityBehaviorProvider entityBehaviorProvider =
                        (IEntityBehaviorProvider)entityGO.AddComponent(typeEntityProvider);

                    entityBehaviorProvider.EditorSetBehavior((IEntityBehavior)component.behaviorComponent);
                }
                else
                {
                    if (component.dataType == ComponentData.DataType.DataFactory)
                        typeEntityComponent = component.referenceFactory.GetType();
                    else if (component.dataType == ComponentData.DataType.Data)
                        typeEntityComponent = component.dataComponent.GetType();

                    typeEntityProvider = EntityDataProviderFinder.FindEntityDataProviderMatching(typeEntityComponent);

                    IEntityDataProvider entityDataProvider =
                        (IEntityDataProvider)entityGO.AddComponent(typeEntityProvider);

                    if (component.dataType == ComponentData.DataType.Data)
                    {
                        entityDataProvider.EditorSetSource(Source.Embedded);
                        entityDataProvider.EditorSetData(component.dataComponent);
                    }
                    else if (component.dataType == ComponentData.DataType.DataFactory)
                    {
                        entityDataProvider.EditorSetSource(Source.Asset);
                        entityDataProvider.EditorSetFactory((IEntityDataFactory)component.referenceFactory);
                    }
                }
            }

            actor.EditorSetEntityTemplate(entityData.Template);

            return entityGO;
        }

        private static void EnsureFolderExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string currentPath = parts[0];

                for (int i = 1; i < parts.Length; i++)
                {
                    string nextPath = $"{currentPath}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = nextPath;
                }
            }
        }
    }


#endif
}