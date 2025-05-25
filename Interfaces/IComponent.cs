using UnityEngine;

namespace EasyCS
{
    public interface IComponent
    {
        public Component Component { get => this as Component; }
        public GameObject gameObject => Component.gameObject;
    }
}
