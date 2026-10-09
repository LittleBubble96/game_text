using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YooAsset.Editor
{
    /// <summary>Stages a complete AB package in Resources for one player build, then removes only its generated assets.</summary>
    public sealed class EmbeddedPackageBuildScope : IDisposable
    {
        private const string AssetRoot = "Assets/Resources/EmbeddedYooAssets";
        private bool _ownsDirectory;

        public EmbeddedPackageBuildScope(string packageDirectory, string packageName)
        {
            if (Directory.Exists(AssetRoot) || File.Exists(AssetRoot + ".meta"))
                throw new InvalidOperationException($"{AssetRoot} already exists. It is reserved for temporary embedded packages; inspect leftover files before rebuilding.");

            string versionFile = YooAssetSettingsData.GetPackageVersionFileName(packageName);
            string version = File.ReadAllText(Path.Combine(packageDirectory, versionFile)).Trim();
            string manifestFile = YooAssetSettingsData.GetManifestBinaryFileName(packageName, version);
            string hashFile = YooAssetSettingsData.GetPackageHashFileName(packageName, version);
            var bytes = File.ReadAllBytes(Path.Combine(packageDirectory, manifestFile));
            if (!ManifestTools.VerifyManifestData(bytes, File.ReadAllText(Path.Combine(packageDirectory, hashFile)).Trim()))
                throw new InvalidOperationException("Cannot embed a package with an invalid manifest checksum.");
            var manifest = ManifestTools.DeserializeFromBinary(bytes, null);
            if (manifest.PackageName != packageName || manifest.PackageVersion != version)
                throw new InvalidOperationException("Embedded package identity does not match the build.");
            if (manifest.BuildBundleType != (int)EBuildBundleType.AssetBundle)
                throw new InvalidOperationException("Embedded packages support AssetBundles only.");

            long totalBytes = 0;
            foreach (var bundle in manifest.BundleList)
            {
                var file = new FileInfo(Path.Combine(packageDirectory, bundle.FileName));
                if (bundle.Encrypted || !file.Exists || file.Length != bundle.FileSize)
                    throw new InvalidOperationException($"Cannot embed incomplete or encrypted bundle: {bundle.FileName}");
                totalBytes += file.Length;
            }

            try
            {
                string destination = Path.Combine(AssetRoot, packageName);
                Directory.CreateDirectory(destination);
                _ownsDirectory = true;
                Copy(packageDirectory, destination, versionFile);
                Copy(packageDirectory, destination, manifestFile);
                Copy(packageDirectory, destination, hashFile);
                foreach (var bundle in manifest.BundleList) Copy(packageDirectory, destination, bundle.FileName);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"[EmbeddedPackage] Staged {manifest.BundleList.Count} bundles ({totalBytes / 1048576f:F2} MiB), version {version}. Final compressed size is checked by the WeChat exporter.");
            }
            catch { Dispose(); throw; }
        }

        private static void Copy(string source, string destination, string name)
        {
            // Keep the original extension in the Resources key; the last .bytes makes Unity import opaque data.
            File.Copy(Path.Combine(source, name), Path.Combine(destination, name + ".bytes"));
        }

        public void Dispose()
        {
            return;
            if (!_ownsDirectory) return;
            string expected = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "EmbeddedYooAssets"));
            if (!string.Equals(Path.GetFullPath(AssetRoot), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to remove an embedded package outside this project's Resources directory.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.DeleteAsset(AssetRoot))
                throw new IOException($"Failed to remove temporary build assets: {AssetRoot}");
            _ownsDirectory = false;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
