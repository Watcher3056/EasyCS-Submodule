using TriInspector;
using UnityEngine;

namespace EasyCS
{
    public class EntityPredefinedScriptableObject : ScriptableObject, IHasEntity
    {
        [SerializeField, HideLabel]
        private EntityPredefined _entityPredefined;

        public Entity Entity => _entityPredefined.Entity;

        #if UNITY_EDITOR
        public void EditorInitialize(EntityPredefined entityPredefined)
        {
            _entityPredefined = entityPredefined;
        }
        #endif
    }
}