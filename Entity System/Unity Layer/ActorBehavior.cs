namespace EasyCS
{
    public abstract class ActorBehavior : ActorComponent
    {
        //protected override void OnGUI()
        //{
        //    base.OnGUI();

        //    if (UnityEditor.Selection.activeGameObject == gameObject)
        //    {
        //        bool hasMissingDependencies = EditorHasMissingDependencies();

        //        bool isExpanded = UnityEditorInternal.InternalEditorUtility.GetIsInspectorExpanded(this);

        //        if (hasMissingDependencies && isExpanded == false)
        //        {
        //            UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(this, true);
        //            UnityEditor.ActiveEditorTracker.sharedTracker.ForceRebuild();
        //        }
        //        else if (hasMissingDependencies == false && isExpanded)
        //        {
        //            UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(this, false);
        //            UnityEditor.ActiveEditorTracker.sharedTracker.ForceRebuild();
        //        }
        //    }
        //}
    }
}