using EasyCS.EventSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EasyCS
{
    public abstract class EasyCSBehavior : MonoBehaviour, IHasContainer, IAwake, IActive
    {
        public IEasyCSObjectResolver EasyCsContainer { get; private set; }
        public IEventSystem EventSystem { get; private set; }

        public virtual bool IsActive => gameObject.activeInHierarchy && enabled;
        public event Action<EasyCSBehavior> OnBeforeDestroy = (behavior) => { };
        private LifetimeLoopSystem _lifetimeLoopSystem;
        private bool _hasAwakeBeenCalled;
        private bool _hasAwakeBeenExecuted;

        public void SetupContainer(IEasyCSObjectResolver container)
        {
            InternalSetupContainer(container);
        }

        internal virtual void InternalSetupContainer(IEasyCSObjectResolver container)
        {
            EasyCsContainer = container;
            EventSystem = container.Resolve<DefaultEventSystem>();
            _lifetimeLoopSystem = container.Resolve<LifetimeLoopSystem>();

            HandleSetupContainer();

            _lifetimeLoopSystem.TryAdd(this);
        }

        protected virtual void HandleSetupContainer()
        {
        }

        public void OnAwake()
        {
            if (enabled)
            {
                HandleAwake();
                HandleOnEnable();

                _hasAwakeBeenExecuted = true;
            }

            _hasAwakeBeenCalled = true;
        }

        protected virtual void HandleAwake()
        {
        }

        private void OnDestroy()
        {
            OnBeforeDestroy.Invoke(this);

            try
            {
                HandleDestroy();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _lifetimeLoopSystem.TryRemove(this);
                _lifetimeLoopSystem = null;
                EasyCsContainer = null;
            }
        }

        protected virtual void HandleDestroy()
        {
        }

        private void OnEnable()
        {
            InternalOnEnable();
        }

        /// <summary>
        /// DO NOT override unless you know what you are doing
        /// </summary>
        internal virtual void InternalOnEnable()
        {
            if (_hasAwakeBeenCalled)
            {
                if (_hasAwakeBeenExecuted == false)
                {
                    HandleAwake();
                    _hasAwakeBeenExecuted = true;
                }

                HandleOnEnable();
            }
        }

        private void OnDisable()
        {
            InternalOnDisable();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            ValidateSetup();
        }

        protected virtual void Reset()
        {
            EditorSortComponentOrder();
        }

        private void ValidateSetup()
        {
            if (transform.root.GetComponent<EntityEditorComponentFlag>() != null)
                return;

            if (gameObject.scene.name == null)
            {
                GameObject root = transform.root.gameObject;

                if (!root.TryGetComponent<PrefabRootData>(out _))
                {
                    string path = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);

                    if (!string.IsNullOrEmpty(path))
                    {
                        GameObject prefabAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

                        if (prefabAsset != null)
                        {
                            bool added;
                            prefabAsset.TryGetElseSetComponent<PrefabRootData>(out added);

                            if (added)
                            {
                                UnityEditor.EditorUtility.SetDirty(prefabAsset);
                                //PrefabUtility.SavePrefabAsset(prefabAsset);
                                Debug.Log($"[EasyCS] Saved PrefabRootData to prefab asset: {prefabAsset.name}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[EasyCS] Could not load prefab asset at path: {path}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[EasyCS] Prefab path not found for GameObject: {root.name}");
                    }
                }
            }
        }

        [ContextMenu("EasyCS/Sort Component Order")]
        private void EditorSortComponentOrder()
        {
            // Check if this is a prefab instance in the scene, and not in prefab edit mode
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(gameObject) &&
                UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null)
            {
                Debug.LogWarning($"[EasyCS] Cannot sort components: {gameObject.name} is a prefab instance. Open prefab in edit mode to sort.");
                return;
            }

            var allComponents = gameObject.GetComponents<Component>().ToList();
            if (allComponents.Count <= 1) return;

            List<Component> sorted = new();

            Component prefabRoot = GetComponent<PrefabRootData>();
            Component entityProvider = GetComponent<EntityProvider>();
            Component entityTemplateProvider = GetComponent<EntityTemplateProvider>();
            Component actor = GetComponent<Actor>();

            // Collect categorized components
            var behaviors = new List<Component>();
            var datas = new List<Component>();
            var actorData = new List<Component>();
            var actorComponents = new List<Component>();
            var untouched = new List<Component>();

            foreach (var comp in allComponents)
            {
                if (comp == null || comp is Transform || comp == prefabRoot || comp == actor)
                    continue;

                if (comp is IEntityBehaviorProvider) behaviors.Add(comp);
                else if (comp is IEntityDataProvider) datas.Add(comp);
                else if (comp is IActorDataProvider) actorData.Add(comp);
                else if (comp is ActorComponent) actorComponents.Add(comp);
                else untouched.Add(comp);
            }

            // Compose new sorted order
            if (prefabRoot) sorted.Add(prefabRoot);
            if (entityProvider) sorted.Add(entityProvider);
            if (entityTemplateProvider) sorted.Add(entityTemplateProvider);
            if (actor) sorted.Add(actor);

            sorted.AddRange(actorData.Except(sorted));
            sorted.AddRange(datas.Except(sorted));
            sorted.AddRange(behaviors.Except(sorted));
            sorted.AddRange(actorComponents.Except(sorted));
            sorted.AddRange(untouched); // maintain order of unrelated components

            sorted.RemoveAll(comp =>
                comp.hideFlags.HasFlag(HideFlags.HideAndDontSave) ||
                comp.hideFlags.HasFlag(HideFlags.HideInInspector) ||
                comp.hideFlags.HasFlag(HideFlags.HideInHierarchy));

            // Reorder using MoveComponentUp
            foreach (var component in sorted)
            {
                if (component == null)
                    continue;

                if (behaviors.Contains(component))
                    UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(component, false);

                int sortIterationsLimit = 1000;

                for (int i = 0; i < sortIterationsLimit; i++)
                {
                    var currentList = gameObject.GetComponents<Component>().ToList();
                    currentList.RemoveAll(comp =>
                        comp.hideFlags.HasFlag(HideFlags.HideAndDontSave) ||
                        comp.hideFlags.HasFlag(HideFlags.HideInInspector) ||
                        comp.hideFlags.HasFlag(HideFlags.HideInHierarchy));

                    int currentIndex = currentList.IndexOf(component);

                    if (currentIndex <= 1) // don't move above Transform
                        break;

                    var previous = currentList[currentIndex - 1];


                    if (sorted.IndexOf(previous) > sorted.IndexOf(component))
                        UnityEditorInternal.ComponentUtility.MoveComponentUp(component);
                    else
                        break;

                    if (i + 1 == sortIterationsLimit)
                        Debug.LogError("[EasyCS]: Cannot sort. Reason: Sort iterations limit was achieved. Aborted.");
                }
            }

            UnityEditor.ActiveEditorTracker.sharedTracker.ForceRebuild();

            Debug.Log($"[EasyCS] Components sorted on {gameObject.name}.");
        }

#endif

        /// <summary>
        /// DO NOT override unless you know what you are doing
        /// </summary>
        internal virtual void InternalOnDisable()
        {
            if (_hasAwakeBeenCalled)
                HandleDisable();
        }

        protected virtual void HandleOnEnable()
        {
        }

        protected virtual void HandleDisable()
        {
        }
    }
}