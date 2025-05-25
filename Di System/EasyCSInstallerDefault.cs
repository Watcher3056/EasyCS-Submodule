#if !ZENJECT_ENABLED && !VCONTAINER_ENABLED

namespace EasyCS
{
    public partial class EasyCSInstaller
    {
        private void Awake()
        {
            EasyCSContainer easyCSContainer = new EasyCSContainer();
            SetupEasyCSContainer(easyCSContainer);

            if (_injectSceneInstances)
                InjectSceneInstances();

            CommonAwake();
        }

        private void Start() => CommonStart();
        private void Update() => CommonUpdate();
        private void FixedUpdate() => CommonFixedUpdate();
        private void LateUpdate() => CommonLateUpdate();
        private void OnDestroy() => CommonDestroy();
    }
}
#endif
