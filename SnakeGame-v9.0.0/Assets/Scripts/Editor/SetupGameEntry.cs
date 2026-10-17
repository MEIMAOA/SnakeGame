using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArknightsTD.EditorTools
{
    /// <summary>
    /// 编辑器初始化器：一键准备好可运行的游戏入口。
    /// <list type="bullet">
    /// <item><b>创建入口场景</b>：新建并保存 Assets/Scenes/Game.unity，
    ///       里面放一个挂 <c>SnakeBootstrap</c> 的 GameObject 与一台相机。</item>
    /// <item><b>打包 exe</b>：用该场景做启动场景，Build 出独立可执行程序。</item>
    /// </list>
    /// <para>
    /// 调用（命令行）：
    /// Tuanjie.exe -batchmode -quit -projectPath &lt;工程&gt;
    ///   -executeMethod ArknightsTD.EditorTools.SetupGameEntry.CreateEntryScene
    /// 或 -executeMethod ArknightsTD.EditorTools.SetupGameEntry.BuildWindows
    /// </para>
    /// </summary>
    public static class SetupGameEntry
    {
        /// <summary>入口场景路径。</summary>
        public const string ScenePath = "Assets/Scenes/Game.unity";

        /// <summary>
        /// 创建（或重建）入口场景：一个空 GameObject 挂 SnakeBootstrap，加一台主相机。
        /// 这样打开游戏就有模式选择菜单，不用手动搭场景。
        /// </summary>
        [MenuItem("ArknightsTD/创建游戏入口场景")]
        public static void CreateEntryScene()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 游戏宿主：SnakeBootstrap 会自己创建相机（若不存在）与控制器
            var host = new GameObject("SnakeBootstrap");
            host.AddComponent<ArknightsTD.SnakeView.SnakeBootstrap>();

            // 也放一个塔防入口（默认禁用，需要时在 Inspector 里启用即可切到塔防）
            var td = new GameObject("BattleBootstrap(塔防,默认关闭)");
            var tdComp = td.AddComponent<ArknightsTD.View.BattleBootstrap>();
            td.SetActive(false);
            EditorUtility.SetDirty(tdComp);

            // 主相机（SnakeBootstrap 会复用 tag 为 MainCamera 的相机）
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 1f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[SetupGameEntry] 已创建入口场景 {ScenePath}（含 SnakeBootstrap 与主相机）");
        }

        /// <summary>
        /// 打包 Windows 独立可执行程序到 Build/Windows/。
        /// <summary>通用打包：target + 架构（arch 非空时设置 ARM64）。</summary>
        private static void BuildFor(BuildTarget target, string folder, string arch)
        {
            if (!File.Exists(ScenePath)) CreateEntryScene();

            var outDir = Path.Combine(Directory.GetCurrentDirectory(), "Build", folder);
            Directory.CreateDirectory(outDir);

            // 正式版产品信息（决定 exe 名称、窗口标题、数据目录名）
            PlayerSettings.productName = "SnakeGame";
            PlayerSettings.companyName = "MMA131845";
            PlayerSettings.bundleVersion = "9.0.0";
            PlayerSettings.runInBackground = true;

            // 目标架构：Windows ARM64 需要显式切换
            if (!string.IsNullOrEmpty(arch))
                EditorUserBuildSettings.SetPlatformSettings("Standalone", "Architecture", arch);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outDir, "SnakeGame.exe"),
                target = target,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[SetupGameEntry] 打包({folder}/{target}/{arch ?? "x64"}) 结果 {summary.result}：" +
                      $"输出={summary.outputPath} 大小={summary.totalSize / 1024 / 1024}MB " +
                      $"错误={summary.totalErrors}");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        [UnityEditor.MenuItem("ArknightsTD/打包 Windows x64")]
        public static void BuildWindows() => BuildFor(BuildTarget.StandaloneWindows64, "Windows64", null);

        [UnityEditor.MenuItem("ArknightsTD/打包 Windows x86(32 位)")]
        public static void BuildWindows32() => BuildFor(BuildTarget.StandaloneWindows, "Windows32", null);

        [UnityEditor.MenuItem("ArknightsTD/打包 Windows ARM64")]
        public static void BuildWindowsARM64() => BuildFor(BuildTarget.StandaloneWindows64, "WindowsARM64", "ARM64");
    }
}
