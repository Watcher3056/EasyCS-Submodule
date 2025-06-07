using System.IO;
using TriInspector;
using UnityEditor;
using UnityEngine;

namespace EasyCS
{
    [DeclareHorizontalGroup("Buttons")]
    [IconClass(ConstantsIcons.IconEntity)]
    public class EntityProvider : EasyCSBehavior, IHasEntity
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
                RegisterIfNeeded();

                return GetEntityNoCheck();
            }
        }

        private EntityContainer _entityContainer;

        private bool _wasRegistered;

        public Entity GetEntityNoCheck()
        {
            if (_source == Source.Embedded)
                return _entityPredefinedEmbedded.Entity;
            else
                return _entityPredefinedScriptable.Entity;
        }

        protected override void HandleSetupContainer()
        {
            _entityContainer = EasyCsContainer.Resolve<EntityContainer>();
        }
        
        protected override void HandleAwake()
        {
            RegisterIfNeeded();
        }

        private void RegisterIfNeeded()
        {
            if (_wasRegistered == false)
            {
                Entity entity = GetEntityNoCheck();

                _entityContainer.RegisterEntity(entity);
                _wasRegistered = true;
            }
        }
        
#if UNITY_EDITOR
        [Button, ShowIf("_source", Source.Embedded), Group("Buttons"), HideInPlayMode]
        private void ConvertToAsset()
        {
            const string defaultFolder = "Assets/Entity Predefined";

            if (!Directory.Exists(defaultFolder))
                Directory.CreateDirectory(defaultFolder);

            string path = $"{defaultFolder}/Entity Predefined {name}.asset";
            CreateAndSavePredefinedAsset(path);
        }

        [Button, ShowIf("_source", Source.Embedded), Group("Buttons"), HideInPlayMode]
        private void ConvertToAssetAs()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Export EntityPredefined",
                $"{name}_Predefined",
                "asset",
                "Choose location to save the EntityPredefined asset");

            if (!string.IsNullOrEmpty(path))
                CreateAndSavePredefinedAsset(path);
        }

        private void CreateAndSavePredefinedAsset(string path)
        {
            var scriptableObject = ScriptableObject.CreateInstance<EntityPredefinedScriptableObject>();
            scriptableObject.EditorInitialize(_entityPredefinedEmbedded);

            AssetDatabase.CreateAsset(scriptableObject, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"EntityPredefined asset saved to: {path}");

            _source = Source.Asset;
            _entityPredefinedScriptable = scriptableObject;

            EditorUtility.SetDirty(this);
        }

        public void SetEntityPredefinedEditorOnly(EntityPredefined predefinedEntity)
        {
            _source = Source.Embedded;
            _entityPredefinedEmbedded = predefinedEntity;

            EditorUtility.SetDirty(this);
        }

        public void SetEntityPredefinedAssetEditorOnly(EntityPredefinedScriptableObject predefinedAsset)
        {
            _source = Source.Asset;
            _entityPredefinedScriptable = predefinedAsset;

            EditorUtility.SetDirty(this);
        }
#endif
    }
}