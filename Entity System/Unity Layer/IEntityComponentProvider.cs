
using System;

namespace EasyCS
{
    public interface IEntityComponentProvider
    {
        public IEntityComponent GetEntityComponent();
        public Type GetEntityComponentType();
        public IEntityComponent AddEntityComponent(Entity entity)
        {
            IEntityComponent component = GetEntityComponent();
            return entity.AddComponent(component);
        }
        public IEntityComponent SetEntityComponent(Entity entity)
        {
            IEntityComponent component = GetEntityComponent();
            Type type = GetEntityComponentType();

            entity.RemoveComponent(type);
            entity.AddComponent(component);

            return component;
        }
        public bool RemoveEntityComponent(Entity entity)
        {
            Type type = GetEntityComponentType();
            IEntityComponent componentOnEntity = entity.GetComponent(type);
            IEntityComponent component = GetEntityComponent();

            if (component.Equals(componentOnEntity))
            {
                entity.RemoveComponent(type);
                return true;
            }

            return false;
        }
    }
}
