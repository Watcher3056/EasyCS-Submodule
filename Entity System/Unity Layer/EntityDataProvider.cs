using System;
using System.IO;
using TriInspector;
using UnityEngine;

namespace EasyCS
{
    public enum Source
    {
        Embedded,
        Asset
    }

    [DeclareHorizontalGroup("Buttons")]
    [IconClass(ConstantsIcons.IconEntity)]
    public abstract class EntityDataProvider<TFactory, TComponent> : ActorComponent, IEntityDataProvider
        where TFactory : ScriptableObject, IEntityDataFactory
        where TComponent : IEntityData
    {
        [SerializeField]
        private Source _source;
#if ODIN_INSPECTOR
        [SerializeField, Sirenix.OdinInspector.Required, Sirenix.OdinInspector.ShowIf("_source", Source.Asset)]
#else
        [SerializeField, Required, ShowIf("_source", Source.Asset)]
#endif
        private TFactory _factory;
#if ODIN_INSPECTOR
        [SerializeField, Sirenix.OdinInspector.ShowIf("_source", Source.Embedded), Sirenix.OdinInspector.HideLabel, Sirenix.OdinInspector.InlineProperty]
#else
        [SerializeField, ShowIf("_source", Source.Embedded), HideLabel, InlineProperty]
#endif
        private TComponent _component;

        public IEntityDataFactory DataFactory => _source == Source.Asset ? _factory : null;
        public IEntityData EntityComponent => _addedComponent;
        public TFactory ComponentFactoryConcrete => _source == Source.Asset ? _factory : null;
        public TComponent EntityComponentConcrete => _addedComponent;
        public Source Source => _source;

        private TComponent _addedComponent;

        private static Type ComponentType { get; } = typeof(TComponent);

        public bool TrySetFactory(IEntityDataFactory factory)
        {
            if (factory is TFactory factoryConcrete)
            {
                SetFactory(factoryConcrete);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void SetFactory(TFactory factory)
        {
            this._factory = factory;
            _source = Source.Asset;

            SetupComponent();

            if (Actor != null && Actor.Entity.IsAlive)
                ((IEntityComponentProvider)this).SetEntityComponent(Actor.Entity);
        }

        public IEntityComponent GetEntityComponent()
        {
            if (_addedComponent == null)
                SetupComponent();

            return _addedComponent;
        }

        public Type GetEntityComponentType() => ComponentType;

        private void SetupComponent()
        {
            if (_source == Source.Asset)
                _addedComponent = (TComponent)_factory.GetProduct();
            else
                _addedComponent = _component;
        }


#if UNITY_EDITOR
        public void EditorSetFactory(IEntityDataFactory factory)
        {
            _factory = (TFactory)factory;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorSetData(IEntityData component)
        {
            _component = (TComponent)component;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorSetSource(Source source)
        {
            _source = source;
            UnityEditor.EditorUtility.SetDirty(this);
        }

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.Button, Sirenix.OdinInspector.ShowIf("EditorShowConvertToAssetButton"),
            Sirenix.OdinInspector.HorizontalGroup("Buttons"), Sirenix.OdinInspector.HideInPlayMode]
#else
        [Button, ShowIf("EditorShowConvertToAssetButton"), Group("Buttons"), HideInPlayMode]
#endif
        private void ConvertToAsset()
        {
            const string defaultFolder = "Assets/EntityDataFactories";

            if (!Directory.Exists(defaultFolder))
                Directory.CreateDirectory(defaultFolder);

            string path = $"{defaultFolder}/{typeof(TComponent).Name}Factory_{name}.asset";
            CreateAndSaveFactoryAsset(path);
        }

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.Button, Sirenix.OdinInspector.ShowIf("EditorShowConvertToAssetButton"), 
            Sirenix.OdinInspector.HorizontalGroup("Buttons"), Sirenix.OdinInspector.HideInPlayMode]
#else
        [Button, ShowIf("EditorShowConvertToAssetButton"), Group("Buttons"), HideInPlayMode]
#endif
        private void ConvertToAssetAs()
        {
            string path = UnityEditor.EditorUtility.SaveFilePanelInProject(
                "Export EntityData Factory",
                $"{typeof(TComponent).Name}Factory_{name}",
                "asset",
                "Choose location to save the EntityDataFactory asset");

            if (!string.IsNullOrEmpty(path))
                CreateAndSaveFactoryAsset(path);
        }

        private bool EditorShowConvertToAssetButton() =>
            _source == Source.Embedded &&
            hideFlags.HasFlag(HideFlags.NotEditable) == false;

        private void CreateAndSaveFactoryAsset(string path)
        {
            var factory = ScriptableObject.CreateInstance<TFactory>();

            if (_component != null)
            {
                if (factory is EntityDataFactory<TComponent> dataFactory)
                {
                    dataFactory.EditorSetData(_component);
                }
            }

            UnityEditor.AssetDatabase.CreateAsset(factory, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            Debug.Log($"EntityDataFactory asset saved to: {path}");

            _source = Source.Asset;
            _factory = factory;

            UnityEditor.EditorUtility.SetDirty(this);
        }

        public IEntityDataFactory EditorGetFactory() => _factory;

        public IEntityData EditorGetComponent() => _component;
#endif
    }
}
