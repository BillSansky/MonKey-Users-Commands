#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MonKey;
using MonKey.Editor;
using MonKey.Editor.Internal;
using MonKey.Extensions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;


[InitializeOnLoad]
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

    [Command("Create new Template Scene", Help = "Creates new scene from template scene", QuickName = "CNTS")]
    public static void CreateSceneFromTemplate([CommandParameter(Help = "The name of the scene to open",
            AutoCompleteMethodName = "SceneNameAutoComplete",
            ForceAutoCompleteUsage = true, OverrideName = "Scene Name", PreventDefaultValueUsage = true)]
        String templateScenePath)
    {
        string[]
            splitNames = templateScenePath.Split('/'); //* Spiliting the scene path to retrive the selected scene name
        if (splitNames.Length == 0) return; //!Error in selection
        string sceneNameToDuplicatefrom = splitNames[splitNames.Length - 1]; //* Selected scene name
        string destinationPath =
            EditorUtility.SaveFilePanel("New Template Scene", "Assets/", sceneNameToDuplicatefrom,
                "unity"); //* This opens file explorer for save path of new scene to Duplicatefrom

        if (!string.IsNullOrEmpty(destinationPath))
        {
            destinationPath = destinationPath.Replace(Application.dataPath, "Assets"); //* Removes the local disk path

            if (!AssetDatabase.CopyAsset(templateScenePath, destinationPath)
            ) //* If fails to copy then this becomes true
            {
                // Then we search for the scene asset that we are trying to copy and try again
                string[] foundAssets = AssetDatabase.FindAssets($"{sceneNameToDuplicatefrom} t:Scene");

                if (foundAssets.Length == 0)
                {
                    Debug.LogError("The selected template scene was not found !!");
                }
                else
                {
                    templateScenePath = AssetDatabase.GUIDToAssetPath(foundAssets[0]);
                    if (!AssetDatabase.CopyAsset(templateScenePath, destinationPath))
                    {
                        Debug.LogErrorFormat("Could not copy template scene from the location, try manually copying",
                            templateScenePath);
                    }
                }
            }
            else
            {
                EditorSceneManager
                    .OpenScene(
                        destinationPath); //* If no issue in copying the selected scene open the new scene created
            }
        }
    }

    private static AssetNameAutoComplete SceneNameAutoComplete()
    {
        //* This is to open the drop down list with all the asssets in the project 
        //* Its referenced in the CommandParameter above for the argument templateScenePath

        return new AssetNameAutoComplete() {CustomType = "SceneAsset", PopulateOnInit = true};
    }
    
    [Command("Set TextMeshPro Text",
        Help = "Replace the TextMeshPro Text with the new one provided",
        QuickName = "STT", 
        ValidationMethodName = nameof(CanExecute))
    ]
    public static void ExecuteCommand(params string[] texts)
    {
        // ReSharper disable once CoVariantArrayConversion
        Undo.RecordObjects(Selection.gameObjects, "Changing TextMeshPro Text");
    
        int textIndex = 0;
        foreach (GameObject gameObject in MonkeyEditorUtils.OrderedSelectedGameObjects)
        {
            var textMeshPro = gameObject.GetComponent<TextMeshProUGUI>();
        
            if(textMeshPro == null)
                continue;
        
            textMeshPro.text = texts[textIndex];
            EditorUtility.SetDirty(textMeshPro);
        
            textIndex++;
            if (textIndex >= texts.Length)
                textIndex = 0;
        }
    }

    public static bool CanExecute() => Selection.gameObjects.All(go => go.GetComponent<TextMeshProUGUI>() != null);
    
    [Command("Delete Children","Deletes All The Children of the selected Object"
        ,DefaultValidation = DefaultValidation.AT_LEAST_ONE_TRANSFORM)]
    public static void DeleteChildren()
    {
        foreach (var gameObject in Selection.gameObjects)
        {
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                Object.DestroyImmediate(gameObject.transform.GetChild(i).gameObject);
            }
        }
    }
    
    [Command("Disable UI Raycast Target On Children")]
    private static void DisableUiRaycastTargetOnChildren()
    {
        var selectedTransforms = Selection.GetFiltered<Transform>(SelectionMode.Editable | SelectionMode.ExcludePrefab);
        selectedTransforms.ForEach(t =>
        {
            t.GetComponentsInChildren<Graphic>(true).ForEach(g =>
            {
                g.raycastTarget = false;
                EditorUtility.SetDirty(g);
            });
        });
    }
    
    [Command("Adjust Box Collider To BoundingBox","Adjusts the size of a box collider to the bounding box of sub renderer")]
    public static void AdjustBoxCollidersToBoundingBox()
    {
        foreach (var gameObject in Selection.gameObjects)
        {
            BoxCollider collider = gameObject.GetComponent<BoxCollider>();
            if (collider)
            {
                Bounds bounds = new Bounds();

                foreach (var renderer in collider.gameObject.GetComponentsInChildren<Renderer>())
                {
                    if (bounds.extents == Vector3.zero)
                        bounds = renderer.bounds;
                    else
                        bounds.Encapsulate(renderer.bounds);
                }

                collider.center = bounds.center;
                collider.size = bounds.size;
            }
        }
    }
    
}

#endif