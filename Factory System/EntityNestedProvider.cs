using UnityEngine;
using System.Collections.Generic;
using EasyCS.EntityFactorySystem.Editor;
using TriInspector;

namespace EasyCS.EntityFactorySystem
{
    [ExecuteAlways]
    public class EntityNestedProvider : MonoBehaviour
    {
        [SerializeField, Required, OnValueChanged("UpdateNestedHierarchy")]
        private List<EntityFactory> _nestedFactories = new List<EntityFactory>();

        [SerializeField, HideInInspector]
        private List<EntityFactory> _nestedInstances = new List<EntityFactory>();
        [SerializeField, HideInInspector]
        private List<GameObject> _nestedInstancesGameobjects = new List<GameObject>();

        public IReadOnlyList<EntityFactory> NestedFactories => _nestedFactories;
        public IReadOnlyList<GameObject> NestedInstancesGameobjects => _nestedInstancesGameobjects;


#if UNITY_EDITOR
        /// <summary>
        /// Editor-only method to setup factories during prefab generation.
        /// </summary>
        public void SetNestedFactoriesEditorOnly(List<EntityFactory> factories)
        {
            _nestedFactories = factories;
            UpdateNestedHierarchy();
        }

        private void UpdateNestedHierarchy()
        {
            // Destroy existing instances
            for (int i = 0; i < _nestedInstancesGameobjects.Count; i++)
            {
                if (_nestedInstancesGameobjects[i] != null)
                {
                    DestroyImmediate(_nestedInstancesGameobjects[i]);
                }
            }

            _nestedInstances.Clear();
            _nestedInstancesGameobjects.Clear();

            // Recreate all from scratch
            foreach (var factory in _nestedFactories)
            {
                if (factory == null)
                    continue;

                GameObject nestedRoot = new GameObject(factory.name);
                nestedRoot.transform.SetParent(transform);

                GameObject entityObject = EntityPrefabBuilder.BuildHierarchyRecursive(factory, nestedRoot.transform, transform);

                nestedRoot.transform.ForEachRecursive(t => t.gameObject.hideFlags = HideFlags.NotEditable, true);

                _nestedInstances.Add(factory);
                _nestedInstancesGameobjects.Add(nestedRoot);
            }
        }

        private void OnTransformChildrenChanged()
        {
            foreach (var nestedGo in _nestedInstancesGameobjects)
                if (nestedGo.transform.parent != transform)
                    nestedGo.transform.SetParent(transform);
        }

#endif
    }
}
