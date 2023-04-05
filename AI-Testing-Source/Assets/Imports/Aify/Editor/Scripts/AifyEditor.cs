using UnityEditor;
using UnityEngine;
using Unity.Barracuda;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Globalization;
using System.Text;
using UnityEngine.Networking;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
using UnityEditor.Experimental.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif
using System;
using System.Collections;


namespace AiKodex
{

    public class AifyEditor : EditorWindow
    {
        string prompt = "";
        string negativePrompt = "";
        static NNModel upscaleModel;
        static Model runtimeModel;
        static IWorker worker;
        static Texture2D inputTexture, outputTexture;
        static RenderTexture outputRenderTexture;

        //Concept Art
        string search = "";
        public enum Magnification
        {
            quarter,
            half,
            x2,
            x4
        };
        static float magnificationAmount;
        static string magnificationTag;
        public enum ModelType
        {
            LightWeightSuperResolution,
            HeavyWeightSuperResolution
        };
        public static ModelType _modelType = ModelType.LightWeightSuperResolution;
        public static Magnification _magnification = Magnification.x4;
        public static bool powerOfTwo = true;

        public static Texture2D sourceImage;
        public static IEnumerable<Texture> selectedTextures;
        private static int conceptArtImageNumber = 5;
        private static int normalStrength = 5; // default 5
        private static bool upscaleGroupEnabled;
        private static bool normalGroupEnabled;
        private static bool specularGroupEnabled;
        private static bool depthGroupEnabled;
        private static float specularCutOff = 0.40f; // default 0.4
        private static float specularContrast = 1.5f; // default 1.5
        private static string appName = "Aify UI";
        public static string normalSuffix = "_normal.png";
        public static string specularSuffix = "_specular.png";
        public static string depthSuffix = "_depth.png";
        public static bool running = false;
        float aspectRatio;
        static Dictionary<string, bool> s_UIHelperFoldouts = new Dictionary<string, bool>();
        private List<string> m_Inputs = new List<string>();
        private List<string> m_Details = new List<string>();
        private Vector2 mainScroll, conceptArtScroll;
        private Vector2 m_InputsScrollPosition = Vector2.zero;
        private Vector2 m_InputsScrollPositionMapDetails = Vector2.zero;
        IEnumerable<Texture> prevSelectedTextures, currentSelectedTextures;
        byte[] encJpg;
        string base64encJpg, resultFromServer, sDb64FromServer;
        string seed = "1";
        bool autoRandomizeSeed = true;
        float img2imgstrength = 0.5f;
        Vector2Int img_dim = new Vector2Int(512, 512);
        int inferenceSteps = 30;
        int cfgScale = 8;
        public enum Sampler
        {
            k_euler_a,
            k_euler,
            k_lms,
            ddim,
            plms,
            k_huen,
            k_euler_ancestral,
            k_dpm_2_ancestral,
            k_dpmpp_2s_ancestral,
            k_dpmpp_2m
        };
        public static Sampler sampler = Sampler.k_euler_a;
        public Texture2D initImage;
        private string _directoryPath = "";
        bool autoPath = true;
        bool previewInInspector;
        float zoomPreview = 0.8f;
        bool fitGrid = true;
        float conceptPreview = 0.3f;
        private Vector2 _scrollPosition = Vector2.zero;
        private bool initDone = false;
        private GUIStyle StatesLabel;
        List<Texture2D> temp;


        void InitStyles()
        {
            initDone = true;
            StatesLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(),
                padding = new RectOffset(),
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
        }



        // create menu item and window
        [MenuItem("Window/Aify Editor")]
        static void Init()
        {
            AifyEditor window = (AifyEditor)EditorWindow.GetWindow(typeof(AifyEditor));
            window.titleContent.text = appName;
            window.minSize = new Vector2(300, 300);
            running = true;
        }

        // window closed
        void OnDestroy()
        {
            running = false;
        }
        private static void ListUIHelper(string sectionTitle, List<string> names, ref Vector2 scrollPosition, float maxHeightMultiplier = 1f)
        {
            int n = names.Count();
            GUILayout.Space(10);
            if (!s_UIHelperFoldouts.TryGetValue(sectionTitle, out bool foldout))
                foldout = true;

            foldout = EditorGUILayout.Foldout(foldout, sectionTitle, true, EditorStyles.foldoutHeader);
            s_UIHelperFoldouts[sectionTitle] = foldout;
            if (foldout)
            {
                float height = Mathf.Min(n * 20f + 2f, 150f * maxHeightMultiplier);
                if (n == 0)
                    return;

                scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUI.skin.box, GUILayout.MinHeight(height));
                Event e = Event.current;
                float lineHeight = 16.0f;

                StringBuilder fullText = new StringBuilder();
                fullText.Append(sectionTitle);
                fullText.AppendLine();
                for (int i = 0; i < n; ++i)
                {
                    string name = names[i];
                    fullText.Append($"{name}");
                    fullText.AppendLine();
                }

                for (int i = 0; i < n; ++i)
                {
                    Rect r = EditorGUILayout.GetControlRect(false, lineHeight);

                    string name = names[i];

                    // Context menu, "Copy"
                    if (e.type == EventType.ContextClick && r.Contains(e.mousePosition))
                    {
                        e.Use();
                        var menu = new GenericMenu();

                        // need to copy current value to be used in delegate
                        // (C# closures close over variables, not their values)
                        menu.AddItem(new GUIContent($"Copy current line"), false, delegate
                        {
                            EditorGUIUtility.systemCopyBuffer = $"{name}";
                        });
                        menu.AddItem(new GUIContent($"Copy section"), false, delegate
                        {
                            EditorGUIUtility.systemCopyBuffer = fullText.ToString();
                        });
                        menu.ShowAsContext();
                    }

                    // Color even line for readability
                    if (e.type == EventType.Repaint)
                    {
                        GUIStyle st = "CN EntryBackEven";
                        if ((i & 1) == 0)
                            st.Draw(r, false, false, false, false);
                    }

                    // layer name on the right side
                    Rect locRect = r;
                    locRect.xMax = locRect.xMin;
                    GUIContent gc = new GUIContent(name.ToString(CultureInfo.InvariantCulture));

                    // calculate size so we can left-align it
                    Vector2 size = EditorStyles.miniBoldLabel.CalcSize(gc);
                    locRect.xMax += size.x;
                    GUI.Label(locRect, gc, EditorStyles.miniBoldLabel);
                    locRect.xMax += 2;
                }

                GUILayout.EndScrollView();
            }

        }

        // main loop/gui
        void OnGUI()
        {

            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            if (!initDone)
                InitStyles();
            GUIStyle style = new GUIStyle("WhiteLargeLabel");
            GUIStyle headStyle = new GUIStyle("Box");
            headStyle.fontSize = 24;
            headStyle.normal.textColor = Color.white;
            EditorGUILayout.BeginHorizontal();
            Texture logo = (Texture)AssetDatabase.LoadAssetAtPath("Assets/Aify/Editor/Aify Logo.png", typeof(Texture));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("      Aify ", headStyle);
            EditorGUILayout.EndVertical();
            GUI.DrawTexture(new Rect(5, 2, 40, 40), logo, ScaleMode.StretchToFill, true, 10.0F);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
            EditorStyles.textArea.wordWrap = true;
            prompt = EditorGUILayout.TextArea(prompt, EditorStyles.textArea, GUILayout.Height(40));
            EditorGUILayout.LabelField("Negative Prompt", EditorStyles.boldLabel);
            EditorStyles.textArea.wordWrap = true;

            negativePrompt = EditorGUILayout.TextArea(negativePrompt, EditorStyles.textArea, GUILayout.Height(20));
            EditorGUILayout.Space(10);


            GUIStyle secondary = new GUIStyle("WhiteLargeLabel");
            EditorGUILayout.LabelField("Settings", secondary);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Dimensions:", GUILayout.MaxWidth(150));
            img_dim = EditorGUILayout.Vector2IntField("", img_dim, GUILayout.MaxWidth(110));
            GUILayout.ExpandWidth(true);
            EditorGUILayout.EndHorizontal();
            img_dim.x = Mathf.Clamp(img_dim.x, 100, 512);
            img_dim.y = Mathf.Clamp(img_dim.y, 100, 512);

            inferenceSteps = EditorGUILayout.IntSlider("Inference Steps", inferenceSteps, 10, 50);
            cfgScale = EditorGUILayout.IntSlider("Cfg Scale", cfgScale, 0, 20);
            sampler = (Sampler)EditorGUILayout.EnumPopup("Sampler Model", sampler);


            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel($"Seed: {seed}");
            autoRandomizeSeed = EditorGUILayout.ToggleLeft("Auto", autoRandomizeSeed, GUILayout.MaxWidth(50));
            if (GUILayout.Button("Randomize", GUILayout.ExpandWidth(false), GUILayout.Width(80)))
                seed = UnityEngine.Random.Range(100000, 999999).ToString();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            initImage = EditorGUILayout.ObjectField("Match Image", initImage, typeof(Texture2D), false, GUILayout.Height(70), GUILayout.Width(70), GUILayout.MaxWidth(220)) as Texture2D;
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(12);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
                initImage = null;
            if (GUILayout.Button("Last", GUILayout.Width(50)))
                initImage = sourceImage;
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            img2imgstrength = EditorGUILayout.Slider("Match Image Strength", img2imgstrength, 0, 1);


            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(autoPath == true);
            if (autoPath)
                _directoryPath = EditorGUILayout.TextField("Textures Folder", "Assets/Aify");
            else
                _directoryPath = EditorGUILayout.TextField("Textures Folder", _directoryPath);
            if (GUILayout.Button(". . /", GUILayout.MaxWidth(50)))
                _directoryPath = EditorUtility.OpenFolderPanel("", "", "");
            EditorGUI.EndDisabledGroup();
            autoPath = EditorGUILayout.ToggleLeft("Auto", autoPath, GUILayout.MaxWidth(50));
            EditorGUILayout.EndHorizontal();

            GUI.enabled = prompt == null || prompt == "" ? false : true;
            if (GUILayout.Button("Generate Image", GUILayout.Height(30)))
            {
                GenerateImage();
                if (autoRandomizeSeed)
                    seed = UnityEngine.Random.Range(100000, 999999).ToString();
            }

            GUI.enabled = true;

            EditorGUILayout.BeginHorizontal();
            previewInInspector = EditorGUILayout.Toggle("Preview Selected Image", previewInInspector, GUILayout.ExpandWidth(true));
            EditorGUI.BeginDisabledGroup(previewInInspector == false);
            zoomPreview = EditorGUILayout.Slider(zoomPreview, 0.3f, 0.95f, GUILayout.ExpandWidth(true));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (previewInInspector && Selection.activeObject != null && Selection.activeObject.GetType().Equals(typeof(Texture2D)))
                GUILayout.Box((Texture2D)Selection.activeObject, GUILayout.Width(position.width * zoomPreview), GUILayout.Height(position.width * zoomPreview), GUILayout.ExpandWidth(true));

            EditorGUILayout.Space(10);
            GUILayout.Label("Concept Art Settings", style);
            EditorGUILayout.LabelField("Search Terms", EditorStyles.boldLabel);

            search = EditorGUILayout.TextArea(search, EditorStyles.textArea, GUILayout.Height(20));
            conceptArtImageNumber = EditorGUILayout.IntSlider("Images to display", conceptArtImageNumber, 1, 20);
            EditorGUILayout.BeginHorizontal();
            fitGrid = EditorGUILayout.Toggle("Fit Grid", fitGrid, GUILayout.ExpandWidth(true));
            conceptPreview = EditorGUILayout.Slider(conceptPreview, 0.2f, 1, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            GUI.enabled = search == null || search == "" ? false : true;
            if (GUILayout.Button("Browse Concept Art", GUILayout.Height(30)))
                temp = ConceptArt();
            GUI.enabled = true;
            if (temp != null)
            {
                if (fitGrid)
                {
                    int ConceptWindowWidth = (int)position.width / 160;
                    int lineChange = 0;
                    for (int i = 0; i < temp.Count(); i++)
                        if (i % ConceptWindowWidth == 0)
                            lineChange++;

                    conceptArtScroll = EditorGUILayout.BeginScrollView(conceptArtScroll, GUILayout.Height(160 * lineChange));
                    EditorGUILayout.BeginVertical();
                    for (int i = 0; i < temp.Count(); i++)
                    {
                        if (i % ConceptWindowWidth == 0)
                        {
                            EditorGUILayout.BeginHorizontal();
                        }
                        GUILayout.Box(temp[i], GUILayout.Width(150), GUILayout.Height(150), GUILayout.ExpandWidth(true));
                        if (i != 0 && i % ConceptWindowWidth == ConceptWindowWidth - 1)
                            EditorGUILayout.EndHorizontal();
                    }
                    if (temp.Count() % ConceptWindowWidth < ConceptWindowWidth && temp.Count() % ConceptWindowWidth != 0)
                        EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndScrollView();
                }
            }
            else
            {
                if (temp != null)
                {
                    conceptArtScroll = EditorGUILayout.BeginScrollView(conceptArtScroll, GUILayout.Height(520 * System.Convert.ToInt32(temp.Count() != 0) * conceptPreview));
                    EditorGUILayout.BeginHorizontal();
                    for (int i = 0; i < temp.Count(); i++)
                        GUILayout.Box(temp[i], GUILayout.Width(position.width * conceptPreview), GUILayout.Height(position.width * conceptPreview), GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndScrollView();
                }

            }
            if (temp != null)
            {
                GUI.enabled = temp.Count != 0;
                if (GUILayout.Button("Clear", GUILayout.Height(20)))
                {
                    temp.Clear();
                }
                GUI.enabled = true;
            }
            EditorGUILayout.Space(10);

            GUILayout.Label("Postprocessing Settings", style);
            GUILayout.Label("Selected Textures");
            prevSelectedTextures = currentSelectedTextures;
            selectedTextures = Selection.GetFiltered(typeof(Texture), SelectionMode.Assets).Cast<Texture>();
            currentSelectedTextures = selectedTextures;

            if (selectedTextures.Count() > 0)
            {
                EditorGUILayout.BeginVertical(GUILayout.MaxWidth(position.width));
                int windowWidth = (int)position.width / 75;
                for (int i = 0; i < selectedTextures.Count(); i++)
                {
                    if (i % windowWidth == 0)
                    {
                        EditorGUILayout.BeginHorizontal();
                    }
                    sourceImage = EditorGUILayout.ObjectField(selectedTextures.ElementAt(i), typeof(Texture2D), false, GUILayout.Height(75), GUILayout.Width(75)) as Texture2D;
                    if (i != 0 && i % windowWidth == windowWidth - 1)
                        EditorGUILayout.EndHorizontal();

                }
                if (selectedTextures.Count() % windowWidth < windowWidth && selectedTextures.Count() % windowWidth != 0)
                    EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();

                if (prevSelectedTextures != currentSelectedTextures)
                {
                    m_Inputs.Clear();
                    for (int i = 0; i < selectedTextures.Count(); i++)
                    {
                        m_Inputs.Add((i + 1).ToString() + ". Name: " + selectedTextures.ElementAt(i).name + " | Dimensions: " + selectedTextures.ElementAt(i).width + "x" + selectedTextures.ElementAt(i).height + " | Format: " + selectedTextures.ElementAt(i).graphicsFormat);
                    }
                }

                ListUIHelper($"Info ({selectedTextures.Count()})", m_Inputs, ref m_InputsScrollPosition);

            }
            else
            {
                Rect rect = EditorGUILayout.BeginHorizontal();
                GUILayout.Label(" None", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
                GUI.Box(rect, GUIContent.none);

            }
            EditorGUILayout.Space();

            upscaleGroupEnabled = EditorGUILayout.BeginToggleGroup("Upscale Resolution Settings", upscaleGroupEnabled);
            _modelType = (ModelType)EditorGUILayout.EnumPopup("Select Neural Network", _modelType);
            _magnification = (Magnification)EditorGUILayout.EnumPopup("Magnification Factor", _magnification);
            powerOfTwo = EditorGUILayout.Toggle("Round dimensions", powerOfTwo);
            EditorGUILayout.EndToggleGroup();
            EditorGUILayout.Space();

            depthGroupEnabled = EditorGUILayout.BeginToggleGroup("Generate Depth Map", depthGroupEnabled);
            EditorGUILayout.HelpBox("You need an active internet connection for this map type", MessageType.Info);
            EditorGUILayout.EndToggleGroup();
            EditorGUILayout.Space();

            normalGroupEnabled = EditorGUILayout.BeginToggleGroup("Generate Normal Map", normalGroupEnabled);
            normalStrength = EditorGUILayout.IntSlider("Strength", normalStrength, 1, 20);
            EditorGUILayout.EndToggleGroup();
            EditorGUILayout.Space();

            specularGroupEnabled = EditorGUILayout.BeginToggleGroup("Generate Smoothness", specularGroupEnabled);
            specularCutOff = EditorGUILayout.Slider("Brightness Cutoff", specularCutOff, 0, 1);
            specularContrast = EditorGUILayout.Slider("Specular Contrast", specularContrast, 0, 2);
            EditorGUILayout.EndToggleGroup();
            EditorGUILayout.Space();


            switch (_magnification)
            {
                case Magnification.quarter:
                    magnificationAmount = 0.125f;
                    magnificationTag = "_xquarter";
                    break;
                case Magnification.half:
                    magnificationAmount = 0.25f;
                    magnificationTag = "_xhalf";
                    break;
                case Magnification.x2:
                    magnificationAmount = 0.5f;
                    magnificationTag = "_x2";
                    break;
                case Magnification.x4:
                    magnificationAmount = 1;
                    magnificationTag = "_x4";
                    break;
            }
            var texturesCount = Convert.ToInt32(upscaleGroupEnabled) + Convert.ToInt32(depthGroupEnabled) + Convert.ToInt32(normalGroupEnabled) + Convert.ToInt32(specularGroupEnabled);
            var listNumber = 0;
            if (prevSelectedTextures != currentSelectedTextures)
            {
                m_Details.Clear();
                if (upscaleGroupEnabled)
                {
                    for (int i = 0; i < selectedTextures.Count(); i++)
                    {
                        listNumber++;
                        if (powerOfTwo)
                            m_Details.Add((listNumber).ToString() + ". BASE TEXTURE | " + "Name: " + selectedTextures.ElementAt(i).name + " | From Dimension: " + selectedTextures.ElementAt(i).width + "x" + selectedTextures.ElementAt(i).height + " | To Dimension: " + Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(selectedTextures.ElementAt(i).width * magnificationAmount * 4)) + "x" + Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(selectedTextures.ElementAt(i).height * magnificationAmount * 4)) + " | Extension: PNG");
                        else
                            m_Details.Add((listNumber).ToString() + ". BASE TEXTURE | " + "Name: " + selectedTextures.ElementAt(i).name + " | From Dimension: " + selectedTextures.ElementAt(i).width + "x" + selectedTextures.ElementAt(i).height + " | To Dimension: " + selectedTextures.ElementAt(i).width * magnificationAmount * 4 + "x" + selectedTextures.ElementAt(i).height * magnificationAmount * 4 + " | Extension: PNG");

                    }
                }
                if (depthGroupEnabled)
                {
                    for (int i = 0; i < selectedTextures.Count(); i++)
                    {
                        listNumber++;
                        m_Details.Add((listNumber).ToString() + ". DEPTH TEXTURE | " + "Name: " + selectedTextures.ElementAt(i).name);
                    }
                }
                if (normalGroupEnabled)
                {
                    for (int i = 0; i < selectedTextures.Count(); i++)
                    {
                        listNumber++;
                        m_Details.Add((listNumber).ToString() + ". NORMAL TEXTURE | " + "Name: " + selectedTextures.ElementAt(i).name);
                    }
                }
                if (specularGroupEnabled)
                {
                    for (int i = 0; i < selectedTextures.Count(); i++)
                    {
                        listNumber++;
                        m_Details.Add((listNumber).ToString() + ". SMOOTHNESS TEXTURE | " + "Name: " + selectedTextures.ElementAt(i).name);
                    }
                }
            }

            ListUIHelper($"Map Details ({selectedTextures.Count() * texturesCount})", m_Details, ref m_InputsScrollPositionMapDetails);


            //  ** Create button GUI **
            EditorGUILayout.Space();
            GUI.enabled = sourceImage; // disabled if no sourceImage selected
            if (GUILayout.Button(new GUIContent("Generate Textures"), GUILayout.Height(30)))
            {
                for (int i = 0; i < selectedTextures.Count(); i++)
                {
                    buildMaps(selectedTextures.ElementAt(i).name, i);
                }
            }
            GUI.enabled = true;
            EditorGUILayout.EndScrollView();
        }

        void GenerateImage()
        {
            if (initImage != null)
            {
                string path = AssetDatabase.GetAssetPath(initImage);
                TextureImporter initImporter = (TextureImporter)TextureImporter.GetAtPath(path);
                initImporter.sRGBTexture = false;
                initImporter.isReadable = true;
                initImporter.textureCompression = TextureImporterCompression.Uncompressed;
                initImporter.SaveAndReimport();
                Texture2D scaledInitImage = initImage;
                TextureScale.Bilinear(scaledInitImage, 512, 512);
                encJpg = scaledInitImage.DeCompress().EncodeToJPG();
                base64encJpg = Convert.ToBase64String(encJpg);
                sDb64FromServer = Post(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("aHR0cDovLzMuMjE5Ljg1LjEzNDo1MDAwL2RhdGE=")), "{\"data\":\"" + $"{prompt}" + "\",\"neg\":\"" + $"{negativePrompt}" + "\",\"width\":\"" + $"{img_dim.x}" + "\",\"height\":\"" + $"{img_dim.y}" + "\",\"steps\":\"" + $"{inferenceSteps}" + "\",\"cfgScale\":\"" + $"{cfgScale}" + "\",\"sampler\":\"" + $"{sampler}" + "\",\"seed\":\"" + $"{seed}" + "\",\"initImg\":\"" + $"{base64encJpg}" + "\",\"img2imgStrength\":\"" + $"{img2imgstrength}" + "\"}");
                initImporter.sRGBTexture = true;
                initImporter.SaveAndReimport();
            }
            else
                sDb64FromServer = Post(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("aHR0cDovLzMuMjE5Ljg1LjEzNDo1MDAwL2RhdGE=")), "{\"data\":\"" + $"{prompt}" + "\",\"neg\":\"" + $"{negativePrompt}" + "\",\"width\":\"" + $"{img_dim.x}" + "\",\"height\":\"" + $"{img_dim.y}" + "\",\"steps\":\"" + $"{inferenceSteps}" + "\",\"cfgScale\":\"" + $"{cfgScale}" + "\",\"sampler\":\"" + $"{sampler}" + "\",\"seed\":\"" + $"{seed}" + "\",\"initImg\":\"" + "\",\"img2imgStrength\":\"" + "\"}");

            if (sDb64FromServer == null)
                Debug.Log("There was an error in generating the image. Please try again. If this problem persists, please check the documentation.");
            else
            {
                sDb64FromServer = sDb64FromServer.Remove(0, 9);
                sDb64FromServer = sDb64FromServer.Remove(sDb64FromServer.Length - 3);
                byte[] imageBytes = System.Convert.FromBase64String(sDb64FromServer);
                outputRenderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
                outputRenderTexture.Create();
                outputTexture = new Texture2D(outputRenderTexture.width, outputRenderTexture.height);
                outputTexture.LoadImage(imageBytes);
                outputTexture.Apply();
                File.WriteAllBytes($"{_directoryPath}/{seed}.png", outputTexture.EncodeToPNG());
                AssetDatabase.Refresh();
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath($"{_directoryPath}/{seed}.png");
            }
        }

        List<Texture2D> ConceptArt()
        {
            List<Texture2D> conceptArtTextureArray = new List<Texture2D>();
            string jsonString = Get(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("aHR0cHM6Ly9sZXhpY2EuYXJ0L2FwaS92MS9zZWFyY2g/cT0=")) + search);
            List<string> srcSmallUrls = new List<string>();
            int index = 0;
            while ((index = jsonString.IndexOf("\"srcSmall\":", index)) != -1)
            {
                index = index + "\"srcSmall\":".Length;
                int endIndex = jsonString.IndexOf(",", index);
                if (endIndex == -1)
                {
                    endIndex = jsonString.IndexOf("}", index);
                }
                string url = jsonString.Substring(index, endIndex - index).Trim().Trim('"');
                srcSmallUrls.Add(url);
            }
            for (int i = 0; i < conceptArtImageNumber; i++)
            {
                byte[] imageBytes = DownloadConceptArtImage(srcSmallUrls.ElementAt(i));
                outputRenderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
                outputRenderTexture.Create();
                outputTexture = new Texture2D(outputRenderTexture.width, outputRenderTexture.height);
                outputTexture.LoadImage(imageBytes);
                outputTexture.Apply();
                conceptArtTextureArray.Add(outputTexture);
            }
            return conceptArtTextureArray;
        }
        public Byte[] DownloadConceptArtImage(string url)
        {
            UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SendWebRequest();
            while (!request.isDone)
            {
                //Timeout Code
            }
            if (request.responseCode.ToString() != "200")
            {
                return null;
            }
            else
            {
                return request.downloadHandler.data;
            }

        }


        void buildMaps(string baseFile, int index)
        {

            float progress = 0.0f;
            bool setReadable = false;

            // check if its readable, if not set it temporarily readable
            string path = AssetDatabase.GetAssetPath(selectedTextures.ElementAt(index));
            TextureImporter initImporter = (TextureImporter)TextureImporter.GetAtPath(path);
            initImporter.sRGBTexture = false;
            initImporter.SaveAndReimport();
            inputTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));
            TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            if (textureImporter.isReadable == false)
            {
                textureImporter.isReadable = true;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                setReadable = true;
            }
            if (!powerOfTwo)
            {
                textureImporter.npotScale = TextureImporterNPOTScale.None;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            if (upscaleGroupEnabled)
            {
                switch (_modelType)
                {
                    case ModelType.LightWeightSuperResolution:
                        runtimeModel = ModelLoader.Load((NNModel)AssetDatabase.LoadAssetAtPath("Assets/Aify/Neural Network Models/LWSR.onnx", typeof(NNModel)));
                        break;
                    case ModelType.HeavyWeightSuperResolution:
                        runtimeModel = ModelLoader.Load((NNModel)AssetDatabase.LoadAssetAtPath("Assets/Aify/Neural Network Models/HWSR.onnx", typeof(NNModel)));
                        break;

                }


                worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, runtimeModel);

                var input = new Tensor(inputTexture, 3);

                worker.Execute(input);

                Tensor output = worker.PeekOutput("output");

                outputRenderTexture = new RenderTexture(inputTexture.width * 4, inputTexture.height * 4, 16, RenderTextureFormat.ARGB32);
                outputRenderTexture.Create();

                output.ToRenderTexture(outputRenderTexture);
                RenderTexture.active = outputRenderTexture;
                outputTexture = new Texture2D(outputRenderTexture.width, outputRenderTexture.height);
                outputTexture.ReadPixels(new Rect(0, 0, outputRenderTexture.width, outputRenderTexture.height), 0, 0);
                outputTexture.Apply();
                // Texture Resize
                TextureScale.Bilinear(outputTexture, (int)(outputTexture.width * magnificationAmount), (int)(outputTexture.height * magnificationAmount));
                File.WriteAllBytes(path.Substring(0, path.Length - Path.GetExtension(path).Length) + magnificationTag + ".png", outputTexture.EncodeToPNG());
                output.Dispose();
                AssetDatabase.Refresh();
                TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(path.Substring(0, path.Length - Path.GetExtension(path).Length) + magnificationTag + ".png");
                if (!powerOfTwo)
                {
                    textureImporter.npotScale = TextureImporterNPOTScale.ToNearest;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                initImporter.sRGBTexture = true;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
                worker.Dispose();
            }


            if (depthGroupEnabled)
            {

                encJpg = inputTexture.DeCompress().EncodeToJPG();
                base64encJpg = Convert.ToBase64String(encJpg);

                resultFromServer = Post(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String("aHR0cDovLzU0LjE2MS4xMTUuNzM6NTAwMC9kYXRh")), "{\"data\":\"" + $"{base64encJpg}" + "\"}");

                if (resultFromServer == null)
                    Debug.Log("There was an error in generating the depth map. Please check your internet connection and try again.");
                else
                {
                    resultFromServer = resultFromServer.Remove(0, 9);
                    resultFromServer = resultFromServer.Remove(resultFromServer.Length - 3);
                    byte[] imageBytes = System.Convert.FromBase64String(resultFromServer);
                    outputRenderTexture = new RenderTexture(inputTexture.width, inputTexture.height, 16, RenderTextureFormat.ARGB32);
                    outputRenderTexture.Create();
                    outputTexture = new Texture2D(outputRenderTexture.width, outputRenderTexture.height);
                    outputTexture.LoadImage(imageBytes);
                    outputTexture.Apply();
                    File.WriteAllBytes(path.Substring(0, path.Length - Path.GetExtension(path).Length) + depthSuffix, outputTexture.EncodeToPNG());
                }
            }


            float progressStep = 1.0f / inputTexture.height;
            Texture2D texSource = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGB24, false, false);
            // clone original texture
            Color[] temp = inputTexture.GetPixels();
            texSource.SetPixels(temp);
            if (specularGroupEnabled)
            {
                Texture2D texSpecular = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGB24, false, false);
                Color[] pixels = new Color[inputTexture.width * inputTexture.height];
                for (int y = 0; y < inputTexture.height; y++)
                {
                    for (int x = 0; x < inputTexture.width; x++)
                    {
                        float bw = inputTexture.GetPixel(x, y).grayscale;
                        // adjust contrast
                        bw *= bw * specularContrast;
                        bw = bw < (specularContrast * specularCutOff) ? -1 : bw;
                        bw = Mathf.Clamp(bw, -1, 1);
                        bw *= 0.5f;
                        bw += 0.5f;
                        Color c = new Color(bw, bw, bw, 1);
                        pixels[x + y * inputTexture.width] = c;
                    }

                    // progress bar
                    progress += progressStep;
                    if (EditorUtility.DisplayCancelableProgressBar(appName, "Creating specular map..", progress))
                    {
                        Debug.Log(appName + ": Specular map creation cancelled by user (strange texture results will occur)");
                        EditorUtility.ClearProgressBar();
                        break;
                    }
                }
                EditorUtility.ClearProgressBar();

                // apply texture
                texSpecular.SetPixels(pixels);

                // save texture as png
                byte[] bytes3 = texSpecular.EncodeToPNG();
                File.WriteAllBytes(path.Substring(0, path.Length - Path.GetExtension(path).Length) + specularSuffix, bytes3);
                // cleanup texture
                UnityEngine.Object.DestroyImmediate(texSpecular);
            }

            if (normalGroupEnabled)
            {
                progress = 0;
                Color[] pixels = new Color[inputTexture.width * inputTexture.height];
                // sobel filter
                Texture2D texNormal = new Texture2D(inputTexture.width, inputTexture.height, TextureFormat.RGB24, false, false);
                Vector3 vScale = new Vector3(0.3333f, 0.3333f, 0.3333f);
                for (int y = 0; y < inputTexture.height; y++)
                {
                    for (int x = 0; x < inputTexture.width; x++)
                    {
                        Color tc = texSource.GetPixel(x - 1, y - 1);
                        Vector3 cSampleNegXNegY = new Vector3(tc.r, tc.g, tc.g);
                        tc = texSource.GetPixel(x, y - 1);
                        Vector3 cSampleZerXNegY = new Vector3(tc.r, tc.g, tc.g);
                        tc = texSource.GetPixel(x + 1, y - 1);
                        Vector3 cSamplePosXNegY = new Vector3(tc.r, tc.g, tc.g);
                        tc = texSource.GetPixel(x - 1, y);
                        Vector3 cSampleNegXZerY = new Vector3(tc.r, tc.g, tc.g);
                        tc = texSource.GetPixel(x + 1, y);
                        Vector3 cSamplePosXZerY = new Vector3(tc.r, tc.g, tc.g);
                        tc = texSource.GetPixel(x - 1, y + 1);
                        Vector3 cSampleNegXPosY = new Vector3(tc.r, tc.g, tc.g);
                        tc = texSource.GetPixel(x, y + 1);
                        Vector3 cSampleZerXPosY = new Vector3(tc.r, tc.g, tc.g);
                        tc = texSource.GetPixel(x + 1, y + 1);
                        Vector3 cSamplePosXPosY = new Vector3(tc.r, tc.g, tc.g);
                        float fSampleNegXNegY = Vector3.Dot(cSampleNegXNegY, vScale);
                        float fSampleZerXNegY = Vector3.Dot(cSampleZerXNegY, vScale);
                        float fSamplePosXNegY = Vector3.Dot(cSamplePosXNegY, vScale);
                        float fSampleNegXZerY = Vector3.Dot(cSampleNegXZerY, vScale);
                        float fSamplePosXZerY = Vector3.Dot(cSamplePosXZerY, vScale);
                        float fSampleNegXPosY = Vector3.Dot(cSampleNegXPosY, vScale);
                        float fSampleZerXPosY = Vector3.Dot(cSampleZerXPosY, vScale);
                        float fSamplePosXPosY = Vector3.Dot(cSamplePosXPosY, vScale);
                        float edgeX = (fSampleNegXNegY - fSamplePosXNegY) * 0.25f + (fSampleNegXZerY - fSamplePosXZerY) * 0.5f + (fSampleNegXPosY - fSamplePosXPosY) * 0.25f;
                        float edgeY = (fSampleNegXNegY - fSampleNegXPosY) * 0.25f + (fSampleZerXNegY - fSampleZerXPosY) * 0.5f + (fSamplePosXNegY - fSamplePosXPosY) * 0.25f;
                        Vector2 vEdge = new Vector2(edgeX, edgeY) * normalStrength;
                        Vector3 norm = new Vector3(vEdge.x, vEdge.y, 1.0f).normalized;
                        Color c = new Color(norm.x * 0.5f + 0.5f, norm.y * 0.5f + 0.5f, norm.z * 0.5f + 0.5f, 1);
                        pixels[x + y * inputTexture.width] = c;
                    }
                    // progress bar
                    progress += progressStep;
                    if (EditorUtility.DisplayCancelableProgressBar(appName, "Creating normal map..", progress))
                    {
                        Debug.Log(appName + ": Normal map creation cancelled by user (strange texture results will occur)");
                        EditorUtility.ClearProgressBar();
                        break;
                    }
                }

                // apply texture
                texNormal.SetPixels(pixels);

                // save texture as png
                byte[] bytes2 = texNormal.EncodeToPNG();
                File.WriteAllBytes(path.Substring(0, path.Length - Path.GetExtension(path).Length) + normalSuffix, bytes2);
                AssetDatabase.Refresh();
                TextureImporter importerNor = (TextureImporter)TextureImporter.GetAtPath(path.Substring(0, path.Length - Path.GetExtension(path).Length) + normalSuffix);
                importerNor.textureType = TextureImporterType.NormalMap;
                TextureImporter OrgImporter = (TextureImporter)TextureImporter.GetAtPath(path);
                OrgImporter.sRGBTexture = true;
                importerNor.SaveAndReimport();

                // remove progressbar
                EditorUtility.ClearProgressBar();

                // cleanup texture
                UnityEngine.Object.DestroyImmediate(texNormal);
            }

            // cleanup texture
            UnityEngine.Object.DestroyImmediate(texSource);

            // restore isReadable setting, if we had changed it
            if (setReadable)
            {
                textureImporter.isReadable = false;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                setReadable = false;
            }
            AssetDatabase.Refresh();

        }

        private string Post(string url, string bodyJsonString)
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SendWebRequest();
            while (!request.isDone)
            {
                //Timeout Code
            }
            if (request.responseCode.ToString() != "200")
            {
                return null;
            }
            else
            {
                return request.downloadHandler.text;

            }
        }
        private string Get(string url)
        {
            UnityWebRequest request = new UnityWebRequest(url, "GET");
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SendWebRequest();
            while (!request.isDone)
            {
                //Timeout Code
            }
            if (request.responseCode.ToString() != "200")
            {
                return null;
            }
            else
            {
                return request.downloadHandler.text;
            }
        }

    }


} // namespace
