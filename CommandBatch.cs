#if UNITY_EDITOR 
using UnityEngine;
using MonKey;
using MonKey.Editor;
 
public static class CommandBatch
 {
     [Command("Remove Component Of Type From Prefabs In Folder",
        Help = "Removes all the component of the given type on all the prefabs present in a given folder, recursively")]
    public static void RemoveComponentsFromPrefabsInFolder(
        [CommandParameter(AutoCompleteMethodName = "FolderAutoComplete", Help = "The name of the folder",
            ForceAutoCompleteUsage = true)]
        string folderName,
        [CommandParameter(Help = "The Type of Component", AutoCompleteMethodName = "ComponentTypeAuto")]
        Type componentType)
    {
        MonKey.Editor.Commands.SelectionUtilities.FindFolder(folderName);
        string folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        string[] folders = AssetDatabase.GetSubFolders(folderName);

        List<string> foldersList = new List<string>(folders);

        foldersList.Add(folderName);

        string[] prefabPaths = AssetDatabase.FindAssets("t:GameObject", foldersList.ToArray());
        foreach (var path in prefabPaths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(path));
            if (!go)
                return;

            var instance = (GameObject) PrefabUtility.InstantiatePrefab(go);

            var comps = instance.GetComponentsInChildren(componentType);
            var originalComps = go.GetComponentsInChildren(componentType);
            for (var index = 0; index < comps.Length; index++)
            {
                var component = comps[index];
                var prefabComponent = originalComps[index];
                Object.DestroyImmediate(component);
                PrefabUtility.ApplyRemovedComponent(instance, prefabComponent, InteractionMode.AutomatedAction);
            }

            Object.DestroyImmediate(instance);
        }

        AssetDatabase.SaveAssets();
    }

    public static AssetNameAutoComplete FolderAutoComplete()
    {
        return new AssetNameAutoComplete() {DirectoryMode = true};
    }

    public static TypeAutoComplete ComponentTypeAuto()
    {
        return new TypeAutoComplete(false, true, true, false, false);
    }
 }
 
 #endif