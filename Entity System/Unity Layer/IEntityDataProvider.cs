namespace EasyCS
{
    public interface IEntityDataProvider : IBehaviour, IEntityComponentProvider
    {
        public Actor Actor { get; }
        public Source Source { get; }
        public IEntityDataFactory DataFactory { get; }
        public IEntityData EntityComponent { get; }

        public bool TrySetFactory(IEntityDataFactory factory);

#if UNITY_EDITOR
        public IEntityDataFactory EditorGetFactory();
        public IEntityData EditorGetComponent();
        public void EditorSetFactory(IEntityDataFactory factory);
        public void EditorSetData(IEntityData component);
        public void EditorSetSource(Source source);
#endif
    }
}