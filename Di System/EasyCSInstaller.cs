using UnityEngine;
using System.Collections.Generic;
using EasyCS.Groups;
using EasyCS.EventSystem;

[assembly: TriInspector.DrawWithTriInspector]
namespace EasyCS
{
    [RequireComponent(typeof(EntityProvider))]
    public partial class EasyCSInstaller :
#if ZENJECT_ENABLED
         Zenject.MonoInstaller
#else
        MonoBehaviour
#endif
    {
        [SerializeField]
        private bool _injectSceneInstances = true;

        public EasyCSContainer EasyCsContainer { get; protected set; }

        protected void SetupEasyCSContainer(EasyCSContainer easyCSContainer)
        {
            EasyCsContainer = easyCSContainer;

            Entity entity = GetComponent<EntityProvider>().GetEntityNoCheck();
            var eventSystem = new DefaultEventSystem();
            var entityContainer = new EntityContainer(entity);
            var entityRootRegistry = EntityRootRegistry.Instance;
            var groupsSystem = new GroupsSystem();
            var lifetimeSystem = new LifetimeLoopSystem();

            EasyCsContainer.Register(eventSystem);
            EasyCsContainer.Register(entityContainer);
            EasyCsContainer.Register(entityRootRegistry);
            EasyCsContainer.Register(groupsSystem);
            EasyCsContainer.Register(lifetimeSystem);
        }

        protected void InjectSceneInstances()
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            List<IHasContainer> found = new();

            foreach (var root in roots)
                found.AddRange(root.GetComponentsInChildren<IHasContainer>(true));

            InitializationHelper.SortForInitialization(found);

            foreach (var instance in found)
                EasyCsContainer.HandleInstantiate(instance);
        }

        protected void CommonAwake() => EasyCsContainer.OnAwake();
        protected void CommonStart() => EasyCsContainer.OnStart();
        protected void CommonUpdate() => EasyCsContainer.OnUpdate(Time.deltaTime);
        protected void CommonFixedUpdate() => EasyCsContainer.OnFixedUpdate(Time.fixedDeltaTime);
        protected void CommonLateUpdate() => EasyCsContainer.OnLateUpdate(Time.deltaTime);
        protected void CommonDestroy() => EasyCsContainer.Dispose();
    }
}
