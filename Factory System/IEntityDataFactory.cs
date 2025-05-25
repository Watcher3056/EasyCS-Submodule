namespace EasyCS
{
    public interface IEntityDataFactory : IFactory, IGUID
    {
        object IFactory.GetProduct() => GetProduct();
        public new IEntityData GetProduct()
        {
            Entity entity = new Entity();
            IEntityData result = GetProduct(entity);

            return result;
        }
        public IEntityData GetProduct(Entity entity);
    }
}
