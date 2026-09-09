using System;
using System.IO;
using UnityEngine;

namespace RogueDrive.Meta
{
    /// <summary>
    /// Сохранение мета-прогресса в JSON в каталоге persistentDataPath.
    ///
    /// Запись выполняется через временный файл с последующей заменой: обрыв записи
    /// не оставляет игрока с испорченным сохранением. Формат версионируется.
    /// </summary>
    public static class SaveService
    {
        const string FileName = "progress.json";
        const string BackupName = "progress.backup.json";

        static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
        static string BackupPath => Path.Combine(Application.persistentDataPath, BackupName);

        public static bool Exists() => File.Exists(FilePath);

        public static void Save(MetaProgressData data)
        {
            if (data == null)
                return;

            data.Version = MetaProgressData.CurrentVersion;

            try
            {
                string json = JsonUtility.ToJson(data, true);
                string temporary = FilePath + ".tmp";

                File.WriteAllText(temporary, json);

                if (File.Exists(FilePath))
                    File.Copy(FilePath, BackupPath, true);

                File.Copy(temporary, FilePath, true);
                File.Delete(temporary);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Не удалось сохранить прогресс: {exception.Message}");
            }
        }

        public static MetaProgressData Load()
        {
            MetaProgressData data = TryRead(FilePath);

            if (data == null)
            {
                data = TryRead(BackupPath);

                if (data != null)
                    Debug.LogWarning("Основное сохранение повреждено, восстановлено из резервной копии.");
            }

            return data == null ? null : Migrate(data);
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Не удалось удалить сохранение: {exception.Message}");
            }
        }

        static MetaProgressData TryRead(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<MetaProgressData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Не удалось прочитать {path}: {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// Приведение старых сохранений к текущему формату.
        /// Точка расширения: при добавлении версии сюда дописывается шаг миграции.
        /// </summary>
        static MetaProgressData Migrate(MetaProgressData data)
        {
            if (data.Version > MetaProgressData.CurrentVersion)
            {
                Debug.LogWarning(
                    $"Сохранение новее приложения (версия {data.Version}), используется как есть.");
                return data;
            }

            data.Version = MetaProgressData.CurrentVersion;
            return data;
        }
    }
}
