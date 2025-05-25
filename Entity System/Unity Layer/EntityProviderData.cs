using System;
using TriInspector;
using UnityEngine;

namespace EasyCS
{
    [Serializable]
    public class EntityProviderData
    {
        public enum Source
        {
            Embedded,
            Asset
        }

        [SerializeField]
        private Source _source = Source.Embedded;

        [SerializeField, Required, ShowIf("_source", Source.Asset)]
        private EntityPredefinedScriptableObject _entityPredefinedScriptable;

        [SerializeField, ShowIf("_source", Source.Embedded)]
        private EntityPredefined _entityPredefinedEmbedded;

        public Entity Entity
        {
            get
            {
                if (_source == Source.Embedded)
                    return _entityPredefinedEmbedded.Entity;
                else
                    return _entityPredefinedScriptable.Entity;
            }
        }

#if UNITY_EDITOR
        public void SetEntityPredefinedEditorOnly(EntityPredefined predefinedEntity)
        {
            _source = Source.Embedded;
            _entityPredefinedEmbedded = predefinedEntity;
        }

        public void SetEntityPredefinedAssetEditorOnly(EntityPredefinedScriptableObject predefinedAsset)
        {
            _source = Source.Asset;
            _entityPredefinedScriptable = predefinedAsset;
        }
#endif
    }
}
