using System;
using System.Linq;
using MapleLib.MapleCryptoLib;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using MapleLib.Helpers;

namespace MapleLib.Configuration
{
    public sealed class EncryptionKey : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _iv = "00 00 00 00";
        private string _aesUserKey = "";
        private WzMutableKey _wzKey;
        private readonly object _keyLock = new();

        [JsonPropertyName("Name")]
        public string Name {
            get => _name;
            set => SetField(ref _name, value);
        }

        [JsonPropertyName("MapleVersion")]
        [JsonConverter(typeof(JsonStringEnumConverter<WzMapleVersion>))]
        public WzMapleVersion MapleVersion { get; set; } = WzMapleVersion.CUSTOM;

        [JsonPropertyName("Iv")]
        public string Iv {
            get => _iv;
            set {
                ArgumentNullException.ThrowIfNull(value);
                byte[] parsed = ByteUtils.HexToBytes(value);
                if (parsed.Length != 4)
                    throw new ArgumentException("IV must contain exactly 4 bytes.", nameof(value));
                if (string.Equals(_iv, value, StringComparison.Ordinal)) return;
                lock (_keyLock)
                {
                    _iv = value;
                    _wzKey = null; // force re-generate
                }
            }
        }

        [JsonPropertyName("AesUserKey")]
        public string AesUserKey {
            get => _aesUserKey;
            set {
                ArgumentNullException.ThrowIfNull(value);
                byte[] parsed = ByteUtils.HexToBytes(value);
                if (parsed.Length != 32)
                    throw new ArgumentException("AES user key must contain exactly 32 bytes.", nameof(value));
                if (string.Equals(_aesUserKey, value, StringComparison.Ordinal)) return;
                lock (_keyLock)
                {
                    _aesUserKey = value;
                    _wzKey = null; // force re-generate
                }
            }
        }

        [JsonIgnore]
        public WzMutableKey WzKey
        {
            get
            {
                lock (_keyLock)
                {
                    if (_wzKey != null)
                        return _wzKey;

                    byte[] iv = ByteUtils.HexToBytes(_iv);
                    byte[] bytes;
                    if (string.IsNullOrWhiteSpace(_aesUserKey))
                    {
                        byte[] defaultKey = MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT;
                        bytes = MapleCryptoConstants.GetTrimmedUserKey(ref defaultKey);
                    }
                    else
                    {
                        bytes = ByteUtils.HexToBytes(_aesUserKey);
                    }

                    var aesUserKey = new byte[MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.Length];
                    for (int i = 0; i < aesUserKey.Length; i += 4)
                        aesUserKey[i] = bytes[i / 4];

                    _wzKey = WzKeyGenerator.GenerateWzKey(iv, aesUserKey);
                    return _wzKey;
                }
            }
        }

        public override string ToString() {
            return _name;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
