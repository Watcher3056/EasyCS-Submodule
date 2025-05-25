using System.Collections.Generic;
using System.Linq;

namespace EasyCS.Groups
{
    
    public class CustomGroup : IGroup
    {
        public IEnumerable<Entity> Entities => _entitiesCommon;

        private HashSet<Entity> _entitiesCommon;
        private Dictionary<Entity, int> _entitiesAll;
        private HashSet<IGroup> _groupsTracked;

        internal CustomGroup(HashSet<IGroup> groupsTracked)
        {
            _groupsTracked = groupsTracked;
            _entitiesAll = Extensions.GetCommonElementsMap(groupsTracked.ToArray());
            _entitiesCommon =
                new HashSet<Entity>(Extensions.GetCommonElementsFromMap(_entitiesAll, _groupsTracked.Count));
        }

        internal void ScoreEntity(Entity entity, int scoreDelta)
        {
            if (_entitiesAll.ContainsKey(entity) == false)
            {
                _entitiesAll.Add(entity, scoreDelta);
            }
            else
            {
                _entitiesAll[entity] += scoreDelta;
                if (_entitiesAll[entity] == _groupsTracked.Count)
                {
                    _entitiesCommon.Add(entity);
                }
            }
        }

        internal void Remove(Entity entity)
        {
            _entitiesCommon.Remove(entity);
            _entitiesAll.Remove(entity);
        }

        internal bool ContainsGroup(IGroup group)
        {
            return _groupsTracked.Contains(group);
        }
    }

}