namespace EasyCS
{
    public interface IHasContainer
    {
        public IEasyCSObjectResolver EasyCsContainer { get; }
        public void SetupContainer(IEasyCSObjectResolver container);
    }
}