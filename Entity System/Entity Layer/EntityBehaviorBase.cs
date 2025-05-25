using EasyCS.EventSystem;
using System;

namespace EasyCS
{
    public abstract class EntityBehaviorBase : IEntityBehavior, IDisposable, IHasContainer
    {
        public IEasyCSObjectResolver EasyCsContainer { get; private set; }
        public IEventSystem EventSystem { get; private set; }

        public Entity Entity
        {
            get => _entity;
            set => _entity = value;
        }

        private Entity _entity;
        


        public void SetupContainer(IEasyCSObjectResolver container)
        {
            EasyCsContainer = container;
            EventSystem = EasyCsContainer.Resolve<DefaultEventSystem>();

            HandleSetupContainer();
        }

        public void SetEntity(Entity entity)
        {
            _entity = entity;
            EventSystem.TrySubscribe(this, Entity);
        }

        public void Dispose()
        {
            EventSystem.TryUnsubscribe(this, Entity);

            HandleDestroy();
        }

        protected virtual void HandleSetupContainer() { }
        protected virtual void HandleDestroy() { }
    }
}