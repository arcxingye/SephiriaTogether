using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeathenEngineering.SteamworksIntegration;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SephiriaTogether
{
    internal sealed class ManagedSaveEntry
    {
        internal string Id;
        internal string Slot;
        internal string MainPath;
        internal string TmpPath;
        internal DateTime Timestamp;
        internal bool IsBackup;
        internal bool IsManagedBackup;
        internal bool CanResumeDungeon;
    }

    internal static class SaveManagement
    {
        private const string BackupDirectoryName = "SephiriaTogetherBackups";
        private static readonly List<ManagedSaveEntry> Entries = new List<ManagedSaveEntry>();

        internal static bool IsBusy { get; private set; }
        internal static string Status { get; private set; } = "";
        internal static IReadOnlyList<ManagedSaveEntry> Saves => Entries;
        internal static string SaveDirectory => SaveData.CommonPath;
        internal static string BackupDirectory => Path.Combine(SaveDirectory, BackupDirectoryName);

        internal static string SelectedSlot
        {
            get
            {
                if (!string.IsNullOrEmpty(SaveManager.Binded)) return NormalizeSlot(SaveManager.Binded);
                return OptionsBinding.Instance?.Options?.GetString("SelectedProfile", SaveManager.defaultSlotName) ??
                       SaveManager.defaultSlotName;
            }
        }

        internal static void Refresh()
        {
            Entries.Clear();
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                foreach (string main in Directory.GetFiles(SaveDirectory, "SLOT*.sav", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileNameWithoutExtension(main);
                    if (!IsActiveSlotName(name)) continue;
                    Add(name, main, Path.Combine(SaveDirectory, name + "TMP.sav"), File.GetLastWriteTime(main),
                        isBackup: false, isManaged: false);
                }

                foreach (string main in Directory.GetFiles(SaveDirectory, "SLOT*backup_*.sav", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileNameWithoutExtension(main);
                    int marker = name.IndexOf("backup_", StringComparison.OrdinalIgnoreCase);
                    if (marker <= 0 || name.Substring(0, marker).EndsWith("TMP", StringComparison.OrdinalIgnoreCase)) continue;
                    string slot = name.Substring(0, marker);
                    string suffix = name.Substring(marker + "backup_".Length);
                    string tmp = Path.Combine(SaveDirectory, slot + "TMPbackup_" + suffix + ".sav");
                    Add(slot + ":game:" + suffix, main, tmp, File.GetLastWriteTime(main),
                        isBackup: true, isManaged: false, slot: slot);
                }

                if (Directory.Exists(BackupDirectory))
                    foreach (string directory in Directory.GetDirectories(BackupDirectory, "*", SearchOption.AllDirectories))
                    {
                        string main = Directory.GetFiles(directory, "SLOT*.sav", SearchOption.TopDirectoryOnly)
                            .FirstOrDefault(path => IsActiveSlotName(Path.GetFileNameWithoutExtension(path)));
                        if (main == null) continue;
                        string slot = Path.GetFileNameWithoutExtension(main);
                        Add(slot + ":managed:" + Path.GetFileName(directory), main,
                            Path.Combine(directory, slot + "TMP.sav"), Directory.GetLastWriteTime(directory),
                            isBackup: true, isManaged: true, slot: slot);
                    }

                Entries.Sort((left, right) => right.Timestamp.CompareTo(left.Timestamp));
                Status = string.Format(MenuText.Get("SaveManagerFound"), Entries.Count);
            }
            catch (Exception exception)
            {
                Status = string.Format(MenuText.Get("SaveManagerError"), exception.Message);
                Plugin.LogInfo("Save manager refresh failed: " + exception);
            }
        }

        internal static void BackupCurrent()
        {
            if (IsBusy || Plugin.InstanceForPatches == null) return;
            Plugin.InstanceForPatches.StartCoroutine(BackupCurrentCoroutine());
        }

        internal static void Activate(ManagedSaveEntry entry)
        {
            if (IsBusy || entry == null || Plugin.InstanceForPatches == null) return;
            Plugin.InstanceForPatches.StartCoroutine(ActivateCoroutine(entry));
        }

        private static IEnumerator BackupCurrentCoroutine()
        {
            IsBusy = true;
            Status = MenuText.Get("SaveManagerSaving");
            string slot = SelectedSlot;
            if (SaveManager.Current != null && SaveManager.CurrentRun != null)
            {
                if (SaveManager.IsSaving == SaveManager.ESaveState.None)
                    SaveManager.Save(saveCurrent: true, saveCurrentRun: true);
                yield return WaitForSaveIdle(15f);
                if (SaveManager.IsSaving != SaveManager.ESaveState.None)
                {
                    Status = MenuText.Get("SaveManagerSwitchTimeout");
                    IsBusy = false;
                    yield break;
                }
            }
            try
            {
                string directory = CreateIndependentBackup(slot, "Manual");
                Plugin.LogInfo($"Independent save backup created: slot={slot}, path={directory}.");
                IsBusy = false;
                Refresh();
                Status = string.Format(MenuText.Get("SaveManagerBackupCreated"), Path.GetFileName(directory));
                yield break;
            }
            catch (Exception exception)
            {
                Status = string.Format(MenuText.Get("SaveManagerError"), exception.Message);
                Plugin.LogInfo("Independent save backup failed: " + exception);
            }
            IsBusy = false;
        }

        private static IEnumerator ActivateCoroutine(ManagedSaveEntry entry)
        {
            IsBusy = true;
            Status = MenuText.Get("SaveManagerSwitching");
            CoopMenu.Close();
            AutoPilot.SetEnabled(false);

            if (SaveManager.Current != null && SaveManager.CurrentRun != null)
            {
                if (SaveManager.IsSaving == SaveManager.ESaveState.None)
                    SaveManager.Save(saveCurrent: true, saveCurrentRun: true);
                yield return WaitForSaveIdle(15f);
                if (SaveManager.IsSaving != SaveManager.ESaveState.None)
                {
                    Status = MenuText.Get("SaveManagerSwitchTimeout");
                    IsBusy = false;
                    yield break;
                }
            }

            HorayNetworkManager manager = NetworkManager.singleton as HorayNetworkManager;
            if (manager != null && (NetworkServer.active || NetworkClient.active))
            {
                manager.MarkAsShuttingDown();
                manager.requestSelfLeave = true;
                if (NetworkServer.active) manager.StopHost();
                else manager.StopClient();
                GameObject steamManager = SingletonObject.Find("SteamManager");
                if (steamManager != null && steamManager.TryGetComponent(out LobbyManager lobby) && lobby.HasLobby)
                    lobby.Leave();
                manager.GoToTitleScene();
            }
            else if (SceneManager.GetActiveScene().name != "Title")
            {
                SceneManager.LoadScene("Title");
            }

            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline &&
                   (SceneManager.GetActiveScene().name != "Title" || NetworkServer.active || NetworkClient.active ||
                    SaveManager.IsSaving != SaveManager.ESaveState.None))
                yield return null;

            if (SceneManager.GetActiveScene().name != "Title" || NetworkServer.active || NetworkClient.active ||
                SaveManager.IsSaving != SaveManager.ESaveState.None)
            {
                Status = MenuText.Get("SaveManagerSwitchTimeout");
                IsBusy = false;
                yield break;
            }

            try
            {
                string activeMain = Path.Combine(SaveDirectory, entry.Slot + ".sav");
                if (File.Exists(activeMain))
                    CreateIndependentBackup(entry.Slot, "BeforeRestore");
                else
                    Plugin.LogInfo($"No active save to snapshot before restore: slot={entry.Slot}.");
                RestoreEntry(entry);
                OptionsBinding.Instance.Options.SetString("SelectedProfile", entry.Slot);
                OptionsBinding.Instance.Save();
                SaveManager.Release();
                Plugin.LogInfo($"Save restored for immediate use: source={entry.MainPath}, slot={entry.Slot}.");
            }
            catch (Exception exception)
            {
                Status = string.Format(MenuText.Get("SaveManagerError"), exception.Message);
                Plugin.LogInfo("Save restore failed: " + exception);
                IsBusy = false;
                yield break;
            }

            UI_TitleLobby title = null;
            deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline && title == null)
            {
                title = Resources.FindObjectsOfTypeAll<UI_TitleLobby>()
                    .FirstOrDefault(candidate => candidate != null && candidate.gameObject.activeInHierarchy);
                if (title == null) yield return null;
            }
            if (title == null)
            {
                Status = MenuText.Get("SaveManagerTitleUnavailable");
                IsBusy = false;
                yield break;
            }

            Status = MenuText.Get("SaveManagerLoading");
            IsBusy = false;
            title.NetworkHost();
        }

        private static IEnumerator WaitForSaveIdle(float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (SaveManager.IsSaving != SaveManager.ESaveState.None && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static string CreateIndependentBackup(string slot, string reason)
        {
            slot = NormalizeSlot(slot);
            string main = Path.Combine(SaveDirectory, slot + ".sav");
            if (!File.Exists(main)) throw new FileNotFoundException("Active save not found", main);
            string directory = Path.Combine(BackupDirectory, slot,
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + reason);
            Directory.CreateDirectory(directory);
            File.Copy(main, Path.Combine(directory, slot + ".sav"), overwrite: true);
            string tmp = Path.Combine(SaveDirectory, slot + "TMP.sav");
            if (File.Exists(tmp)) File.Copy(tmp, Path.Combine(directory, slot + "TMP.sav"), overwrite: true);
            File.WriteAllText(Path.Combine(directory, "info.txt"),
                $"Slot={slot}{Environment.NewLine}Created={DateTime.Now:O}{Environment.NewLine}Reason={reason}");
            return directory;
        }

        private static void RestoreEntry(ManagedSaveEntry entry)
        {
            if (!File.Exists(entry.MainPath)) throw new FileNotFoundException("Backup save not found", entry.MainPath);
            Directory.CreateDirectory(SaveDirectory);
            AtomicCopy(entry.MainPath, Path.Combine(SaveDirectory, entry.Slot + ".sav"));
            string targetTmp = Path.Combine(SaveDirectory, entry.Slot + "TMP.sav");
            if (!string.IsNullOrEmpty(entry.TmpPath) && File.Exists(entry.TmpPath))
                AtomicCopy(entry.TmpPath, targetTmp);
            else
            {
                SaveData freshRun = new SaveData(useEncryption: true, ".sav", 1) { enableCloudSave = false };
                freshRun.CreateNew(entry.Slot + "TMP");
            }
        }

        private static void AtomicCopy(string source, string target)
        {
            string temporary = target + ".strestore";
            File.Copy(source, temporary, overwrite: true);
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
        }

        private static void Add(string id, string main, string tmp, DateTime timestamp, bool isBackup,
            bool isManaged, string slot = null)
        {
            Entries.Add(new ManagedSaveEntry
            {
                Id = id,
                Slot = NormalizeSlot(slot ?? Path.GetFileNameWithoutExtension(main)),
                MainPath = main,
                TmpPath = File.Exists(tmp) ? tmp : null,
                Timestamp = timestamp,
                IsBackup = isBackup,
                IsManagedBackup = isManaged,
                CanResumeDungeon = CanResumeDungeon(tmp)
            });
        }

        private static bool CanResumeDungeon(string tmpPath)
        {
            if (string.IsNullOrEmpty(tmpPath) || !File.Exists(tmpPath)) return false;
            try
            {
                SaveData run = new SaveData(useEncryption: true, ".sav", 1) { enableCloudSave = false };
                return run.LoadFromString(File.ReadAllText(tmpPath)) && run.GetBool("RunStarted", fallback: false);
            }
            catch (Exception exception)
            {
                Plugin.LogInfo($"Could not inspect dungeon state in save {tmpPath}: {exception.Message}");
                return false;
            }
        }

        private static bool IsActiveSlotName(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("SLOT", StringComparison.OrdinalIgnoreCase)) return false;
            return value.Length > 4 && value.Skip(4).All(char.IsDigit);
        }

        private static string NormalizeSlot(string value)
        {
            if (string.IsNullOrEmpty(value)) return SaveManager.defaultSlotName;
            string name = Path.GetFileNameWithoutExtension(value);
            int tmp = name.IndexOf("TMP", StringComparison.OrdinalIgnoreCase);
            if (tmp > 0) name = name.Substring(0, tmp);
            int backup = name.IndexOf("backup_", StringComparison.OrdinalIgnoreCase);
            if (backup > 0) name = name.Substring(0, backup);
            return name;
        }
    }
}
