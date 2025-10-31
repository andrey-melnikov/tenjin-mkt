//
//  Copyright (c) 2022 Tenjin. All rights reserved.
//

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
#endif

public class BuildPostProcessor : MonoBehaviour
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget buildTarget, string path)
    {
        if (buildTarget == BuildTarget.iOS)
        {
            BuildiOS(path);
        }
        else if (buildTarget == BuildTarget.Android)
        {
            BuildAndroid(path);
        }
    }

    private static void BuildAndroid(string path = "")
    {
        Debug.Log("TenjinSDK: Starting Android Build");
    }

    private static void BuildiOS(string path = "")
    {
#if UNITY_IOS
        Debug.Log("TenjinSDK: Starting iOS Build");

        string projectPath = Path.Combine(path, "Unity-iPhone.xcodeproj/project.pbxproj");
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

#if UNITY_2019_3_OR_NEWER
        string buildTarget = project.GetUnityFrameworkTargetGuid();
#else
        string buildTarget = project.TargetGuidByName("Unity-iPhone");
#endif

        AddFrameworksToProject(project, buildTarget);
        AddLinkerFlags(project, buildTarget);
        UpdatePlist(path);

        File.WriteAllText(projectPath, project.WriteToString());
#endif  
    }

#if UNITY_IOS
    [PostProcessBuild(50)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
    {
        if (buildTarget == BuildTarget.iOS)
        {
            EmbedSignFramework(path);
        }
    }

    public static void EmbedSignFramework(string path)
    {
        string projPath = PBXProject.GetPBXProjectPath(path);
        if (!File.Exists(projPath))
        {
            Debug.LogError("Project file does not exist: " + projPath);
            return;
        }
        
        PBXProject proj = new PBXProject();
        proj.ReadFromString(File.ReadAllText(projPath));

        // Get the target GUID
        string unityFrameworkTargetGuid = proj.GetUnityFrameworkTargetGuid();
        string targetGuid = proj.GetUnityMainTargetGuid();

        string zipPathInUnity = "Packages/tenjin-mkt/Runtime/Plugins/iOS/TenjinSDK.xcframework.zip";
        
        string extractionPath = Path.Combine(path, "Frameworks");
        string frameworkPath = Path.Combine(extractionPath, "TenjinSDK.xcframework");

        if (Directory.Exists(frameworkPath))
        {
            Directory.Delete(frameworkPath, true);
        }

        try
        {
            ZipFile.ExtractToDirectory(zipPathInUnity, extractionPath);

            // Delete --MACOSX metadata folder
            string macosxMetaFolder = Path.Combine(extractionPath, "__MACOSX");
            if (Directory.Exists(macosxMetaFolder))
            {
                Directory.Delete(macosxMetaFolder, true);
            }
        }
        catch (IOException e)
        {
            Debug.LogError("Failed to extract zip file: " + e.Message);
            return;
        }

        // Add the .xcframework to the Xcode project and embed it in the main target
        AddFrameworkToXcodeProject(proj, targetGuid, unityFrameworkTargetGuid, frameworkPath);
        
        File.WriteAllText(projPath, proj.WriteToString());
    }

    private static void AddFrameworkToXcodeProject(PBXProject proj, string targetGuid, string unityFrameworkTargetGuid, string frameworkPath)
    {
        string fileGuid = proj.AddFile(frameworkPath, "Frameworks/TenjinSDK.xcframework");
        proj.AddFileToEmbedFrameworks(targetGuid, fileGuid);

        string fileGuidForUnityFramework = proj.AddFile(frameworkPath, "Frameworks/TenjinSDK.xcframework");
        proj.AddFileToBuildSection(targetGuid, proj.GetFrameworksBuildPhaseByTarget(targetGuid), fileGuid);
        proj.AddFileToBuildSection(unityFrameworkTargetGuid, proj.GetFrameworksBuildPhaseByTarget(unityFrameworkTargetGuid), fileGuidForUnityFramework);
    }

    private static void AddFrameworksToProject(PBXProject project, string buildTarget)
    {
        List<string> frameworks = new List<string>
        {
            "AdServices.framework",
            "AdSupport.framework",
            "AppTrackingTransparency.framework",
            "StoreKit.framework"
        };

        foreach (string framework in frameworks)
        {
            Debug.Log("TenjinSDK: Adding framework: " + framework);
            project.AddFrameworkToProject(buildTarget, framework, true);
        }
    }

    private static void AddLinkerFlags(PBXProject project, string buildTarget)
    {
        Debug.Log("TenjinSDK: Adding -ObjC flag to other linker flags (OTHER_LDFLAGS)");
        project.AddBuildProperty(buildTarget, "OTHER_LDFLAGS", "-ObjC");
    }

    private static void UpdatePlist(string path)
    {
        string plistPath = Path.Combine(path, "Info.plist");
        PlistDocument plist = new PlistDocument();
            
        plist.ReadFromFile(plistPath);

        plist.root.SetString("NSUserTrackingUsageDescription", 
                "We request to track data to enhance ad performance and user experience. Your privacy is respected.");

        File.WriteAllText(plistPath, plist.WriteToString());
    }

#endif
}
