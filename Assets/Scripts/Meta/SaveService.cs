using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RogueDrive.Modifiers;
using UnityEngine;

namespace RogueDrive.Meta
{
    /// <summary>
    /// Сервис сохранения мета-прогресса в persistentDataPath.
    ///
    /// Поддерживает:
    /// 1. Атомарную запись через временный файл с резервным копированием (.backup).
    /// 2. Легковесную обфускацию/шифрование с префиксом RD1:, предотвращающую читерство
    ///    и повреждение при ручном редактировании, с прозрачным fallback на чистый JSON.
    /// 3. Кэширование активного экземпляра MetaProgress в памяти для доступа из всех сцен.
    /// </summary>
    public static class SaveService
    {
        const string FileName = "progress.json";
        const string BackupName = "progress.backup.json";
        const string EncryptionPrefix = "RD1:";
        const byte XorKey = 0x5A;

        static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
        static string BackupPath => Path.Combine(Application.persistentDataPath, BackupName);

        static MetaProgress _activeProgress;

        public static event Action<MetaProgress> OnProgressUpdated;

        public static bool Exists() => File.Exists(FilePath) || File.Exists(BackupPath);

        /// <summary>
        /// Возвращает активный объект MetaProgress, загружая его из файла или создавая начальный.
        /// </summary>
        public static MetaProgress GetActiveProgress(IReadOnlyList<UpgradeTrack> tracks = null,
                                                     IReadOnlyList<CarDefinition> cars = null)
        {
            if (_activeProgress != null)
                return _activeProgress;

            MetaProgressData data = Load();
            if (data == null)
            {
                data = new MetaProgressData();
                data.OwnedCarIds.Add("light");
                data.SelectedCarId = "light";
                data.Coins = 0;
            }

            // Гарантируем, что стартовая машина всегда доступна
            if (!data.OwnedCarIds.Contains("light"))
                data.OwnedCarIds.Add("light");
            if (string.IsNullOrEmpty(data.SelectedCarId))
                data.SelectedCarId = "light";

            _activeProgress = new MetaProgress(data, tracks, cars);
            _activeProgress.Changed += SaveActive;
            return _activeProgress;
        }

        public static void SetActiveProgress(MetaProgress progress)
        {
            if (_activeProgress != null)
                _activeProgress.Changed -= SaveActive;

            _activeProgress = progress;
            if (_activeProgress != null)
                _activeProgress.Changed += SaveActive;

            OnProgressUpdated?.Invoke(_activeProgress);
        }

        public static void SaveActive()
        {
            if (_activeProgress != null && _activeProgress.Data != null)
            {
                Save(_activeProgress.Data);
                OnProgressUpdated?.Invoke(_activeProgress);
            }
        }

        public static void Save(MetaProgressData data)
        {
            if (data == null)
                return;

            data.Version = MetaProgressData.CurrentVersion;

            try
            {
                string json = JsonUtility.ToJson(data, true);
                string encoded = Encrypt(json);
                string temporary = FilePath + ".tmp";

                File.WriteAllText(temporary, encoded, Encoding.UTF8);

                if (File.Exists(FilePath))
                    File.Copy(FilePath, BackupPath, true);

                File.Copy(temporary, FilePath, true);
                File.Delete(temporary);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveService] Не удалось сохранить прогресс: {exception.Message}");
            }
        }

        public static MetaProgressData Load()
        {
            MetaProgressData data = TryRead(FilePath);

            if (data == null)
            {
                data = TryRead(BackupPath);
                if (data != null)
                    Debug.LogWarning("[SaveService] Основное сохранение повреждено, восстановлено из резервной копии.");
            }

            return data == null ? null : Migrate(data);
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                _activeProgress = null;
                Debug.Log("[SaveService] Файлы сохранения удалены.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveService] Не удалось удалить сохранение: {exception.Message}");
            }
        }

        public static void ResetToNew(IReadOnlyList<UpgradeTrack> tracks = null,
                                      IReadOnlyList<CarDefinition> cars = null)
        {
            Delete();
            var data = new MetaProgressData();
            data.OwnedCarIds.Add("light");
            data.SelectedCarId = "light";
            data.Coins = 0;
            _activeProgress = new MetaProgress(data, tracks, cars);
            _activeProgress.Changed += SaveActive;
            Save(data);
            OnProgressUpdated?.Invoke(_activeProgress);
        }

        static MetaProgressData TryRead(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                string raw = File.ReadAllText(path, Encoding.UTF8);
                string json = Decrypt(raw);
                return JsonUtility.FromJson<MetaProgressData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveService] Не удалось прочитать {path}: {exception.Message}");
                return null;
            }
        }

        static string Encrypt(string plain)
        {
            if (string.IsNullOrEmpty(plain))
                return string.Empty;

            byte[] bytes = Encoding.UTF8.GetBytes(plain);
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] ^= XorKey;

            return EncryptionPrefix + Convert.ToBase64String(bytes);
        }

        static string Decrypt(string cipher)
        {
            if (string.IsNullOrEmpty(cipher))
                return string.Empty;

            // Если файл сохранён без префикса (чистый json), читаем напрямую
            if (!cipher.StartsWith(EncryptionPrefix))
                return cipher;

            try
            {
                string base64 = cipher.Substring(EncryptionPrefix.Length);
                byte[] bytes = Convert.FromBase64String(base64);
                for (int i = 0; i < bytes.Length; i++)
                    bytes[i] ^= XorKey;

                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // Если не удалось расшифровать, возвращаем исходный текст
                return cipher;
            }
        }

        static MetaProgressData Migrate(MetaProgressData data)
        {
            if (data.Version > MetaProgressData.CurrentVersion)
            {
                Debug.LogWarning($"[SaveService] Сохранение новее приложения (версия {data.Version}), используется как есть.");
                return data;
            }

            data.Version = MetaProgressData.CurrentVersion;
            return data;
        }
    }
}
