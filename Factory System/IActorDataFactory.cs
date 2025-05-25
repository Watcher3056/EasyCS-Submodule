
namespace EasyCS
{
    public interface IActorDataFactory : IFactory, IGUID
    {
        object IFactory.GetProduct() => GetProduct();
        public new IActorData GetProduct();
    }
}
