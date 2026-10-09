using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace TEngine
{
    // Prevent direct SDK exports from silently building a player without the staged package.
    public sealed class WechatEmbeddedBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
#if WEIXINMINIGAME
            if (report.summary.platform != BuildTarget.WebGL || !Settings.UpdateSetting.WechatEmbedResources) return;
            if (!File.Exists("Assets/Resources/EmbeddedYooAssets/DefaultPackage/DefaultPackage.version.bytes"))
                throw new BuildFailedException("微信包内资源尚未准备。请使用 TEngine/Build/一键打包Webgl(Release/Develop)，不要直接使用微信转换面板。恢复CDN模式可关闭 UpdateSetting.WechatEmbedResources。");
#endif
        }
    }
}
