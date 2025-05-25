using System;
using System.Collections.Generic;
using UnityEngine;
using TriInspector;
using UnityEngine.Serialization;
using UnityEngine.Pool;
using UnityEditor;

namespace EasyCS.EntityFactorySystem
{
    [CreateAssetMenu(fileName = "NewEntityFactory", menuName = "EasyCS/EntityFactory")]
    public partial class EntityFactory : ScriptableObject
    {
        [Serializable]
        public class EntityData
        {
            public string Name;

            [SerializeReference, ReadOnly, ShowIf("EditorShowMissingTypes"),
         ListDrawerSettings(AlwaysExpanded = true),
         InfoBox("Dependencies is not resolved! Default instances will be created.", TriMessageType.Warning, "EditorShowMissingTypes")]
            public List<IEntityComponent> ComponentsMissing = new List<IEntityComponent>();

            [ListDrawerSettings(AlwaysExpanded = true), OnValueChanged("EditorValidate")]
            public List<ComponentData> Components = new List<ComponentData>();
            [Required]
            public List<EntityFactory> NestedFactories = new List<EntityFactory>();

            [SerializeReference, ListDrawerSettings(AlwaysExpanded = true, ShowElementLabels = true)]
            public List<EntityData> ChildEntities = new List<EntityData>();


            public IEnumerable<IEntityData> DataMissing
            {
                get
                {
                    foreach (var component in ComponentsMissing)
                        if (component is IEntityData entityData)
                            yield return entityData;
                }
            }
            public IEnumerable<IEntityBehavior> BehaviorsMissing
            {
                get
                {
                    foreach (var component in ComponentsMissing)
                        if (component is IEntityBehavior entityBehavior)
                            yield return entityBehavior;
                }
            }

            private bool EditorShowMissingTypes => ComponentsMissing.Count > 0;

#if UNITY_EDITOR
            private void EditorValidate()
            {
                ValidateDependencies(this);
            }
#endif
        }

        [Serializable]
        public class ComponentData
        {
            public enum DataType { Data, DataFactory, Behavior }

            [LabelWidth(100)]
            public DataType dataType;

            [LabelWidth(160)]
            [SerializeReference, Indent, HideLabel, InlineProperty]
            [ShowIf("dataType", DataType.Data), InfoBox("Required!", TriMessageType.Error, "EditorShowErrorDataComponent")]
            public IEntityData dataComponent;

            [LabelWidth(160)]
            [SerializeReference, Indent, HideLabel, InlineProperty]
            [ShowIf("dataType", DataType.Behavior), InfoBox("Required!", TriMessageType.Error, "EditorShowErrorBehaviorComponent")]
            public IEntityBehavior behaviorComponent;

            [LabelWidth(160)]
            [ShowIf("dataType", DataType.DataFactory), Required, Indent, HideLabel, InlineProperty]
            public EntityDataFactoryBase referenceFactory;

#if UNITY_EDITOR
            private bool EditorShowErrorDataComponent() => dataComponent == null;
            private bool EditorShowErrorBehaviorComponent() => behaviorComponent == null;
#endif
        }



        [SerializeField, ReadOnly]
        private int _dataVersion = 1;

        [SerializeField, HideLabel, FormerlySerializedAs("Entity")]
        private EntityData _entity;

        public EntityData Entity => _entity;


        public IEnumerable<EntityData> GetAllEntities()
        {
            return GetAllEntitiesRecursive(_entity);
        }

        private IEnumerable<EntityData> GetAllEntitiesRecursive(EntityData entity)
        {
            yield return entity;

            if (entity.ChildEntities == null)
                yield break;

            foreach (var child in entity.ChildEntities)
            {
                if (child == null) continue;

                foreach (var descendant in GetAllEntitiesRecursive(child))
                {
                    yield return descendant;
                }
            }
        }

        public Entity GetEntity(EntityContainer container, Guid? id = null)
        {
            return CreateEntityRecursive(_entity, container, null, id, out _);
        }

        private Entity CreateEntityRecursive(EntityData data, EntityContainer container, Entity? parentEntity, Guid? id, out HashSet<Entity> createdSiblings)
        {
            Entity entity = id != null ? container.CreateNew((Guid)id) : container.CreateNew();

            foreach (var componentDataMissing in data.DataMissing)
                entity.AddData((IEntityData)componentDataMissing.Clone());

            // Attach components
            foreach (var component in data.Components)
                AddComponent(entity, component);

            foreach (var componentBehaviorMissing in data.BehaviorsMissing)
                entity.AddBehavior(componentBehaviorMissing.GetType());

            // Set parent if any
            if (parentEntity.HasValue)
            {
                entity.AddData(new EntityDataParent { Value = parentEntity.Value });
            }

            // Handle children
            var children = new HashSet<Entity>();
            foreach (var childData in data.ChildEntities)
            {
                if (childData == null) continue;
                Entity child = CreateEntityRecursive(childData, container, entity, null, out _);
                children.Add(child);
            }

            if (children.Count > 0)
            {
                entity.AddData(new EntityDataChilds { childs = children });
            }

            createdSiblings = children;
            return entity;
        }

        private void AddComponent(Entity entity, ComponentData component)
        {
            switch (component.dataType)
            {
                case ComponentData.DataType.Data when component.dataComponent != null:
                    entity.AddData((IEntityData)component.dataComponent.Clone());
                    break;

                case ComponentData.DataType.DataFactory when component.referenceFactory != null:
                    entity.AddData(component.referenceFactory);
                    break;

                case ComponentData.DataType.Behavior when component.behaviorComponent != null:
                    entity.AddBehavior(component.behaviorComponent.GetType());
                    break;
            }
        }

        private void ValidateDependenciesAll()
        {
            IEnumerable<EntityData> entities = GetAllEntities();
            foreach (var entityData in entities)
                ValidateDependencies(entityData);
        }

        private static void ValidateDependencies(EntityData entityData)
        {
            List<Type> typesMissing = ListPool<Type>.Get();
            HashSet<Type> typesExist = HashSetPool<Type>.Get();

            foreach (var component in entityData.Components)
            {
                Type componentType = null;

                if (component.dataType == ComponentData.DataType.Behavior)
                    componentType = component.behaviorComponent?.GetType();
                else if (component.dataType == ComponentData.DataType.DataFactory)
                    componentType = component.referenceFactory?.GetProductType();
                else if (component.dataType == ComponentData.DataType.Data)
                    componentType = component.dataComponent?.GetType();

                if (componentType == null)
                    continue;

                if (typesExist.Contains(componentType) == false)
                    typesExist.Add(componentType);

                var isBehavior = typeof(IEntityBehavior).IsAssignableFrom(componentType);

                if (isBehavior == false)
                    continue;

                List<Type> typesRequired = Injector.GetRequiredInjectionTypes(componentType);

                foreach (var type in typesRequired)
                {
                    if (typesMissing.Contains(type) == false)
                        typesMissing.Add(type);
                }
            }

            typesMissing.RemoveAll(type =>
                typesExist.Contains(type));

            typesMissing.Sort((t1, t2) =>
            {
                bool isData1 = typeof(IEntityData).IsAssignableFrom(t1);
                bool isData2 = typeof(IEntityData).IsAssignableFrom(t2);

                if (isData1 && !isData2) return -1;
                if (!isData1 && isData2) return 1;

                return string.Compare(t1.Name, t2.Name, StringComparison.Ordinal);
            });

            entityData.ComponentsMissing.Clear();

            foreach (var type in typesMissing)
            {
                bool isData = typeof(IEntityData).IsAssignableFrom(type);
                bool isBehavior = typeof(IEntityBehavior).IsAssignableFrom(type);

                var component = Activator.CreateInstance(type);

                entityData.ComponentsMissing.Add((IEntityComponent)component);
            }

            ListPool<Type>.Release(typesMissing);
            HashSetPool<Type>.Release(typesExist);
        }

#if UNITY_EDITOR
        [Button("Edit Entity Factory"), PropertyOrder(-1)]
        private void EditEntityFactory()
        {
            Editor.EntityPrefabBuilder.StartEditing(this);
            EditorUtility.SetDirty(this);
        }

        public void SetEntityData(EntityData entityData)
        {
            _entity = entityData;
        }

        private void OnValidate()
        {
            ValidateDependenciesAll();
        }
#endif
    }
}