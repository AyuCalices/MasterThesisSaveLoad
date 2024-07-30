using System;
using System.Collections.Generic;
using System.Text;
using SaveLoadSystem.Core.Component;
using SaveLoadSystem.Core.Serializable;
using SaveLoadSystem.Utility;
using UnityEngine;

namespace SaveLoadSystem.Core
{
    public enum StorageType
    {
        Binary,
        Json,
        XML
    }

    public enum EncryptionType
    {
        None,
        Aes
    }
    
    public enum CompressionType
    {
        None,
        Gzip
    }

    public enum IntegrityCheckType
    {
        None,
        Hashing,
        CRC32,
        Adler32
    }
    
    [CreateAssetMenu]
    public class SaveLoadManager : ScriptableObject, ISaveConfig
    {
        [Header("Version")] 
        [SerializeField] private int major;
        [SerializeField] private int minor;
        [SerializeField] private int patch;
        
        [Header("File Name")]
        [SerializeField] private string defaultFileName;
        [SerializeField] private string savePath;
        [SerializeField] private string extensionName;
        [SerializeField] private string metaDataExtensionName;
        
        [Header("Storage")]
        [SerializeField] private StorageType storageType;
        [SerializeField] private EncryptionType encryptionType;
        [SerializeField] private CompressionType compressionType;
        [SerializeField] private IntegrityCheckType integrityCheckType;
        
        [Header("Slot Settings")] 
        [SerializeField] private bool autoSaveOnSaveFocusSwap;
        
        public event Action<SaveFocus, SaveFocus> OnBeforeFocusChange;
        public event Action<SaveFocus, SaveFocus> OnAfterFocusChange;
        
        public string SavePath => savePath;
        public string ExtensionName => extensionName;
        public string MetaDataExtensionName => metaDataExtensionName;
        public SaveVersion SaveVersion => new(major, minor, patch);
        public HashSet<SaveSceneManager> TrackedSaveSceneManagers { get; } = new();
        public bool HasSaveFocus => _saveFocus != null;
        public SaveFocus SaveFocus
        {
            get
            {
                if (!HasSaveFocus)
                {
                    SetFocus();
                }

                return _saveFocus;
            }
        }

        private SaveFocus _saveFocus;
        private HashSet<SaveSceneManager> _scenesToReload;
        
        private static readonly byte[] DefaultAesKey = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");
        private byte[] _aesKey;
        private static readonly byte[] DefaultIvKey = Encoding.UTF8.GetBytes("abcdef9876543210");
        private byte[] _aesIv;

        #region Simple Save

        public string[] GetAllSaveFiles()
        {
            return SaveLoadUtility.FindAllSaveFiles(this);
        }
        
        public void SimpleSaveActiveScenes(string fileName = null)
        {
            SetFocus(fileName);

            var activeScenes = UnityUtility.GetActiveScenes();
            SaveFocus.SnapshotScenes(activeScenes);
            SaveFocus.WriteToDisk();
        }

        public void SimpleLoadActiveScenes(string fileName = null)
        {
            SetFocus(fileName);

            if (SaveFocus.IsPersistent)
            {
                var activeScenes = UnityUtility.GetActiveScenes();
                SaveFocus.LoadScenes(activeScenes);
            }
            else
            {
                Debug.LogWarning($"Couldn't load, because there is no save file with name '{fileName}.{ExtensionName}' at path '{SavePath}'");
            }
        }

        #endregion

        #region Focus Save

        public void SetFocus(string fileName = null)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = defaultFileName;
                Debug.LogWarning("Initialized the save system with the default file Name.");
            }
            
            if (HasSaveFocus)
            {
                //same to current save
                if (_saveFocus.FileName == fileName) return;
                
                //other save, but still has pending data that can be saved
                if (_saveFocus.HasPendingData && autoSaveOnSaveFocusSwap)
                {
                    _saveFocus.WriteToDisk();
                }
            }
            
            SwapFocus(new SaveFocus(this, fileName));
        }

        public void ReleaseFocus()
        {
            //save pending data if allowed
            if (HasSaveFocus && _saveFocus.HasPendingData && autoSaveOnSaveFocusSwap)
            {
                _saveFocus.WriteToDisk();
            }
            
            SwapFocus(null);
        }

        #endregion

        public ISerializeStrategy GetSerializeStrategy()
        {
            ISerializeStrategy strategy = GetSerializationStrategy();
            strategy = WrapWithCompression(strategy);
            strategy = WrapWithEncryption(strategy);
            return strategy;
        }
        
        public void SetAesEncryption(byte[] aesKey, byte[] aesIv)
        {
            _aesKey = aesKey;
            _aesIv = aesIv;
        }

        public void ClearAesEncryption()
        {
            _aesKey = null;
            _aesIv = null;
        }

        #region Internal

        internal void RegisterSaveSceneManager(SaveSceneManager saveSceneManager)
        {
            TrackedSaveSceneManagers.Add(saveSceneManager);
        }
        
        internal void UnregisterSaveSceneManager(SaveSceneManager saveSceneManager)
        {
            TrackedSaveSceneManagers.Remove(saveSceneManager);
        }

        #endregion

        #region Private

        private void SwapFocus(SaveFocus newSaveFocus)
        {
            SaveFocus oldSaveFocus = _saveFocus;
            
            OnBeforeFocusChange?.Invoke(oldSaveFocus, newSaveFocus);

            _saveFocus = newSaveFocus;
            
            OnAfterFocusChange?.Invoke(oldSaveFocus, newSaveFocus);
        }
        
        private ISerializeStrategy GetSerializationStrategy()
        {
            return storageType switch
            {
                StorageType.Binary => new BinarySerializeStrategy(),
                StorageType.Json => new JsonSerializeStrategy(),
                StorageType.XML => new XmlSerializeStrategy(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        private ISerializeStrategy WrapWithCompression(ISerializeStrategy strategy)
        {
            return compressionType switch
            {
                CompressionType.None => strategy,
                CompressionType.Gzip => new GzipCompressionSerializationStrategy(strategy),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private ISerializeStrategy WrapWithEncryption(ISerializeStrategy strategy)
        {
            switch (encryptionType)
            {
                case EncryptionType.None:
                    return strategy;
                
                case EncryptionType.Aes:
                    if (_aesKey == null || _aesIv == null)
                    {
                        return new AesEncryptSerializeStrategy(strategy, DefaultAesKey, DefaultIvKey);
                    }
                    return new AesEncryptSerializeStrategy(strategy, _aesKey, _aesIv);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion
    }
}
