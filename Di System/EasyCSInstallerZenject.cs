#if ZENJECT_ENABLED
using Zenject;

namespace EasyCS
{
    public partial class EasyCSInstaller : IInitializable
    {

        public override void InstallBindings()
        {
            EasyCSContainer easyCsContainer = new EasyCSContainer(Container);
            SetupEasyCSContainer(easyCsContainer);

            Container.BindInterfacesAndSelfTo<EasyCSInstaller>().FromInstance(this).AsSingle();
            Container.BindInterfacesAndSelfTo<EasyCSContainer>().FromInstance(EasyCsContainer).AsSingle();
        }

        public void Initialize()
        {
            if (_injectSceneInstances)
                InjectSceneInstances();
        }
    }
}
#endif
