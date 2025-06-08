using System;
using System.Collections.Generic;
using TriInspector;
using UnityEngine;

namespace EasyCS
{
    [RequireComponent(typeof(Actor))]
    [IconClass(ConstantsIcons.IconEntity)]
#if ODIN_INSPECTOR
    [HideMonoScript]
#endif
    public class EntityTemplateProvider : EasyCSBehavior
    {
        [field: SerializeField]
        public EntityTemplateAsset EntityTemplate { get; private set; }


#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            EditorApplyEntityTemplate();
        }

        private void EditorApplyEntityTemplate()
        {
            if (EntityTemplate == null)
                return;

            Actor actor = GetComponent<Actor>();
            if (actor == null)
                return;

            var componentTypes = EntityTemplate.GetComponentTypes();

            List<Type> providersToAdd = new List<Type>();

            foreach (var type in componentTypes)
            {
                Type providerType = EntityComponentProviderFinder.FindEntityComponentProviderMatching(type);

                if (providerType != null)
                {
                    var allComponents = actor.EditorGetAllComponentsSerialized();
                    var entityComponentsMissing = actor.EditorGetEntityComponentsMissingSerialized();

                    if (allComponents.Exists(c => c.GetType() == providerType))
                        continue;
                    if (entityComponentsMissing.Exists(c => c.GetType() == type))
                        continue;

                    providersToAdd.Add(providerType);
                }
            }

            if (providersToAdd.Count > 0)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null) return;

                    foreach (var provider in providersToAdd)
                        if (gameObject.GetComponent(provider) == null)
                            UnityEditor.Undo.AddComponent(gameObject, provider);

                    UnityEditor.EditorUtility.SetDirty(gameObject);
                };
            }
        }

        public void EditorSetEntityTemplate(EntityTemplateAsset entityTemplateAsset)
        {
            EntityTemplate = entityTemplateAsset;
            EditorApplyEntityTemplate();
        }

#endif
    }
}
