using UnityEngine;

namespace EasyCS
{
    public interface IBehaviour : IComponent
    {
        public Behaviour Behaviour { get => this as Behaviour; }
        Component IComponent.Component => Behaviour;
    }
}
