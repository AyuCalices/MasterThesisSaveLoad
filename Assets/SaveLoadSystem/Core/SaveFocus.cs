using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SaveLoadSystem.Core.Component;
using SaveLoadSystem.Core.DataTransferObject;
using SaveLoadSystem.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace SaveLoadSystem.Core
{
    public class SaveFocus
    {
        public string FileName { get; }
        public bool HasPendingData { get; private set; }
        public bool IsPersistent { get; private set; }

        private readonly SaveLoadManager _saveLoadManager;
        private readonly AsyncOperationQueue _asyncQueue;
        
        private JObject _customMetaData;
        private SaveMetaData _metaData;
        private SaveData _saveData;
        
        public event Action OnBeforeSnapshot;
        public event Action OnAfterSnapshot;
        public event Action OnBeforeDeleteFromDisk;
        public event Action OnAfterDeleteFromDisk;
        public event Action OnBeforeWriteToDisk;
        public event Action OnAfterWriteToDisk;
        public event Action OnBeforeLoad;
        public event Action OnAfterLoad;

        public SaveFocus(SaveLoadManager saveLoadManager, string fileName)
        {
            _asyncQueue = new AsyncOperationQueue();
            _saveLoadManager = saveLoadManager;
            FileName = fileName;

            Initialize();
        }

        private void Initialize()
        {
            _asyncQueue.Enqueue(async () =>
            {
                if (SaveLoadUtility.MetaDataExists(_saveLoadManager, FileName) && SaveLoadUtility.SaveDataExists(_saveLoadManager, FileName))
                {
                    _metaData = await SaveLoadUtility.ReadMetaDataAsync(_saveLoadManager, _saveLoadManager, FileName);
                    IsPersistent = true;
                    _customMetaData = _metaData.CustomData;
                }
                else
                {
                    _customMetaData = new();
                }
            });
        }

        public void SetCustomMetaData(string identifier, object data)
        {
            if (!data.GetType().IsSerializable())
            {
                Debug.LogWarning("You attempted to save data, that is not serializable!");
                return;
            }
            
            _asyncQueue.Enqueue(() =>
            {
                _customMetaData.Add(identifier, JToken.FromObject(data));
                return Task.CompletedTask;
            });
        }

        public T GetCustomMetaData<T>(string identifier)
        {
            if (_customMetaData?[identifier] == null)
            {
                return default;
            }
            
            return _customMetaData[identifier].ToObject<T>();
        }
        
        public void SnapshotActiveScenes()
        {
            SnapshotScenes(UnityUtility.GetActiveScenes());
        }

        public void SnapshotScenes(params Scene[] scenesToSnapshot)
        {
            _asyncQueue.Enqueue(() =>
            {
                _saveData ??= new SaveData();
                HasPendingData = true;
                InternalSnapshotActiveScenes(_saveData, scenesToSnapshot);
                
                return Task.CompletedTask;
            });
        }
        
        public void WriteToDisk()
        {
            _asyncQueue.Enqueue(async () =>
            {
                if (!HasPendingData)
                {
                    Debug.LogWarning("Aborted attempt to save without pending data!");
                    return;
                }
                
                _metaData = new SaveMetaData()
                {
                    SaveVersion = _saveLoadManager.SaveVersion,
                    ModificationDate = DateTime.Now,
                    CustomData = _customMetaData
                };

                OnBeforeWriteToDisk?.Invoke();
                await SaveLoadUtility.WriteDataAsync(_saveLoadManager, _saveLoadManager, FileName, _metaData, _saveData);

                IsPersistent = true;
                HasPendingData = false;
                OnAfterWriteToDisk?.Invoke();
                
                Debug.LogWarning("Save Completed!");
            });
        }
        
        public void SaveActiveScenes()
        {
            SnapshotActiveScenes();
            WriteToDisk();
        }

        public void SaveScenes(params Scene[] scenesToSave)
        {
            SnapshotScenes(scenesToSave);
            WriteToDisk();
        }

        public void ApplySnapshotToActiveScenes()
        {
            ApplySnapshotToScenes(UnityUtility.GetActiveScenes());
        }

        public void ApplySnapshotToScenes(params Scene[] scenesToApplySnapshot)
        {
            _asyncQueue.Enqueue(() =>
            {
                if (_saveData == null) return Task.CompletedTask;
                
                InternalLoadActiveScenes(_saveData, scenesToApplySnapshot);
                return Task.CompletedTask;
            });
        }
        
        public void LoadActiveScenes()
        {
            ReadFromDisk();
            ApplySnapshotToActiveScenes();
        }
        
        public void LoadScenes(params Scene[] scenesToLoad)
        {
            ReadFromDisk();
            ApplySnapshotToScenes(scenesToLoad);
        }

        public void ReadFromDisk()
        {
            _asyncQueue.Enqueue(async () =>
            {
                if (!SaveLoadUtility.SaveDataExists(_saveLoadManager, FileName) 
                    || !SaveLoadUtility.MetaDataExists(_saveLoadManager, FileName)) return;
                
                //only load saveData, if it is persistent and not initialized
                if (IsPersistent && _saveData == null)
                {
                    _saveData = await SaveLoadUtility.ReadSaveDataSecureAsync(_saveLoadManager, _saveLoadManager.SaveVersion, _saveLoadManager, FileName);
                }
            });
        }
        
        public void ReloadThenLoadActiveScenes()
        {
            ReloadThenLoadScenes(UnityUtility.GetActiveScenes());
        }
        
        /// <summary>
        /// data will be applied after awake
        /// </summary>
        /// <param name="scenesToLoad"></param>
        public void ReloadThenLoadScenes(params Scene[] scenesToLoad)
        {
            _asyncQueue.Enqueue(async () =>
            {
                if (!SaveLoadUtility.SaveDataExists(_saveLoadManager, FileName) 
                    || !SaveLoadUtility.MetaDataExists(_saveLoadManager, FileName)) return;
                
                //only load saveData, if it is persistent and not initialized
                if (IsPersistent && _saveData == null)
                {
                    _saveData = await SaveLoadUtility.ReadSaveDataSecureAsync(_saveLoadManager, _saveLoadManager.SaveVersion, _saveLoadManager, FileName);
                }

                //buffer save paths, because they will be null later on the scene array
                var savePaths = new string[scenesToLoad.Length];
                for (var index = 0; index < scenesToLoad.Length; index++)
                {
                    savePaths[index] = scenesToLoad[index].path;
                }

                //async reload scene and continue, when all are loaded
                var loadTasks = scenesToLoad.Select(scene => LoadSceneAsync(scene.path)).ToList();
                await Task.WhenAll(loadTasks);

                //remap save paths to the reloaded scenes
                List<Scene> matchingGuids = new();
                foreach (var scene in UnityUtility.GetActiveScenes())
                {
                    if (savePaths.Contains(scene.path))
                    {
                        matchingGuids.Add(scene);
                    }
                }
                
                InternalLoadActiveScenes(_saveData, matchingGuids.ToArray());
            });
        }
        
        public void WipeActiveSceneData()
        {
            WipeSceneData(UnityUtility.GetActiveScenes());
        }

        public void WipeSceneData(params Scene[] scenesToWipe)
        {
            _asyncQueue.Enqueue(() =>
            {
                if (_saveData == null) return Task.CompletedTask;

                foreach (var scene in scenesToWipe)
                {
                    _saveData.RemoveSceneData(scene);
                }
                HasPendingData = true;

                return Task.CompletedTask;
            });
        }
        
        public void DeleteSceneDataFromDisk(params Scene[] scenesToWipe)
        {
            foreach (var scene in scenesToWipe)
            {
                WipeSceneData(scene);
            }

            DeleteFromDisk();
        }
        
        public void DeleteFromDisk()
        {
            _asyncQueue.Enqueue(async () =>
            {
                OnBeforeDeleteFromDisk?.Invoke();

                await SaveLoadUtility.DeleteAsync(_saveLoadManager, FileName);

                IsPersistent = false;
                OnAfterDeleteFromDisk?.Invoke();
                
                Debug.LogWarning("Delete Completed!");
            });
        }

        #region Private Methods

        private void InternalSnapshotActiveScenes(SaveData saveData, params Scene[] scenesToSnapshot)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            
            OnBeforeSnapshot?.Invoke();

            foreach (var scene in scenesToSnapshot)
            {
                if (!scene.isLoaded)
                {
                    Debug.LogWarning($"Tried to snapshot the unloaded scene {scene.name}!");
                    continue;
                }

                if (Application.isPlaying)
                {
                    //SaveSceneManagers will register themself during runtime
                    foreach (var trackedSaveSceneManager in _saveLoadManager.TrackedSaveSceneManagers.
                                 Where(trackedSaveSceneManager => trackedSaveSceneManager.gameObject.scene == scene))
                    {
                        saveData.SetSceneData(scene, trackedSaveSceneManager.CreateSnapshot());
                    }
                }
                else
                {
                    //If editor mode, SaveSceneManagers must be searched
                    var saveSceneManager = UnityUtility.FindObjectOfTypeInScene<SaveSceneManager>(scene, true);
                    saveData.SetSceneData(scene, saveSceneManager.CreateSnapshot());
                }
            }

            OnAfterSnapshot?.Invoke();
            
            stopwatch.Stop();
            UnityEngine.Debug.LogWarning("Time taken: " + stopwatch.ElapsedMilliseconds + " ms");
            
            Debug.LogWarning("Snapshot Completed!");
        }

        private void InternalLoadActiveScenes(SaveData saveData, params Scene[] scenesToLoad)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            
            OnBeforeLoad?.Invoke();

            foreach (var scene in scenesToLoad)
            {
                if (!scene.isLoaded)
                {
                    Debug.LogWarning($"Tried to load a save into the unloaded scene {scene.name}!");
                    continue;
                }

                if (!saveData.TryGetSceneData(scene, out SceneDataContainer sceneDataContainer)) continue;
                
                if (Application.isPlaying)
                {
                    //SaveSceneManagers will register themself during runtime
                    foreach (var trackedSaveSceneManager in _saveLoadManager.TrackedSaveSceneManagers.Where(
                                 trackedSaveSceneManager => trackedSaveSceneManager.gameObject.scene == scene))
                    {
                        trackedSaveSceneManager.LoadSnapshot(sceneDataContainer);
                    }
                }
                else
                {
                    //If editor mode, SaveSceneManagers must be searched
                    var saveSceneManager = UnityUtility.FindObjectOfTypeInScene<SaveSceneManager>(scene, true);
                    saveSceneManager.LoadSnapshot(sceneDataContainer);
                }
            }

            OnAfterLoad?.Invoke();
            
            stopwatch.Stop();
            UnityEngine.Debug.LogWarning("Time taken: " + stopwatch.ElapsedMilliseconds + " ms");
            
            Debug.LogWarning("Loading Completed!");
        }
        
        private Task<AsyncOperation> LoadSceneAsync(string scenePath)
        {
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(scenePath);
            TaskCompletionSource<AsyncOperation> tcs = new TaskCompletionSource<AsyncOperation>();

            if (asyncOperation != null)
            {
                asyncOperation.allowSceneActivation = true;
                asyncOperation.completed += operation =>
                {
                    tcs.SetResult(operation);
                };
            }
            else
            {
                tcs.SetResult(null);
            }
            
            // Return the task
            return tcs.Task;
        }

        #endregion
    }

    public class AsyncOperationQueue
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly Queue<Func<Task>> _queue = new Queue<Func<Task>>();

        // Enqueue a task to be executed
        public void Enqueue(Func<Task> task)
        {
            _queue.Enqueue(task);
            ProcessQueue();
        }

        // Process the queue
        private async void ProcessQueue()
        {
            await _semaphore.WaitAsync();

            try
            {
                while (_queue.Count > 0)
                {
                    var task = _queue.Dequeue();
                    await task();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
