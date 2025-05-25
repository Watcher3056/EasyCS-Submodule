#if VCONTAINER_ENABLED && !ZENJECT_ENABLED
using VContainer;
using VContainer.Unity;

namespace EasyCS
{
    public partial class EasyCSInstaller : IInstaller, IInitializable
    {
        public void Install(IContainerBuilder builder)
        {
            EasyCSContainer easyCSContainer = new EasyCSContainer(builder);
            SetupEasyCSContainer(easyCSContainer);

            builder.RegisterEntryPoint((resolver) => this, Lifetime.Singleton);
            builder.RegisterEntryPoint((resolver) =>
            {
                resolver.Inject(easyCSContainer);
                return easyCSContainer;
            }, Lifetime.Singleton).AsSelf();
        }

        public void Initialize()
        {
            if (_injectSceneInstances)
                InjectSceneInstances();
        }
    }
}
#endif
