using MapleLib.WzLib;
using System;
using System.IO;

namespace MapleLib.Img
{
    /// <summary>
    /// Lightweight IMG filesystem entry used for UI tree views.
    /// Does not load/parse the underlying .img until explicitly resolved.
    /// </summary>
    public sealed class ImgFileWzImageReference : WzObject
    {
        private readonly VirtualWzDirectory _parentDir;
        private readonly string _fileName; // includes ".img"

        public ImgFileWzImageReference(VirtualWzDirectory parentDir, string fileName)
        {
            _parentDir = parentDir ?? throw new ArgumentNullException(nameof(parentDir));
            _fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        }

        public VirtualWzDirectory ParentDirectory => _parentDir;

        public string FileName => _fileName;

        public string RelativePath
        {
            get
            {
                if (string.IsNullOrEmpty(_parentDir.RelativePath))
                    return _fileName;
                return Path.Combine(_parentDir.RelativePath, _fileName);
            }
        }

        public override string Name
        {
            get => _fileName;
            set => throw new NotSupportedException();
        }

        public override WzObjectType ObjectType => WzObjectType.Image;

        public override WzObject Parent
        {
            get => _parentDir;
            internal set { }
        }

        public override WzFile WzFileParent => null;

        public override object WzValue => RelativePath;

        public override void Remove()
        {
            // Deletion is handled via ImgFileSystemManager.DeleteImage in UI code.
            throw new NotSupportedException();
        }

        public override void Dispose()
        {
            // Nothing to dispose; this is just a reference.
        }

        public WzImage Resolve()
        {
            var img = _parentDir.Manager.LoadImage(_parentDir.CategoryName, RelativePath);
            if (img != null)
            {
                img.Parent = _parentDir;
            }
            return img;
        }
    }
}

