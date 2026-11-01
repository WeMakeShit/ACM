// Avatar Creation Manager — About / Welcome window — By DTAA
// Pure editor UI. Does not alter any avatar assets, scenes or settings.
using UnityEditor;
using UnityEngine;

public sealed class ACMAboutWindow : EditorWindow
{
    private const string BannerPath = "Packages/com.wemakeshit.acm/Editor/AvatarCreationManager_Banner.png";
    private const string KofiUrl = "https://ko-fi.com/wemakeshit";
    private Vector2 scroll;
    private Texture2D banner;

    [MenuItem("Tools/ACM/About ACM", false, 101)]
    public static void ShowWindow()
    {
        var window = GetWindow<ACMAboutWindow>("About ACM");
        window.minSize = new Vector2(420f, 465f);
        window.Show();
        window.Focus();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Space(8f);
        if (banner == null)
            banner = AssetDatabase.LoadAssetAtPath<Texture2D>(BannerPath);
        if (banner != null)
        {
            float usableWidth = Mathf.Max(200f, EditorGUIUtility.currentViewWidth - 28f);
            float height = usableWidth * (float)banner.height / Mathf.Max(1f, (float)banner.width);
            Rect bannerRect = GUILayoutUtility.GetRect(usableWidth, height,
                GUILayout.ExpandWidth(true), GUILayout.Height(height));
            GUI.DrawTexture(bannerRect, banner, ScaleMode.ScaleToFit, true);
        }
        else
        {
            EditorGUILayout.HelpBox("ACM banner couldn't be found at " + BannerPath, MessageType.Warning);
        }

        GUILayout.Space(8f);
        var center = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 16, wordWrap = true
        };
        GUILayout.Label("Avatar Creation Manager", center);
        var muted = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
        GUILayout.Label("By DTAA    |    v" + ACMUpdater.CurrentVersion, muted);
        GUILayout.Space(12f);
        GUILayout.Label("About ACM", EditorStyles.boldLabel);
        GUILayout.Label(
            "Avatar Creation Manager (ACM) is a Unity editor tool made to take some of the " +
            "hassle out of creating VRChat avatars.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(8f);
        GUILayout.Label(
            "Setting up animations, toggles, menus, and parameters by hand can get pretty " +
            "tedious, especially when you're still figuring out how everything works. " +
            "ACM was made to simplify that process.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(8f);
        GUILayout.Label(
            "You can set up poses, dances, clothing, accessories, and custom toggles " +
            "without having to build everything from scratch. It also helps organize " +
            "your Action Wheel menus, works with existing avatar setups, and includes " +
            "tools for checking and repairing common issues.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(8f);
        GUILayout.Label(
            "The goal is simple: spend less time fighting with Unity and more time " +
            "actually working on your avatar.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(12f);
        GUILayout.Label("Updates", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This version of ACM is installed and updated through VRChat Creator Companion (VCC). " +
            "Use VCC to see available versions or roll back to a previous version. " +
            "ACM will never overwrite its own VCC-managed package files.", MessageType.Info);
        if (GUILayout.Button("How to Update in VCC", GUILayout.Height(28f)))
            Application.OpenURL("https://vcc.docs.vrchat.com/vpm/packages/");
        GUILayout.Space(7f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open ACM", GUILayout.Height(30f)))
                AvatarCreationManager.ShowTool();
            if (GUILayout.Button("Support on Ko-fi", GUILayout.Height(30f)))
                Application.OpenURL(KofiUrl);
        }
        GUILayout.Space(7f);
        GUILayout.Label("Made by DTAA", muted);
        GUILayout.Space(10f);
        EditorGUILayout.EndScrollView();
    }
}
