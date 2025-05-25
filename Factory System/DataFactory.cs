using System;
using UnityEngine;

namespace EasyCS
{
    public interface IFactory
    {
        public object GetProduct();
        public Type GetProductType();
    }
    public abstract class DataFactory : ScriptableObject, IGUID, IFactory
    {
        [SerializeField]
        private ComponentGUID guid;
        public ComponentGUID GUID => guid;

        public abstract object GetProduct();

        public abstract Type GetProductType();
    }
}
