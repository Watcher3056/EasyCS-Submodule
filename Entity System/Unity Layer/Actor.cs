using System;
using System.Collections.Generic;
using System.Linq;
using TriInspector;
using UnityEngine;

namespace EasyCS
{
    [DisallowMultipleComponent]
    [SelectionBase]
    [IconClass(ConstantsIcons.IconActor)]
#if ODIN_INSPECTOR
    [HideMonoScript]
#endif
    public class Actor : EasyCSBehavior, IHasEntity
    {
        public enum EntityComponentSetupPolicy
        {
            DoNotModify,
            AddMissingFromActor,
            OverrideFromActor,
            SetAsInActor
        }
        [SerializeField, ReadOnly]
        private EntityProvider _entityProvider;

        [ShowInPlayMode, ReadOnly, ShowInInspector]
        public Entity Entity { get; private set; }
        public EntityContainer EntityContainer { get; private set; }

        public event Action<Entity> OnEntityChanged = (entity) => { };
        public event Action<Actor, ActorComponent> OnComponentAdded = (actor, component) => { };
        public event Action<Actor, ActorComponent> OnComponentRemoved = (actor, component) => { };
        public event Action<Actor, IActorDataProvider> OnActorDataAdded = (actor, data) => { };
        public event Action<Actor, IActorDataProvider> OnActorDataRemoved = (actor, data) => { };

        public IReadOnlyCollection<IEntityData> DataComponents => Entity.DataComponents;
        public IReadOnlyCollection<IEntityBehavior> BehaviorComponents => Entity.BehaviorComponents;
        public EntityProvider EntityProvider => _entityProvider;

        public IReadOnlyDictionary<Type, ActorComponent> ActorComponents => _actorComponents;
        [ShowInPlayMode, ReadOnly]
        public IEnumerable<IActorData> ActorData => GetAllActorData();

        [ShowInPlayMode, ShowInInspector, ReadOnly]
        public Actor Parent { get; private set; }
        [ShowInPlayMode, ShowInInspector, ReadOnly]
        public Actor Root
        {
            get
            {
                Actor result = Parent;
                while (result != null && result.Parent != null)
                    result = result.Parent;
                if (result == null)
                    result = this;

                return result;
            }
        }
        public IReadOnlyCollection<Actor> Childs => Childs;
        private HashSet<Actor> _childs = new();

        public new GameObject gameObject
        {
            get
            {
                if (_gameObject == null)
                    _gameObject = base.gameObject;
                return _gameObject;
            }
        }
        public new Transform transform
        {
            get
            {
                if (_transform == null)
                    _transform = base.transform;
                return _transform;
            }
        }

        private Transform _transform;
        private GameObject _gameObject;

        private Dictionary<Type, ActorComponent> _actorComponents = new Dictionary<Type, ActorComponent>();
        private readonly Dictionary<Type, object> _actorComponentsAndData = new();

        [SerializeField, ReadOnly]
        private List<ActorComponent> _allComponents = new List<ActorComponent>();

        [SerializeReference, HideInInspector]
        private List<IEntityComponent> _entityComponentsMissing = new List<IEntityComponent>();

#if UNITY_EDITOR
        [LabelText("Childs"), ShowInPlayMode, ShowInInspector, ReadOnly]
        private List<Actor> EditorChilds => _childs.ToList();
        [ShowInInspector, ReadOnly, ShowIf("EditorHasMissingEntityComponentsDependencies"),
            LabelText("Missing Entity Dependencies"),
         ListDrawerSettings(AlwaysExpanded = true),
         InfoBox("Entity Dependencies is not resolved! Default instances will be created.", TriMessageType.Warning, "EditorHasMissingEntityComponentsDependencies"),
            MaxDrawDepth(2)]
        private List<IEntityComponent> EditorEntityComponentsMissingRequired
        {
            get
            {
                List<IEntityComponent> entityComponents = new();

                foreach (var component in _entityComponentsMissing)
                {
                    if (component == null)
                        continue;

                    var type = component.GetType();
                    bool hasRuntimeOnly = Attribute.IsDefined(type, typeof(RuntimeOnlyAttribute));

                    if (!hasRuntimeOnly)
                        entityComponents.Add(component);
                }

                return entityComponents;
            }
        }
        [LabelText("Missing Actor Dependencies"), ShowInInspector, ShowIf("EditorHasMissingActorComponentsDependencies"), ReadOnly,
            InfoBox("Actor has missing dependencies for this component!",
            TriMessageType.Error,
            "EditorHasMissingActorComponentsDependencies"),
            ListDrawerSettings(AlwaysExpanded = true)]
        private List<string> _editorActorComponentsMissing = new List<string>();

        [LabelText("Unused Actor Dependencies"), ShowInInspector, ShowIf("EditorHasUnusedDependencies"), ReadOnly]
        private List<ActorComponent> _editorUnusedDependencies = new List<ActorComponent>();
#endif

        public IEnumerable<IEntityData> EntityDataMissing
        {
            get
            {
                foreach (var component in _entityComponentsMissing)
                    if (component is IEntityData entityData)
                        yield return entityData;
            }
        }
        public IEnumerable<IEntityBehavior> EntityBehaviorsMissing
        {
            get
            {
                foreach (var component in _entityComponentsMissing)
                    if (component is IEntityBehavior entityBehavior)
                        yield return entityBehavior;
            }
        }

        private const string ErrorCannotSetEntityAlreadySet = "Cannot set entity. Reason: Already set. Aborted.";

        protected override void HandleSetupContainer()
        {
            EntityContainer = EasyCsContainer.Resolve<EntityContainer>();
        }

        protected override void HandleAwake()
        {
            for (int i = 0; i < _allComponents.Count; i++)
            {
                var component = _allComponents[i];
                RegisterActorComponent(component, false);
            }

            if (_entityProvider != null)
            {
                SetEntity(_entityProvider.Entity, EntityComponentSetupPolicy.AddMissingFromActor);
            }
        }

        public void SetEntity(Entity entity, EntityComponentSetupPolicy setupPolicy = EntityComponentSetupPolicy.OverrideFromActor)
        {
            if (Entity.Equals(entity))
            {
                this.LogError(ErrorCannotSetEntityAlreadySet);
                return;
            }

            if (entity.Equals(Entity.Empty) == false)
            {
                foreach (var component in _allComponents)
                    component.InternalHandleDetachFromEntity(entity);
            }

            this.Entity = entity;

            if (entity.IsAlive)
            {
                SetupEntityComponents(setupPolicy);

                EntityContainer.InternalAttachActorToEntity(entity, this);

                foreach (var component in _allComponents)
                    component.InternalHandleAttachToEntity(entity);
            }

            OnEntityChanged.Invoke(entity);
        }

        public void SetupEntityComponents(EntityComponentSetupPolicy setupPolicy)
        {
            if (setupPolicy == EntityComponentSetupPolicy.DoNotModify)
                return;
            if (Entity.IsAlive == false)
                return;

            if (setupPolicy == EntityComponentSetupPolicy.SetAsInActor)
            {
                while (Entity.BehaviorComponents.Count > 0)
                    Entity.RemoveComponent(Entity.BehaviorComponents.First());
                while (Entity.DataComponents.Count > 0)
                    Entity.RemoveComponent(Entity.DataComponents.First());
            }

            // 1. Apply missing IEntityData
            foreach (var data in EntityDataMissing)
            {
                Type type = data.GetType();

                switch (setupPolicy)
                {
                    case EntityComponentSetupPolicy.AddMissingFromActor:
                        if (!Entity.HasComponent(type))
                            Entity.AddData(data);
                        break;

                    case EntityComponentSetupPolicy.OverrideFromActor:
                        if (Entity.HasComponent(type))
                            Entity.RemoveComponent(type);
                        Entity.SetData(data);
                        break;

                    case EntityComponentSetupPolicy.SetAsInActor:
                        Entity.AddData(data);
                        break;
                }
            }

            // 2. Apply components from providers
            foreach (ActorComponent actorComponent in _allComponents)
            {
                IEntityComponentProvider entityComponentProvider = actorComponent as IEntityComponentProvider;

                if (entityComponentProvider == null)
                    continue;

                if (setupPolicy == EntityComponentSetupPolicy.AddMissingFromActor)
                {
                    Type type = entityComponentProvider.GetEntityComponentType();
                    if (Entity.HasComponent(type) == false)
                        entityComponentProvider.AddEntityComponent(Entity);
                }
                else if (setupPolicy == EntityComponentSetupPolicy.OverrideFromActor)
                {
                    entityComponentProvider.SetEntityComponent(Entity);
                }
                else if (setupPolicy == EntityComponentSetupPolicy.SetAsInActor)
                {
                    entityComponentProvider.AddEntityComponent(Entity);
                }
            }

            // 3. Apply missing IEntityBehavior
            foreach (var behavior in EntityBehaviorsMissing)
            {
                Type type = behavior.GetType();

                switch (setupPolicy)
                {
                    case EntityComponentSetupPolicy.AddMissingFromActor:
                        if (!Entity.HasComponent(type))
                            Entity.AddBehavior(type);
                        break;

                    case EntityComponentSetupPolicy.OverrideFromActor:
                        Entity.AddBehavior(type);
                        break;

                    case EntityComponentSetupPolicy.SetAsInActor:
                        Entity.AddBehavior(type);
                        break;
                }
            }
        }

        private void UpdateParent()
        {
            if (Parent != null)
                Parent._childs.Remove(this);

            Parent = gameObject.GetComponentInParent<Actor>(true, false);

            if (Parent != null)
                Parent._childs.Add(this);
        }

        private void OnTransformParentChanged()
        {
            UpdateParent();
        }

        public T GetActorComponent<T>() where T : ActorComponent
        {
            Type type = typeof(T);
            ActorComponent result = GetActorComponent(type);

            return (T)result;
        }

        public ActorComponent GetActorComponent(Type type)
        {
            ActorComponent result = null;

            _actorComponents.TryGetValue(type, out result);

            return result;
        }

        public IReadOnlyList<ActorComponent> GetAllComponents() => _allComponents;

        private void RegisterActorComponent(ActorComponent component, bool addToAllList)
        {
            Type type = component.GetType();

            if (_actorComponents.ContainsKey(type))
                return;

            component.OnBeforeDestroy += HandleComponentDestroyed;
            _actorComponents.Add(type, component);
            _actorComponentsAndData.Add(type, component);

            if (component is IActorDataProvider actorDataProvider && actorDataProvider is not IActorData)
            {
                _actorComponentsAndData.Add(actorDataProvider.GetActorDataType(), actorDataProvider.GetActorData());
            }

            if (addToAllList)
                _allComponents.Add(component);

            OnComponentAdded.Invoke(this, component);
        }

        private void UnregisterActorComponent(ActorComponent component)
        {
            Type type = component.GetType();

            if (_actorComponents.Remove(type))
            {
                if (component is IActorDataProvider actorDataProvider)
                {
                    _actorComponentsAndData.Remove(actorDataProvider.GetActorDataType());
                }

                component.OnBeforeDestroy -= HandleComponentDestroyed;
                _allComponents.Remove(component);
                OnComponentRemoved(this, component);
            }
        }

        public IReadOnlyDictionary<Type, object> GetAllActorComponentsAndData() => _actorComponentsAndData;

        public IEnumerable<IActorDataProvider> GetAllActorDataProviders()
        {
            foreach (ActorComponent component in _allComponents)
                if (component is IActorDataProvider actorDataProvider)
                    yield return actorDataProvider;
        }

        public IEnumerable<IActorData> GetAllActorData()
        {
            foreach (IActorDataProvider dataProvider in GetAllActorDataProviders())
                yield return dataProvider.GetActorData();
        }

        public T GetActorData<T>() where T : class, IActorData
        {
            if (_actorComponentsAndData.TryGetValue(typeof(T), out var data))
                return (T)data;
            else
                return default(T);
        }

        private void HandleComponentDestroyed(EasyCSBehavior behavior)
        {
            UnregisterActorComponent((ActorComponent)behavior);
        }

        protected override void HandleDestroy()
        {
            if (Entity.IsEmpty == false)
                EntityContainer.InternalDetachActorFromEntity(Entity, this);

            EntityContainer = null;
            Entity = Entity.Empty;

            _actorComponents.Clear();
            _actorComponentsAndData.Clear();
            _allComponents.Clear();
            _entityComponentsMissing.Clear();

            if (Parent != null)
            {
                Parent._childs.Remove(this);
                Parent = null;
            }
            foreach (Actor child in _childs)
            {
                child.Parent = null;
                child.UpdateParent();
            }
        }

#if UNITY_EDITOR
        public enum EditorDependenciesType
        {
            Entity,
            ActorComponent,
            All
        }

        private bool EditorShowWarningDisabledEntityNotSet() => Entity.IsEmpty;
        private bool EditorShowWarningDisabledEntityNotAlive() => Entity.IsEmpty == false && Entity.IsAlive == false;

        private bool EditorHasMissingEntityComponentsDependencies() => EditorEntityComponentsMissingRequired.Count > 0;

        private bool EditorHasMissingActorComponentsDependencies()
        {
            EditorUpdateMissingActorDependenciesList();

            return _editorActorComponentsMissing != null && _editorActorComponentsMissing.Count > 0;
        }

        [Button("Add Missing Actor Components"), ShowIf("EditorHasMissingActorComponentsDependencies"), ShowInEditMode]
        private void EditorAddAllMissingActorComponentsDependencies()
        {
            foreach (var component in _allComponents)
                EditorAddMissingDependenciesForComponent(component, EditorDependenciesType.ActorComponent);
        }

        [Button("Add Missing Entity Components"), ShowIf("EditorHasMissingEntityComponentsDependencies"), ShowInEditMode]
        private void EditorAddAllMissingEntityComponentsDependencies()
        {
            foreach (var component in _allComponents)
                EditorAddMissingDependenciesForComponent(component, EditorDependenciesType.Entity);
        }

        [Button("Remove Unused Components"), GUIColor("yellow"), ShowIf("EditorHasUnusedDependencies"), ShowInEditMode]
        private void EditorRemoveAllUnusedDependencies()
        {
            UnityEditor.Undo.RecordObject(gameObject, "Remove Unused Components");
            foreach (var component in _editorUnusedDependencies)
            {
                UnityEditor.Undo.DestroyObjectImmediate(component);
            }
        }

        [Button("Kill Entity"), GUIColor("yellow"), ShowIf("EditorShowKillEntityButton"), ShowInPlayMode]
        private void EditorKillEntity()
        {
            _editorKillEntityConfirm = true;
        }

        private bool _editorKillEntityConfirm;

        [Button("Kill Entity"), GUIColor("orange"), ShowIf("_editorKillEntityConfirm"), ShowInPlayMode]
        private void EditorKillEntityConfirm()
        {
            Entity.Kill();
            _editorKillEntityConfirm = false;
        }

        [Button("Cancel"), GUIColor("lime"), ShowIf("_editorKillEntityConfirm"), ShowInPlayMode]
        private void EditorKillEntityCancel()
        {
            _editorKillEntityConfirm = false;
        }

        private bool EditorShowKillEntityButton() => Entity.IsAlive && !_editorKillEntityConfirm;

        public void EditorAddMissingDependenciesForComponent(ActorComponent component, EditorDependenciesType dependenciesType)
        {
            List<Type> _missingList = EditorGetMissingDependenciesForComponent(component, dependenciesType, true);

            foreach (var type in _missingList)
            {
                if (type == null)
                {
                    Debug.LogWarning($"[EasyCS] Type not found for missing dependency: {type}");
                    continue;
                }

                Type componentType = null;
                bool added = false;

                if (typeof(IEntityData).IsAssignableFrom(type))
                {
                    if (dependenciesType != EditorDependenciesType.All &&
                        dependenciesType != EditorDependenciesType.Entity)
                        continue;

                    if (Attribute.IsDefined(type, typeof(RuntimeOnlyAttribute)))
                        continue;

                    componentType = EntityDataProviderFinder.FindEntityDataProviderMatching(type);

                    if (componentType == null)
                    {
                        Debug.LogWarning($"[EasyCS] No provider found for required type: {type.FullName}");
                        continue;
                    }
                }
                else if (typeof(ActorComponent).IsAssignableFrom(type))
                {
                    if (dependenciesType != EditorDependenciesType.All &&
                        dependenciesType != EditorDependenciesType.ActorComponent)
                        continue;

                    componentType = type;
                }

                Component existing = gameObject.GetComponent(componentType);

                if (existing == null)
                {
                    UnityEditor.Undo.RecordObject(gameObject, $"Add {componentType.Name}");
                    existing = UnityEditor.Undo.AddComponent(gameObject, componentType);
                    added = true;
                }


                if (added)
                {
                    Debug.Log($"[EasyCS] Auto-attached: {componentType.Name} to satisfy dependency: {type.Name} for {component.name}");
                    UnityEditor.EditorUtility.SetDirty(gameObject);
                }
            }

            EditorUpdateMissingActorDependenciesList();
        }

        public void EditorOnComponentAdded()
        {
            OnValidate();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            _allComponents = transform.GetComponentsInChildrenUntil<ActorComponent, Actor>();

            // Sort for proper initialization
            _allComponents.Sort((a, b) =>
            {
                int GetPriority(Component c)
                {
                    if (c is IEntityDataProvider) return 0;
                    if (c is IEntityBehaviorProvider) return 1;
                    return 2;
                }

                return GetPriority(a).CompareTo(GetPriority(b));
            });

            foreach (var component in _allComponents)
                component.SetActor(this);

            EditorValidateAllComponentDependencies();
            ValidateSetup();
            EditorUpdateUnusedDependencies();
        }

        public void EditorSetEntityProvider(EntityProvider entityProvider)
        {
            _entityProvider = entityProvider;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void EditorUpdateUnusedDependencies()
        {
            _editorUnusedDependencies = EditorGetUnusedActorComponentsAll()
                .Where(c =>
                typeof(IEntityDataProvider).IsAssignableFrom(c.GetType()) ||
                typeof(IActorDataProvider).IsAssignableFrom(c.GetType()))
                .ToList();
        }

        private void ValidateSetup()
        {
            if (transform.root.GetComponent<EntityEditorComponentFlag>() != null)
                return;

            if (gameObject.scene.name != null)
            {
                UnityEditor.SceneManagement.PrefabStage prefabStage =
                    UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();

                if (prefabStage != null && prefabStage.scene.name == gameObject.scene.name)
                    return;

                bool added;
                _entityProvider = gameObject.TryGetElseSetComponent<EntityProvider>(out added);

                if (added)
                    UnityEditor.EditorUtility.SetDirty(gameObject);
            }
        }

        private void EditorValidateAllComponentDependencies()
        {
            EditorValidateEntityComponentsDependencies();
            EditorValidateActorComponentsDependencies();
        }

        private void EditorUpdateMissingActorDependenciesList()
        {
            _editorActorComponentsMissing = EditorGetMissingDependenciesNamesForAllComponents(EditorDependenciesType.ActorComponent);
        }

        private void EditorValidateEntityComponentsDependencies()
        {
            var existingDataTypesFromProviders = new HashSet<Type>(
                _allComponents
                    .OfType<IEntityDataProvider>()
                    .Select(p => p.GetEntityComponentType()));

            var existingDataTypesAll = new HashSet<Type>(
                existingDataTypesFromProviders
                .Concat(_entityComponentsMissing.Select(c => c.GetType()))
            );

            HashSet<Type> requiredTypesAll = new HashSet<Type>();

            bool componentsChanged = false;
            bool missingListChanged = false;

            foreach (var component in _allComponents)
            {
                if (component == null) continue;

                Type componentType = null;

                if (component is IEntityBehaviorProvider entityBehaviorProvider)
                    componentType = entityBehaviorProvider.GetEntityComponentType();
                else
                    componentType = component.GetType();

                var requiredTypes = Injector.GetRequiredInjectionTypes(componentType);

                foreach (var depType in requiredTypes)
                {
                    if (requiredTypesAll.Contains(depType) == false)
                        requiredTypesAll.Add(depType);

                    if (typeof(IEntityData).IsAssignableFrom(depType))
                    {
                        if (existingDataTypesAll.Contains(depType)) continue;

                        if (_entityComponentsMissing.All(c => c.GetType() != depType))
                        {
                            var instance = (IEntityComponent)Activator.CreateInstance(depType);
                            _entityComponentsMissing.Add(instance);
                            existingDataTypesAll.Add(depType);
                            missingListChanged = true;
                            Debug.Log($"[EasyCS] Marked missing IEntityData: {depType.Name} for {component.GetType().Name}", gameObject);
                        }
                    }
                }
            }


            _entityComponentsMissing.RemoveAll(
                c => !requiredTypesAll.Contains(c.GetType()) ||
                existingDataTypesFromProviders.Contains(c.GetType()));

            if (componentsChanged)
            {
                OnValidate();
            }
        }

        private void EditorValidateActorComponentsDependencies()
        {
            EditorUpdateMissingActorDependenciesList();
        }

        

        public List<string> EditorGetMissingDependenciesNamesForAllComponents(EditorDependenciesType dependenciesType)
        {
            List<string> missingList = EditorGetMissingDependenciesForAllComponents(dependenciesType)
                .Select(t => t.FullName)
                .ToList();

            return missingList;
        }

        public List<Type> EditorGetMissingDependenciesForAllComponents(EditorDependenciesType dependenciesType)
        {
            List<Type> missingList = new List<Type>();

            foreach (var component in _allComponents)
                missingList.AddRange(EditorGetMissingDependenciesForComponent(component, dependenciesType, false));

            return missingList;
        }

        public List<string> EditorGetMissingDependenciesNamesForComponent(
            ActorComponent component, EditorDependenciesType dependenciesType, bool ignoreRuntimeComponents)
        {
            return EditorGetMissingDependenciesForComponent(component, dependenciesType, ignoreRuntimeComponents)
                .Select(t => t.FullName)
                .ToList();
        }

        public List<Type> EditorGetMissingDependenciesForComponent(
            ActorComponent component, EditorDependenciesType dependenciesType, bool ignoreRuntimeComponents)
        {
            List<Type> missing = new();
            if (component == null)
                return missing;

            List<Type> requiredTypes = null;

            if (component is IEntityComponentProvider entityComponentProvider)
                requiredTypes = Injector.GetRequiredInjectionTypes(entityComponentProvider.GetEntityComponentType());
            else
                requiredTypes = Injector.GetRequiredInjectionTypes(component.GetType());

            var existingComponentTypes = new HashSet<Type>(_allComponents.Select(c => c.GetType()));
            var existingDataTypes = new HashSet<Type>(
                _allComponents
                    .OfType<IEntityComponentProvider>()
                    .Select(p => p.GetEntityComponentType())
            );

            foreach (var depType in requiredTypes)
            {
                if (typeof(ActorComponent).IsAssignableFrom(depType))
                {
                    if (dependenciesType == EditorDependenciesType.All ||
                        dependenciesType == EditorDependenciesType.ActorComponent)
                    {
                        if (!existingComponentTypes.Contains(depType))
                            missing.Add(depType);
                    }
                }
                else if (typeof(IEntityData).IsAssignableFrom(depType))
                {
                    if (ignoreRuntimeComponents && Attribute.IsDefined(depType, typeof(RuntimeOnlyAttribute)))
                        continue;

                    if (dependenciesType == EditorDependenciesType.All ||
                        dependenciesType == EditorDependenciesType.Entity)
                    {
                        if (!existingDataTypes.Contains(depType))
                            missing.Add(depType);
                    }
                }
            }

            return missing;
        }

        public List<ActorComponent> EditorGetUnusedActorComponentsAll()
        {
            HashSet<Type> requiredTypes = new();

            foreach (var component in _allComponents)
            {
                if (component == null) continue;

                List<Type> dependencies = null;

                if (component is IEntityComponentProvider entityComponentProvider)
                    dependencies = Injector.GetRequiredInjectionTypes(entityComponentProvider.GetEntityComponentType());
                else
                    dependencies = Injector.GetRequiredInjectionTypes(component.GetType());

                foreach (var dep in dependencies)
                    requiredTypes.Add(dep);
            }

            List<ActorComponent> unused = new();

            foreach (var component in _allComponents)
            {
                if (component == null) continue;

                var type = component.GetType();
                Type exposedType = null;

                if (component is IEntityComponentProvider entityComponentProvider)
                    exposedType = entityComponentProvider.GetEntityComponentType();
                else if (component is IActorDataProvider actorDataProvider)
                    exposedType = actorDataProvider.GetActorDataType();

                bool isUsed = requiredTypes.Contains(type) || (exposedType != null && requiredTypes.Contains(exposedType));

                if (!isUsed)
                    unused.Add(component);
            }

            return unused;
        }

        public List<ActorComponent> EditorGetUnusedActorComponentsForComponent(ActorComponent component)
        {
            List<ActorComponent> unused = new();
            if (component == null)
                return unused;

            var requiredTypes = new HashSet<Type>(Injector.GetRequiredInjectionTypes(component.GetType()));

            foreach (var other in _allComponents)
            {
                if (other == null || other == component)
                    continue;

                var type = other.GetType();
                Type exposedType = null;

                if (other is IEntityComponentProvider provider)
                    exposedType = provider.GetEntityComponentType();

                bool requiredByType = requiredTypes.Contains(type);
                bool requiredByProvided = exposedType != null && requiredTypes.Contains(exposedType);

                if (!requiredByType && !requiredByProvided)
                    unused.Add(other);
            }

            return unused;
        }

        public bool EditorHasUnusedDependencies() => _editorUnusedDependencies.Count > 0;

        public List<ActorComponent> EditorGetAllComponentsSerialized() => _allComponents;
        public List<IEntityComponent> EditorGetEntityComponentsMissingSerialized() => _entityComponentsMissing;
#endif
    }
}