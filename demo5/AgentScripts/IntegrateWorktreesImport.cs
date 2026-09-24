using System;using UnityEditor;using UnityEditor.SceneManagement;
public static class IntegrateWorktreesImport {
 public static string Prepare(){if(EditorApplication.isPlaying)throw new Exception("Stop play mode before merge.");if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Active scene has unsaved edits; preserve them first.");AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();return "Auto refresh and assembly reload suspended for file integration.";}
 public static string Import(){AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();AssetDatabase.Refresh();return "Refresh resumed; imports and compilation requested.";}
}
