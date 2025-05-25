#if VCONTAINER_ENABLED && !ZENJECT_ENABLED
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace EasyCS
{
    public class LifeTimeScopeWithInstallers : LifetimeScope
    {
        [SerializeField]
        private List<InterfaceReference<IInstaller>> _installers = new List<InterfaceReference<IInstaller>>();

        protected override void Configure(IContainerBuilder builder)
        {
            foreach (var installer in _installers)
                installer.Value.Install(builder);
        }
    }
}
#endif