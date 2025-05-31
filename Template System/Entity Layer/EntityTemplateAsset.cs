using System;
using System.Collections.Generic;
using System.Linq;
using TriInspector;
using UnityEditor;
using UnityEngine;

namespace EasyCS
{
    [CreateAssetMenu(fileName = "EntityTemplate", menuName = "EasyCS/Entity Template")]
    public class EntityTemplateAsset : ScriptableObject, IEntityTemplateProvider, IGUID
    {
        [SerializeField, PropertyOrder(-1)]
        private ComponentGUID guid;
        public ComponentGUID GUID => guid;

        [ValidateInput("ValidateUniqueDataTypes")]
        [SerializeReference, ListDrawerSettings(Draggable = false, AlwaysExpanded = true), MaxDrawDepth(2)]
        private List<IEntityData> _dataComponents = new();

        [ValidateInput("ValidateUniqueBehaviorTypes")]
        [SerializeReference, ListDrawerSettings(Draggable = false, AlwaysExpanded = true), MaxDrawDepth(2)]
        private List<IEntityBehavior> _behaviorComponents = new();

        [LabelText("Included Templates"), ListDrawerSettings(AlwaysExpanded = true)]
        [SerializeField]
        private List<EntityTemplateAsset> _includedTemplates = new();

        [SerializeReference, HideInInspector]
        private List<IEntityData> _componentsMissing = new List<IEntityData>();

        public void ApplyTemplate(Entity entity, EntityTemplateSetupPolicy setupPolicy = EntityTemplateSetupPolicy.AddMissingFromTemplate)
        {
            if (setupPolicy == EntityTemplateSetupPolicy.DoNotModify || entity.IsAlive == false)
                return;

            var allComponents = GetFlattenedComponents();

            if (setupPolicy == EntityTemplateSetupPolicy.SetAsInTemplate)
            {
                foreach (var data in entity.DataComponents.ToList())
                    entity.RemoveComponent(data);

                foreach (var behavior in entity.BehaviorComponents.ToList())
                    entity.RemoveComponent(behavior);
            }

            foreach (var componentType in allComponents)
            {
                bool hasComponent = entity.HasComponent(componentType);
                if (!hasComponent)
                {
                    entity.AddComponent(componentType);
                }
            }
        }

        public HashSet<Type> GetComponentTypes()
        {
            var all = GetFlattenedComponents();

            return all;
        }

        private HashSet<Type> GetFlattenedComponents()
        {
            var (data, behaviors) = GetFlattenedComponentsInstances();
            var result = new HashSet<Type>();

            foreach (var d in data)
            {
                if (d != null)
                {
                    result.Add(d.GetType());
                }
            }

            foreach (var b in behaviors)
            {
                if (b != null)
                {
                    result.Add(b.GetType());
                }
            }

            return result;
        }

        private (List<IEntityData> data, List<IEntityBehavior> behaviors) GetFlattenedComponentsInstances()
        {
            var visited = new HashSet<EntityTemplateAsset>();
            var resultData = new Dictionary<Type, IEntityData>();
            var resultBehaviors = new Dictionary<Type, IEntityBehavior>();

            void Collect(EntityTemplateAsset template)
            {
                if (template == null || visited.Contains(template))
                    return;

                visited.Add(template);

                foreach (var sub in template._includedTemplates)
                {
                    Collect(sub);
                }

                foreach (var d in template._dataComponents)
                {
                    if (d == null) continue;

                    var type = d.GetType();
                    if (!resultData.ContainsKey(type))
                    {
                        resultData.Add(type, d);
                    }
                }

                foreach (var d in _componentsMissing)
                {
                    if (d == null) continue;

                    var type = d.GetType();
                    if (!resultData.ContainsKey(type))
                    {
                        resultData.Add(type, d);
                    }
                }

                foreach (var b in template._behaviorComponents)
                {
                    if (b == null) continue;

                    var type = b.GetType();
                    if (!resultBehaviors.ContainsKey(type))
                    {
                        resultBehaviors.Add(type, b);
                    }
                }
            }

            Collect(this);

            var dataList = resultData.Values.OrderBy(d => d?.GetType().Name).ToList();
            var behaviorList = resultBehaviors.Values.OrderBy(b => b?.GetType().Name).ToList();

            return (dataList, behaviorList);
        }

#if UNITY_EDITOR
        [ShowInInspector, ReadOnly, LabelText("Final Data Components"), PropertyOrder(0), ListDrawerSettings(AlwaysExpanded = true), MaxDrawDepth(2)]
        private List<IEntityData> EditorFinalData => GetFlattenedComponentsInstances().data;

        [ShowInInspector, ReadOnly, LabelText("Final Behavior Components"), PropertyOrder(0), ListDrawerSettings(AlwaysExpanded = true), MaxDrawDepth(2)]
        private List<IEntityBehavior> EditorFinalBehaviors => GetFlattenedComponentsInstances().behaviors;

        private TriValidationResult ValidateUniqueDataTypes()
        {
            EditorSortComponents();
            OnValidate();

            var types = new HashSet<Type>();
            foreach (var data in _dataComponents)
            {
                if (data == null) continue;
                var type = data.GetType();
                if (!types.Add(type))
                {
                    return TriValidationResult.Error($"Duplicate data type detected: {type.Name}");
                }
            }
            return TriValidationResult.Valid;
        }

        private TriValidationResult ValidateUniqueBehaviorTypes()
        {
            EditorSortComponents();
            OnValidate();

            var types = new HashSet<Type>();
            foreach (var behavior in _behaviorComponents)
            {
                if (behavior == null) continue;
                var type = behavior.GetType();
                if (!types.Add(type))
                {
                    return TriValidationResult.Error($"Duplicate behavior type detected: {type.Name}");
                }
            }
            return TriValidationResult.Valid;
        }

        private void OnValidate()
        {
            EditorSortComponents();
            EditorUpdateMissingBehaviorDependencies();
        }

        private void EditorSortComponents()
        {
            _dataComponents = _dataComponents.OrderBy(d => d?.GetType().Name).ToList();
            _behaviorComponents = _behaviorComponents.OrderBy(b => b?.GetType().Name).ToList();
        }

        private bool EditorHasMissingBehaviorDependencies() => _componentsMissing.Count > 0;

        private void EditorUpdateMissingBehaviorDependencies()
        {
            var (allData, allBehaviors) = GetFlattenedComponentsInstances();
            HashSet<Type> existingTypes = new HashSet<Type>();
            foreach (var d in allData) { if (d != null) existingTypes.Add(d.GetType()); }
            foreach (var b in allBehaviors) { if (b != null) existingTypes.Add(b.GetType()); }

            HashSet<Type> requiredBehaviorTypes = new HashSet<Type>();

            foreach (var behavior in allBehaviors)
            {
                if (behavior == null) continue;
                foreach (var depType in Injector.GetRequiredInjectionTypes(behavior.GetType()))
                {
                    if (typeof(IEntityComponent).IsAssignableFrom(depType) && !Attribute.IsDefined(depType, typeof(RuntimeOnlyAttribute)))
                    {
                        requiredBehaviorTypes.Add(depType);
                    }
                }
            }

            _componentsMissing.Clear();
            foreach (var requiredType in requiredBehaviorTypes)
            {
                if (!existingTypes.Contains(requiredType))
                {
                    _componentsMissing.Add((IEntityData)Activator.CreateInstance(requiredType));
                }
            }
        }

        [Button("Add Missing Components"), ShowIf("EditorHasMissingBehaviorDependencies"), ShowInEditMode]
        private void EditorAddMissingBehaviorDependencies()
        {
            UnityEditor.Undo.RecordObject(this, "Add Missing Components");
            bool changed = false;
            foreach (var missingBehavior in _componentsMissing.ToList())
            {
                if (missingBehavior == null) continue;
                Type missingType = missingBehavior.GetType();
                if (!_dataComponents.Any(b => b?.GetType() == missingType))
                {
                    _dataComponents.Add((IEntityData)Activator.CreateInstance(missingType));
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(this);
                OnValidate();
            }
        }

        [ShowInInspector, ReadOnly, ShowIf("EditorHasMissingBehaviorDependencies"),
            LabelText("Missing Behavior Dependencies"),
            ListDrawerSettings(AlwaysExpanded = true),
            InfoBox("Behavior Dependencies is not resolved! Default instances will be created.", TriMessageType.Warning, "EditorHasMissingBehaviorDependencies"),
            MaxDrawDepth(2)]
        private List<IEntityData> EditorMissingBehaviorDependenciesDisplay => _componentsMissing;
#endif
    }
}
