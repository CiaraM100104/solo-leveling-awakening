using System;
using System.IO;
using UnityEngine;

namespace SLA.Core
{
    /// <summary>
    /// Reads and writes the save file. Saving raises <see cref="_collect"/> with an empty <see cref="SaveData"/>
    /// that each system fills in; loading raises <see cref="_apply"/> so each system restores its part.
    /// When to save is the game state manager's job (only at state exits, never mid-gate).
    /// </summary>
    public class SaveService : MonoBehaviour
    {
        [SerializeField] private string _fileName = "save.json";

        [Header("Channels")]
        [SerializeField] private VoidEventChannel _saveRequested;
        [Tooltip("Raised with an empty save for every system to fill.")]
        [SerializeField] private SaveDataChannel _collect;
        [Tooltip("Raised with a loaded save for every system to restore from.")]
        [SerializeField] private SaveDataChannel _apply;
        [Tooltip("1 when a save file exists, 0 when not. For the title screen's Continue button.")]
        [SerializeField] private IntEventChannel _saveExists;

        /// <summary>File name inside persistentDataPath. Tests point this at a throwaway file.</summary>
        public string FileName
        {
            get => _fileName;
            set => _fileName = value;
        }

        public string FilePath => Path.Combine(Application.persistentDataPath, _fileName);

        public bool HasSave => File.Exists(FilePath);

        private void OnEnable()
        {
            _saveRequested.Raised += Save;
        }

        private void OnDisable()
        {
            _saveRequested.Raised -= Save;
        }

        private void Start()
        {
            _saveExists.Raise(HasSave ? 1 : 0);
        }

        public void Save()
        {
            var data = new SaveData();
            _collect.Raise(data);
            string json = SaveCodec.ToJson(data);
            string temp = FilePath + ".tmp";
            try
            {
                // Write a temporary file first so a crash mid-write never leaves a half-written save.
                File.WriteAllText(temp, json);
                File.Copy(temp, FilePath, true);
                File.Delete(temp);
                _saveExists.Raise(1);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[SLA] Could not save to {FilePath}: {e.Message}");
            }
        }

        /// <summary>Loads and applies the save. False if there is none or it can't be read.</summary>
        public bool Load()
        {
            if (!HasSave)
            {
                return false;
            }
            string json;
            try
            {
                json = File.ReadAllText(FilePath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[SLA] Could not read {FilePath}: {e.Message}");
                return false;
            }

            if (!SaveCodec.TryFromJson(json, out var data, out string error))
            {
                Debug.LogError($"[SLA] Save not loaded: {error}");
                return false;
            }
            _apply.Raise(data);
            return true;
        }

        public void DeleteSave()
        {
            if (HasSave)
            {
                File.Delete(FilePath);
            }
            _saveExists.Raise(0);
        }
    }
}
