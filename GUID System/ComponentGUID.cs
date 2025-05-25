using TriInspector;
using System;
using UnityEngine;

namespace EasyCS
{
    [Serializable]
#if ODIN_INSPECTOR
     [Sirenix.OdinInspector.HideLabel, Sirenix.OdinInspector.BoxGroup("GUID")]
#endif
    public class ComponentGUID : IEquatable<ComponentGUID>
    {
        public enum IdSourceType
        {
            Random = 1,
            Custom = 2
        }
        
        [ShowInInspector, ReadOnly] public string ID => sourceType == IdSourceType.Random ? _randomId : _customId;

        [SerializeField, OnValueChanged(nameof(UpdateGUID))]
        private IdSourceType sourceType = IdSourceType.Random;

        [SerializeField, HideInInspector] private string _randomId;

        [SerializeField, ShowIf("sourceType", IdSourceType.Custom), OnValueChanged(nameof(UpdateGUID))]
        private string _customId;

        public IdSourceType SourceType
        {
            get { return sourceType; }
            set
            {
                sourceType = value;
                UpdateGUID();
            }
        }

        public ComponentGUID()
        {
            SetRandomGUID();
        }

        public Guid GetGuid()
        {
            return new Guid(ID);
        }

        [Button]
        [ShowIf("sourceType", IdSourceType.Random)]
        public void RenerateGUID()
        {
            _randomId = null;
            UpdateGUID();
        }

        public void SetCustomID(string customId)
        {
            this._customId = customId;
            this.sourceType = IdSourceType.Custom;
            UpdateGUID();
        }

        private void SetRandomGUID()
        {
            _randomId = Guid.NewGuid().ToString().Replace("-", "");
        }

        public void UpdateGUID()
        {
            if (sourceType == IdSourceType.Random)
            {
                if (_randomId == null || _randomId == String.Empty)
                    SetRandomGUID();
            }
            else if (sourceType == IdSourceType.Custom)
            {
            }
        }

        public bool Equals(ComponentGUID other) => ID.Equals(other?.ID);
    }
}