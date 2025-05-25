using System.Collections;
using System.Collections.Generic;

namespace EasyCS.Groups
{
    public interface IGroup : IEnumerable<Entity>
    {
        public IEnumerable<Entity> Entities { get; }

        public IEnumerable<Actor> Actors
        {
            get
            {
                foreach (var entity in Entities)
                {
                    Actor actor = entity.Actor;
                    if (actor != null)
                        yield return actor;
                }
                
                yield break;
            }
        }
        IEnumerator<Entity> IEnumerable<Entity>.GetEnumerator() => Entities.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Entities.GetEnumerator();
    }
}