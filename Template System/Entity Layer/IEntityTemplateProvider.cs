
using System;
using System.Collections.Generic;

namespace EasyCS
{
    public interface IEntityTemplateProvider
    {
        public void ApplyTemplate(Entity entity, EntityTemplateSetupPolicy setupPolicy = EntityTemplateSetupPolicy.AddMissingFromTemplate);
        public HashSet<Type> GetComponentTypes();
    }
}
