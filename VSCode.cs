
namespace dotBunny.Unity
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public static class VSCode
    {

        public const float Version = 1.7f;

        public const string VersionCode = "-RELEASE";

        public const string UnityDebuggerURL = "https://unity.gallery.vsassets.io/_apis/public/gallery/publisher/unity/extension/unity-debug/latest/assetbyname/Microsoft.VisualStudio.Services.VSIXPackage";

        #region Properties
        public static string CodePath
        {
            get
            {
		        string current = EditorPrefs.GetString("VSCode_CodePath", "");
                if(current == "" || !VSCodeExists(current))
                {
                    EditorPrefs.SetString("VSCode_CodePath", AutodetectCodePath());
                }
                return EditorPrefs.GetString("VSCode_CodePath", current);
            }
            set 
            {
                EditorPrefs.SetString("VSCode_CodePath", value);
            }
        }
        static string ProgramFilesx86()
		{
			if( 8 == IntPtr.Size 
				|| (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"))))
			{
				return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
			}

			return Environment.GetEnvironmentVariable("ProgramFiles");
		}
		
        public static bool Debug
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_Debug", false);
            }
            set
            {
                EditorPrefs.SetBool("VSCode_Debug", value);
            }
        }

        public static bool Enabled
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_Enabled", false);
            }
            set
            {
                if (!Enabled && value)
                {
                    ClearProjectFiles();
                }
                EditorPrefs.SetBool("VSCode_Enabled", value);
            }
        }
        public static bool UseUnityDebugger
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_UseUnityDebugger", false);
            }
            set
            {
                if ( value != UseUnityDebugger ) {
                    
                    // Set value
                    EditorPrefs.SetBool("VSCode_UseUnityDebugger", value);
                    if ( value ) {
                        WriteLaunchFile = false;
                    }
                    
                    // Update launch file
                    UpdateLaunchFile();
                }
            }
        }
        
        public static bool AutoOpenEnabled
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_AutoOpenEnabled", false);
            }
            set
            {
                EditorPrefs.SetBool("VSCode_AutoOpenEnabled", value);
            }
        }

        public static bool WriteLaunchFile
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_WriteLaunchFile", true);
            }
            set
            {
                EditorPrefs.SetBool("VSCode_WriteLaunchFile", value);
            }
        }
        static bool AutomaticUpdates
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_AutomaticUpdates", false);
            }
            set
            {
                EditorPrefs.SetBool("VSCode_AutomaticUpdates", value);
            }
        }

        static float GitHubVersion
        {
            get
            {
                return EditorPrefs.GetFloat("VSCode_GitHubVersion", Version);
            }
            set
            {
                EditorPrefs.SetFloat("VSCode_GitHubVersion", value);
            }
        }
        static DateTime LastUpdate
        {
            get
            {

                DateTime lastTime = new DateTime(2015, 10, 8);

                if (EditorPrefs.HasKey("VSCode_LastUpdate"))
                {
                    DateTime.TryParse(EditorPrefs.GetString("VSCode_LastUpdate"), out lastTime);
                }
                return lastTime;
            }
            set
            {
                EditorPrefs.SetString("VSCode_LastUpdate", value.ToString());
            }
        }

        static string LaunchPath
        {
            get
            {
                return SettingsFolder + System.IO.Path.DirectorySeparatorChar + "launch.json";
            }
        }

        static string ProjectPath
        {
            get
            {
                return System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
            }
        }
        static bool RevertExternalScriptEditorOnExit
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_RevertScriptEditorOnExit", true);
            }
            set
            {
                EditorPrefs.SetBool("VSCode_RevertScriptEditorOnExit", value);
            }
        }
        static string SettingsFolder
        {
            get
            {
                return ProjectPath + System.IO.Path.DirectorySeparatorChar + ".vscode";
            }
        }

        static string SettingsPath
        {

            get
            {
                return SettingsFolder + System.IO.Path.DirectorySeparatorChar + "settings.json";
            }
        }

        static int UpdateTime
        {
            get
            {
                return EditorPrefs.GetInt("VSCode_UpdateTime", 7);
            }
            set
            {
                EditorPrefs.SetInt("VSCode_UpdateTime", value);
            }
        }

        #endregion

        static VSCode()
        {
            if (Enabled)
            {
                UpdateUnityPreferences(true);
                UpdateLaunchFile();

                DateTime targetDate = LastUpdate.AddDays(UpdateTime);
                if (DateTime.Now >= targetDate && AutomaticUpdates)
                {
                    CheckForUpdate();
                }

                if (AutoOpenEnabled)
                {
                    CheckForAutoOpen();
                }
                
            }

            System.AppDomain.CurrentDomain.DomainUnload += System_AppDomain_CurrentDomain_DomainUnload;
        }
        static void System_AppDomain_CurrentDomain_DomainUnload(object sender, System.EventArgs e)
        {
            if (Enabled && RevertExternalScriptEditorOnExit)
            {
                UpdateUnityPreferences(false);
            }
        }


        #region Public Members

        public static void SyncSolution()
        {
            System.Type T = System.Type.GetType("UnityEditor.SyncVS,UnityEditor");
            System.Reflection.MethodInfo SyncSolution = T.GetMethod("SyncSolution", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            SyncSolution.Invoke(null, null);

        }

        public static void UpdateSolution()
        {
            if (!VSCode.Enabled)
            {
                return;
            }

            if (VSCode.Debug)
            {
                UnityEngine.Debug.Log("[VSCode] Updating Solution & Project Files");
            }

            var currentDirectory = Directory.GetCurrentDirectory();
            var solutionFiles = Directory.GetFiles(currentDirectory, "*.sln");
            var projectFiles = Directory.GetFiles(currentDirectory, "*.csproj");

            foreach (var filePath in solutionFiles)
            {
                string content = File.ReadAllText(filePath);
                content = ScrubSolutionContent(content);

                File.WriteAllText(filePath, content);

                ScrubFile(filePath);
            }

            foreach (var filePath in projectFiles)
            {
                string content = File.ReadAllText(filePath);
                content = ScrubProjectContent(content);

                File.WriteAllText(filePath, content);

                ScrubFile(filePath);
            }

        }

        #endregion

        #region Private Members
    
        static string AutodetectCodePath() 
        {
            string[] possiblePaths =
#if UNITY_EDITOR_OSX
            {
                "/Applications/Visual Studio Code.app",
                "/Applications/Visual Studio Code - Insiders.app"
            };
#elif UNITY_EDITOR_WIN
            {
                ProgramFilesx86() + Path.DirectorySeparatorChar + "Microsoft VS Code"
                + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "code.cmd",
                ProgramFilesx86() + Path.DirectorySeparatorChar + "Microsoft VS Code Insiders"
                + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "code-insiders.cmd"
            };
#else
            {
                "/usr/bin/code",
                "/bin/code",
                "/usr/local/bin/code"
            };
#endif
            for(int i = 0; i < possiblePaths.Length; i++)
            {
                if(VSCodeExists(possiblePaths[i])) 
                {
                    return possiblePaths[i];
                }
            }
            PrintNotFound(possiblePaths[0]);
            return possiblePaths[0]; 
        }

        static void CallVSCode(string args)
        {
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            if(!VSCodeExists(CodePath))
            {
            	PrintNotFound(CodePath);
            	return;
            }

#if UNITY_EDITOR_OSX
            proc.StartInfo.FileName = "open";

            // Check the path to see if there is "Insiders"
            if (CodePath.Contains("Insiders"))
            {  
                proc.StartInfo.Arguments = " -n -b \"com.microsoft.VSCodeInsiders\" --args " + args.Replace(@"\", @"\\");
            } 
            else 
            {
                proc.StartInfo.Arguments = " -n -b \"com.microsoft.VSCode\" --args " + args.Replace(@"\", @"\\");
            }

            proc.StartInfo.UseShellExecute = false;
#elif UNITY_EDITOR_WIN
            proc.StartInfo.FileName = CodePath;
	        proc.StartInfo.Arguments = args;
            proc.StartInfo.UseShellExecute = false;
#else
            proc.StartInfo.FileName = CodePath;
	        proc.StartInfo.Arguments = args.Replace(@"\", @"\\");
            proc.StartInfo.UseShellExecute = false;
#endif
            proc.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.Start();
        }
        static void CheckForUpdate()
        {
            var fileContent = string.Empty;

            EditorUtility.DisplayProgressBar("VSCode", "Checking for updates ...", 0.5f);
            try
            {
                using (var webClient = new System.Net.WebClient())
                {
                    fileContent = webClient.DownloadString("https://raw.githubusercontent.com/dotBunny/VSCode/master/Plugins/Editor/VSCode.cs");
                }
            }
            catch (Exception e)
            {
                if (Debug)
                {
                    UnityEngine.Debug.Log("[VSCode] " + e.Message);

                }

                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            LastUpdate = DateTime.Now;

            if (fileContent.Substring(0, 2) != "/*")
            {
                int startPosition = fileContent.IndexOf("/*", StringComparison.CurrentCultureIgnoreCase);

                fileContent = fileContent.Substring(startPosition);
            }

            string[] fileExploded = fileContent.Split('\n');
            if (fileExploded.Length > 7)
            {
                float github = Version;
                if (float.TryParse(fileExploded[6].Replace("*", "").Trim(), out github))
                {
                    GitHubVersion = github;
                }


                if (github > Version)
                {
                    var GUIDs = AssetDatabase.FindAssets("t:Script VSCode");
                    var path = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length) + System.IO.Path.DirectorySeparatorChar +
                               AssetDatabase.GUIDToAssetPath(GUIDs[0]).Replace('/', System.IO.Path.DirectorySeparatorChar);

                    if (EditorUtility.DisplayDialog("VSCode Update", "A newer version of the VSCode plugin is available, would you like to update your version?", "Yes", "No"))
                    {
                        System.IO.FileInfo fileInfo = new System.IO.FileInfo(path);
                        fileInfo.IsReadOnly = false;

                        File.WriteAllText(path, fileContent);

                        AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(GUIDs[0]), ImportAssetOptions.ForceUpdate);
                    }

                }
            }
        }

        static void CheckForAutoOpen()
        {
            double timeInSeconds = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            int unityLaunchTimeInSeconds = (int)(timeInSeconds - EditorApplication.timeSinceStartup);
            int prevUnityLaunchTime = EditorPrefs.GetInt("VSCode_UnityLaunchTime", 0);
            if (unityLaunchTimeInSeconds > prevUnityLaunchTime) {
                VSCode.MenuOpenProject();
                EditorPrefs.SetInt("VSCode_UnityLaunchTime", unityLaunchTimeInSeconds);
            }
        }

        static void ClearProjectFiles()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var solutionFiles = Directory.GetFiles(currentDirectory, "*.sln");
            var projectFiles = Directory.GetFiles(currentDirectory, "*.csproj");
            var unityProjectFiles = Directory.GetFiles(currentDirectory, "*.unityproj");

            foreach (string solutionFile in solutionFiles)
            {
                File.Delete(solutionFile);
            }
            foreach (string projectFile in projectFiles)
            {
                File.Delete(projectFile);
            }
            foreach (string unityProjectFile in unityProjectFiles)
            {
                File.Delete(unityProjectFile);
            }
#if !UNITY_4_0 && !UNITY_4_1 && !UNITY_4_2 && !UNITY_4_3 && !UNITY_4_5 && !UNITY_4_6 && !UNITY_4_7
            SyncSolution();
#endif
        }
        static void FixUnityPreferences()
        {
            System.Type T = System.Type.GetType("UnityEditor.PreferencesWindow,UnityEditor");

            if (EditorWindow.focusedWindow == null)
                return;

            if (EditorWindow.focusedWindow.GetType() == T)
            {
                var window = EditorWindow.GetWindow(T, true, "Unity Preferences");


                if (window == null)
                {
                    if (Debug)
                    {
                        UnityEngine.Debug.Log("[VSCode] No Preferences Window Found (really?)");
                    }
                    return;
                }

                var invokerType = window.GetType();
                var invokerMethod = invokerType.GetMethod("ReadPreferences",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (invokerMethod != null)
                {
                    invokerMethod.Invoke(window, null);
                }
                else if (Debug)
                {
                    UnityEngine.Debug.Log("[VSCode] No Reflection Method Found For Preferences");
                }
            }
        }
        static int GetDebugPort()
        {
#if UNITY_EDITOR_WIN
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "netstat";
            process.StartInfo.Arguments = "-a -n -o -p TCP";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string[] lines = output.Split('\n');

            process.WaitForExit();

            foreach (string line in lines)
            {
                string[] tokens = Regex.Split(line, "\\s+");
                if (tokens.Length > 4)
                {
                    int test = -1;
                    int.TryParse(tokens[5], out test);

                    if (test > 1023)
                    {
                        try
                        {
                            var p = System.Diagnostics.Process.GetProcessById(test);
                            if (p.ProcessName == "Unity")
                            {
                                return test;
                            }
                        }
                        catch
                        {

                        }
                    }
                }
            }
#else
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "lsof";
            process.StartInfo.Arguments = "-c /^Unity$/ -i 4tcp -a";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string[] lines = output.Split('\n');

            process.WaitForExit();

            foreach (string line in lines)
            {
                int port = -1;
                if (line.StartsWith("Unity"))
                {
                    string[] portions = line.Split(new string[] { "TCP *:" }, System.StringSplitOptions.None);
                    if (portions.Length >= 2)
                    {
                        Regex digitsOnly = new Regex(@"[^\d]");
                        string cleanPort = digitsOnly.Replace(portions[1], "");
                        if (int.TryParse(cleanPort, out port))
                        {
                            if (port > -1)
                            {
                                return port;
                            }
                        }
                    }
                }
            }
#endif
            return -1;
        }
        static void InstallUnityDebugger()
        {
            EditorUtility.DisplayProgressBar("VSCode", "Downloading Unity Debugger ...", 0.1f);
            byte[] fileContent;
            
            try
            {
                using (var webClient = new System.Net.WebClient())
                {
                    fileContent = webClient.DownloadData(UnityDebuggerURL);
                }
            }
            catch (Exception e)
            {
                if (Debug)
                {
                    UnityEngine.Debug.Log("[VSCode] " + e.Message);
                }
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            
            if ( fileContent != null ) {
                string fileName = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".vsix";
                File.WriteAllBytes(fileName, fileContent);
                
                CallVSCode(fileName);
            }

        }
   
        [MenuItem("Assets/Open C# Project In Code", false, 1000)]
        static void MenuOpenProject()
        {

            SyncSolution();
            CallVSCode("\"" + ProjectPath + "\"");
        }
        static void PrintNotFound(string path)
        {
            UnityEngine.Debug.LogError("[VSCode] Code executable in '" + path + "' not found. Check your" +
            "Visual Studio Code installation and insert the correct path in the Preferences menu.");
        }

        [MenuItem("Assets/Open C# Project In Code", true, 1000)]
        static bool ValidateMenuOpenProject()
        {
            return Enabled;
        }

        [PreferenceItem("VSCode")]
        static void VSCodePreferencesItem()
        {
            if (EditorApplication.isCompiling)
            {
                EditorGUILayout.HelpBox("Please wait for Unity to finish compiling. \nIf the window doesn't refresh, simply click on the window or move it around to cause a repaint to happen.", MessageType.Warning);
                return;
            }
            EditorGUILayout.BeginVertical();

            var developmentInfo = "Support development of this plugin, follow @reapazor and @dotbunny on Twitter.";
            var versionInfo = string.Format("{0:0.00}", Version) + VersionCode + ", GitHub version @ " + string.Format("{0:0.00}", GitHubVersion);
            EditorGUILayout.HelpBox(developmentInfo + " --- [ " + versionInfo + " ]", MessageType.None);

            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("VS Code Path", GUILayout.Width(75));
#if UNITY_5_3_OR_NEWER            
            CodePath = EditorGUILayout.DelayedTextField(CodePath,  GUILayout.ExpandWidth(true));
#else
            CodePath = EditorGUILayout.TextField(CodePath,  GUILayout.ExpandWidth(true));
#endif        
            GUI.SetNextControlName("PathSetButton");    
            if(GUILayout.Button("...", GUILayout.Height(14), GUILayout.Width(20)))
            {
                GUI.FocusControl("PathSetButton");
                string path = EditorUtility.OpenFilePanel( "Visual Studio Code Executable", "", "" );
                if( path.Length != 0 && File.Exists(path) || Directory.Exists(path))
                {
                    CodePath = path;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            Enabled = EditorGUILayout.Toggle(new GUIContent("Enable Integration", "Should the integration work its magic for you?"), Enabled);

            UseUnityDebugger = EditorGUILayout.Toggle(new GUIContent("Use Unity Debugger", "Should the integration integrate with Unity's VSCode Extension (must be installed)."), UseUnityDebugger);

            AutoOpenEnabled = EditorGUILayout.Toggle(new GUIContent("Enable Auto Open", "When opening a project in Unity, should it automatically open in VS Code?"), AutoOpenEnabled);

            EditorGUILayout.Space();
            RevertExternalScriptEditorOnExit = EditorGUILayout.Toggle(new GUIContent("Revert Script Editor On Unload", "Should the external script editor setting be reverted to its previous setting on project unload? This is useful if you do not use Code with all your projects."),RevertExternalScriptEditorOnExit);
            
            Debug = EditorGUILayout.Toggle(new GUIContent("Output Messages To Console", "Should informational messages be sent to Unity's Console?"), Debug);

            WriteLaunchFile = EditorGUILayout.Toggle(new GUIContent("Always Write Launch File", "Always write the launch.json settings when entering play mode?"), WriteLaunchFile);

            EditorGUILayout.Space();

            AutomaticUpdates = EditorGUILayout.Toggle(new GUIContent("Automatic Updates", "Should the plugin automatically update itself?"), AutomaticUpdates);

            UpdateTime = EditorGUILayout.IntSlider(new GUIContent("Update Timer (Days)", "After how many days should updates be checked for?"), UpdateTime, 1, 31);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (EditorGUI.EndChangeCheck())
            {
                UpdateUnityPreferences(Enabled);

                if (VSCode.Debug)
                {
                    if (Enabled)
                    {
                        UnityEngine.Debug.Log("[VSCode] Integration Enabled");
                    }
                    else
                    {
                        UnityEngine.Debug.Log("[VSCode] Integration Disabled");
                    }
                }
            }

            if (GUILayout.Button(new GUIContent("Force Update", "Check for updates to the plugin, right NOW!")))
            {
                CheckForUpdate();
                EditorGUILayout.EndVertical();
                return;
            }
            if (GUILayout.Button(new GUIContent("Write Workspace Settings", "Output a default set of workspace settings for VSCode to use, ignoring many different types of files.")))
            {
                WriteWorkspaceSettings();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.Space();

            if (UseUnityDebugger)
            {
                EditorGUILayout.HelpBox("In order for the \"Use Unity Debuggger\" option to function above, you need to have installed the Unity Debugger Extension for Visual Studio Code.", MessageType.Warning);
                if (GUILayout.Button(new GUIContent("Install Unity Debugger", "Install the Unity Debugger Extension into Code")))
                {
                    InstallUnityDebugger();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

        }
        [UnityEditor.Callbacks.OnOpenAssetAttribute()]
        static bool OnOpenedAsset(int instanceID, int line)
        {
            if (!Enabled)
            {
                return false;
            }
            string appPath = ProjectPath;

            UnityEngine.Object selected = EditorUtility.InstanceIDToObject(instanceID);

            if (selected.GetType().ToString() == "UnityEditor.MonoScript" ||
                selected.GetType().ToString() == "UnityEngine.Shader")
            {
                string completeFilepath = appPath + Path.DirectorySeparatorChar + AssetDatabase.GetAssetPath(selected);

                string args = null;
                if (line == -1)
                {
                    args = "\"" + ProjectPath + "\" \"" + completeFilepath + "\" -r";
                }
                else
                {
                    args = "\"" + ProjectPath + "\" -g \"" + completeFilepath + ":" + line.ToString() + "\" -r";
                }
                // call 'open'
                CallVSCode(args);

                return true;
            }
            return false;

        }
        static void OnPlaymodeStateChanged()
        {
            if (UnityEngine.Application.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UpdateLaunchFile();
            }
        }

        [UnityEditor.Callbacks.DidReloadScripts()]
        static void OnScriptReload()
        {
            EditorApplication.playmodeStateChanged -= OnPlaymodeStateChanged;
            EditorApplication.playmodeStateChanged += OnPlaymodeStateChanged;
        }
        static void ScrubFile(string path)
        {
            string[] lines = File.ReadAllLines(path);
            System.Collections.Generic.List<string> newLines = new System.Collections.Generic.List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i].Trim()) || lines[i].Trim() == "\t" || lines[i].Trim() == "\t\t")
                {

                }
                else
                {
                    newLines.Add(lines[i]);
                }
            }
            File.WriteAllLines(path, newLines.ToArray());
        }

        static string ScrubProjectContent(string content)
        {
            if (content.Length == 0)
                return "";

#if !UNITY_EDITOR_WIN
            if (content.IndexOf("<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>") != -1)
            {
                content = Regex.Replace(content, "<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>", "<TargetFrameworkVersion>v2.0</TargetFrameworkVersion>");
            }
#endif

            string targetPath = "";// "<TargetPath>Temp" + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "Debug" + Path.DirectorySeparatorChar + "</TargetPath>"; //OutputPath
            string langVersion = "<LangVersion>default</LangVersion>";


            bool found = true;
            int location = 0;
            string addedOptions = "";
            int startLocation = -1;
            int endLocation = -1;
            int endLength = 0;

            while (found)
            {
                startLocation = -1;
                endLocation = -1;
                endLength = 0;
                addedOptions = "";
                startLocation = content.IndexOf("<PropertyGroup", location);

                if (startLocation != -1)
                {

                    endLocation = content.IndexOf("</PropertyGroup>", startLocation);
                    endLength = (endLocation - startLocation);


                    if (endLocation == -1)
                    {
                        found = false;
                        continue;
                    }
                    else
                    {
                        found = true;
                        location = endLocation;
                    }

                    if (content.Substring(startLocation, endLength).IndexOf("<TargetPath>") == -1)
                    {
                        addedOptions += "\n\r\t" + targetPath + "\n\r";
                    }

                    if (content.Substring(startLocation, endLength).IndexOf("<LangVersion>") == -1)
                    {
                        addedOptions += "\n\r\t" + langVersion + "\n\r";
                    }

                    if (!string.IsNullOrEmpty(addedOptions))
                    {
                        content = content.Substring(0, endLocation) + addedOptions + content.Substring(endLocation);
                    }
                }
                else
                {
                    found = false;
                }
            }

            return content;
        }
        static string ScrubSolutionContent(string content)
        {
            content = content.Replace(
                "Microsoft Visual Studio Solution File, Format Version 11.00\r\n# Visual Studio 2018\r\n",
                "\r\nMicrosoft Visual Studio Solution File, Format Version 12.00\r\n# Visual Studio 2012");

            int startIndex = content.IndexOf("GlobalSection(SolutionProperties) = preSolution");
            if (startIndex != -1)
            {
                int endIndex = content.IndexOf("EndGlobalSection", startIndex);
                content = content.Substring(0, startIndex) + content.Substring(endIndex + 16);
            }

            return content;
        }
        static void UpdateLaunchFile()
        {
            if (!VSCode.Enabled)
            {
                return;
            }
            else if (VSCode.UseUnityDebugger)
            {
                if (!Directory.Exists(VSCode.SettingsFolder))
                    System.IO.Directory.CreateDirectory(VSCode.SettingsFolder);
                string fileContent = "{\n\t\"version\": \"0.2.0\",\n\t\"configurations\": [\n\t\t{\n\t\t\t\"name\": \"Unity Editor\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\t\t},\n\t\t{\n\t\t\t\"name\": \"Windows Player\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\t\t},\n\t\t{\n\t\t\t\"name\": \"OSX Player\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\t\t},\n\t\t{\n\t\t\t\"name\": \"Linux Player\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\t\t},\n\t\t{\n\t\t\t\"name\": \"iOS Player\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\t\t},\n\t\t{\n\t\t\t\"name\": \"Android Player\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\n\t\t}\n\t]\n}";
                File.WriteAllText(VSCode.LaunchPath, fileContent);
            }
            else if (VSCode.WriteLaunchFile)
            {
                int port = GetDebugPort();
                if (port > -1)
                {
                    if (!Directory.Exists(VSCode.SettingsFolder))
                        System.IO.Directory.CreateDirectory(VSCode.SettingsFolder);
                    string fileContent = "{\n\t\"version\":\"0.2.0\",\n\t\"configurations\":[ \n\t\t{\n\t\t\t\"name\":\"Unity\",\n\t\t\t\"type\":\"mono\",\n\t\t\t\"request\":\"attach\",\n\t\t\t\"address\":\"localhost\",\n\t\t\t\"port\":" + port + "\n\t\t}\n\t]\n}";
                    File.WriteAllText(VSCode.LaunchPath, fileContent);

                    if (VSCode.Debug)
                    {
                        UnityEngine.Debug.Log("[VSCode] Debug Port Found (" + port + ")");
                    }
                }
                else
                {
                    if (VSCode.Debug)
                    {
                        UnityEngine.Debug.LogWarning("[VSCode] Unable to determine debug port.");
                    }
                }
            }
        }
        static void UpdateUnityPreferences(bool enabled)
        {
            if (enabled)
            {
                if (EditorPrefs.GetString("kScriptsDefaultApp") != CodePath)
                {
                    EditorPrefs.SetString("VSCode_PreviousApp", EditorPrefs.GetString("kScriptsDefaultApp"));
                }
                EditorPrefs.SetString("kScriptsDefaultApp", CodePath);
                if (EditorPrefs.GetString("kScriptEditorArgs") != "-r -g `$(File):$(Line)`")
                {
                    EditorPrefs.SetString("VSCode_PreviousArgs", EditorPrefs.GetString("kScriptEditorArgs"));
                }

                EditorPrefs.SetString("kScriptEditorArgs", "-r -g `$(File):$(Line)`");
                EditorPrefs.SetString("kScriptEditorArgs" + CodePath, "-r -g `$(File):$(Line)`");
                if (EditorPrefs.GetBool("kMonoDevelopSolutionProperties", false))
                {
                    EditorPrefs.SetBool("VSCode_PreviousMD", true);
                }
                EditorPrefs.SetBool("kMonoDevelopSolutionProperties", false);
                if (EditorPrefs.GetBool("kExternalEditorSupportsUnityProj", false))
                {
                    EditorPrefs.SetBool("VSCode_PreviousUnityProj", true);
                }
                EditorPrefs.SetBool("kExternalEditorSupportsUnityProj", false);

                if (!EditorPrefs.GetBool("AllowAttachedDebuggingOfEditor", false))
                {
                    EditorPrefs.SetBool("VSCode_PreviousAttach", false);
                }
                EditorPrefs.SetBool("AllowAttachedDebuggingOfEditor", true);
                
            }
            else
            {
                if (!string.IsNullOrEmpty(EditorPrefs.GetString("VSCode_PreviousApp")))
                {
                    EditorPrefs.SetString("kScriptsDefaultApp", EditorPrefs.GetString("VSCode_PreviousApp"));
                }
                if (!string.IsNullOrEmpty(EditorPrefs.GetString("VSCode_PreviousArgs")))
                {
                    EditorPrefs.SetString("kScriptEditorArgs", EditorPrefs.GetString("VSCode_PreviousArgs"));
                }
                if (EditorPrefs.GetBool("VSCode_PreviousMD", false))
                {
                    EditorPrefs.SetBool("kMonoDevelopSolutionProperties", true);
                }
                if (EditorPrefs.GetBool("VSCode_PreviousUnityProj", false))
                {
                    EditorPrefs.SetBool("kExternalEditorSupportsUnityProj", true);
                }
                EditorPrefs.SetBool("AllowAttachedDebuggingOfEditor", true);
                
            }

            FixUnityPreferences();
        }
        static bool VSCodeExists(string curPath)
        {
            #if UNITY_EDITOR_OSX
            return System.IO.Directory.Exists(curPath);
            #else
            System.IO.FileInfo code = new System.IO.FileInfo(curPath);
            return code.Exists;
            #endif
        }
        static void WriteWorkspaceSettings()
        {
            if (Debug)
            {
                UnityEngine.Debug.Log("[VSCode] Workspace Settings Written");
            }

            if (!Directory.Exists(VSCode.SettingsFolder))
            {
                System.IO.Directory.CreateDirectory(VSCode.SettingsFolder);
            }

            string exclusions =
                "{\n" +
                "\t\"files.exclude\":\n" +
                "\t{\n" +
                // Hidden Files
                "\t\t\"**/.DS_Store\":true,\n" +
                "\t\t\"**/.git\":true,\n" +
                "\t\t\"**/.gitignore\":true,\n" +
                "\t\t\"**/.gitattributes\":true,\n" +
                "\t\t\"**/.gitmodules\":true,\n" +
                "\t\t\"**/.svn\":true,\n" +


                // Project Files
                "\t\t\"**/*.booproj\":true,\n" +
                "\t\t\"**/*.pidb\":true,\n" +
                "\t\t\"**/*.suo\":true,\n" +
                "\t\t\"**/*.user\":true,\n" +
                "\t\t\"**/*.userprefs\":true,\n" +
                "\t\t\"**/*.unityproj\":true,\n" +
                "\t\t\"**/*.dll\":true,\n" +
                "\t\t\"**/*.exe\":true,\n" +

                // Media Files
                "\t\t\"**/*.pdf\":true,\n" +

                // Audio
                "\t\t\"**/*.mid\":true,\n" +
                "\t\t\"**/*.midi\":true,\n" +
                "\t\t\"**/*.wav\":true,\n" +

                // Textures
                "\t\t\"**/*.gif\":true,\n" +
                "\t\t\"**/*.ico\":true,\n" +
                "\t\t\"**/*.jpg\":true,\n" +
                "\t\t\"**/*.jpeg\":true,\n" +
                "\t\t\"**/*.png\":true,\n" +
                "\t\t\"**/*.psd\":true,\n" +
                "\t\t\"**/*.tga\":true,\n" +
                "\t\t\"**/*.tif\":true,\n" +
                "\t\t\"**/*.tiff\":true,\n" +

                // Models
                "\t\t\"**/*.3ds\":true,\n" +
                "\t\t\"**/*.3DS\":true,\n" +
                "\t\t\"**/*.fbx\":true,\n" +
                "\t\t\"**/*.FBX\":true,\n" +
                "\t\t\"**/*.lxo\":true,\n" +
                "\t\t\"**/*.LXO\":true,\n" +
                "\t\t\"**/*.ma\":true,\n" +
                "\t\t\"**/*.MA\":true,\n" +
                "\t\t\"**/*.obj\":true,\n" +
                "\t\t\"**/*.OBJ\":true,\n" +

                // Unity File Types
                "\t\t\"**/*.asset\":true,\n" +
                "\t\t\"**/*.cubemap\":true,\n" +
                "\t\t\"**/*.flare\":true,\n" +
                "\t\t\"**/*.mat\":true,\n" +
                "\t\t\"**/*.meta\":true,\n" +
                "\t\t\"**/*.prefab\":true,\n" +
                "\t\t\"**/*.unity\":true,\n" +

                // Folders
                "\t\t\"build/\":true,\n" +
                "\t\t\"Build/\":true,\n" +
                "\t\t\"Library/\":true,\n" +
                "\t\t\"library/\":true,\n" +
                "\t\t\"obj/\":true,\n" +
                "\t\t\"Obj/\":true,\n" +
                "\t\t\"ProjectSettings/\":true,\r" +
                "\t\t\"temp/\":true,\n" +
                "\t\t\"Temp/\":true\n" +
                "\t}\n" +
                "}";
            File.WriteAllText(VSCode.SettingsPath, exclusions);
        }

        #endregion
    }

    public class VSCodeAssetPostprocessor : AssetPostprocessor
    {

        private static void OnGeneratedCSProjectFiles()
        {
            VSCode.UpdateSolution();
        }
    }
}
