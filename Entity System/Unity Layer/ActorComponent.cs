using EasyCS.EventSystem;
using System.Collections.Generic;
using TriInspector;
using UnityEngine;

namespace EasyCS
{
    [HideMonoScript]
    public class ActorComponent : EasyCSBehavior, IEventListener<EventEntityKilled>
    {
        [SerializeField, ReadOnly, Required, ShowIf("EditorShowActor")]
        private Actor _actor;
        public Actor Actor => _actor;
        public Entity Entity => _actor != null ? _actor.Entity : Entity.Empty;

#if UNITY_EDITOR
        [InfoBox("Disable reason: Entity not set", TriMessageType.Warning, "EditorShowWarningDisabledEntityNotSet")]
        [ShowInInspector, ReadOnly, ShowIf("EditorShowWarningDisabledEntityNotSet"), ShowInPlayMode]
        private bool _entityNotSet = true;
        [InfoBox("Disable reason: Entity not alive", TriMessageType.Warning, "EditorShowWarningDisabledEntityNotAlive")]
        [ShowInInspector, ReadOnly, ShowIf("EditorShowWarningDisabledEntityNotAlive"), ShowInPlayMode]
        private bool _entityNotAlive = true;
        [LabelText("Missing Dependencies"), ShowInInspector, ShowIf("EditorHasMissingDependencies"), ReadOnly,
            InfoBox("Actor has missing dependencies for this component!", TriMessageType.Error, "EditorHasMissingDependencies"),
            ListDrawerSettings(AlwaysExpanded = true)]
        private List<string> _editorMissingDependencies = new List<string>();
#endif

        public void HandleEvent(in EventContext<EventEntityKilled> ctx)
        {
            enabled = false;

#if UNITY_EDITOR

            // Workaround to force update the editor and show the Warning InfoBox
            if (UnityEditor.Selection.activeGameObject == gameObject)
            {
                UnityEditor.Selection.activeGameObject = null;
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    UnityEditor.Selection.activeGameObject = gameObject;
                };
            }
#endif
        }

        protected override void HandleAwake()
        {

        }

        protected override void HandleDestroy()
        {
            _actor = null;
        }

        internal override void InternalOnEnable()
        {
            base.InternalOnEnable();

            if (Entity.IsAlive == false)
                enabled = false;
        }

        internal virtual void InternalHandleAttachToEntity(Entity curEntity)
        {
            EventSystem.TrySubscribe(this, curEntity);

            HandleAttachToEntity(curEntity);
            enabled = true;
        }

        /// <summary>
        /// Invokes only when curEntity.IsAlive == true
        /// Component will be instantly enabled after
        /// </summary>
        /// <param name="curEntity"></param>
        protected virtual void HandleAttachToEntity(Entity curEntity)
        {

        }

        internal virtual void InternalHandleDetachFromEntity(Entity prevEntity)
        {
            EventSystem.TryUnsubscribe(this, prevEntity);

            HandleDetachFromEntity(prevEntity);
            enabled = false;
        }

        /// <summary>
        /// Invokes only when prevEntity.IsEmpty == false
        /// Component will be instantly disabled after
        /// </summary>
        /// <param name="prevEntity"></param>
        protected virtual void HandleDetachFromEntity(Entity prevEntity)
        {

        }

        public void SetActor(Actor actor) => _actor = actor;

#if UNITY_EDITOR

        private bool EditorShowWarningDisabled() =>
            EditorShowWarningDisabledEntityNotSet() || EditorShowWarningDisabledEntityNotAlive();
        private bool EditorShowWarningDisabledEntityNotSet() => Entity.IsEmpty;
        private bool EditorShowWarningDisabledEntityNotAlive() => Entity.IsEmpty == false && Entity.IsAlive == false;

        protected internal bool EditorHasMissingDependencies()
        {
            EditorUpdateMissingDependencies();

            return _editorMissingDependencies != null && _editorMissingDependencies.Count > 0;
        }

        private void EditorUpdateMissingDependencies()
        {
            _editorMissingDependencies =
                Actor?.EditorGetMissingDependenciesNamesForComponent(this, Actor.EditorDependenciesType.ActorComponent, true);
        }

        protected virtual void OnGUI()
        {
            if (Application.isPlaying) return;
            EditorUpdateMissingDependencies();
        }

        [Button("Add Missing Actor Components"), ShowIf("EditorHasMissingDependencies")]
        public void EditorAddMissingDependencies()
        {
            if (Actor == null)
            {
                Debug.LogError("[EasyCS] Actor is null");
                return;
            }

            Actor.EditorAddMissingDependenciesForComponent(this, Actor.EditorDependenciesType.ActorComponent);
        }

        private bool EditorShowActor()
        {
            if (transform.root.GetComponent<EntityEditorComponentFlag>() != null)
                return false;
            if (Actor != null)
                return false;
            return true;
        }

        protected override void Reset()
        {
            base.Reset();

            GetComponentInParent<Actor>()?.EditorOnComponentAdded();
        }
#endif
    }
}
