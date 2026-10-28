// Avatar Creation Manager v0.8.2 (VCC package + Smart Engine + About) — By DTAA
// Unity 2022.3 + VRChat SDK3 editor-only utility. Installed by VCC under Packages/com.wemakeshit.acm/Editor/.
// Does not use EasyLoco or Modular Avatar. Original menus, parameters and FX assets are never modified.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

public sealed partial class AvatarCreationManager : EditorWindow
{
    [Serializable]
    private class Pose
    {
        public string label = "";
        public int id;
        public AnimationClip clip;
        public Texture2D icon;
    }

    [Serializable]
    private class PoseFile
    {
        public string label;
        public int id;
        public string guid;
        public long localId;
        public string iconGuid;
        public long iconId;
    }

    [Serializable]
    private class Dance
    {
        public string label = "";
        public int id;
        public AnimationClip clip;
        public AudioClip audio;
        public bool loop = true;
        public float volume = .25f;
        public Texture2D icon;
    }

    [Serializable]
    private class DanceFile
    {
        public string label;
        public int id;
        public string clipGuid; public long clipId;
        public string audioGuid; public long audioId;
        public string iconGuid; public long iconId;
        public bool loop;
        public float volume;
    }

    [Serializable]
    private class ToggleEntry
    {
        public string id = "";
        public string label = "";
        public string category = "Accessories";
        public string menuCategory = ""; // v0.6.5 legacy path, migrated when resolvable.
        public string destinationKey = ""; // Stable path:/custom: category selection.
        public bool automatic = true;
        public GameObject target;
        public string lastKnownTargetPath = ""; // Keep a lost scene reference recoverable.
        public AnimationClip onClip;
        public AnimationClip offClip;
        public bool saved = true;
        public bool synced = true;
        public bool defaultOn;
        public Texture2D icon;
    }

    [Serializable]
    private class ToggleFile
    {
        public string id;
        public string label;
        public string category;
        public string menuCategory;
        public string destinationKey;
        public bool automatic;
        public string targetPath;
        public string onGuid; public long onId;
        public string offGuid; public long offId;
        public string iconGuid; public long iconId;
        public bool saved;
        public bool synced;
        public bool defaultOn;
    }

    [Serializable]
    private class CategoryEntry
    {
        public string id = "";
        public string name = "";
        public string parentKey = "path:Appearance";
        public Texture2D icon;
    }

    [Serializable]
    private class CategoryFile
    {
        public string id;
        public string name;
        public string parentKey;
        public string iconGuid;
        public long iconId;
    }

    private class CategoryOption
    {
        public string key;
        public string path;
        public string label;
    }

    [Serializable]
    private class MenuIconsFile
    {
        public string standingGuid; public long standingId;
        public string crouchingGuid; public long crouchingId;
        public string proneGuid; public long proneId;
        public string dancesGuid; public long dancesId;
        public string clothingGuid; public long clothingId;
        public string accessoriesGuid; public long accessoriesId;
        public string customGuid; public long customId;
        public string moreGuid; public long moreId;
        public string acmGuid; public long acmId;
        public string hatsGuid; public long hatsId;
        public string glassesGuid; public long glassesId;
        public string appearanceGuid; public long appearanceId;
        public string posesGuid; public long posesId;
    }

    [Serializable]
    private class ProfileFile
    {
        public List<PoseFile> standing = new List<PoseFile>();
        public List<PoseFile> crouching = new List<PoseFile>();
        public List<PoseFile> prone = new List<PoseFile>();
        public string sourceControllerPath;
        public List<DanceFile> dances = new List<DanceFile>();
        public List<ToggleFile> toggles = new List<ToggleFile>();
        public List<CategoryFile> categories = new List<CategoryFile>();
        public MenuIconsFile menuIcons = new MenuIconsFile();
        public int toggleMenuLayout;
    }

    [Serializable]
    private class OriginalFile
    {
        public string menuPath;
        public string parameterPath;
        public string controllerPath;
        public bool baseWasDefault;
        public bool customizedLayers;
        public bool customizedExpressions;
        public string generatedFolder;
        public string actionPath;
        public string fxPath;
        public bool actionWasDefault;
        public bool fxWasDefault;
        public bool acmMetadataInitialized;
        public List<string> ownedParameters = new List<string>();
    }

    private const string Root = "Assets/AvatarPoseManager";
    private const string Profiles = Root + "/Profiles";
    private const string Generated = Root + "/Generated";
    // All generated assets stay inside one disposable Build_* folder. Group by type
    // without moving existing builds: older flat Build_* folders remain valid.
    private const string MenusSubfolder = "Menus";
    private const string ParametersSubfolder = "Parameters";
    private const string ControllersSubfolder = "Controllers";
    private const string AnimationsSubfolder = "Animations";
    private const string BlendTreesSubfolder = "BlendTrees";

    private static string BuildSubfolder(string build, string section)
    {
        return build + "/" + section;
    }

    private static string BuildAssetPath(string build, string section, string file)
    {
        return BuildSubfolder(build, section) + "/" + file;
    }

    // v0.7.3 and older wrote directly in Build_*. Recognize both layouts when
    // updating/recovering, so the user's assigned controller/menus aren't mistaken
    // for an unrelated manual setup after upgrading ACM.
    private static bool MatchesBuildAsset(string path, string build,
        string section, string file)
    {
        return !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(build) &&
            (path == build + "/" + file || path == BuildAssetPath(build, section, file));
    }

    private static void CreateBuildSubfolders(string build)
    {
        foreach (string name in new [] { MenusSubfolder, ParametersSubfolder,
            ControllersSubfolder, AnimationsSubfolder, BlendTreesSubfolder })
        {
            string path = BuildSubfolder(build, name);
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(build, name);
            if (!AssetDatabase.IsValidFolder(path))
                throw new Exception("ACM couldn't create generated asset folder: " + path);
        }
    }
    private const string BannerAsset = "Packages/com.wemakeshit.acm/Editor/AvatarCreationManager_Banner.png";
    private const string SampleBase =
        "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers/vrc_AvatarV3LocomotionLayer.controller";

    private static readonly string[] Parameters =
        { "APM_Standing", "APM_Crouching", "APM_Prone" };
    private static readonly string[] StanceNames = { "Standing", "Crouching", "Prone" };
    private static readonly string[] IdleClipNames =
        { "proxy_stand_still", "proxy_crouch_still", "proxy_low_crawl_still" };

    [SerializeField] private VRCAvatarDescriptor avatar;
    [SerializeField] private AnimatorController sourceBase;
    // Explicit recovery selectors for avatars whose menu/parameters were previously
    // redirected to a generated ACM root. Never guess among unrelated menu assets.
    [SerializeField] private VRCExpressionsMenu originalRootMenu;
    [SerializeField] private VRCExpressionParameters originalExpressionParameters;
    [SerializeField] private AnimatorController originalFXController;
    [SerializeField] private AnimatorController originalActionController;
    [SerializeField] private List<Pose> standing = new List<Pose>();
    [SerializeField] private List<Pose> crouching = new List<Pose>();
    [SerializeField] private List<Pose> prone = new List<Pose>();
    [SerializeField] private Vector2 scroll;
    [SerializeField] private List<Dance> dances = new List<Dance>();
    [SerializeField] private List<ToggleEntry> toggles = new List<ToggleEntry>();
    // Excludes unfinished NEW entries only while a build is running; editor data is unchanged.
    private List<ToggleEntry> activeBuildToggles;
    private List<ToggleEntry> GenerationToggles { get { return activeBuildToggles ?? toggles; } }
    [SerializeField] private List<CategoryEntry> customCategories = new List<CategoryEntry>();
    [SerializeField] private string newCategoryName = "";
    [SerializeField] private string newCategoryParent = "path:Appearance";
    [SerializeField] private Texture2D newCategoryIcon;
    [SerializeField] private int activeTab;
    [SerializeField] private bool showFullBanner = false;
    [SerializeField] private bool showStanding = true;
    [SerializeField] private bool showCrouching = false;
    [SerializeField] private bool showProne = false;
    // Previous profile layout value retained only for backward compatibility.
    // v0.6.4 always routes items into their matching existing/new category menu.
    private enum ToggleMenuLayout { DirectToggles, RootCategoryMenus }
    [SerializeField] private ToggleMenuLayout toggleMenuLayout = ToggleMenuLayout.RootCategoryMenus;
    [SerializeField] private bool showIconSettings = false;
    [SerializeField] private Texture2D standingMenuIcon;
    [SerializeField] private Texture2D crouchingMenuIcon;
    [SerializeField] private Texture2D proneMenuIcon;
    [SerializeField] private Texture2D dancesMenuIcon;
    [SerializeField] private Texture2D clothingMenuIcon;
    [SerializeField] private Texture2D accessoriesMenuIcon;
    [SerializeField] private Texture2D customMenuIcon;
    [SerializeField] private Texture2D moreMenuIcon;
    [SerializeField] private Texture2D acmMenuIcon;
    [SerializeField] private Texture2D hatsMenuIcon;
    [SerializeField] private Texture2D glassesMenuIcon;
    [SerializeField] private Texture2D appearanceMenuIcon;
    [SerializeField] private Texture2D posesMenuIcon;
    private const string DanceParam = "ACM_Dance";
    private const string TogglePrefix = "ACM_T_";
    private const string OwnedFxPrefix = "ACM | ";
    private const string OwnedDanceLayer = "ACM | Dances";
    private const string SampleAction = "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers/vrc_AvatarV3ActionLayer.controller";
    private const string SampleFX = "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers/vrc_AvatarV3FaceLayer.controller";
    [SerializeField] private string loadedAvatarKey = "";

    // Persist the editor-scene avatar's identity over Unity's Play Mode scene reload.
    // The scene reference itself can become Unity's "fake null" on reload.
    [SerializeField] private string avatarGlobalId = "";
    [SerializeField] private string avatarScenePath = "";
    [SerializeField] private string avatarSiblingPath = "";
    [SerializeField] private string avatarDisplayName = "";

    private void OnEnable()
    {
        // Unity can deserialize editor collections as null after a domain reload.
        if (standing == null) standing = new List<Pose>();
        if (crouching == null) crouching = new List<Pose>();
        if (prone == null) prone = new List<Pose>();
        if (dances == null) dances = new List<Dance>();
        if (toggles == null) toggles = new List<ToggleEntry>();
        if (customCategories == null) customCategories = new List<CategoryEntry>();
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (avatar != null)
        {
            RememberAvatar();
            if (string.IsNullOrEmpty(loadedAvatarKey) && !EditorApplication.isPlayingOrWillChangePlaymode)
                LoadProfile();
        }
        else if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += RebindAfterReload;
            EditorApplication.delayCall += AutoFindAvatarIfNeeded;
        }
    }

    private void AutoFindAvatarIfNeeded()
    {
        if (this == null || avatar != null || EditorApplication.isPlayingOrWillChangePlaymode ||
            !string.IsNullOrEmpty(avatarGlobalId)) return;
        var matches = Resources.FindObjectsOfTypeAll<VRCAvatarDescriptor>()
            .Where(x => x != null && x.gameObject.scene.IsValid() && x.gameObject.scene.isLoaded)
            .ToArray();
        if (matches.Length == 1)
        {
            avatar = matches[0]; RememberAvatar(); LoadProfile(); Repaint();
        }
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall -= RebindAfterReload;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // Save BEFORE Unity destroys and reconstructs scene objects.
            if (avatar != null)
            {
                RememberAvatar();
                SaveProfile();
            }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall -= RebindAfterReload;
            EditorApplication.delayCall += RebindAfterReload;
        }
        Repaint();
    }

    private static string SiblingIndexPath(Transform transform)
    {
        var indices = new Stack<string>();
        for (Transform current = transform; current != null; current = current.parent)
            indices.Push(current.GetSiblingIndex().ToString());
        return string.Join(".", indices.ToArray());
    }

    private void RememberAvatar()
    {
        if (avatar == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        avatarGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(avatar).ToString();
        avatarScenePath = avatar.gameObject.scene.path;
        avatarSiblingPath = SiblingIndexPath(avatar.transform);
        avatarDisplayName = avatar.name;
    }

    private VRCAvatarDescriptor FindRememberedAvatar()
    {
        if (!string.IsNullOrEmpty(avatarGlobalId) &&
            GlobalObjectId.TryParse(avatarGlobalId, out var globalId))
        {
            var resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId)
                as VRCAvatarDescriptor;
            if (resolved != null && resolved.gameObject.scene.IsValid()) return resolved;
        }

        // Fallback for a reloaded scene whose GlobalObjectId has changed.
        // Require BOTH the scene and the transform path/name to match.
        if (string.IsNullOrEmpty(avatarScenePath) || string.IsNullOrEmpty(avatarSiblingPath))
            return null;
        var indices = avatarSiblingPath.Split('.');
        for (int n = 0; n < SceneManager.sceneCount; n++)
        {
            Scene scene = SceneManager.GetSceneAt(n);
            if (!scene.isLoaded || scene.path != avatarScenePath) continue;
            var roots = scene.GetRootGameObjects();
            if (!int.TryParse(indices[0], out int rootIndex) ||
                rootIndex < 0 || rootIndex >= roots.Length) return null;
            Transform current = roots[rootIndex].transform;
            for (int i = 1; i < indices.Length && current != null; i++)
            {
                if (!int.TryParse(indices[i], out int childIndex) ||
                    childIndex < 0 || childIndex >= current.childCount) return null;
                current = current.GetChild(childIndex);
            }
            if (current != null && current.name == avatarDisplayName)
                return current.GetComponent<VRCAvatarDescriptor>();
        }
        return null;
    }

    private void RebindAfterReload()
    {
        if (this == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (avatar != null) return;
        var restored = FindRememberedAvatar();
        if (restored == null) return;
        avatar = restored;
        // Keep any already-loaded pose list; only reload if this avatar's profile differs.
        string nextKey = Key();
        if (string.IsNullOrEmpty(loadedAvatarKey) || loadedAvatarKey != nextKey)
            LoadProfile();
        Repaint();
    }

    // The header is drawn outside the scrolling region so it stays visible while
    // editing long lists of standing, crouching and prone animations.
    private Texture2D _bannerTexture;

    private Texture2D GetBannerTexture()
    {
        if (_bannerTexture != null) return _bannerTexture;

        // Standard installation path (included in the ZIP).
        _bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BannerAsset);
        if (_bannerTexture != null) return _bannerTexture;

        // Also support users who placed the image elsewhere inside Assets.
        string[] matches = AssetDatabase.FindAssets("AvatarCreationManager_Banner t:Texture2D");
        foreach (string guid in matches)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileNameWithoutExtension(path),
                "AvatarCreationManager_Banner", StringComparison.OrdinalIgnoreCase))
            {
                _bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (_bannerTexture != null) return _bannerTexture;
            }
        }
        return null;
    }

    // The supplied ACM artwork contains its own title. Small by default, with
    // an optional expanded view. Never draw a duplicate title or overlay.
    private void DrawBanner()
    {
        Texture2D banner = GetBannerTexture();
        if (banner == null)
        {
            EditorGUILayout.LabelField("AVATAR CREATION MANAGER", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Banner not found: " + BannerAsset, MessageType.Warning);
            return;
        }

        float contentWidth = Mathf.Max(1f, EditorGUIUtility.currentViewWidth - 16f);
        float naturalHeight = contentWidth * ((float)banner.height / banner.width);
        float headerHeight = showFullBanner ? naturalHeight :
            Mathf.Min(naturalHeight, Mathf.Clamp(position.height * .16f, 88f, 124f));
        Rect rect = GUILayoutUtility.GetRect(0f, headerHeight,
            GUILayout.ExpandWidth(true), GUILayout.Height(headerHeight));
        if (showFullBanner)
        {
            GUI.DrawTexture(rect, banner, ScaleMode.ScaleToFit, true);
        }
        else
        {
            // Full width, compact header: crop vertically around the banner's
            // built-in title instead of covering it with a separate title.
            float visibleV = Mathf.Clamp01(headerHeight / contentWidth *
                ((float)banner.width / banner.height));
            float bottom = Mathf.Clamp(.55f - visibleV * .5f, 0f, 1f - visibleV);
            GUI.DrawTextureWithTexCoords(rect, banner,
                new Rect(0f, bottom, 1f, visibleV), true);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            bool selected = GUILayout.Toggle(showFullBanner,
                showFullBanner ? "Collapse banner" : "View full banner",
                EditorStyles.miniButton, GUILayout.Width(132f));
            if (selected != showFullBanner)
            {
                showFullBanner = selected;
                Repaint();
            }
        }
    }

    private static void DrawPlayModePreview(List<Pose> standing, List<Pose> crouching, List<Pose> prone)
    {
        EditorGUILayout.LabelField("Standing poses: " + standing.Count);
        EditorGUILayout.LabelField("Crouching poses: " + crouching.Count);
        EditorGUILayout.LabelField("Prone poses: " + prone.Count);
    }

    [MenuItem("Tools/Avatar Creation Manager")]
    public static void ShowTool()
    {
        var window = GetWindow<AvatarCreationManager>("Avatar Creation Manager");
        window.minSize = new Vector2(420, 440);
        window.Show();
    }

    private void OnGUI()
    {
        // Permanent branded header above the scrolling controls.
        // This is the actual EditorWindow UI, not an external preview.
        EditorGUILayout.Space(5);
        DrawBanner();
        // Available even when no avatar is selected or Unity is in Play Mode.
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("About ACM", GUILayout.Height(26)))
                ACMAboutWindow.ShowWindow();
            if (GUILayout.Button("Check for Updates", GUILayout.Height(26)))
            {
                ACMAboutWindow.ShowWindow();
                ACMUpdater.CheckForUpdates(true);
            }
        }
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.HelpBox(
            "ACM uses your existing menus, parameters and controllers. It makes new copies and preserves manual toggles.",
            MessageType.Info);

        // Play Mode scene reload invalidates EditorWindow's scene-object reference.
        // Keep the pose list and avatar identity, and show a read-only view.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Avatar", string.IsNullOrEmpty(avatarDisplayName)
                ? "Last selected avatar" : avatarDisplayName);
            EditorGUILayout.HelpBox(
                "Play Mode: ACM is temporarily read-only. Your saved poses are kept. " +
                "Exit Play Mode and your avatar will reconnect automatically.", MessageType.Info);
            DrawPlayModePreview(standing, crouching, prone);
            EditorGUILayout.LabelField("Dances: " + dances.Count +
                "    Toggles: " + toggles.Count);
            EditorGUILayout.EndScrollView();
            Watermark();
            return;
        }

        if (avatar == null && !string.IsNullOrEmpty(avatarGlobalId))
            RebindAfterReload();

        VRCAvatarDescriptor selected = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(
            "Avatar", avatar, typeof(VRCAvatarDescriptor), true);
        if (selected != avatar)
        {
            avatar = selected;
            // Override selections always belong to the avatar the user picked.
            originalRootMenu = null;
            originalExpressionParameters = null;
            originalFXController = null;
            originalActionController = null;
            if (avatar != null)
            {
                RememberAvatar();
                LoadProfile();
            }
            else
            {
                // Only the user's field change intentionally clears selection.
                avatarGlobalId = avatarScenePath = avatarSiblingPath = avatarDisplayName = "";
                loadedAvatarKey = "";
            }
        }
        if (avatar == null)
        {
            EditorGUILayout.HelpBox(
                "Choose your avatar root with VRC Avatar Descriptor. " +
                "If you just exited Play Mode, try Reconnect Avatar.", MessageType.Warning);
            if (GUILayout.Button("Find Avatar Automatically")) AutoFindAvatarIfNeeded();
            var candidates = Resources.FindObjectsOfTypeAll<VRCAvatarDescriptor>()
                .Where(x => x != null && x.gameObject.scene.IsValid() && x.gameObject.scene.isLoaded)
                .ToArray();
            if (candidates.Length > 1)
            {
                EditorGUILayout.LabelField("Found more than one avatar. Choose one:");
                foreach (var candidate in candidates)
                    if (GUILayout.Button("Use " + candidate.name))
                    { avatar = candidate; RememberAvatar(); LoadProfile(); break; }
            }
            if (GUILayout.Button("Reconnect Last Avatar")) RebindAfterReload();
            EditorGUILayout.EndScrollView();
            Watermark();
            return;
        }

        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        sourceBase = (AnimatorController)EditorGUILayout.ObjectField(
            new GUIContent("Source Base (optional)",
                "Leave empty to use your avatar's original Base controller, or the SDK example when Base is Default."),
            sourceBase, typeof(AnimatorController), false);

        EditorGUILayout.LabelField("Existing Avatar Setup", EditorStyles.boldLabel);
        originalRootMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField(
            new GUIContent("Original Root Menu", "Drag the ORIGINAL menu containing your Appearance/Weapons/etc. Not a generated ACM Main Menu."),
            originalRootMenu, typeof(VRCExpressionsMenu), false);
        originalExpressionParameters = (VRCExpressionParameters)EditorGUILayout.ObjectField(
            new GUIContent("Original Parameters", "Drag the ORIGINAL VRC Expression Parameters asset used by your manual toggles."),
            originalExpressionParameters, typeof(VRCExpressionParameters), false);
        originalFXController = (AnimatorController)EditorGUILayout.ObjectField(
            new GUIContent("Original FX (optional)", "Choose the manual FX Animator Controller if an old ACM generation replaced your assigned FX."),
            originalFXController, typeof(AnimatorController), false);
        originalActionController = (AnimatorController)EditorGUILayout.ObjectField(
            new GUIContent("Original Action (optional)", "Choose the manual Action Animator Controller only if it was replaced by an old ACM build."),
            originalActionController, typeof(AnimatorController), false);
        bool manualSourceSet = originalRootMenu != null || originalExpressionParameters != null;
        if (manualSourceSet && (originalRootMenu == null || originalExpressionParameters == null))
            EditorGUILayout.HelpBox("Choose BOTH Original Root Menu and Original Parameters to recover an older ACM setup.", MessageType.Warning);
        string currentRootPath = AssetDatabase.GetAssetPath(avatar.expressionsMenu);
        string currentParamsPath = AssetDatabase.GetAssetPath(avatar.expressionParameters);
        if (IsGeneratedAcmAsset(currentRootPath) || IsGeneratedAcmAsset(currentParamsPath))
            EditorGUILayout.HelpBox(
                "Your avatar currently points at ACM-generated Expressions assets. " +
                "ACM will preserve the original menu/parameters recorded before installation. " +
                "If those records are wrong, assign your real original assets above (e.g. Skele Menu and SkelePara). " +
                "This does not edit either original asset.", MessageType.Warning);
        else if (originalRootMenu != null && originalExpressionParameters != null)
            EditorGUILayout.HelpBox("ACM will copy " + originalRootMenu.name + " (" +
                (originalRootMenu.controls == null ? 0 : originalRootMenu.controls.Count) +
                " top-level controls) and " + originalExpressionParameters.name +
                " into a combined generated setup. Original assets stay unchanged.", MessageType.Info);
        if (originalRootMenu != null && originalExpressionParameters != null &&
            GUILayout.Button("Restore Selected Original Setup (No Generate)", GUILayout.Height(28)))
            RestoreSelectedOriginalSetup();

        activeTab = GUILayout.Toolbar(activeTab, new [] { "Poses", "Dances", "Toggles", "Categories" });
        EditorGUILayout.Space(7);
        if (activeTab == 0)
        {
            DrawCategory("Standing Idles", standing, ref showStanding);
            DrawCategory("Crouching Poses", crouching, ref showCrouching);
            DrawCategory("Prone Poses", prone, ref showProne);
        }
        else if (activeTab == 1) DrawDances();
        else if (activeTab == 2) DrawToggles();
        else DrawCustomCategories();

        DrawMenuIconSettings();
        if (EditorGUI.EndChangeCheck() || GUI.changed) SaveProfile();

        EditorGUILayout.Space(5);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Pose List", GUILayout.Height(28))) SaveProfile();
            if (GUILayout.Button("Reload Pose List", GUILayout.Height(28))) LoadProfile();
        }

        DrawSmartHealthPanel();

        bool installed = HasActiveGeneratedBuild();
        if (installed)
            EditorGUILayout.HelpBox(
                "ACM installed. Edit poses, dances, or toggles then click Update. " +
                "Existing manual controls are preserved.", MessageType.Info);
        if (GUILayout.Button("Repair Avatar Setup", GUILayout.Height(28)))
        {
            // Explicit user-selected originals take precedence over an unreliable
            // old manifest when repairing a previously generated avatar.
            if (originalRootMenu != null && originalExpressionParameters != null)
                RestoreSelectedOriginalSetup();
            else
                RepairAvatarSetup(true);
        }
        EditorGUILayout.HelpBox(
            "Appearance categories: create nested tabs in Categories, then choose each toggle's " +
            "destination in Toggles. Existing submenus are reused; full menus get Page 2, " +
            "Page 3, etc. Poses and Dances stay under Avatar Creation Manager.", MessageType.Info);
        EditorGUILayout.HelpBox(
            "Safe integration: existing manual menus and toggle animations are retained. " +
            "ACM creates missing menu/parameter assets only with your permission. " +
            "If a generated toggle would animate something already controlled by another FX clip, ACM stops instead of overwriting it.",
            MessageType.None);
        if (GUILayout.Button(installed ? "Update Avatar" : "Generate Avatar",
                GUILayout.Height(38)))
            Generate();
        if (File.Exists(OriginalPath()) &&
            GUILayout.Button("Restore Original Avatar Setup", GUILayout.Height(28)))
            Restore();
        EditorGUILayout.EndScrollView();
        Watermark();
    }

    private bool HasActiveGeneratedBuild()
    {
        if (avatar == null || !File.Exists(OriginalPath())) return false;
        try
        {
            var info = JsonUtility.FromJson<OriginalFile>(File.ReadAllText(OriginalPath()));
            return info != null && !string.IsNullOrEmpty(info.generatedFolder) &&
                MatchesBuildAsset(AssetDatabase.GetAssetPath(avatar.expressionsMenu),
                    info.generatedFolder, MenusSubfolder, "MainMenu.asset") &&
                MatchesBuildAsset(AssetDatabase.GetAssetPath(avatar.expressionParameters),
                    info.generatedFolder, ParametersSubfolder, "Parameters.asset");
        }
        catch { return false; }
    }

    private static void Watermark()
    {
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(.65f, .65f, .65f, .65f);
        EditorGUILayout.BeginHorizontal(GUILayout.Height(23));
        GUILayout.FlexibleSpace();
        GUILayout.Label("By DTAA", style, GUILayout.Width(95));
        GUILayout.Space(12);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawCategory(string heading, List<Pose> list, ref bool expanded)
    {
        EditorGUILayout.Space(3);
        expanded = EditorGUILayout.Foldout(expanded,
            heading + " (" + list.Count + ")", true);
        if (!expanded) return;
        for (int i = 0; i < list.Count; i++)
        {
            Pose pose = list[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Pose " + (i + 1) + " | ID " +
                        (pose.id == 0 ? "new" : pose.id.ToString()), GUILayout.Width(145));
                    if (GUILayout.Button("▲", GUILayout.Width(27)) && i > 0)
                    {
                        var other = list[i - 1]; list[i - 1] = pose; list[i] = other;
                        GUI.changed = true;
                    }
                    if (GUILayout.Button("▼", GUILayout.Width(27)) && i < list.Count - 1)
                    {
                        var other = list[i + 1]; list[i + 1] = pose; list[i] = other;
                        GUI.changed = true;
                    }
                    if (GUILayout.Button("Remove", GUILayout.Width(66)))
                    {
                        list.RemoveAt(i);
                        GUI.changed = true;
                        break;
                    }
                }
                pose.label = EditorGUILayout.TextField("Menu name", pose.label);
                pose.clip = (AnimationClip)EditorGUILayout.ObjectField(
                    "Animation", pose.clip, typeof(AnimationClip), false);
                pose.icon = (Texture2D)EditorGUILayout.ObjectField(
                    "Action Wheel icon (optional)", pose.icon, typeof(Texture2D), false);
            }
        }
        if (GUILayout.Button("+ Add " + heading))
        {
            list.Add(new Pose());
            GUI.changed = true;
        }
    }

    private void DrawDances()
    {
        EditorGUILayout.HelpBox(
            "Drag dance animations here. Music is optional, PC-only, and plays at a short distance. " +
            "Only one dance can be active at a time.", MessageType.Info);
        for (int i = 0; i < dances.Count; i++)
        {
            var dance = dances[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Dance " + (i + 1), EditorStyles.boldLabel);
                    if (GUILayout.Button("Up", GUILayout.Width(35)) && i > 0)
                    { var d = dances[i-1]; dances[i-1] = dance; dances[i] = d; GUI.changed = true; }
                    if (GUILayout.Button("Down", GUILayout.Width(55)) && i < dances.Count-1)
                    { var d = dances[i+1]; dances[i+1] = dance; dances[i] = d; GUI.changed = true; }
                    if (GUILayout.Button("Remove", GUILayout.Width(66)))
                    { dances.RemoveAt(i); GUI.changed = true; break; }
                }
                dance.label = EditorGUILayout.TextField("Menu name", dance.label);
                dance.clip = (AnimationClip)EditorGUILayout.ObjectField("Dance", dance.clip, typeof(AnimationClip), false);
                dance.icon = (Texture2D)EditorGUILayout.ObjectField("Action Wheel icon (optional)", dance.icon, typeof(Texture2D), false);
                dance.audio = (AudioClip)EditorGUILayout.ObjectField("Music (optional)", dance.audio, typeof(AudioClip), false);
                if (dance.audio != null)
                {
                    dance.loop = EditorGUILayout.Toggle("Loop music", dance.loop);
                    dance.volume = EditorGUILayout.Slider("Music volume", dance.volume, 0f, .5f);
                }
            }
        }
        if (GUILayout.Button("+ Add Dance")) { dances.Add(new Dance()); GUI.changed = true; }
    }

    // Collect all existing Appearance submenus from the ORIGINAL, never from
    // a generated ACM root. Merge custom category definitions by stable IDs.
    private VRCExpressionsMenu CategorySourceMenu()
    {
        if (originalRootMenu != null && !IsGeneratedAcmAsset(
                AssetDatabase.GetAssetPath(originalRootMenu))) return originalRootMenu;
        if (avatar != null && File.Exists(OriginalPath()))
        {
            try
            {
                var original = JsonUtility.FromJson<OriginalFile>(File.ReadAllText(OriginalPath()));
                if (original != null && !IsGeneratedAcmAsset(original.menuPath))
                {
                    var found = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(original.menuPath);
                    if (found != null) return found;
                }
            }
            catch { /* UI-only discovery; generating still validates originals. */ }
        }
        if (avatar != null && avatar.expressionsMenu != null &&
            !IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(avatar.expressionsMenu)))
            return avatar.expressionsMenu;
        return null;
    }

    private static void CollectAppearanceFolders(VRCExpressionsMenu menu, string path,
        List<CategoryOption> choices, HashSet<int> ancestors, int depth)
    {
        if (menu == null || depth > 12 || !ancestors.Add(menu.GetInstanceID())) return;
        foreach (var ctrl in menu.controls ?? new List<VRCExpressionsMenu.Control>())
        {
            if (ctrl == null || ctrl.type != VRCExpressionsMenu.Control.ControlType.SubMenu ||
                ctrl.subMenu == null || string.IsNullOrWhiteSpace(ctrl.name)) continue;
            string childPath = path + "/" + ctrl.name.Trim();
            if (!choices.Any(o => string.Equals(o.path, childPath, StringComparison.OrdinalIgnoreCase)))
                choices.Add(new CategoryOption { key="path:" + childPath,
                    path=childPath, label=childPath });
            CollectAppearanceFolders(ctrl.subMenu, childPath, choices, ancestors, depth + 1);
        }
        ancestors.Remove(menu.GetInstanceID());
    }

    private List<CategoryOption> CategoryChoices()
    {
        var choices = new List<CategoryOption> {
            new CategoryOption { key="path:Appearance", path="Appearance", label="Appearance" }
        };
        var rootMenu = CategorySourceMenu();
        if (rootMenu != null)
        {
            var appearance = (rootMenu.controls ?? new List<VRCExpressionsMenu.Control>())
                .Where(c => c != null &&
                    string.Equals(c.name, "Appearance", StringComparison.OrdinalIgnoreCase) &&
                    c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu != null)
                .ToList();
            if (appearance.Count == 1)
                CollectAppearanceFolders(appearance[0].subMenu, "Appearance", choices,
                    new HashSet<int>(), 0);
        }

        var remaining = new List<CategoryEntry>((customCategories ??
            new List<CategoryEntry>()).Where(c => c != null));
        for (int pass=0; pass < customCategories.Count && remaining.Count > 0; pass++)
        {
            bool progressed = false;
            foreach (var c in remaining.ToArray())
            {
                var parent = choices.FirstOrDefault(o => o.key == c.parentKey);
                if (parent == null || string.IsNullOrWhiteSpace(c.name)) continue;
                choices.Add(new CategoryOption { key="custom:" + c.id,
                    path=parent.path + "/" + c.name.Trim(),
                    label=parent.path + "/" + c.name.Trim() });
                remaining.Remove(c);
                progressed = true;
            }
            if (!progressed) break;
        }
        return choices;
    }

    private static int DropdownCategory(string title, string current,
        List<CategoryOption> options, out string selected)
    {
        string[] labels = new [] { "(Select category)" }
            .Concat(options.Select(o => o.label)).ToArray();
        int index = options.FindIndex(o => o.key == current) + 1;
        int newIndex = EditorGUILayout.Popup(title, Mathf.Max(0, index), labels);
        selected = newIndex <= 0 ? "" : options[newIndex - 1].key;
        return newIndex;
    }

    private void DrawCustomCategories()
    {
        EditorGUILayout.HelpBox(
            "Build your Appearance menu hierarchy. Select a parent, name the new tab and " +
            "optionally assign a PNG icon. Existing Appearance submenus appear in the same list. " +
            "Categories can be nested; your original menu assets are NEVER modified.", MessageType.Info);
        var options = CategoryChoices();
        foreach (var item in customCategories.ToArray())
        {
            if (item == null) continue;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    item.name = EditorGUILayout.TextField("Category", item.name);
                    if (GUILayout.Button("Remove", GUILayout.Width(68)))
                    {
                        // Do not lose toggle/child destination associations silently.
                        bool used = toggles.Any(t => t.destinationKey == "custom:" + item.id) ||
                            customCategories.Any(c => c != item && c.parentKey == "custom:" + item.id);
                        if (used)
                            EditorUtility.DisplayDialog("Category in use",
                                "Move child categories and toggles out of this category before removing it.", "OK");
                        else { customCategories.Remove(item); GUI.changed = true; }
                        break;
                    }
                }
                var own = options.FirstOrDefault(o => o.key == "custom:" + item.id);
                var parents = options.Where(o => o.key != "custom:" + item.id &&
                    (own == null || !o.path.StartsWith(own.path + "/", StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                string selected;
                DropdownCategory("Inside category", item.parentKey, parents, out selected);
                if (!string.IsNullOrEmpty(selected)) item.parentKey = selected;
                item.icon = (Texture2D)EditorGUILayout.ObjectField("Menu icon (optional)",
                    item.icon, typeof(Texture2D), false);
                var located = options.FirstOrDefault(o => o.key == "custom:" + item.id);
                if (located != null)
                    EditorGUILayout.LabelField("Destination", located.path, EditorStyles.miniLabel);
                else EditorGUILayout.HelpBox("Choose a valid parent category and name.", MessageType.Warning);
            }
        }
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Add Appearance Category", EditorStyles.boldLabel);
        newCategoryName = EditorGUILayout.TextField("Category name", newCategoryName);
        string newParent;
        DropdownCategory("Parent category", newCategoryParent, CategoryChoices(), out newParent);
        if (!string.IsNullOrEmpty(newParent)) newCategoryParent = newParent;
        newCategoryIcon = (Texture2D)EditorGUILayout.ObjectField("Menu icon (optional)",
            newCategoryIcon, typeof(Texture2D), false);
        if (GUILayout.Button("+ Create Category", GUILayout.Height(27)))
        {
            if (string.IsNullOrWhiteSpace(newCategoryName) ||
                newCategoryName.IndexOfAny(new [] { '/', '\\' }) >= 0)
                EditorUtility.DisplayDialog("Invalid name", "Enter a category name without slashes.", "OK");
            else
            {
                customCategories.Add(new CategoryEntry { id=Guid.NewGuid().ToString("N"),
                    name=newCategoryName.Trim(), parentKey=newCategoryParent,
                    icon=newCategoryIcon });
                newCategoryName = "";
                newCategoryIcon = null;
                GUI.changed = true;
            }
        }
    }

    private void DrawToggles()
    {
        EditorGUILayout.HelpBox(
            "Each toggle chooses an Appearance category. Open the Categories tab to create " +
            "nested tabs or reuse folders already in your original Appearance menu. " +
            "Automatic toggles animate a child object's visibility; manual toggles use your clips.",
            MessageType.Info);
        var destinations = CategoryChoices();
        foreach (var category in new [] { "Clothing", "Accessories", "Custom" })
        {
            EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
            for (int i = 0; i < toggles.Count; i++)
            {
                var item = toggles[i];
                if (item.category != category) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        item.label = EditorGUILayout.TextField("Name", item.label);
                        if (GUILayout.Button("Remove", GUILayout.Width(65)))
                        { toggles.RemoveAt(i); GUI.changed = true; break; }
                    }
                    item.automatic = EditorGUILayout.Toggle("Automatic setup", item.automatic);
                    if (item.automatic)
                    {
                        item.target = (GameObject)EditorGUILayout.ObjectField(
                            "Show / Hide object", item.target, typeof(GameObject), true);
                        if (item.target == null)
                            EditorGUILayout.HelpBox(
                                "Unconfigured draft. Select a child GameObject; ACM will try to " +
                                "reconnect a uniquely named child when you generate. New unfinished " +
                                "drafts are skipped without blocking other features.",
                                MessageType.Info);
                    }
                    else
                    {
                        item.onClip = (AnimationClip)EditorGUILayout.ObjectField(
                            "On animation", item.onClip, typeof(AnimationClip), false);
                        item.offClip = (AnimationClip)EditorGUILayout.ObjectField(
                            "Off animation", item.offClip, typeof(AnimationClip), false);
                    }
                    item.icon = (Texture2D)EditorGUILayout.ObjectField(
                        "Action Wheel icon (optional)", item.icon, typeof(Texture2D), false);
                    // Migrate a v0.6.5 explicit path when it matches an existing folder.
                    if (string.IsNullOrEmpty(item.destinationKey) &&
                        !string.IsNullOrWhiteSpace(item.menuCategory))
                    {
                        var matched = destinations.FirstOrDefault(o => string.Equals(
                            o.path, item.menuCategory.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (matched != null) item.destinationKey = matched.key;
                    }
                    string selected;
                    DropdownCategory("Appearance category", item.destinationKey,
                        destinations, out selected);
                    item.destinationKey = selected;
                    if (string.IsNullOrEmpty(item.destinationKey))
                        EditorGUILayout.HelpBox("Choose a category to include this toggle.", MessageType.Warning);
                    item.defaultOn = EditorGUILayout.Toggle("Starts ON", item.defaultOn);
                    item.saved = EditorGUILayout.Toggle("Remember choice", item.saved);
                    item.synced = EditorGUILayout.Toggle("Others can see it", item.synced);
                }
            }
            if (GUILayout.Button("+ Add " + category + " Toggle"))
            {
                toggles.Add(new ToggleEntry { id = Guid.NewGuid().ToString("N").Substring(0, 10),
                    category = category, destinationKey = "path:Appearance" }); GUI.changed = true;
            }
        }
    }

    private void DrawMenuIconSettings()
    {
        EditorGUILayout.Space(6);
        showIconSettings = EditorGUILayout.Foldout(showIconSettings,
            "Menu / Folder Icons (optional)", true);
        if (!showIconSettings) return;
        EditorGUILayout.HelpBox(
            "ACM uses your PNGs from Packages/com.wemakeshit.acm/Icons/ for the ACM main submenu, " +
            "Standing, Crouching, Prone and pagination controls. Unprovided menu " +
            "icons remain empty. Assign a PNG here to override a default; existing " +
            "manual menus and icons are never restyled.",
            MessageType.None);
        standingMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Standing menu", standingMenuIcon, typeof(Texture2D), false);
        crouchingMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Crouching menu", crouchingMenuIcon, typeof(Texture2D), false);
        proneMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Prone menu", proneMenuIcon, typeof(Texture2D), false);
        dancesMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Dances menu", dancesMenuIcon, typeof(Texture2D), false);
        acmMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Avatar Creation Manager", acmMenuIcon, typeof(Texture2D), false);
        posesMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Poses submenu", posesMenuIcon, typeof(Texture2D), false);
        appearanceMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Appearance submenu", appearanceMenuIcon, typeof(Texture2D), false);
        clothingMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Clothing submenu", clothingMenuIcon, typeof(Texture2D), false);
        hatsMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Hats submenu", hatsMenuIcon, typeof(Texture2D), false);
        glassesMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Glasses submenu", glassesMenuIcon, typeof(Texture2D), false);
        accessoriesMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Accessories submenu", accessoriesMenuIcon, typeof(Texture2D), false);
        customMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Custom Toggles submenu", customMenuIcon, typeof(Texture2D), false);
        moreMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Overflow pages", moreMenuIcon, typeof(Texture2D), false);
    }

    private static void Folders()
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "AvatarPoseManager");
        if (!AssetDatabase.IsValidFolder(Profiles)) AssetDatabase.CreateFolder(Root, "Profiles");
        if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder(Root, "Generated");
    }

    private string Key()
    {
        if (avatar == null) return "None";
        string identity = GlobalObjectId.GetGlobalObjectIdSlow(avatar).ToString();
        // GlobalObjectId can be nonpersistent for unsaved scenes; advise saving scene first.
        using (var hash = SHA256.Create())
        {
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(identity));
            return BitConverter.ToString(bytes, 0, 8).Replace("-", "");
        }
    }

    private string ProfilePath() { return Profiles + "/Poses_" + Key() + ".json"; }
    private string OriginalPath() { return Profiles + "/Original_" + Key() + ".json"; }

    private static void FileId(UnityEngine.Object obj, out string guid, out long fileId)
    {
        guid = ""; fileId = 0;
        if (obj != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out fileId);
    }

    private static T ResolveAsset<T>(string guid, long localId) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(guid)) return null;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (!(a is T)) continue;
            string found; long foundId;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out found, out foundId)
                && foundId == localId) return (T)a;
        }
        return null;
    }

    private string RelativePath(Transform target)
    {
        if (avatar == null || target == null) return "";
        if (target == avatar.transform) return "";
        var names = new Stack<string>();
        for (var t = target; t != null && t != avatar.transform; t = t.parent)
            names.Push(t.name);
        Transform at = avatar.transform;
        foreach (var name in names)
        {
            at = at.Find(name);
            if (at == null) return "";
        }
        return string.Join("/", names.ToArray());
    }

    private GameObject FromRelativePath(string relative)
    {
        if (avatar == null || string.IsNullOrEmpty(relative)) return null;
        var child = avatar.transform.Find(relative);
        return child == null ? null : child.gameObject;
    }

    private static List<PoseFile> ToFiles(List<Pose> poses)
    {
        var result = new List<PoseFile>();
        foreach (var pose in poses)
        {
            string guid = "";
            long localId = 0;
            string iconGuid; long iconId;
            FileId(pose.icon, out iconGuid, out iconId);
            if (pose.clip != null)
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pose.clip, out guid, out localId);
            result.Add(new PoseFile { label = pose.label, id = pose.id, guid = guid,
                localId = localId, iconGuid = iconGuid, iconId = iconId });
        }
        return result;
    }

    private static List<Pose> FromFiles(List<PoseFile> input)
    {
        var result = new List<Pose>();
        if (input == null) return result;
        foreach (var item in input)
        {
            AnimationClip clip = null;
            if (!string.IsNullOrEmpty(item.guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(item.guid);
                foreach (var candidate in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(candidate is AnimationClip)) continue;
                    string foundGuid;
                    long foundId;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out foundGuid, out foundId)
                        && foundId == item.localId)
                    {
                        clip = (AnimationClip)candidate;
                        break;
                    }
                }
            }
            result.Add(new Pose { label = item.label, id = item.id, clip = clip,
                icon = ResolveAsset<Texture2D>(item.iconGuid, item.iconId) });
        }
        return result;
    }

    private MenuIconsFile CaptureMenuIcons()
    {
        var m = new MenuIconsFile();
        FileId(standingMenuIcon, out m.standingGuid, out m.standingId);
        FileId(crouchingMenuIcon, out m.crouchingGuid, out m.crouchingId);
        FileId(proneMenuIcon, out m.proneGuid, out m.proneId);
        FileId(dancesMenuIcon, out m.dancesGuid, out m.dancesId);
        FileId(clothingMenuIcon, out m.clothingGuid, out m.clothingId);
        FileId(accessoriesMenuIcon, out m.accessoriesGuid, out m.accessoriesId);
        FileId(customMenuIcon, out m.customGuid, out m.customId);
        FileId(moreMenuIcon, out m.moreGuid, out m.moreId);
        FileId(acmMenuIcon, out m.acmGuid, out m.acmId);
        FileId(hatsMenuIcon, out m.hatsGuid, out m.hatsId);
        FileId(glassesMenuIcon, out m.glassesGuid, out m.glassesId);
        FileId(appearanceMenuIcon, out m.appearanceGuid, out m.appearanceId);
        FileId(posesMenuIcon, out m.posesGuid, out m.posesId);
        return m;
    }

    private void LoadMenuIcons(MenuIconsFile m)
    {
        standingMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.standingGuid, m.standingId);
        crouchingMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.crouchingGuid, m.crouchingId);
        proneMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.proneGuid, m.proneId);
        dancesMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.dancesGuid, m.dancesId);
        clothingMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.clothingGuid, m.clothingId);
        accessoriesMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.accessoriesGuid, m.accessoriesId);
        customMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.customGuid, m.customId);
        moreMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.moreGuid, m.moreId);
        acmMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.acmGuid, m.acmId);
        hatsMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.hatsGuid, m.hatsId);
        glassesMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.glassesGuid, m.glassesId);
        appearanceMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.appearanceGuid, m.appearanceId);
        posesMenuIcon = m == null ? null : ResolveAsset<Texture2D>(m.posesGuid, m.posesId);
    }

    private void SaveProfile()
    {
        if (avatar == null) return;
        Folders();
        var config = new ProfileFile
        {
            standing = ToFiles(standing), crouching = ToFiles(crouching), prone = ToFiles(prone),
            sourceControllerPath = sourceBase == null ? "" : AssetDatabase.GetAssetPath(sourceBase),
            menuIcons = CaptureMenuIcons(),
            toggleMenuLayout = (int)toggleMenuLayout,
            categories = customCategories.Where(c => c != null).Select(c => {
                string ig; long ii; FileId(c.icon, out ig, out ii);
                return new CategoryFile { id=c.id, name=c.name,
                    parentKey=c.parentKey, iconGuid=ig, iconId=ii };
            }).ToList(),
            dances = dances.Where(d => d != null).Select(d =>
            {
                string cg, ag; long ci, ai;
                FileId(d.clip, out cg, out ci); FileId(d.audio, out ag, out ai);
                string ig; long ii; FileId(d.icon, out ig, out ii);
                return new DanceFile { label=d.label, id=d.id, clipGuid=cg, clipId=ci,
                    audioGuid=ag, audioId=ai, iconGuid=ig, iconId=ii,
                    loop=d.loop, volume=d.volume };
            }).ToList(),
            toggles = toggles.Where(t => t != null).Select(t =>
            {
                string og, fg; long oi, fi;
                FileId(t.onClip, out og, out oi); FileId(t.offClip, out fg, out fi);
                string ig; long ii; FileId(t.icon, out ig, out ii);
                return new ToggleFile { id=t.id, label=t.label, category=t.category,
                    menuCategory=t.menuCategory, destinationKey=t.destinationKey,
                    automatic=t.automatic, targetPath=t.target == null ?
                        (t.lastKnownTargetPath ?? "") : RelativePath(t.target.transform),
                    onGuid=og, onId=oi, offGuid=fg, offId=fi, iconGuid=ig, iconId=ii,
                    saved=t.saved, synced=t.synced, defaultOn=t.defaultOn };
            }).ToList()
        };
        File.WriteAllText(ProfilePath(), JsonUtility.ToJson(config, true));
        loadedAvatarKey = Key();
    }

    private void LoadProfile()
    {
        standing = new List<Pose>(); crouching = new List<Pose>(); prone = new List<Pose>();
        dances = new List<Dance>(); toggles = new List<ToggleEntry>();
        customCategories = new List<CategoryEntry>();
        sourceBase = null;
        LoadMenuIcons(null);
        toggleMenuLayout = ToggleMenuLayout.RootCategoryMenus;
        if (avatar == null) { loadedAvatarKey = ""; return; }
        loadedAvatarKey = Key();
        if (!File.Exists(ProfilePath())) return;
        try
        {
            var file = JsonUtility.FromJson<ProfileFile>(File.ReadAllText(ProfilePath()));
            if (file == null) return;
            standing = FromFiles(file.standing);
            crouching = FromFiles(file.crouching);
            prone = FromFiles(file.prone);
            LoadMenuIcons(file.menuIcons);
            toggleMenuLayout = ToggleMenuLayout.RootCategoryMenus;
            if (file.categories != null)
                customCategories = file.categories.Where(c => c != null).Select(c => new CategoryEntry {
                    id=c.id, name=c.name, parentKey=c.parentKey,
                    icon=ResolveAsset<Texture2D>(c.iconGuid,c.iconId) }).ToList();
            if (file.dances != null)
                dances = file.dances.Where(d => d != null).Select(d => new Dance { label=d.label, id=d.id,
                    clip=ResolveAsset<AnimationClip>(d.clipGuid, d.clipId),
                    audio=ResolveAsset<AudioClip>(d.audioGuid, d.audioId),
                    icon=ResolveAsset<Texture2D>(d.iconGuid, d.iconId),
                    loop=d.loop, volume=d.volume }).ToList();
            if (file.toggles != null)
                toggles = file.toggles.Where(t => t != null).Select(t => new ToggleEntry {
                    id=t.id, label=t.label, category=t.category,
                    menuCategory=t.menuCategory ?? "",
                    destinationKey=t.destinationKey ?? "", automatic=t.automatic,
                    target=FromRelativePath(t.targetPath),
                    lastKnownTargetPath=t.targetPath ?? "",
                    onClip=ResolveAsset<AnimationClip>(t.onGuid, t.onId),
                    offClip=ResolveAsset<AnimationClip>(t.offGuid, t.offId),
                    icon=ResolveAsset<Texture2D>(t.iconGuid, t.iconId),
                    defaultOn=t.defaultOn, saved=t.saved, synced=t.synced }).ToList();
            if (!string.IsNullOrEmpty(file.sourceControllerPath))
                sourceBase = AssetDatabase.LoadAssetAtPath<AnimatorController>(file.sourceControllerPath);
        }
        catch (Exception e) { Debug.LogWarning("APM: Could not read saved pose list: " + e.Message); }
    }

    private static int AllocateIds(List<Pose> poses, string label)
    {
        if (poses.Count > 254) throw new Exception(label + " supports up to 254 custom poses.");
        var occupied = new HashSet<int>();
        foreach (var pose in poses)
        {
            if (pose == null || pose.clip == null)
                throw new Exception(label + ": every entry needs an animation clip.");
            if (pose.clip.legacy)
                throw new Exception("Legacy animation is not supported: " + pose.clip.name);
            if (pose.id < 1 || pose.id > 255 || !occupied.Add(pose.id)) pose.id = 0;
            if (string.IsNullOrWhiteSpace(pose.label)) pose.label = pose.clip.name;
        }
        foreach (var pose in poses.Where(p => p.id == 0))
        {
            for (int id = 1; id <= 255; id++)
                if (occupied.Add(id)) { pose.id = id; break; }
        }
        return poses.Count;
    }

    private static List<AnimatorState> AllStates(AnimatorStateMachine machine)
    {
        if (machine == null) return new List<AnimatorState>();
        var list = machine.states.Where(s => s.state != null).Select(s => s.state).ToList();
        foreach (var child in machine.stateMachines)
            if (child.stateMachine != null) list.AddRange(AllStates(child.stateMachine));
        return list;
    }

    private static int CountClip(BlendTree tree, string exactName)
    {
        int found = 0;
        if (tree == null) return 0;
        foreach (var child in tree.children)
        {
            if (child.motion is AnimationClip clip && clip.name == exactName) found++;
            else if (child.motion is BlendTree nested) found += CountClip(nested, exactName);
        }
        return found;
    }

    private static AnimatorState FindTarget(AnimatorController controller, string stance, string clipName)
    {
        if (controller == null) throw new Exception("Missing Base Animator Controller.");
        var states = controller.layers.Where(l => l.stateMachine != null)
            .SelectMany(l => AllStates(l.stateMachine)).ToList();
        var exact = states.Where(s => s != null && s.motion is BlendTree tree &&
            CountClip(tree, clipName) == 1).ToList();
        if (exact.Count == 1) return exact[0];
        if (exact.Count > 1)
            throw new Exception("Ambiguous " + stance + " idle: multiple states contain " + clipName + ".");
        string available = string.Join(", ", states.Select(s => s.name).Distinct().Take(22).ToArray());
        throw new Exception("No compatible " + stance + " idle slot found (" + clipName + "). " +
            "Your Base controller might use a different layout. States include: " + available +
            ". Select the SDK example as Source Base ONLY if you want standard locomotion.");
    }

    // Construct a fresh BlendTree rather than Object.Instantiate(source).
    // Unity 2022.3 can emit native kStrongPPtrMask assertions when cloning a
    // controller-embedded BlendTree subasset, even when Generate succeeds.
    // Copy the public BlendTree settings and the full ChildMotion structs,
    // recursively replacing only nested BlendTree references. AnimationClips
    // remain shared asset references (no source clips/controllers are edited).
    private static BlendTree CopyTree(BlendTree tree, string folder, ref int sequence)
    {
        if (tree == null)
            throw new Exception("ACM: Cannot reconstruct a missing locomotion Blend Tree.");

        var cloned = new BlendTree
        {
            name = "APM_Blend_" + (sequence++),
            blendType = tree.blendType,
            blendParameter = tree.blendParameter,
            blendParameterY = tree.blendParameterY
        };

        // Asset-own the new tree before linking it into another generated tree.
        AssetDatabase.CreateAsset(cloned,
            BuildAssetPath(folder, BlendTreesSubfolder, cloned.name + ".asset"));
        var children = tree.children;
        for (int i = 0; i < children.Length; i++)
        {
            var entry = children[i];
            if (entry.motion is BlendTree nested)
                entry.motion = CopyTree(nested, folder, ref sequence);
            // Keep each child's threshold, 2D position, timeScale, cycleOffset,
            // mirror and directBlendParameter exactly as supplied by Unity.
            children[i] = entry;
        }

        // Avoid automatic threshold recalculation while assigning the children.
        // Restore original automatic/manual mode once the graph is in place.
        cloned.useAutomaticThresholds = false;
        cloned.children = children;
        cloned.minThreshold = tree.minThreshold;
        cloned.maxThreshold = tree.maxThreshold;
        cloned.useAutomaticThresholds = tree.useAutomaticThresholds;
        EditorUtility.SetDirty(cloned);
        return cloned;
    }

    private static bool ReplaceClip(BlendTree tree, string exactName, Motion replacement)
    {
        var children = tree.children;
        bool changed = false;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].motion is AnimationClip clip && clip.name == exactName)
            {
                var entry = children[i]; entry.motion = replacement; children[i] = entry; changed = true;
            }
            else if (children[i].motion is BlendTree nested)
                changed |= ReplaceClip(nested, exactName, replacement);
        }
        if (changed) { tree.children = children; EditorUtility.SetDirty(tree); }
        return changed;
    }

    private static AnimationClip OriginalClip(BlendTree tree, string exactName)
    {
        foreach (var child in tree.children)
        {
            if (child.motion is AnimationClip clip && clip.name == exactName) return clip;
            if (child.motion is BlendTree sub)
            {
                var clip2 = OriginalClip(sub, exactName);
                if (clip2 != null) return clip2;
            }
        }
        return null;
    }

    private static BlendTree Selector(string folder, string parameter,
        AnimationClip defaultClip, List<Pose> poses, ref int sequence)
    {
        var tree = new BlendTree
        {
            name = "APM_Selector_" + (sequence++),
            blendType = BlendTreeType.Simple1D,
            blendParameter = parameter,
            useAutomaticThresholds = false
        };
        AssetDatabase.CreateAsset(tree,
            BuildAssetPath(folder, BlendTreesSubfolder, tree.name + ".asset"));
        tree.AddChild(defaultClip, 0);
        foreach (var pose in poses.OrderBy(p => p.id)) tree.AddChild(pose.clip, pose.id);
        EditorUtility.SetDirty(tree);
        return tree;
    }

    private static void PatchStance(AnimatorState target, string originalClip,
        string parameter, List<Pose> poses, string folder, ref int sequence)
    {
        var originalTree = target.motion as BlendTree;
        if (originalTree == null) throw new Exception("No Blend Tree in state " + target.name);
        var cloned = CopyTree(originalTree, folder, ref sequence);
        var defaultClip = OriginalClip(cloned, originalClip);
        if (defaultClip == null) throw new Exception("Missing default clip " + originalClip);
        var selector = Selector(folder, parameter, defaultClip, poses, ref sequence);
        if (!ReplaceClip(cloned, originalClip, selector))
            throw new Exception("Failed inserting selector for " + originalClip);
        target.motion = cloned;
        EditorUtility.SetDirty(target);
    }

    // Apply defaults only to ACM-created controls. The PNG file's OWN name
    // determines the menu it belongs to, rather than a generic shared icon.
    // A custom image assigned in ACM always overrides this lookup.
    // Manually authored controls are copied without changing their icons.
    private const string DefaultIconFolder = "Packages/com.wemakeshit.acm/Icons/";

    private static Texture2D DefaultSubmenuIcon(string menuName)
    {
        if (string.IsNullOrWhiteSpace(menuName)) return null;
        string requested = menuName.Trim();
        // VRChat's visible labels can differ slightly from the pack's names.
        // These are exact, documented aliases—not visual or fuzzy guesses.
        if (string.Equals(requested, "Avatar Creation Manager", StringComparison.OrdinalIgnoreCase))
            requested = "Menu";
        else if (string.Equals(requested, "Crouching", StringComparison.OrdinalIgnoreCase))
            requested = "Crouch";
        else if (string.Equals(requested, "Previous Page", StringComparison.OrdinalIgnoreCase))
            requested = "PreviousPage";
        else if (string.Equals(requested, "Next Page", StringComparison.OrdinalIgnoreCase) ||
            requested.StartsWith("Page ", StringComparison.OrdinalIgnoreCase))
            requested = "NextPage";

        // Match a real PNG in Packages/com.wemakeshit.acm/Icons, e.g. "ACM Poses.png" for
        // Poses or "ACM Dances.png" for Dances. A missing file stays iconless.
        // Never substitute "ACM Menu.png" for an unrelated category.
        if (requested.IndexOfAny(new [] { '/', '\\' }) >= 0) return null;
        Texture2D match = AssetDatabase.LoadAssetAtPath<Texture2D>(
            DefaultIconFolder + "ACM " + requested + ".png");
        return match != null ? match : AssetDatabase.LoadAssetAtPath<Texture2D>(
            DefaultIconFolder + requested + ".png");
    }

    private static VRCExpressionsMenu.Control Submenu(string name, VRCExpressionsMenu target,
        Texture2D icon = null)
    {
        return new VRCExpressionsMenu.Control
        {
            name = name, type = VRCExpressionsMenu.Control.ControlType.SubMenu,
            subMenu = target, icon = icon != null ? icon : DefaultSubmenuIcon(name)
        };
    }

    private static VRCExpressionsMenu NewMenu(string folder, string name)
    {
        var menu = CreateInstance<VRCExpressionsMenu>();
        menu.name = name;
        menu.controls = new List<VRCExpressionsMenu.Control>();
        AssetDatabase.CreateAsset(menu, AssetDatabase.GenerateUniqueAssetPath(
            BuildAssetPath(folder, MenusSubfolder, SafeAssetStem(name) + ".asset")));
        return menu;
    }

    private static VRCExpressionsMenu.Control PoseControl(string label, string parameter,
        int id, Texture2D icon = null)
    {
        return new VRCExpressionsMenu.Control
        {
            name = label,
            type = id == 0
                ? VRCExpressionsMenu.Control.ControlType.Button
                : VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter = new VRCExpressionsMenu.Control.Parameter { name = parameter },
            value = id, icon = icon
        };
    }

    private static VRCExpressionsMenu BuildPages(
        string folder, string stance, string parameter, List<Pose> poses)
    {
        var choices = new List<VRCExpressionsMenu.Control> { PoseControl("Default", parameter, 0) };
        choices.AddRange(poses.Select(p => PoseControl(p.label, parameter, p.id, p.icon)));
        var pages = new List<VRCExpressionsMenu>();
        int pageCount = choices.Count <= 8 ? 1 : (choices.Count + 5) / 6;
        for (int n = 0; n < pageCount; n++)
            pages.Add(NewMenu(folder, stance + "_Page_" + (n + 1)));
        for (int page = 0; page < pageCount; page++)
        {
            var menu = pages[page];
            if (page > 0) menu.controls.Add(Submenu("Previous Page", pages[page - 1]));
            foreach (var control in choices.Skip(page * 6).Take(pageCount == 1 ? 8 : 6))
                menu.controls.Add(control);
            if (page < pageCount - 1) menu.controls.Add(Submenu("Next Page", pages[page + 1]));
            if (menu.controls.Count > 8) throw new Exception("Menu overflow in " + stance);
        }
        return pages[0];
    }

    private static VRCExpressionParameters.Parameter NewParameter(string name)
    {
        return new VRCExpressionParameters.Parameter
        {
            name = name, valueType = VRCExpressionParameters.ValueType.Int,
            defaultValue = 0, saved = true, networkSynced = true
        };
    }

    private static int ParamBits(IEnumerable<VRCExpressionParameters.Parameter> parameters)
    {
        return parameters.Where(p => p != null && p.networkSynced)
            .Sum(p => p.valueType == VRCExpressionParameters.ValueType.Bool ? 1 : 8);
    }


    // ---------------- ACM generation helpers ----------------
    private static int LayerIndex(VRCAvatarDescriptor.CustomAnimLayer[] layers,
        VRCAvatarDescriptor.AnimLayerType type)
    {
        return Array.FindIndex(layers, l => l.type == type);
    }

    private static VRCExpressionParameters.Parameter BoolParameter(ToggleEntry entry)
    {
        return new VRCExpressionParameters.Parameter {
            name = TogglePrefix + entry.id,
            valueType = VRCExpressionParameters.ValueType.Bool,
            defaultValue = entry.defaultOn ? 1f : 0f,
            saved = entry.saved,
            networkSynced = entry.synced
        };
    }

    private static int AllocateDanceIds(List<Dance> list)
    {
        if (list.Count > 255) throw new Exception("A maximum of 255 dances is supported.");
        var used = new HashSet<int>();
        foreach (var d in list)
        {
            if (d == null || d.clip == null || d.clip.legacy)
                throw new Exception("Each dance needs a non-Legacy animation clip.");
            if (d.id <= 0 || d.id > 255 || !used.Add(d.id)) d.id = 0;
            if (string.IsNullOrWhiteSpace(d.label)) d.label = d.clip.name;
        }
        foreach (var d in list.Where(d => d.id == 0))
            for (int n = 1; n <= 255; n++) if (used.Add(n)) { d.id = n; break; }
        return list.Count;
    }

    private static string AlphanumericKey(string name)
    {
        return new string((name ?? "").Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant).ToArray());
    }

    // Locate an avatar CHILD only if its name is a unique, exact normalized match.
    // Never choose the first result from an ambiguous search.
    private GameObject UniqueChildNamed(string name)
    {
        string key = AlphanumericKey(name);
        if (avatar == null || key.Length < 3) return null;
        var found = avatar.GetComponentsInChildren<Transform>(true)
            .Where(t => t != null && t != avatar.transform &&
                AlphanumericKey(t.name) == key).Take(2).ToArray();
        return found.Length == 1 ? found[0].gameObject : null;
    }

    private List<ToggleEntry> PrepareBuildToggles(OriginalFile previous,
        List<string> notes)
    {
        var ready = new List<ToggleEntry>();
        if (toggles == null) toggles = new List<ToggleEntry>();
        foreach (var item in toggles)
        {
            if (item == null) { notes.Add("Skipped an empty toggle draft"); continue; }
            if (string.IsNullOrEmpty(item.id))
                item.id = Guid.NewGuid().ToString("N").Substring(0, 10);
            if (string.IsNullOrWhiteSpace(item.label))
                item.label = item.target != null ? item.target.name : "New Toggle";
            if (string.IsNullOrEmpty(item.destinationKey) &&
                string.IsNullOrEmpty(item.menuCategory))
                item.destinationKey = "path:Appearance";
            bool existed = previous != null && previous.ownedParameters != null &&
                previous.ownedParameters.Contains(TogglePrefix + item.id);
            if (item.automatic && item.target == null)
            {
                if (!string.IsNullOrEmpty(item.lastKnownTargetPath))
                    item.target = FromRelativePath(item.lastKnownTargetPath);
                if (item.target == null)
                    item.target = UniqueChildNamed(item.label);
                if (item.target != null)
                    notes.Add("Relinked '" + item.label + "' to an avatar child");
                else if (item.onClip != null && item.offClip != null &&
                    !item.onClip.legacy && !item.offClip.legacy)
                {
                    item.automatic = false;
                    notes.Add("Used supplied ON/OFF clips for '" + item.label + "'");
                }
            }
            bool incomplete = item.automatic ? item.target == null :
                item.onClip == null || item.offClip == null;
            if (incomplete)
            {
                if (existed)
                    throw new Exception("Previously generated toggle '" + item.label +
                        "' has lost its GameObject or animation clips. Reassign it in Toggles. " +
                        "ACM will NOT silently delete an existing working toggle.");
                notes.Add("Skipped unfinished toggle '" + item.label +
                    "' (select an avatar child object or both ON/OFF clips)");
                continue;
            }
            if (item.automatic)
                item.lastKnownTargetPath = RelativePath(item.target.transform);
            ready.Add(item);
        }
        ValidateToggles(ready);
        return ready;
    }

    private void ValidateToggles(List<ToggleEntry> entries)
    {
        var ids = new HashSet<string>();
        var paths = new HashSet<string>();
        foreach (var t in entries)
        {
            if (t == null) throw new Exception("A toggle entry is empty.");
            if (string.IsNullOrEmpty(t.id)) t.id = Guid.NewGuid().ToString("N").Substring(0,10);
            if (!ids.Add(t.id)) throw new Exception("Two toggles have the same internal ID.");
            if (string.IsNullOrWhiteSpace(t.label))
                t.label = t.target != null ? t.target.name : "Toggle " + t.id;
            if (t.automatic)
            {
                if (t.target == null) throw new Exception("'" + t.label + "' needs a GameObject.");
                if (t.target == avatar.gameObject)
                    throw new Exception("Do not toggle the entire avatar root. Choose a child object.");
                string path = RelativePath(t.target.transform);
                if (string.IsNullOrEmpty(path) || avatar.transform.Find(path) != t.target.transform)
                    throw new Exception("'" + t.label + "' must be a uniquely named child of the selected avatar.");
                if (!paths.Add(path)) throw new Exception("Multiple toggles target the same object: " + path);
            }
            else if (t.onClip == null || t.offClip == null ||
                t.onClip.legacy || t.offClip.legacy)
                throw new Exception("Manual toggle '" + t.label +
                    "' needs both non-Legacy On and Off animations.");
        }
    }

    // Rebuild only ACM-owned layers when updating. User-authored layers are preserved.
    private static void RemoveOwnedLayers(AnimatorController controller)
    {
        for (int i = controller.layers.Length - 1; i >= 0; i--)
            if (IsOwnedLayerName(controller.layers[i].name)) controller.RemoveLayer(i);
    }

    // Only touch our own generated FX and dance layers, not arbitrary user layers.
    private static bool IsOwnedLayerName(string name)
    {
        if (name == OwnedDanceLayer) return true;
        if (string.IsNullOrEmpty(name) || !name.StartsWith(OwnedFxPrefix, StringComparison.Ordinal))
            return false;
        string suffix = name.Substring(OwnedFxPrefix.Length);
        return suffix.Length == 10 && suffix.All(c =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    }

    // A Base/FX/Action with no custom controller uses VRChat's default SDK controller.
    // An explicitly empty custom layer can be created from scratch, without borrowing
    // any of the avatar's existing FX layers or rewriting their state machines.
    private static AnimatorController CopyOriginalPlayable(string originalPath,
        bool wasDefault, string sdkSample, string targetPath, string label)
    {
        string sourcePath = wasDefault ? sdkSample : originalPath;
        if (string.IsNullOrEmpty(sourcePath) && !wasDefault)
        {
            var created = AnimatorController.CreateAnimatorControllerAtPath(targetPath);
            if (created == null) throw new Exception("Cannot create ACM " + label + " controller.");
            return created;
        }
        if (sourcePath.StartsWith(Generated + "/", StringComparison.Ordinal))
            throw new Exception("The saved original " + label + " controller points into an ACM build. " +
                "ACM will not treat an older generated controller as your manual baseline. " +
                "Restore your original controller assignment from a backup before updating.");
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(sourcePath) == null)
            throw new Exception("Your original " + label + " controller is missing: " + sourcePath +
                ". ACM did not replace it. Reassign your manual controller or restore a backup.");
        return CopyPlayable(sourcePath, targetPath);
    }

    // Conservative safeguard: never animate the same target properties controlled
    // by an existing manual FX clip. This avoids shader/visibility state conflicts.
    private static void CheckManualFxCurveConflicts(AnimatorController originalFx,
        List<ToggleEntry> newToggles)
    {
        if (originalFx == null || newToggles.Count == 0) return;
        var manualCurves = new HashSet<string>(StringComparer.Ordinal);
        foreach (var clip in originalFx.animationClips.Where(c => c != null).Distinct())
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                manualCurves.Add(CurveKey(binding));
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                manualCurves.Add(CurveKey(binding));
        }
        foreach (var toggle in newToggles)
        {
            if (!toggle.automatic)
            {
                foreach (var clip in new [] { toggle.onClip, toggle.offClip })
                {
                    foreach (var curve in AnimationUtility.GetCurveBindings(clip))
                        if (manualCurves.Contains(CurveKey(curve)))
                            throw new Exception("The custom toggle '" + toggle.label +
                                "' animates a property already controlled by your original FX controller (" +
                                curve.path + " / " + curve.propertyName + "). " +
                                "No changes made. Use a different animation/property or disable that conflicting manual FX state first.");
                    foreach (var curve in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                        if (manualCurves.Contains(CurveKey(curve)))
                            throw new Exception("The custom toggle '" + toggle.label +
                                "' conflicts with an existing FX material/object curve (" + curve.path +
                                " / " + curve.propertyName + "). No changes made.");
                }
            }
        }
    }

    private static string CurveKey(EditorCurveBinding binding)
    {
        return binding.path + "|" +
            (binding.type == null ? "" : binding.type.AssemblyQualifiedName) + "|" +
            binding.propertyName;
    }

    private static AnimatorController CopyPlayable(string sourcePath, string targetPath)
    {
        if (string.IsNullOrEmpty(sourcePath) ||
            AssetDatabase.LoadAssetAtPath<AnimatorController>(sourcePath) == null)
            throw new Exception("Cannot find controller for " + targetPath + ".");
        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            throw new Exception("Could not make a protected copy of " + sourcePath);
        var copy = AssetDatabase.LoadAssetAtPath<AnimatorController>(targetPath);
        if (copy == null) throw new Exception("Could not load generated controller: " + targetPath);
        RemoveOwnedLayers(copy);
        return copy;
    }

    private static AnimatorStateMachine NewOwnedLayer(AnimatorController controller, string label)
    {
        controller.AddLayer(label);
        var all = controller.layers;
        var layer = all[all.Length-1];
        layer.defaultWeight = 1f;
        all[all.Length-1] = layer;
        controller.layers = all;
        if (layer.stateMachine == null) throw new Exception("Could not create Animator layer " + label);
        return layer.stateMachine;
    }

    private static void AddBoolSwitch(AnimatorStateMachine sm, AnimatorState off,
        AnimatorState on, string parameter)
    {
        sm.defaultState = off;
        var toOn = sm.AddAnyStateTransition(on);
        toOn.hasExitTime = false; toOn.duration = 0f; toOn.canTransitionToSelf = false;
        toOn.AddCondition(AnimatorConditionMode.If, 0f, parameter);
        var toOff = sm.AddAnyStateTransition(off);
        toOff.hasExitTime = false; toOff.duration = 0f; toOff.canTransitionToSelf = false;
        toOff.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static AnimationClip CreateObjectClip(string folder, string path,
        bool active, string name)
    {
        var clip = new AnimationClip(); clip.name = name;
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive"),
            AnimationCurve.Linear(0f, active ? 1f : 0f,
                .033f, active ? 1f : 0f));
        AssetDatabase.CreateAsset(clip,
            BuildAssetPath(folder, AnimationsSubfolder, name + ".anim"));
        return clip;
    }

    // Check generated visibility toggles against original manual FX curves.
    // ACM never takes control of a GameObject that is already toggled in manual FX.
    private void CheckObjectToggleConflicts(AnimatorController originalFx)
    {
        if (originalFx == null) return;
        var occupied = new HashSet<string>(StringComparer.Ordinal);
        foreach (var clip in originalFx.animationClips.Where(c => c != null).Distinct())
        {
            foreach (var curve in AnimationUtility.GetCurveBindings(clip))
                if (curve.type == typeof(GameObject) && curve.propertyName == "m_IsActive")
                    occupied.Add(curve.path);
        }
        foreach (var toggle in GenerationToggles.Where(t => t.automatic))
        {
            string path = RelativePath(toggle.target.transform);
            if (occupied.Contains(path))
                throw new Exception("'" + toggle.label + "' is already animated by your original FX " +
                    "controller. ACM refused to make another toggle for " + path +
                    " because that could break your existing manual setup.");
        }
    }

    private void BuildToggles(AnimatorController controller, string folder)
    {
        foreach (var t in GenerationToggles)
        {
            var name = TogglePrefix + t.id;
            if (!controller.parameters.Any(p => p.name == name))
                controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            else if (controller.parameters.Any(p => p.name == name &&
                    p.type != AnimatorControllerParameterType.Bool))
                throw new Exception("Animator parameter conflict: " + name);
            var sm = NewOwnedLayer(controller, OwnedFxPrefix + t.id);
            var off = sm.AddState("OFF"); var on = sm.AddState("ON");
            off.writeDefaultValues = false; on.writeDefaultValues = false;
            off.motion = t.automatic ?
                CreateObjectClip(folder, RelativePath(t.target.transform), false,
                    "ACM_Off_" + t.id) : t.offClip;
            on.motion = t.automatic ?
                CreateObjectClip(folder, RelativePath(t.target.transform), true,
                    "ACM_On_" + t.id) : t.onClip;
            AddBoolSwitch(sm, off, on, name);
        }
    }

    // Resolve SDK behaviours by *concrete* type. Some projects contain old SDK
    // assemblies or partially-loaded assemblies; selecting the first name match
    // could yield a type Unity cannot instantiate on an Animator state.
    private static System.Type FindBehaviour(string shortName)
    {
        var matches = new List<System.Type>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type[] types;
            try { types = assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            { types = ex.Types; }
            catch (Exception) { continue; }
            if (types == null) continue;
            foreach (var type in types)
            {
                if (type == null || type.Name != shortName || type.IsAbstract ||
                    type.ContainsGenericParameters ||
                    !typeof(StateMachineBehaviour).IsAssignableFrom(type)) continue;
                matches.Add(type);
            }
        }
        // Prefer VRChat's SDKBase implementations to similarly named legacy or
        // user-installed types, but never assume an assembly name is stable.
        return matches.OrderByDescending(t =>
            t.FullName != null && t.FullName.StartsWith("VRC.SDKBase.",
                StringComparison.Ordinal) ? 2 :
            t.FullName != null && t.FullName.StartsWith("VRC.",
                StringComparison.Ordinal) ? 1 : 0).FirstOrDefault();
    }

    // Unity can return null when a StateMachineBehaviour script could not be
    // created, even after reflection found the type. Keep all fallback objects
    // inside ACM's temporary generated controller, never the original asset.
    private static StateMachineBehaviour AddDanceBehaviour(AnimatorController controller,
        AnimatorState state, System.Type behaviourType, string danceName)
    {
        if (controller == null || state == null || behaviourType == null)
            throw new Exception("Dance '" + danceName +
                "' could not initialize its Action Animator state/SDK behaviour.");
        if (!typeof(StateMachineBehaviour).IsAssignableFrom(behaviourType) ||
            behaviourType.IsAbstract)
            throw new Exception("Dance '" + danceName + "': SDK type " +
                behaviourType.FullName + " is not a usable StateMachineBehaviour.");

        StateMachineBehaviour behaviour = null;
        Exception nativeError = null;
        try { behaviour = state.AddStateMachineBehaviour(behaviourType); }
        catch (Exception error) { nativeError = error; }
        if (behaviour != null) return behaviour;

        // Supported Unity alternative: a persisted SMB subasset attached to
        // AnimatorState.behaviours. Only used if Unity's native add failed.
        StateMachineBehaviour fallback = null;
        try
        {
            fallback = ScriptableObject.CreateInstance(behaviourType) as StateMachineBehaviour;
            if (fallback == null) throw new Exception("SDK type could not be instantiated.");
            fallback.name = "ACM " + behaviourType.Name;
            AssetDatabase.AddObjectToAsset(fallback, controller);
            var existing = state.behaviours ?? new StateMachineBehaviour[0];
            state.behaviours = existing.Concat(new [] { fallback }).ToArray();
            if (!state.behaviours.Any(b => b == fallback))
                throw new Exception("Unity did not retain the SDK behaviour in the state.");
            EditorUtility.SetDirty(fallback);
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(controller);
            return fallback;
        }
        catch (Exception fallbackError)
        {
            // The staged controller is discarded on failure. Explain the root
            // cause instead of surfacing a misleading NullReferenceException.
            throw new Exception("Dance '" + danceName + "': Unity could not add " +
                behaviourType.FullName + " to the Action state. " +
                "Native add: " + (nativeError == null ? "returned null" : nativeError.Message) +
                "; fallback: " + fallbackError.Message +
                ". Check the VRChat SDK installation and Unity Console.", fallbackError);
        }
    }

    private static void SetBehaviourField(StateMachineBehaviour behaviour,
        string name, object value, bool required = true)
    {
        if (behaviour == null)
            throw new Exception("VRChat SDK state behaviour is missing while setting '" +
                name + "'.");
        var flags = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.IgnoreCase;
        var type = behaviour.GetType();
        var field = type.GetField(name, flags);
        var property = field == null ? type.GetProperty(name, flags) : null;
        var expectedType = field != null ? field.FieldType :
            property != null && property.CanWrite ? property.PropertyType : null;
        if (expectedType == null)
        {
            if (required) throw new Exception("VRChat SDK " + type.Name +
                " does not have a writable '" + name + "' member. " +
                "The installed SDK may have changed this behaviour API.");
            return;
        }
        if (value is string enumName && expectedType.IsEnum)
            value = Enum.Parse(expectedType, enumName, true);
        if (value is AudioClip[] && expectedType.IsGenericType &&
            expectedType.GetGenericTypeDefinition() == typeof(List<>) &&
            expectedType.GetGenericArguments()[0] == typeof(AudioClip))
            value = new List<AudioClip>((AudioClip[])value);
        try
        {
            if (field != null) field.SetValue(behaviour, value);
            else property.SetValue(behaviour, value, null);
            EditorUtility.SetDirty(behaviour);
        }
        catch (Exception error)
        {
            throw new Exception("Cannot configure VRChat " + type.Name + "." + name +
                " (expected " + expectedType.Name + "). " + error.Message, error);
        }
    }

    private static void AttachPlayableWeight(AnimatorController controller,
        AnimatorState state, float weight, System.Type behaviourType, string danceName)
    {
        if (behaviourType == null)
            throw new Exception("SDK does not expose Playable Layer Control. " +
                "ACM cannot safely create dances with this SDK version.");
        var behaviour = AddDanceBehaviour(controller, state, behaviourType, danceName);
        SetBehaviourField(behaviour, "layer", "Action");
        SetBehaviourField(behaviour, "goalWeight", weight);
        SetBehaviourField(behaviour, "blendDuration", 0f, false);
    }

    private static void AttachDanceAudio(AnimatorController controller,
        AnimatorState state, Dance d, System.Type audioType)
    {
        if (d.audio == null) return;
        if (audioType == null)
            throw new Exception("Animator Play Audio is unavailable in this SDK.");
        var behaviour = AddDanceBehaviour(controller, state, audioType,
            d.label + " (audio)");
        SetBehaviourField(behaviour, "SourcePath", "ACM_DanceAudio");
        SetBehaviourField(behaviour, "Clips", new [] { d.audio });
        SetBehaviourField(behaviour, "Loop", d.loop);
        SetBehaviourField(behaviour, "Volume", new Vector2(d.volume, d.volume));
        SetBehaviourField(behaviour, "PlayOnEnter", true);
        SetBehaviourField(behaviour, "StopOnEnter", true);
        SetBehaviourField(behaviour, "StopOnExit", true);
    }

    private static void BuildDances(AnimatorController controller,
        List<Dance> list, System.Type playableBehaviour, System.Type audioBehaviour)
    {
        if (controller == null)
            throw new Exception("Action controller copy is null. Existing Action controller was left unchanged.");
        if (list == null || list.Count == 0) return;
        if (!controller.parameters.Any(p => p.name == DanceParam))
            controller.AddParameter(DanceParam, AnimatorControllerParameterType.Int);
        else if (controller.parameters.Any(p => p.name == DanceParam &&
            p.type != AnimatorControllerParameterType.Int))
            throw new Exception("Dance parameter conflicts with another Animator parameter.");
        var sm = NewOwnedLayer(controller, OwnedDanceLayer);
        if (sm == null) throw new Exception("Unity could not create the ACM dance Action layer.");
        var off = sm.AddState("Normal locomotion");
        if (off == null) throw new Exception("Unity could not create the normal Action state.");
        off.writeDefaultValues = false;
        AttachPlayableWeight(controller, off, 0f, playableBehaviour, "Normal locomotion");
        sm.defaultState = off;
        var stop = sm.AddAnyStateTransition(off);
        if (stop == null) throw new Exception("Unity could not add the Stop Dance transition.");
        stop.duration = 0f; stop.hasExitTime = false; stop.canTransitionToSelf = false;
        stop.AddCondition(AnimatorConditionMode.Equals, 0f, DanceParam);
        foreach (var d in list)
        {
            var label = d == null ? "(empty)" : d.label;
            try
            {
                if (d == null || d.clip == null || d.clip.legacy)
                    throw new Exception("A non-Legacy animation clip is required.");
                var on = sm.AddState(d.label);
                if (on == null) throw new Exception("Unity could not create the Action state.");
                on.motion = d.clip; on.writeDefaultValues = false;
                AttachPlayableWeight(controller, on, 1f, playableBehaviour, label);
                AttachDanceAudio(controller, on, d, audioBehaviour);
                var transition = sm.AddAnyStateTransition(on);
                if (transition == null) throw new Exception("Could not add dance transition.");
                transition.hasExitTime = false;
                transition.duration = 0f;
                transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.Equals, (float)d.id, DanceParam);
            }
            catch (Exception error)
            {
                throw new Exception("Could not generate dance '" + label + "': " +
                    error.Message, error);
            }
        }
        EditorUtility.SetDirty(controller);
    }

    private static VRCExpressionsMenu BuildChoices(string folder, string title,
        List<VRCExpressionsMenu.Control> choices)
    {
        if (choices.Count == 0) return null;
        var pages = new List<VRCExpressionsMenu>();
        int count = (choices.Count + 5) / 6;
        for (int n=0; n<count; n++)
            pages.Add(NewMenu(folder, title + "_Page_" + (n+1)));
        for (int n=0; n<count; n++)
        {
            var p = pages[n];
            if (n>0) p.controls.Add(Submenu("Previous Page",pages[n-1]));
            p.controls.AddRange(choices.Skip(n*6).Take(6));
            if (n<count-1) p.controls.Add(Submenu("Next Page",pages[n+1]));
        }
        return pages[0];
    }

    // v0.6.6: Original menu hierarchy is authoritative. Navigation pages are
    // created only in the NEW generated build and never alter user-owned assets.
    private static bool IsForwardPage(VRCExpressionsMenu.Control c, string folder)
    {
        return c != null && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu &&
            c.subMenu != null && c.subMenu.name.StartsWith("ACM_Page_", StringComparison.Ordinal) &&
            AssetDatabase.GetAssetPath(c.subMenu).StartsWith(folder + "/", StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(c.name) &&
            c.name.StartsWith("Page ", StringComparison.Ordinal);
    }

    private static List<VRCExpressionsMenu> PageChain(string folder, VRCExpressionsMenu first)
    {
        var pages = new List<VRCExpressionsMenu>();
        var ids = new HashSet<int>();
        for (var current = first; current != null; )
        {
            if (!ids.Add(current.GetInstanceID()) || pages.Count >= 100)
                throw new Exception("Cyclic or excessive ACM pagination in " + first.name);
            pages.Add(current);
            var next = (current.controls ?? new List<VRCExpressionsMenu.Control>())
                .Where(c => IsForwardPage(c, folder)).ToList();
            if (next.Count > 1) throw new Exception("Multiple page links found in " + current.name);
            current = next.Count == 1 ? next[0].subMenu : null;
        }
        return pages;
    }

    private static List<VRCExpressionsMenu.Control> PageContents(string folder,
        VRCExpressionsMenu first)
    {
        var result = new List<VRCExpressionsMenu.Control>();
        var chain = PageChain(folder, first);
        for (int i=0; i<chain.Count; i++)
            foreach (var c in chain[i].controls ?? new List<VRCExpressionsMenu.Control>())
                if (c != null && !IsForwardPage(c, folder) &&
                    !(i>0 && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu &&
                      c.subMenu == chain[i-1] && c.name == "Previous Page"))
                    result.Add(c);
        return result;
    }

    private static void AppendWithPages(string folder, VRCExpressionsMenu menu,
        VRCExpressionsMenu.Control added)
    {
        var chain = PageChain(folder, menu);
        var last = chain[chain.Count-1];
        if (last.controls == null) last.controls = new List<VRCExpressionsMenu.Control>();
        if (last.controls.Count > 8)
            throw new Exception("Original menu exceeds VRChat's 8 controls: " + last.name);
        if (last.controls.Count < 8)
        {
            last.controls.Add(added);
            EditorUtility.SetDirty(last);
            return;
        }
        // Exactly 8 controls: one must shift into the new generated page in
        // order to make room for a page link. Preserve the original control
        // (including its icon, parameters, and submenu) without editing it.
        int lastContentIndex = last.controls.FindLastIndex(c => c != null &&
            !(c.name == "Previous Page" && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu));
        if (lastContentIndex < 0)
            throw new Exception("No room for pagination in " + last.name);
        var moved = last.controls[lastContentIndex];
        int nextNumber = chain.Count + 1;
        var newPage = NewMenu(folder, "ACM_Page_" + SafeAssetStem(menu.name) + "_" + nextNumber);
        newPage.controls.Add(Submenu("Previous Page", last));
        newPage.controls.Add(moved);
        last.controls.RemoveAt(lastContentIndex);
        string forwardLabel = "Page " + nextNumber;
        if (last.controls.Any(c => c != null &&
            string.Equals(c.name, forwardLabel, StringComparison.OrdinalIgnoreCase)))
            forwardLabel = "Page " + nextNumber + " (ACM)";
        last.controls.Add(Submenu(forwardLabel, newPage));
        newPage.controls.Add(added);
        EditorUtility.SetDirty(last);
        EditorUtility.SetDirty(newPage);
    }

    private static void AppendPoseCategoryControls(string folder,
        List<VRCExpressionsMenu.Control> target, string stance, string parameter,
        List<Pose> poses, Texture2D icon)
    {
        if (poses == null || poses.Count == 0) return;
        if (poses.Count == 1)
            target.Add(PoseControl(poses[0].label, parameter, poses[0].id,
                poses[0].icon != null ? poses[0].icon : DefaultSubmenuIcon(stance)));
        else
            target.Add(Submenu(stance, BuildPages(folder, stance, parameter, poses), icon));
    }

    private static VRCExpressionsMenu GetOrCreateCategory(string folder,
        VRCExpressionsMenu parent, string name, Texture2D icon)
    {
        if (parent == null) throw new Exception("Cannot resolve the target category.");
        if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." ||
            name.IndexOfAny(new [] { '/', '\\' }) >= 0 || name.Length > 64)
            throw new Exception("Invalid Expressions Menu category '" + name + "'.");
        var matches = PageChain(folder, parent).SelectMany(p =>
            (p.controls ?? new List<VRCExpressionsMenu.Control>())
                .Where(c => c != null && string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase))
                .Select(c => new { page=p, control=c })).ToList();
        if (matches.Count > 1)
            throw new Exception("Duplicate category '" + name + "' already exists in " + parent.name);
        if (matches.Count == 0)
        {
            var created = NewMenu(folder, "ACM_Category_" + SafeAssetStem(name));
            AppendWithPages(folder, parent, Submenu(name, created, icon));
            return created;
        }
        var selected = matches[0].control;
        if (selected.type != VRCExpressionsMenu.Control.ControlType.SubMenu || selected.subMenu == null)
            throw new Exception("'" + name + "' exists in '" + parent.name +
                "' but isn't a submenu. ACM won't replace manual controls.");
        string existingPath = AssetDatabase.GetAssetPath(selected.subMenu);
        if (existingPath.StartsWith(folder + "/", StringComparison.Ordinal))
            return selected.subMenu;
        if (IsGeneratedAcmAsset(existingPath))
            throw new Exception("Existing category references an OLD ACM build; use the original menu.");
        var copy = UnityEngine.Object.Instantiate(selected.subMenu);
        copy.name = "ACM_Merged_" + SafeAssetStem(name);
        AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(
            BuildAssetPath(folder, MenusSubfolder, copy.name + ".asset")));
        selected.subMenu = copy; // Control lives in a generated menu, not the original.
        EditorUtility.SetDirty(matches[0].page);
        return copy;
    }

    private static void AddControlsPreservingManual(string folder,
        VRCExpressionsMenu target, List<VRCExpressionsMenu.Control> additions)
    {
        if (additions == null || additions.Count == 0) return;
        var names = new HashSet<string>(PageContents(folder, target)
            .Where(c => c != null).Select(c => c.name), StringComparer.OrdinalIgnoreCase);
        foreach (var item in additions)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.name))
                throw new Exception("An ACM item is missing its display name.");
            if (!names.Add(item.name))
                throw new Exception("'" + item.name + "' already exists in '" +
                    target.name + "'. Rename the ACM item; original entries are preserved.");
            AppendWithPages(folder, target, item);
        }
    }

    private static string SafeAssetStem(string label)
    {
        var chars = (label ?? "Category").Select(c => char.IsLetterOrDigit(c) || c == '_'
            ? c : '_').ToArray();
        string stem = new string(chars);
        if (stem.Length > 45) stem = stem.Substring(0, 45);
        return string.IsNullOrEmpty(stem) ? "Category" : stem;
    }

    private string ToggleDestination(ToggleEntry toggle,
        Dictionary<string, CategoryOption> available)
    {
        if (!string.IsNullOrEmpty(toggle.destinationKey) &&
            available.ContainsKey(toggle.destinationKey))
            return available[toggle.destinationKey].path;
        // Older profiles had a path string; allow it only if a real or defined
        // category exists at that path. We never guess from toggle/object names.
        if (!string.IsNullOrWhiteSpace(toggle.menuCategory))
        {
            var old = available.Values.FirstOrDefault(o => string.Equals(
                o.path, toggle.menuCategory, StringComparison.OrdinalIgnoreCase));
            if (old != null) { toggle.destinationKey = old.key; return old.path; }
        }
        // New toggles without a destination start in the existing Appearance
        // menu. The user can move them into any custom nested category later.
        if (string.IsNullOrEmpty(toggle.destinationKey) &&
            string.IsNullOrEmpty(toggle.menuCategory))
        {
            toggle.destinationKey = "path:Appearance";
            return "Appearance";
        }
        throw new Exception("Category for '" + toggle.label +
            "' no longer exists. Re-select it in the Toggles tab; ACM did not relocate it silently.");
    }

    private Texture2D IconForCategory(string name)
    {
        // Explicit overrides take priority; all other defaults are matched by
        // PNG filename. "ACM Menu" belongs ONLY to Avatar Creation Manager.
        Texture2D selected = null;
        if (string.Equals(name, "Avatar Creation Manager", StringComparison.OrdinalIgnoreCase))
            selected = acmMenuIcon;
        else if (string.Equals(name, "Poses", StringComparison.OrdinalIgnoreCase))
            selected = posesMenuIcon;
        else if (string.Equals(name, "Dances", StringComparison.OrdinalIgnoreCase))
            selected = dancesMenuIcon;
        else if (string.Equals(name, "Appearance", StringComparison.OrdinalIgnoreCase))
            selected = appearanceMenuIcon;
        else if (string.Equals(name, "Hats", StringComparison.OrdinalIgnoreCase))
            selected = hatsMenuIcon;
        else if (string.Equals(name, "Glasses", StringComparison.OrdinalIgnoreCase))
            selected = glassesMenuIcon;
        else if (string.Equals(name, "Clothing", StringComparison.OrdinalIgnoreCase))
            selected = clothingMenuIcon;
        else if (string.Equals(name, "Accessories", StringComparison.OrdinalIgnoreCase))
            selected = accessoriesMenuIcon;
        else if (string.Equals(name, "Custom Toggles", StringComparison.OrdinalIgnoreCase))
            selected = customMenuIcon;
        return selected != null ? selected : DefaultSubmenuIcon(name);
    }

    private void AddCategoryAcmControls(string folder, VRCExpressionsMenu root)
    {
        bool anyPose = standing.Count > 0 || crouching.Count > 0 || prone.Count > 0;
        if (anyPose || dances.Count > 0)
        {
            var acm = GetOrCreateCategory(folder, root, "Avatar Creation Manager", IconForCategory("Avatar Creation Manager"));
            if (anyPose)
            {
                var poseControls = new List<VRCExpressionsMenu.Control>();
                AppendPoseCategoryControls(folder, poseControls, "Standing", Parameters[0],
                    standing, standingMenuIcon);
                AppendPoseCategoryControls(folder, poseControls, "Crouching", Parameters[1],
                    crouching, crouchingMenuIcon);
                AppendPoseCategoryControls(folder, poseControls, "Prone", Parameters[2],
                    prone, proneMenuIcon);
                var poseMenu = GetOrCreateCategory(folder, acm, "Poses", IconForCategory("Poses"));
                AddControlsPreservingManual(folder, poseMenu, poseControls);
            }
            if (dances.Count > 0)
            {
                var danceControls = new List<VRCExpressionsMenu.Control> {
                    PoseControl("Stop Dance", DanceParam, 0) };
                danceControls.AddRange(dances.Select(d =>
                    PoseControl(d.label, DanceParam, d.id, d.icon)));
                var dancesMenu = GetOrCreateCategory(folder, acm, "Dances", IconForCategory("Dances"));
                AddControlsPreservingManual(folder, dancesMenu, danceControls);
            }
        }

        // Validate category definitions and construct ALL declared custom menus,
        // even ones which don't have toggles yet. Icons only apply on creation.
        var choices = CategoryChoices();
        if (choices.Count(o => o.key.StartsWith("custom:", StringComparison.Ordinal)) !=
            customCategories.Count)
            throw new Exception("Categories contain an invalid name, missing parent or cycle.");
        var duplicateCustomPath = choices
            .Where(o => o.key.StartsWith("custom:", StringComparison.Ordinal))
            .GroupBy(o => o.path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCustomPath != null)
            throw new Exception("Two custom categories have the same destination: " +
                duplicateCustomPath.Key + ". Reuse one category rather than duplicating it.");
        var ids = new HashSet<string>();
        foreach (var c in customCategories)
            if (string.IsNullOrWhiteSpace(c.id) || !ids.Add(c.id))
                throw new Exception("Duplicate/missing category ID. Remove and recreate the affected category.");
        var known = choices.ToDictionary(o => o.key, o => o);
        // Resolve in hierarchy order, so parents always exist before children.
        foreach (var option in choices.Where(o => o.key.StartsWith("custom:", StringComparison.Ordinal))
            .OrderBy(o => o.path.Count(ch => ch == '/')))
        {
            var category = customCategories.First(c => "custom:" + c.id == option.key);
            string[] segments = option.path.Split('/');
            VRCExpressionsMenu parent = root;
            for (int i=0; i<segments.Length; i++)
                parent = GetOrCreateCategory(folder, parent, segments[i],
                    i == segments.Length - 1 ? category.icon : IconForCategory(segments[i]));
        }

        var groups = new Dictionary<string, List<VRCExpressionsMenu.Control>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var entry in GenerationToggles)
        {
            string path = ToggleDestination(entry, known);
            if (!path.StartsWith("Appearance", StringComparison.OrdinalIgnoreCase) ||
                (path.Length > 10 && path[10] != '/'))
                throw new Exception("Toggle destination must be inside Appearance: " + path);
            if (!groups.ContainsKey(path))
                groups.Add(path, new List<VRCExpressionsMenu.Control>());
            groups[path].Add(BoolMenuControl(entry));
        }
        foreach (var group in groups)
        {
            var segments = group.Key.Split('/');
            VRCExpressionsMenu target = root;
            foreach (string segment in segments)
                target = GetOrCreateCategory(folder, target, segment, IconForCategory(segment));
            AddControlsPreservingManual(folder, target, group.Value);
        }
    }

    private static VRCExpressionsMenu.Control BoolMenuControl(ToggleEntry t)
    {
        return new VRCExpressionsMenu.Control {
            name=t.label,
            type=VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter=new VRCExpressionsMenu.Control.Parameter { name=TogglePrefix+t.id },
            value=1f, icon=t.icon
        };
    }

    // A generated assignment is not the user's original menu, even when it was
    // accidentally recorded as the baseline in an old ACM manifest.
    private static bool IsGeneratedAcmAsset(string path)
    {
        return !string.IsNullOrEmpty(path) &&
            path.StartsWith(Generated + "/", StringComparison.Ordinal);
    }

    private static bool IsOwnedAcmMenuControl(VRCExpressionsMenu.Control c)
    {
        if (c == null) return false;
        // Flattened leaf controls are owned by ACM if their parameter uses
        // ACM's reserved namespace. Call this ONLY on known ACM-generated roots;
        // manual originals with these names are separately rejected as conflicts.
        if (IsAcmParameterName(ControlParameterName(c))) return true;
        // The submenu may be named 'Standing', 'Dances', etc. after flattening.
        // Its generated asset path is authoritative, not the display name.
        return c.type == VRCExpressionsMenu.Control.ControlType.SubMenu &&
            c.subMenu != null &&
            IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(c.subMenu));
    }

    private static bool IsAcmParameterName(string name)
    {
        return Parameters.Contains(name) || name == DanceParam ||
            (!string.IsNullOrEmpty(name) && name.StartsWith(TogglePrefix, StringComparison.Ordinal));
    }

    private static string ControlParameterName(VRCExpressionsMenu.Control c)
    {
        return c != null && c.parameter != null ? c.parameter.name : "";
    }

    private static bool SameControl(VRCExpressionsMenu.Control a, VRCExpressionsMenu.Control b)
    {
        return a != null && b != null && a.name == b.name && a.type == b.type &&
            a.subMenu == b.subMenu && ControlParameterName(a) == ControlParameterName(b) &&
            Mathf.Approximately(a.value, b.value);
    }

    private static bool SameManualParameter(VRCExpressionParameters.Parameter a,
        VRCExpressionParameters.Parameter b)
    {
        return a.name == b.name && a.valueType == b.valueType &&
            Mathf.Approximately(a.defaultValue, b.defaultValue) &&
            a.saved == b.saved && a.networkSynced == b.networkSynced;
    }

    // The selected ORIGINAL menu is authoritative. Earlier versions merged
    // apparent manual entries from previously generated MainMenu assets, which
    // could resurrect stale near-duplicates (e.g. SkeleSettings vs SkeletonSettings).
    // NEVER import old generated root controls. If users edited those outputs,
    // they can explicitly move their edits to their real original menu instead.
    private static List<VRCExpressionsMenu.Control> PreserveManualControls(
        VRCExpressionsMenu original, VRCExpressionsMenu currentGenerated)
    {
        if (original == null)
            throw new Exception("Select your original Expressions Menu before generating.");
        var result = (original.controls ?? new List<VRCExpressionsMenu.Control>())
            .Where(c => c != null).ToList();
        if (result.Any(IsOwnedAcmMenuControl))
            throw new Exception("Your selected Original Root Menu contains an ACM-generated " +
                "control. Select the actual manual menu, not a previous ACM Main Menu.");
        // Existing exact duplicates are NOT silently removed. The user owns them.
        return result;
    }

    // Keep the complete original parameter asset. Do not carry orphaned stale
    // parameters from a generated copy into a future generation.
    private static List<VRCExpressionParameters.Parameter> PreserveManualParameters(
        VRCExpressionParameters original, VRCExpressionParameters currentGenerated,
        OriginalFile prior)
    {
        if (original == null)
            throw new Exception("Select your original Expression Parameters asset.");
        return (original.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>())
            .Where(p => p != null).ToList();
    }

    // Validate every original control in its original logical order. A full
    // eight-slot menu may move its last ORIGINAL control onto an ACM Page 2;
    // that is a navigation-only relocation, not deletion or alteration.
    private static void ValidateOriginalControlsPresent(
        VRCExpressionsMenu original, VRCExpressionsMenu combined)
    {
        ValidateMenuTreePreserved(original, combined, new HashSet<string>(), 0);
    }

    private static List<VRCExpressionsMenu.Control> FlattenOwnedPagesForValidation(
        VRCExpressionsMenu menu)
    {
        if (menu == null) return new List<VRCExpressionsMenu.Control>();
        // Generated submenus/pages reside under Build_*/Menus (old builds were flat).
        string path = AssetDatabase.GetAssetPath(menu);
        string folder = string.IsNullOrEmpty(path) ? "" : Path.GetDirectoryName(path).Replace('\\', '/');
        return PageContents(folder, menu);
    }

    private static void ValidateMenuTreePreserved(
        VRCExpressionsMenu original, VRCExpressionsMenu combined,
        HashSet<string> visited, int depth)
    {
        if (original == null || combined == null || depth > 12)
            throw new Exception("Cannot validate the original menu hierarchy.");
        var source = (original.controls ?? new List<VRCExpressionsMenu.Control>())
            .Where(c => c != null).ToList();
        var target = FlattenOwnedPagesForValidation(combined);
        if (target.Count < source.Count)
            throw new Exception("Original controls were lost from menu: " + original.name);
        for (int i=0; i<source.Count; ++i)
        {
            var a = source[i]; var b = target[i];
            if (a.name != b.name || a.type != b.type || a.icon != b.icon ||
                ControlParameterName(a) != ControlParameterName(b) ||
                !Mathf.Approximately(a.value, b.value))
                throw new Exception("Original control '" + a.name +
                    "' changed during paging. Avatar setup not modified.");
            if (a.type != VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                if (a.subMenu != b.subMenu)
                    throw new Exception("Unexpected submenu change in '" + a.name + "'.");
                continue;
            }
            if (a.subMenu == b.subMenu) continue;
            string key = a.subMenu == null ? "" : a.subMenu.GetInstanceID() + ":" +
                (b.subMenu == null ? "" : b.subMenu.GetInstanceID().ToString());
            if (string.IsNullOrEmpty(key) || b.subMenu == null || !visited.Add(key))
                throw new Exception("Cyclic submenu or missing original at '" + a.name + "'.");
            ValidateMenuTreePreserved(a.subMenu, b.subMenu, visited, depth + 1);
        }
    }

    private static void ValidateOriginalParametersPresent(
        VRCExpressionParameters original, VRCExpressionParameters combined)
    {
        foreach (var p in original.parameters ??
            Array.Empty<VRCExpressionParameters.Parameter>())
            if (p != null && !combined.parameters.Any(x => SameManualParameter(p, x)))
                throw new Exception("Cannot verify original parameter '" + p.name +
                    "' in ACM's combined parameters. Build stopped before avatar assignment.");
    }

    // Safe setup repair. ACM must never silently replace an existing manual menu.
    // If originals moved, locate one unambiguous replacement; otherwise recover
    // ACM-free copies from the currently installed, *identifiable* ACM build.
    // An avatar with no menu or params and no prior ACM install can receive new
    // empty assets, but only after telling the user what will be created.
    private const string Recovery = Root + "/Recovery";

    private static string FindMovedAsset<T>(string oldPath) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(oldPath)) return null;
        string desired = Path.GetFileNameWithoutExtension(oldPath);
        var candidates = AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrEmpty(path) &&
                !path.StartsWith(Generated + "/", StringComparison.Ordinal) &&
                string.Equals(Path.GetFileNameWithoutExtension(path), desired,
                    StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.LoadAssetAtPath<T>(path) != null)
            .Distinct().ToList();
        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static void PrepareRecoveryFolder()
    {
        Folders();
        if (!AssetDatabase.IsValidFolder(Recovery))
            AssetDatabase.CreateFolder(Root, "Recovery");
    }

    private static string RecoveryAssetPath(string stem)
    {
        return AssetDatabase.GenerateUniqueAssetPath(
            Recovery + "/" + stem + "_" +
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".asset");
    }

    private static string RecoverMenuFromCurrent(VRCExpressionsMenu generated,
        OriginalFile prior)
    {
        if (generated == null || prior == null || string.IsNullOrEmpty(prior.generatedFolder) ||
            !MatchesBuildAsset(AssetDatabase.GetAssetPath(generated), prior.generatedFolder,
                MenusSubfolder, "MainMenu.asset"))
            return null;
        var controls = (generated.controls ?? new List<VRCExpressionsMenu.Control>()).ToList();
        var owned = controls.Where(c => IsOwnedAcmMenuControl(c) &&
            (c.type != VRCExpressionsMenu.Control.ControlType.SubMenu ||
             (c.subMenu != null &&
              AssetDatabase.GetAssetPath(c.subMenu).StartsWith(
                  prior.generatedFolder + "/", StringComparison.Ordinal)))).ToList();
        if (owned.Count == 0) return null; // Not safely identifiable as ACM content.
        PrepareRecoveryFolder();
        var recovered = UnityEngine.Object.Instantiate(generated);
        recovered.name = "ACM_RecoveredOriginalMenu";
        recovered.controls = controls.Where(c => !owned.Contains(c)).ToList();
        string path = RecoveryAssetPath("RecoveredMenu");
        AssetDatabase.CreateAsset(recovered, path);
        return path;
    }

    private static string RecoverParametersFromCurrent(VRCExpressionParameters generated,
        OriginalFile prior)
    {
        if (generated == null || prior == null || string.IsNullOrEmpty(prior.generatedFolder) ||
            !MatchesBuildAsset(AssetDatabase.GetAssetPath(generated), prior.generatedFolder,
                ParametersSubfolder, "Parameters.asset"))
            return null;
        var all = (generated.parameters ??
            Array.Empty<VRCExpressionParameters.Parameter>()).Where(p => p != null).ToList();
        HashSet<string> owned = prior.ownedParameters != null && prior.ownedParameters.Count > 0
            ? new HashSet<string>(prior.ownedParameters)
            : new HashSet<string>(all.Where(p => Parameters.Contains(p.name) ||
                p.name == DanceParam || p.name.StartsWith(TogglePrefix, StringComparison.Ordinal))
                .Select(p => p.name));
        if (!Parameters.All(owned.Contains) ||
            owned.Any(name => all.Count(p => p.name == name) != 1))
            return null; // Missing or ambiguous ACM-owned parameters.
        PrepareRecoveryFolder();
        var recovered = UnityEngine.Object.Instantiate(generated);
        recovered.name = "ACM_RecoveredOriginalParameters";
        recovered.parameters = all.Where(p => !owned.Contains(p.name)).ToArray();
        string path = RecoveryAssetPath("RecoveredParameters");
        AssetDatabase.CreateAsset(recovered, path);
        return path;
    }

    private bool RepairAvatarSetup(bool showResult, bool quietIfNoChanges = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (showResult) EditorUtility.DisplayDialog("ACM Repair",
                "Exit Play Mode before repairing avatar assets.", "OK");
            return false;
        }
        if (avatar == null)
        {
            if (showResult) EditorUtility.DisplayDialog("ACM Repair",
                "Choose your avatar first.", "OK");
            return false;
        }

        var manifestPath = OriginalPath();
        OriginalFile prior = null;
        try
        {
            if (File.Exists(manifestPath))
                prior = JsonUtility.FromJson<OriginalFile>(File.ReadAllText(manifestPath));
            var fixes = new List<string>();
            string menuPath = prior == null ? null : prior.menuPath;
            string paramPath = prior == null ? null : prior.parameterPath;

            if (prior != null)
            {
                // First try moved original assets; no source copies are changed.
                if (AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(menuPath) == null)
                {
                    var moved = FindMovedAsset<VRCExpressionsMenu>(menuPath);
                    if (moved != null) { menuPath = moved; fixes.Add("Located relocated menu"); }
                    else
                    {
                        var recovered = RecoverMenuFromCurrent(avatar.expressionsMenu, prior);
                        if (recovered == null) throw new Exception(
                            "Original menu missing, and no safe ACM recovery snapshot could be identified. " +
                            "ACM did not create an empty replacement because that could lose manual controls.");
                        menuPath = recovered; fixes.Add("Recovered manual menu entries from previous ACM build");
                    }
                }
                if (AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(paramPath) == null)
                {
                    var moved = FindMovedAsset<VRCExpressionParameters>(paramPath);
                    if (moved != null) { paramPath = moved; fixes.Add("Located relocated parameters"); }
                    else
                    {
                        var recovered = RecoverParametersFromCurrent(avatar.expressionParameters, prior);
                        if (recovered == null) throw new Exception(
                            "Original parameters missing, and no safe ACM recovery snapshot could be identified. " +
                            "ACM did not reset your parameters.");
                        paramPath = recovered; fixes.Add("Recovered manual parameters from previous ACM build");
                    }
                }
                if (prior.menuPath != menuPath || prior.parameterPath != paramPath)
                {
                    prior.menuPath = menuPath;
                    prior.parameterPath = paramPath;
                    File.WriteAllText(manifestPath, JsonUtility.ToJson(prior, true));
                }
                // If an assigned field has become truly null, safely reconnect its
                // original reference. Do not replace a non-null current assignment.
                var existingMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(menuPath);
                var existingParams = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(paramPath);
                if (avatar.expressionsMenu == null || avatar.expressionParameters == null)
                {
                    Undo.RecordObject(avatar, "ACM Reconnect Missing Assets");
                    if (avatar.expressionsMenu == null)
                    { avatar.expressionsMenu = existingMenu; fixes.Add("Reconnected original menu"); }
                    if (avatar.expressionParameters == null)
                    { avatar.expressionParameters = existingParams; fixes.Add("Reconnected original parameters"); }
                    avatar.customExpressions = true;
                    EditorUtility.SetDirty(avatar);
                    EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
                }
            }
            else
            {
                // First-time setup only. Preserve every assigned existing asset.
                bool needMenu = avatar.expressionsMenu == null;
                bool needParams = avatar.expressionParameters == null;
                if (needMenu || needParams)
                {
                    // If an old ACM-generated assignment exists without its manifest,
                    // repair cannot safely infer the original manual controls.
                    var curMenu = AssetDatabase.GetAssetPath(avatar.expressionsMenu);
                    var curParams = AssetDatabase.GetAssetPath(avatar.expressionParameters);
                    if (curMenu.StartsWith(Generated + "/", StringComparison.Ordinal) ||
                        curParams.StartsWith(Generated + "/", StringComparison.Ordinal))
                        throw new Exception("ACM generation record is missing. " +
                            "Restore the original assets from a project backup before continuing.");
                    if (!EditorUtility.DisplayDialog("ACM: Create Missing Setup?",
                        "This avatar has no " + (needMenu ? "Expressions Menu" : "") +
                        (needMenu && needParams ? " or " : "") +
                        (needParams ? "Expression Parameters" : "") +
                        " assigned. ACM can create NEW empty assets for these missing pieces. " +
                        "This does not recover any previously deleted manual controls. Proceed?",
                        "Create Missing", "Cancel")) return false;
                    PrepareRecoveryFolder();
                    Undo.RecordObject(avatar, "ACM Create Missing Expressions Assets");
                    if (needMenu)
                    {
                        var menu = CreateInstance<VRCExpressionsMenu>();
                        menu.name = "ACM_InitialMenu";
                        menu.controls = new List<VRCExpressionsMenu.Control>();
                        string path = RecoveryAssetPath("InitialMenu");
                        AssetDatabase.CreateAsset(menu, path);
                        avatar.expressionsMenu = menu;
                        fixes.Add("Created missing Expressions Menu");
                    }
                    if (needParams)
                    {
                        var pars = CreateInstance<VRCExpressionParameters>();
                        pars.name = "ACM_InitialParameters";
                        pars.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
                        string path = RecoveryAssetPath("InitialParameters");
                        AssetDatabase.CreateAsset(pars, path);
                        avatar.expressionParameters = pars;
                        fixes.Add("Created missing Expression Parameters");
                    }
                    avatar.customExpressions = true;
                    EditorUtility.SetDirty(avatar);
                    EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
                }
            }
            if (fixes.Count > 0)
            {
                AssetDatabase.SaveAssets();
                SmartRememberRepairs(fixes);
            }
            if (showResult && (!quietIfNoChanges || fixes.Count > 0)) EditorUtility.DisplayDialog("ACM Repair",
                fixes.Count == 0 ? "Setup is valid. No repairs needed." :
                "Repaired:\n- " + string.Join("\n- ", fixes.ToArray()), "OK");
            return true;
        }
        catch (Exception error)
        {
            Debug.LogWarning("ACM Repair: " + error.Message);
            if (showResult) EditorUtility.DisplayDialog("ACM Repair - Needs Attention",
                error.Message + "\nYour existing controls were not overwritten.", "OK");
            return false;
        }
    }

    private void Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Avatar Creation Manager",
                "Exit Play Mode before generating or updating poses.", "OK");
            return;
        }
        string stagingFolder = null;
        string priorManifestContents = null;
        bool didWriteManifest = false;
        bool applied = false;
        GameObject audioObjectCreated = null;
        VRCExpressionsMenu previousMenu = null;
        VRCExpressionParameters previousParams = null;
        bool previousCustomExpressions = false;
        bool previousCustomLayers = false;
        VRCAvatarDescriptor.CustomAnimLayer[] previousLayers = null;
        string buildStep = "Checking avatar and setup";
        var buildNotes = new List<string>();
        SmartRestorePoint preBuild = null;
        string inputFingerprint = null;
        string initialManifestContents = null;

        try
        {
            smartGenerationRunning = true;
            smartPendingRepairs.Clear();
            if (avatar == null) throw new Exception("Choose an avatar.");
            // Capture assignments before even the auto-repair path can change them.
            preBuild = SmartCapture("Before ACM Generate / Update");
            initialManifestContents = preBuild == null ? null : preBuild.originalManifestJson;
            var animator = avatar.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                throw new Exception("Avatar needs a valid Humanoid Animator rig.");
            // A user-selected original pair overrides a corrupt old ACM manifest.
            // Otherwise auto-repair missing originals without touching manual files.
            bool manualSourceSet = originalRootMenu != null ||
                originalExpressionParameters != null;
            if (manualSourceSet && (originalRootMenu == null || originalExpressionParameters == null))
                throw new Exception("Select BOTH Original Root Menu and Original Parameters. " +
                    "ACM won't combine mismatched manual and generated assets.");
            bool explicitOriginals = originalRootMenu != null &&
                originalExpressionParameters != null;
            if (!explicitOriginals && !RepairAvatarSetup(true, true))
            {
                // Repair may have partially reattached/created assets before it
                // reported failure. A cancelled/failed repair is not a build.
                if (preBuild != null && SmartRestorePointUsable(preBuild))
                    SmartApplySnapshot(preBuild, false);
                if (initialManifestContents != null)
                {
                    Folders();
                    if (string.IsNullOrEmpty(initialManifestContents))
                    {
                        if (File.Exists(OriginalPath())) File.Delete(OriginalPath());
                    }
                    else File.WriteAllText(OriginalPath(), initialManifestContents);
                }
                return;
            }

            buildStep = "Checking pose, dance and toggle entries";
            // Keep IDs stable across sorting and changing the animations.
            AllocateIds(standing, "Standing");
            AllocateIds(crouching, "Crouching");
            AllocateIds(prone, "Prone");
            AllocateDanceIds(dances);
            // Incomplete toggle drafts are assessed after loading the prior manifest.

            string manifestPath = OriginalPath();
            priorManifestContents = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null;
            OriginalFile prior = priorManifestContents == null
                ? null : JsonUtility.FromJson<OriginalFile>(priorManifestContents);
            // Never mistake an ACM-generated menu for the original manual root.
            // An explicit pair can repair a lost/corrupt manifest, provided controller
            // baselines are also verifiable below.
            if (prior == null && IsGeneratedAcmAsset(
                AssetDatabase.GetAssetPath(avatar.expressionsMenu)) && !explicitOriginals)
                throw new Exception("This avatar uses a generated ACM menu but its original " +
                    "record is missing. Select your real Original Root Menu and Original " +
                    "Parameters above (e.g. Skele Menu and SkelePara) before generating.");

            var layers = avatar.baseAnimationLayers;
            int baseIndex = Array.FindIndex(layers,
                l => l.type == VRCAvatarDescriptor.AnimLayerType.Base);
            if (baseIndex < 0) throw new Exception("Avatar has no Base playable layer.");
            int actionIndex = LayerIndex(layers, VRCAvatarDescriptor.AnimLayerType.Action);
            int fxIndex = LayerIndex(layers, VRCAvatarDescriptor.AnimLayerType.FX);
            if (actionIndex < 0 || fxIndex < 0)
                throw new Exception("Avatar needs Action and FX playable layers.");

            // Existing versions store the original assignments in this manifest.
            // Importantly, we NEVER use a previously generated Base controller as source.
            OriginalFile origin = prior ?? new OriginalFile
            {
                menuPath = AssetDatabase.GetAssetPath(avatar.expressionsMenu),
                parameterPath = AssetDatabase.GetAssetPath(avatar.expressionParameters),
                controllerPath = AssetDatabase.GetAssetPath(layers[baseIndex].animatorController),
                baseWasDefault = layers[baseIndex].isDefault,
                customizedLayers = avatar.customizeAnimationLayers,
                customizedExpressions = avatar.customExpressions,
                actionPath = AssetDatabase.GetAssetPath(layers[actionIndex].animatorController),
                fxPath = AssetDatabase.GetAssetPath(layers[fxIndex].animatorController),
                actionWasDefault = layers[actionIndex].isDefault,
                fxWasDefault = layers[fxIndex].isDefault
            };
            if (prior != null && prior.ownedParameters == null)
                prior.ownedParameters = new List<string>();
            if (customCategories == null) customCategories = new List<CategoryEntry>();
            customCategories.RemoveAll(c => c == null);
            activeBuildToggles = PrepareBuildToggles(prior, buildNotes);
            SaveProfile();
            if (prior != null && !origin.acmMetadataInitialized)
            {
                // Migration from an older pose-only APM manifest.
                origin.actionPath = AssetDatabase.GetAssetPath(layers[actionIndex].animatorController);
                origin.fxPath = AssetDatabase.GetAssetPath(layers[fxIndex].animatorController);
                origin.actionWasDefault = layers[actionIndex].isDefault;
                origin.fxWasDefault = layers[fxIndex].isDefault;
            }
            origin.acmMetadataInitialized = true;
            if (explicitOriginals)
            {
                origin.menuPath = AssetDatabase.GetAssetPath(originalRootMenu);
                origin.parameterPath = AssetDatabase.GetAssetPath(originalExpressionParameters);
            }
            if (originalFXController != null)
            {
                origin.fxPath = AssetDatabase.GetAssetPath(originalFXController);
                origin.fxWasDefault = false;
            }
            if (originalActionController != null)
            {
                origin.actionPath = AssetDatabase.GetAssetPath(originalActionController);
                origin.actionWasDefault = false;
            }
            if (sourceBase != null && (prior == null ||
                IsGeneratedAcmAsset(origin.controllerPath)))
            {
                origin.controllerPath = AssetDatabase.GetAssetPath(sourceBase);
                origin.baseWasDefault = false;
            }
            if (IsGeneratedAcmAsset(origin.menuPath) || IsGeneratedAcmAsset(origin.parameterPath))
                throw new Exception("Your saved original Menu/Parameters point into an old ACM build. " +
                    "Select the real Original Root Menu and Original Parameters in the ACM window. " +
                    "Do not use MainMenu.asset or Parameters.asset from ACM Generated folders.");
            if (string.IsNullOrEmpty(origin.menuPath) || string.IsNullOrEmpty(origin.parameterPath))
                throw new Exception("Missing original Expressions asset reference. " +
                    "Select the original root menu and parameters above.");

            var originalMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(origin.menuPath);
            var originalParams = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(origin.parameterPath);
            if (originalMenu == null || originalParams == null)
                throw new Exception("Original Expressions Menu or Parameters missing. " +
                    "Select your real existing assets above; ACM did not reset them.");
            if (originalMenu.controls != null && originalMenu.controls.Count == 1 &&
                IsOwnedAcmMenuControl(originalMenu.controls[0]))
                throw new Exception("The recorded original root contains ONLY the ACM submenu, " +
                    "not your manually created menus. Select your real original menu " +
                    "(e.g. Skele Menu) and its parameters instead.");

            bool originalBaseAssigned = layers[baseIndex].isDefault == origin.baseWasDefault &&
                (origin.baseWasDefault || AssetDatabase.GetAssetPath(
                    layers[baseIndex].animatorController) == origin.controllerPath);
            bool installed = prior != null && !string.IsNullOrEmpty(prior.generatedFolder) &&
                MatchesBuildAsset(AssetDatabase.GetAssetPath(avatar.expressionsMenu),
                    prior.generatedFolder, MenusSubfolder, "MainMenu.asset") &&
                MatchesBuildAsset(AssetDatabase.GetAssetPath(avatar.expressionParameters),
                    prior.generatedFolder, ParametersSubfolder, "Parameters.asset") &&
                ((MatchesBuildAsset(AssetDatabase.GetAssetPath(layers[baseIndex].animatorController),
                    prior.generatedFolder, ControllersSubfolder, "Locomotion.controller") &&
                    !layers[baseIndex].isDefault) || originalBaseAssigned);

            bool restored = AssetDatabase.GetAssetPath(avatar.expressionsMenu) == origin.menuPath &&
                AssetDatabase.GetAssetPath(avatar.expressionParameters) == origin.parameterPath &&
                layers[baseIndex].isDefault == origin.baseWasDefault &&
                (origin.baseWasDefault ||
                 AssetDatabase.GetAssetPath(layers[baseIndex].animatorController) == origin.controllerPath);

            // If the assigned controller/menu changed outside ACM, do not overwrite
            // that unrelated setup without the user's explicit original-asset choice.
            if (prior != null && !installed && !restored && !explicitOriginals)
                throw new Exception("Your avatar's menu/base setup changed outside ACM. " +
                    "Select your intended Original Root Menu and Original Parameters " +
                    "above to confirm the correct baseline; no assets were changed.");
            if (prior != null && !installed && !restored && explicitOriginals &&
                !EditorUtility.DisplayDialog("ACM: Confirm Original Assets",
                    "Use '" + originalMenu.name + "' and '" + originalParams.name +
                    "' as ACM's NEW original baseline? ACM will clone these assets and " +
                    "keep existing originals unchanged. The avatar descriptor will be assigned " +
                    "the combined copies when generation succeeds.", "Use Originals", "Cancel"))
                return;

            // Do not silently replace an FX or Action controller that someone selected manually
            // after the last ACM generation. The source of truth is the original controller.
            if (installed)
            {
                string fxCurrent = AssetDatabase.GetAssetPath(layers[fxIndex].animatorController);
                string actionCurrent = AssetDatabase.GetAssetPath(layers[actionIndex].animatorController);
                bool fxExpected = fxCurrent == origin.fxPath ||
                    MatchesBuildAsset(fxCurrent, prior.generatedFolder,
                        ControllersSubfolder, "FX.controller") ||
                    (origin.fxWasDefault && layers[fxIndex].isDefault);
                bool actionExpected = actionCurrent == origin.actionPath ||
                    MatchesBuildAsset(actionCurrent, prior.generatedFolder,
                        ControllersSubfolder, "Action.controller") ||
                    (origin.actionWasDefault && layers[actionIndex].isDefault);
                if ((!fxExpected && originalFXController == null) ||
                    (!actionExpected && originalActionController == null))
                    throw new Exception("The FX or Action Controller changed since ACM was installed. " +
                        "Assign the real Original FX/Action in the ACM setup section " +
                        "or restore the original controller. No changes made.");
            }

            // Skip truly unchanged builds only when BOTH the source inputs and
            // currently assigned generated outputs still match the last success.
            inputFingerprint = SmartInputFingerprint(origin);
            if (SmartCanSkipBuild(origin, installed, inputFingerprint))
            {
                EditorUtility.DisplayDialog("ACM — Already Up to Date",
                    "No input assets or ACM configuration changed since the last successful build. " +
                    "The current generated outputs also match. Nothing was replaced.", "OK");
                return;
            }

            // ORIGINAL MENU / PARAMETERS ARE THE SOURCE OF TRUTH.
            // A previous ACM 'Main Menu' may contain only its own submenu;
            // cloning it again would drop Appearance, Weapons, SkeletonSettings, etc.
            // No re-import from old generated roots: that copied stale or duplicate
            // manual-looking controls into new builds. Original assets win.
            VRCExpressionsMenu oldGeneratedMenu = installed ? avatar.expressionsMenu : null;
            VRCExpressionParameters oldGeneratedParams = installed
                ? avatar.expressionParameters : null;
            var menuControls = PreserveManualControls(originalMenu, oldGeneratedMenu);
            var parameters = PreserveManualParameters(originalParams, oldGeneratedParams, prior);
            if (parameters.GroupBy(p => p.name).Any(g => g.Count() > 1))
                throw new Exception("Duplicate parameter names exist in the original asset. " +
                    "ACM did not modify them.");
            if (parameters.Any(p => IsAcmParameterName(p.name)))
                throw new Exception("An original, manually assigned parameter uses a reserved " +
                    "ACM name. ACM will not overwrite it. Rename that conflict manually.");

            buildStep = "Checking Expression Parameter limits";
            bool hasPoses = standing.Count > 0 || crouching.Count > 0 || prone.Count > 0;
            var newParams = hasPoses ? Parameters.Select(NewParameter).ToList() :
                new List<VRCExpressionParameters.Parameter>();
            if (dances.Count > 0)
                newParams.Add(new VRCExpressionParameters.Parameter {
                    name=DanceParam, valueType=VRCExpressionParameters.ValueType.Int,
                    defaultValue=0, saved=false, networkSynced=true });
            newParams.AddRange(GenerationToggles.Select(BoolParameter));
            if (newParams.Select(p=>p.name).Distinct().Count()!=newParams.Count ||
                parameters.Any(p=>newParams.Any(n=>n.name==p.name)))
                throw new Exception("Parameter name collision detected. No changes made.");
            if (parameters.Count + newParams.Count > 8192 ||
                ParamBits(parameters.Concat(newParams)) > 256)
                throw new Exception("Too many VRChat Expression Parameters. Remove some toggles or unsync optional ones.");
            parameters.AddRange(newParams);

            buildStep = "Validating Base locomotion compatibility";
            // Accessory-only builds do not need to clone or rewrite Base locomotion.
            AnimatorController baseSource = null;
            string baseSourcePath = null;
            if (hasPoses)
            {
                baseSource = sourceBase;
                if (baseSource == null)
                    baseSource = origin.baseWasDefault
                        ? AssetDatabase.LoadAssetAtPath<AnimatorController>(SampleBase)
                        : AssetDatabase.LoadAssetAtPath<AnimatorController>(origin.controllerPath);
                if (baseSource == null)
                    throw new Exception("Original Base controller missing. Choose Source Base explicitly.");
                baseSourcePath = AssetDatabase.GetAssetPath(baseSource);
                if (baseSourcePath.StartsWith(Generated + "/", StringComparison.Ordinal))
                    throw new Exception("Source Base points to a previously generated controller. " +
                        "Choose the original Base controller instead.");
                for (int i = 0; i < 3; i++)
                    FindTarget(baseSource, StanceNames[i], IdleClipNames[i]);
                foreach (var p in baseSource.parameters)
                    if (Parameters.Contains(p.name) &&
                        p.type != AnimatorControllerParameterType.Float)
                        throw new Exception("Animator parameter conflict: " + p.name);
            }

            Folders();
            stagingFolder = Generated + "/Build_" +
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" +
                Guid.NewGuid().ToString("N").Substring(0, 6);
            string batchName = stagingFolder.Substring(Generated.Length + 1);
            AssetDatabase.CreateFolder(Generated, batchName);
            CreateBuildSubfolders(stagingFolder);

            buildStep = "Creating safe copies of menu and parameters";
            var paramsCopy = Instantiate(originalParams);
            paramsCopy.name = originalParams.name + " + ACM";
            paramsCopy.parameters = parameters.ToArray();
            AssetDatabase.CreateAsset(paramsCopy,
                BuildAssetPath(stagingFolder, ParametersSubfolder, "Parameters.asset"));
            ValidateOriginalParametersPresent(originalParams, paramsCopy);

            var menuCopy = Instantiate(originalMenu);
            menuCopy.name = originalMenu.name + " + ACM";
            // Keep Unity-instantiated serializable controls, not the original
            // control objects from PreserveManualControls. Later submenu redirects
            // must NEVER modify the user's original Skele Menu in memory.
            menuCopy.controls = (menuCopy.controls ?? new List<VRCExpressionsMenu.Control>())
                .Where(c => c != null).ToList();
            AssetDatabase.CreateAsset(menuCopy,
                BuildAssetPath(stagingFolder, MenusSubfolder, "MainMenu.asset"));
            ValidateOriginalControlsPresent(originalMenu, menuCopy);
            // Append controls into EXISTING category submenus where possible.
            // Appearance keeps its original entries; no ACM hub or direct root
            // accessory toggles are created.
            buildStep = "Merging Appearance categories and paging menus";
            AddCategoryAcmControls(stagingFolder, menuCopy);
            ValidateOriginalControlsPresent(originalMenu, menuCopy);
            ValidateOriginalParametersPresent(originalParams, paramsCopy);

            buildStep = "Building locomotion poses";
            AnimatorController controllerCopy = null;
            if (hasPoses)
            {
                string controllerPath = BuildAssetPath(stagingFolder,
                    ControllersSubfolder, "Locomotion.controller");
                if (!AssetDatabase.CopyAsset(baseSourcePath, controllerPath))
                    throw new Exception("Failed to clone original Base controller: " + baseSourcePath);
                controllerCopy = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controllerCopy == null)
                    throw new Exception("Could not load cloned Base controller.");
                foreach (string parameter in Parameters)
                    if (!controllerCopy.parameters.Any(p => p.name == parameter))
                        controllerCopy.AddParameter(parameter, AnimatorControllerParameterType.Float);
                int sequence = 0;
                List<Pose>[] poseLists = { standing, crouching, prone };
                for (int i = 0; i < 3; i++)
                {
                    var target = FindTarget(controllerCopy, StanceNames[i], IdleClipNames[i]);
                    PatchStance(target, IdleClipNames[i], Parameters[i],
                        poseLists[i], stagingFolder, ref sequence);
                }
            }
            // ALWAYS rebuild FX/Action from the original manual controller, NEVER
            // chain copies of old ACM-generated FX/Action. This keeps existing layers,
            // transitions and manual toggle curves intact on each ACM update.
            bool previousFxGenerated = installed && MatchesBuildAsset(
                AssetDatabase.GetAssetPath(layers[fxIndex].animatorController),
                prior.generatedFolder, ControllersSubfolder, "FX.controller");
            bool previousActionGenerated = installed && MatchesBuildAsset(
                AssetDatabase.GetAssetPath(layers[actionIndex].animatorController),
                prior.generatedFolder, ControllersSubfolder, "Action.controller");
            AnimatorController fxCopy = null;
            AnimatorController actionCopy = null;
            buildStep = "Building FX toggles";
            if (GenerationToggles.Count > 0)
            {
                var manualFx = origin.fxWasDefault
                    ? AssetDatabase.LoadAssetAtPath<AnimatorController>(SampleFX)
                    : AssetDatabase.LoadAssetAtPath<AnimatorController>(origin.fxPath);
                if (manualFx == null && (origin.fxWasDefault || !string.IsNullOrEmpty(origin.fxPath)))
                    throw new Exception("Your original FX controller is missing. No changes made. " +
                        "Restore the original FX controller before generating toggles.");
                CheckManualFxCurveConflicts(manualFx, GenerationToggles);
                CheckObjectToggleConflicts(manualFx);
                fxCopy = CopyOriginalPlayable(origin.fxPath, origin.fxWasDefault,
                    SampleFX, BuildAssetPath(stagingFolder, ControllersSubfolder,
                        "FX.controller"), "FX");
                BuildToggles(fxCopy, stagingFolder);
            }
            buildStep = "Building Action dances";
            if (dances.Count > 0)
            {
                try
                {
                    actionCopy = CopyOriginalPlayable(origin.actionPath, origin.actionWasDefault,
                        SampleAction, BuildAssetPath(stagingFolder, ControllersSubfolder,
                            "Action.controller"), "Action");
                }
                catch (Exception error)
                {
                    throw new Exception("Cannot prepare protected Action controller: " +
                        error.Message, error);
                }
                var playableType = FindBehaviour("VRC_PlayableLayerControl") ??
                    FindBehaviour("VRCPlayableLayerControl");
                if (playableType == null) throw new Exception(
                    "VRChat Playable Layer Control is unavailable. Please repair or update your Avatar SDK.");
                var audioType = FindBehaviour("VRC_AnimatorPlayAudio") ??
                    FindBehaviour("VRCAnimatorPlayAudio");
                if (dances.Any(d=>d.audio != null) && audioType == null)
                    throw new Exception("This VRChat SDK has no usable Animator Play Audio behaviour.");
                BuildDances(actionCopy, dances, playableType, audioType);
                if (actionCopy == null || !actionCopy.layers.Any(l => l.name == OwnedDanceLayer))
                    throw new Exception("Dance Action layer was not retained by the generated controller.");
            }
            buildStep = "Saving generated assets";
            AssetDatabase.SaveAssets();

            // Build has passed all checks. Keep snapshot for recovery on unexpected failure.
            previousMenu = avatar.expressionsMenu;
            previousParams = avatar.expressionParameters;
            previousCustomExpressions = avatar.customExpressions;
            previousCustomLayers = avatar.customizeAnimationLayers;
            previousLayers = (VRCAvatarDescriptor.CustomAnimLayer[])layers.Clone();

            // A small, local-range AudioSource is used only for optional PC dance music.
            if (dances.Any(d=>d.audio != null))
            {
                var node = avatar.transform.Find("ACM_DanceAudio");
                if (node == null)
                {
                    audioObjectCreated = new GameObject("ACM_DanceAudio");
                    audioObjectCreated.transform.SetParent(avatar.transform, false);
                    node = audioObjectCreated.transform;
                }
                var audio = node.GetComponent<AudioSource>();
                if (audio == null) audio = node.gameObject.AddComponent<AudioSource>();
                audio.playOnAwake = false; audio.spatialBlend = 1f;
                audio.minDistance = 1.5f; audio.maxDistance = 8f;
                audio.rolloffMode = AudioRolloffMode.Linear;
                audio.volume = .25f;
            }
            buildStep = "Assigning generated assets to the avatar";
            Undo.RecordObject(avatar, installed ? "Update Avatar Creation Manager" :
                "Generate Avatar Creation Manager");
            avatar.customExpressions = true;
            avatar.expressionsMenu = menuCopy;
            avatar.expressionParameters = paramsCopy;
            bool previousBaseGenerated = installed && MatchesBuildAsset(
                AssetDatabase.GetAssetPath(layers[baseIndex].animatorController),
                prior.generatedFolder, ControllersSubfolder, "Locomotion.controller");
            if (controllerCopy != null)
            {
                layers[baseIndex].isDefault = false;
                layers[baseIndex].animatorController = controllerCopy;
            }
            else if (previousBaseGenerated)
            {
                layers[baseIndex].isDefault = origin.baseWasDefault;
                layers[baseIndex].animatorController = origin.baseWasDefault ? null :
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(origin.controllerPath);
            }
            avatar.customizeAnimationLayers = true;
            if (actionCopy != null)
            { layers[actionIndex].isDefault = false;
              layers[actionIndex].animatorController = actionCopy; }
            if (fxCopy != null)
            { layers[fxIndex].isDefault = false;
              layers[fxIndex].animatorController = fxCopy; }
            else if (previousFxGenerated)
            {
                // The last ACM-generated toggle was removed: return to the ORIGINAL FX.
                layers[fxIndex].isDefault = origin.fxWasDefault;
                layers[fxIndex].animatorController = origin.fxWasDefault ? null :
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(origin.fxPath);
            }
            if (actionCopy == null && previousActionGenerated)
            {
                // All dances removed: return to the original Action layer.
                layers[actionIndex].isDefault = origin.actionWasDefault;
                layers[actionIndex].animatorController = origin.actionWasDefault ? null :
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(origin.actionPath);
            }
            avatar.baseAnimationLayers = layers;
            applied = true;

            // Persist the new output path only after successful generation.
            origin.generatedFolder = stagingFolder;
            origin.ownedParameters = newParams.Select(x=>x.name).ToList();
            File.WriteAllText(manifestPath, JsonUtility.ToJson(origin, true));
            didWriteManifest = true;
            EditorUtility.SetDirty(avatar);
            EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // Store the last working reference graph and a bounded restore history.
            // A history write error must not undo an otherwise valid avatar build.
            try { SmartCommitSuccessfulBuild(preBuild, origin, inputFingerprint); }
            catch (Exception historyError)
            { Debug.LogWarning("ACM build succeeded, but Smart History could not be saved: " + historyError); }

            EditorUtility.DisplayDialog("Avatar Creation Manager",
                (buildNotes.Count == 0 ? "" : "Setup notes:\n- " +
                    string.Join("\n- ", buildNotes.ToArray()) + "\n\n") +
                "ACM built nested Appearance category menus and paginated full tabs from '" + originalMenu.name + "' (" +
                (originalMenu.controls == null ? 0 : originalMenu.controls.Count) +
                " original top-level controls) and '" + originalParams.name +
                "'. All original controls and parameters were checked in the combined " +
                "copies. The source files were not modified. " +
                "Test your manual FX toggles in VRChat Build & Test before uploading.", "OK");
        }
        catch (Exception error)
        {
            Debug.LogError("ACM build failed during: " + buildStep + "\n" + error);
            // Restore the descriptor as it existed BEFORE repair/generation, even if
            // failure happened before the new build reached its final commit.
            if (preBuild != null && avatar != null && SmartRestorePointUsable(preBuild))
            {
                try { SmartApplySnapshot(preBuild, false); }
                catch (Exception restoreError)
                { Debug.LogError("ACM automatic descriptor rollback failed: " + restoreError); }
            }
            else if (applied && avatar != null && previousLayers != null)
            {
                avatar.expressionsMenu = previousMenu;
                avatar.expressionParameters = previousParams;
                avatar.customExpressions = previousCustomExpressions;
                avatar.customizeAnimationLayers = previousCustomLayers;
                avatar.baseAnimationLayers = previousLayers;
                EditorUtility.SetDirty(avatar);
                EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
            }
            try
            {
                // Restore the exact manifest before any automatic repair; repair
                // may itself have changed the saved original references.
                if (initialManifestContents != null)
                {
                    Folders();
                    if (string.IsNullOrEmpty(initialManifestContents))
                    {
                        if (File.Exists(OriginalPath())) File.Delete(OriginalPath());
                    }
                    else File.WriteAllText(OriginalPath(), initialManifestContents);
                }
                else if (didWriteManifest)
                {
                    if (priorManifestContents == null) File.Delete(OriginalPath());
                    else File.WriteAllText(OriginalPath(), priorManifestContents);
                }
                if (audioObjectCreated != null) DestroyImmediate(audioObjectCreated);
                if (!string.IsNullOrEmpty(stagingFolder) &&
                    AssetDatabase.IsValidFolder(stagingFolder))
                    AssetDatabase.DeleteAsset(stagingFolder);
            }
            catch (Exception cleanupError)
            {
                Debug.LogError("ACM cleanup also failed: " + cleanupError);
            }
            SmartRecordFailure(buildStep, error);
            string explanation = error is NullReferenceException ?
                "A Unity or SDK object unexpectedly became null during " + buildStep +
                ". The generated changes were discarded. See the first red ACM error " +
                "in Unity Console for the exact stack trace." :
                error.Message;
            EditorUtility.DisplayDialog("Avatar Creation Manager - Build Aborted",
                "Step: " + buildStep + "\n\n" + explanation +
                "\n\nOriginal project assets were not edited. See Console for details.", "OK");
        }
        finally
        {
            activeBuildToggles = null;
            smartGenerationRunning = false;
            smartPendingRepairs.Clear();
        }
    }

    // Immediate recovery for users whose avatar was redirected to an old ACM-only
    // Main Menu. This reassigns the exact selected ORIGINAL menu/parameters and
    // optional original FX/Action controllers, never altering the source assets.
    private void RestoreSelectedOriginalSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || avatar == null)
        {
            EditorUtility.DisplayDialog("ACM Recovery",
                "Exit Play Mode and choose an avatar before restoring.", "OK");
            return;
        }
        if (originalRootMenu == null || originalExpressionParameters == null)
        {
            EditorUtility.DisplayDialog("ACM Recovery",
                "Drag your real Original Root Menu and Original Parameters into both fields first.", "OK");
            return;
        }
        if (IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(originalRootMenu)) ||
            IsGeneratedAcmAsset(AssetDatabase.GetAssetPath(originalExpressionParameters)))
        {
            EditorUtility.DisplayDialog("ACM Recovery",
                "You selected assets inside ACM's Generated folder. Choose the original " +
                "manual menu and parameters instead.", "OK");
            return;
        }
        string details = "Set this avatar's active Expressions Menu to '" +
            originalRootMenu.name + "' and Parameters to '" +
            originalExpressionParameters.name + "'?";
        if (originalFXController != null) details += "\nAlso restore FX: " + originalFXController.name;
        if (originalActionController != null) details += "\nAlso restore Action: " + originalActionController.name;
        details += "\n\nOnly the Avatar Descriptor references will be changed. " +
            "Your original assets are not modified; Unity Undo is available.";
        if (!EditorUtility.DisplayDialog("ACM: Restore Manual Setup", details,
            "Restore Selected", "Cancel")) return;
        Undo.RecordObject(avatar, "ACM Restore Selected Original Assets");
        avatar.expressionsMenu = originalRootMenu;
        avatar.expressionParameters = originalExpressionParameters;
        avatar.customExpressions = true;
        var layers = avatar.baseAnimationLayers;
        bool changedLayers = false;
        if (originalFXController != null)
        {
            int ix = LayerIndex(layers, VRCAvatarDescriptor.AnimLayerType.FX);
            if (ix >= 0)
            {
                layers[ix].isDefault = false;
                layers[ix].animatorController = originalFXController;
                changedLayers = true;
            }
        }
        if (originalActionController != null)
        {
            int ix = LayerIndex(layers, VRCAvatarDescriptor.AnimLayerType.Action);
            if (ix >= 0)
            {
                layers[ix].isDefault = false;
                layers[ix].animatorController = originalActionController;
                changedLayers = true;
            }
        }
        if (changedLayers)
        {
            avatar.baseAnimationLayers = layers;
            avatar.customizeAnimationLayers = true;
        }
        EditorUtility.SetDirty(avatar);
        EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("ACM Recovery",
            "Selected original assets are now assigned to the avatar. " +
            "Check your existing Action Wheel controls and manual toggles before generating.", "OK");
    }

    private void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Avatar Creation Manager",
                "Exit Play Mode before restoring the avatar.", "OK");
            return;
        }
        try
        {
            if (avatar == null || !File.Exists(OriginalPath()))
                throw new Exception("No saved original assignment found.");
            var origin = JsonUtility.FromJson<OriginalFile>(File.ReadAllText(OriginalPath()));
            var menu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(origin.menuPath);
            var parameters = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(origin.parameterPath);
            var controller = string.IsNullOrEmpty(origin.controllerPath) ? null :
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(origin.controllerPath);
            if (menu == null || parameters == null || (!origin.baseWasDefault && controller == null))
                throw new Exception("Original assets are missing. Restore your Unity project backup.");
            var layers = avatar.baseAnimationLayers;
            int index = Array.FindIndex(layers,
                l => l.type == VRCAvatarDescriptor.AnimLayerType.Base);
            if (index < 0) throw new Exception("Base playable layer missing.");
            if (!EditorUtility.DisplayDialog("Restore Avatar",
                "Restore original Expressions Menu, Parameters and Base controller? " +
                "Your current generated files will be kept for recovery.", "Restore", "Cancel")) return;
            Undo.RecordObject(avatar, "Restore Avatar Creation Manager");
            avatar.expressionsMenu = menu;
            avatar.expressionParameters = parameters;
            avatar.customExpressions = origin.customizedExpressions;
            layers[index].isDefault = origin.baseWasDefault;
            layers[index].animatorController = controller;
            int actionIndex = LayerIndex(layers, VRCAvatarDescriptor.AnimLayerType.Action);
            int fxIndex = LayerIndex(layers, VRCAvatarDescriptor.AnimLayerType.FX);
            if (actionIndex >= 0)
            {
                layers[actionIndex].isDefault = origin.actionWasDefault;
                layers[actionIndex].animatorController =
                    string.IsNullOrEmpty(origin.actionPath) ? null :
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(origin.actionPath);
            }
            if (fxIndex >= 0)
            {
                layers[fxIndex].isDefault = origin.fxWasDefault;
                layers[fxIndex].animatorController =
                    string.IsNullOrEmpty(origin.fxPath) ? null :
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(origin.fxPath);
            }
            avatar.baseAnimationLayers = layers;
            avatar.customizeAnimationLayers = origin.customizedLayers;
            EditorUtility.SetDirty(avatar);
            EditorSceneManager.MarkSceneDirty(avatar.gameObject.scene);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Restored", "Original avatar assignment restored.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Restore Failed", e.Message, "OK");
        }
    }
}
