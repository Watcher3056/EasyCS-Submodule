using System;
using System.Collections.Generic;
using System.Linq;

namespace EasyCS
{
    public static class EntityDependencyHelper
    {
        public static List<Type> EditorGetMissingEntityBehaviorDependencies(IEnumerable<IEntityComponent> allComponents)
        {
            List<Type> missingBehaviors = new List<Type>();

            // Collect all existing IEntityBehavior types from the provided components
            HashSet<Type> existingBehaviorTypes = new HashSet<Type>(
                allComponents
                    .OfType<IEntityBehavior>() // Directly existing behaviors
                    .Select(c => c.GetType())
            );

            // Also consider types provided by IEntityBehaviorProvider
            foreach (var comp in allComponents.OfType<IEntityBehaviorProvider>())
            {
                existingBehaviorTypes.Add(comp.GetEntityComponentType());
            }

            // Iterate through all components to find what they require
            foreach (var component in allComponents)
            {
                if (component == null) continue;

                Type componentType = null;
                // Determine the type to get dependencies for.
                // If it's an IEntityBehaviorProvider, use its provided type.
                // Otherwise, use the component's own type.
                if (component is IEntityBehaviorProvider entityBehaviorProvider)
                    componentType = entityBehaviorProvider.GetEntityComponentType();
                else
                    componentType = component.GetType();

                var requiredTypes = Injector.GetRequiredInjectionTypes(componentType);

                foreach (var depType in requiredTypes)
                {
                    // Check if the dependency is an IEntityBehavior
                    if (typeof(IEntityBehavior).IsAssignableFrom(depType))
                    {
                        // Ignore runtime-only components for editor-time missing checks
                        if (Attribute.IsDefined(depType, typeof(RuntimeOnlyAttribute)))
                            continue;

                        // If the required behavior type is not already existing
                        if (!existingBehaviorTypes.Contains(depType))
                        {
                            // And it's not already in our missing list (to avoid duplicates)
                            if (!missingBehaviors.Contains(depType))
                            {
                                missingBehaviors.Add(depType);
                            }
                        }
                    }
                }
            }

            return missingBehaviors;
        }
    }
}
