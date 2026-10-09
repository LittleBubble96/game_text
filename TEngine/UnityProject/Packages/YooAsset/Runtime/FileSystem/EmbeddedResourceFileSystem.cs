using System;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

namespace YooAsset
{
    /// <summary>Small, release-bound packages embedded as Resources TextAssets in the Unity data file.</summary>
    [Preserve]
    internal sealed class EmbeddedResourceFileSystem : DefaultWebServerFileSystem
    {
        internal const string ResourceRoot = "EmbeddedYooAssets";

        [Preserve]
        public EmbeddedResourceFileSystem() { }

        internal string ResourcePath(string fileName) => $"{ResourceRoot}/{PackageName}/{fileName}";

        internal byte[] Read(string fileName)
        {
            var asset = Resources.Load<TextAsset>(ResourcePath(fileName));
            if (asset == null)
                throw new InvalidOperationException($"Missing embedded resource: {ResourcePath(fileName)}. Rebuild using TEngine's one-click WebGL build.");
            try { return asset.bytes; }
            finally { Resources.UnloadAsset(asset); }
        }

        internal string ReadText(string fileName) => Encoding.UTF8.GetString(Read(fileName)).Trim('\uFEFF', '\r', '\n', ' ');

        public override FSInitializeFileSystemOperation InitializeFileSystemAsync() => new Initialize(this);
        public override FSRequestPackageVersionOperation RequestPackageVersionAsync(bool appendTimeTicks, int timeout) => new Version(this);
        public override FSLoadPackageManifestOperation LoadPackageManifestAsync(string packageVersion, int timeout) => new LoadManifest(this, packageVersion);
        public override FSLoadBundleOperation LoadBundleFile(PackageBundle bundle) => new LoadBundle(this, bundle);

        private sealed class Initialize : FSInitializeFileSystemOperation
        {
            private readonly EmbeddedResourceFileSystem _fs;
            internal Initialize(EmbeddedResourceFileSystem fs) { _fs = fs; }
            internal override void InternalStart()
            {
                try
                {
                    if (string.IsNullOrEmpty(_fs.ReadText(YooAssetSettingsData.GetPackageVersionFileName(_fs.PackageName))))
                        throw new InvalidOperationException("Embedded package version is empty.");
                    Status = EOperationStatus.Succeed;
                }
                catch (Exception e) { Error = e.Message; Status = EOperationStatus.Failed; }
            }
            internal override void InternalUpdate() { }
        }

        private sealed class Version : FSRequestPackageVersionOperation
        {
            private readonly EmbeddedResourceFileSystem _fs;
            internal Version(EmbeddedResourceFileSystem fs) { _fs = fs; }
            internal override void InternalStart()
            {
                try
                {
                    PackageVersion = _fs.ReadText(YooAssetSettingsData.GetPackageVersionFileName(_fs.PackageName));
                    Status = EOperationStatus.Succeed;
                }
                catch (Exception e) { Error = e.Message; Status = EOperationStatus.Failed; }
            }
            internal override void InternalUpdate() { }
        }

        private sealed class LoadManifest : FSLoadPackageManifestOperation
        {
            private readonly EmbeddedResourceFileSystem _fs;
            private readonly string _version;
            private DeserializeManifestOperation _operation;
            internal LoadManifest(EmbeddedResourceFileSystem fs, string version) { _fs = fs; _version = version; }
            internal override void InternalStart()
            {
                try
                {
                    var bytes = _fs.Read(YooAssetSettingsData.GetManifestBinaryFileName(_fs.PackageName, _version));
                    var hash = _fs.ReadText(YooAssetSettingsData.GetPackageHashFileName(_fs.PackageName, _version));
                    if (!ManifestTools.VerifyManifestData(bytes, hash))
                        throw new InvalidOperationException("Embedded manifest checksum mismatch.");
                    _operation = new DeserializeManifestOperation(null, bytes);
                    _operation.StartOperation();
                    AddChildOperation(_operation);
                }
                catch (Exception e) { Error = e.Message; Status = EOperationStatus.Failed; }
            }
            internal override void InternalUpdate()
            {
                if (IsDone || _operation == null) return;
                _operation.UpdateOperation();
                Progress = _operation.Progress;
                if (!_operation.IsDone) return;
                if (_operation.Status != EOperationStatus.Succeed)
                {
                    Error = _operation.Error;
                    Status = EOperationStatus.Failed;
                    return;
                }
                Manifest = _operation.Manifest;
                if (Manifest.PackageName != _fs.PackageName || Manifest.PackageVersion != _version)
                {
                    Error = "Embedded manifest package/version mismatch.";
                    Status = EOperationStatus.Failed;
                    return;
                }
                foreach (var bundle in Manifest.BundleList)
                    if (!_fs.Exists(bundle)) _fs.RecordCatalogFile(bundle.BundleGUID, new FileWrapper(bundle.FileName));
                Status = EOperationStatus.Succeed;
            }
        }

        private sealed class LoadBundle : FSLoadBundleOperation
        {
            private readonly EmbeddedResourceFileSystem _fs;
            private readonly PackageBundle _bundle;
            private ResourceRequest _resourceRequest;
            private TextAsset _source;
            private AssetBundleCreateRequest _bundleRequest;
            internal LoadBundle(EmbeddedResourceFileSystem fs, PackageBundle bundle) { _fs = fs; _bundle = bundle; }
            internal override void InternalStart()
            {
                if (_bundle.Encrypted || _bundle.BundleType != (int)EBuildBundleType.AssetBundle)
                {
                    Error = "Embedded resources require unencrypted AssetBundles.";
                    Status = EOperationStatus.Failed;
                    return;
                }
                _resourceRequest = Resources.LoadAsync<TextAsset>(_fs.ResourcePath(_bundle.FileName));
            }
            internal override void InternalUpdate()
            {
                if (IsDone) return;
                try
                {
                    if (_bundleRequest == null)
                    {
                        if (!_resourceRequest.isDone) return;
                        _source = _resourceRequest.asset as TextAsset;
                        if (_source == null) throw new InvalidOperationException($"Missing embedded bundle: {_bundle.FileName}");
                        _bundleRequest = AssetBundle.LoadFromMemoryAsync(_source.bytes, _bundle.UnityCRC);
                    }
                    Progress = _bundleRequest.progress;
                    if (!_bundleRequest.isDone) return;
                    if (_bundleRequest.assetBundle == null) throw new InvalidOperationException($"Invalid embedded bundle: {_bundle.FileName}");
                    Result = new AssetBundleResult(_fs, _bundle, _bundleRequest.assetBundle, null);
                    ReleaseSource();
                    Status = EOperationStatus.Succeed;
                }
                catch (Exception e) { ReleaseSource(); Error = e.Message; Status = EOperationStatus.Failed; }
            }
            private void ReleaseSource()
            {
                if (_source != null) Resources.UnloadAsset(_source);
                _source = null;
                _resourceRequest = null;
            }
            // Do not block the WebGL event loop. Callers must use asynchronous loading.
            internal override void InternalWaitForAsyncComplete()
            {
                Error = "Embedded WebGL bundles require asynchronous loading.";
                Status = EOperationStatus.Failed;
                InternalAbort();
                YooLogger.Error(Error);
            }
            internal override void InternalAbort()
            {
                // Unity requests cannot be cancelled. Release their results when they finish.
                if (_bundleRequest != null)
                {
                    var request = _bundleRequest;
                    void Cleanup(AsyncOperation _)
                    {
                        if (request.assetBundle != null) request.assetBundle.Unload(true);
                        ReleaseSource();
                    }
                    if (request.isDone) Cleanup(request);
                    else request.completed += Cleanup;
                }
                else if (_resourceRequest != null)
                {
                    var request = _resourceRequest;
                    void Cleanup(AsyncOperation _)
                    {
                        _source = request.asset as TextAsset;
                        ReleaseSource();
                    }
                    if (request.isDone) Cleanup(request);
                    else request.completed += Cleanup;
                }
            }
        }
    }
}
