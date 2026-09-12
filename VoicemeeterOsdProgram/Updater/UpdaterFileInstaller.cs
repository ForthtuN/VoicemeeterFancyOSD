using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VoicemeeterOsdProgram.Updater;

internal static class UpdaterFileInstaller
{
    internal static bool TryInstall(
        string sourceFolder,
        string destinationFolder,
        string backupFolderName,
        IProgress<double> progress,
        Action<Exception> onError = null)
    {
        string stageFolder = Path.Combine(destinationFolder, $".{Guid.NewGuid():N}.update-stage");
        string backupFolder = Path.Combine(destinationFolder, backupFolderName);

        try
        {
            DirectoryInfo source = new(sourceFolder);
            if (!source.Exists) return false;

            CopyTree(sourceFolder, stageFolder);

            if (Directory.Exists(backupFolder))
            {
                Directory.Delete(backupFolder, true);
            }

            var stagedFiles = Directory.GetFiles(stageFolder, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var stagedDirectories = Directory.GetDirectories(stageFolder, "*", SearchOption.AllDirectories)
                .OrderBy(path => path.Length)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            ulong totalSize = (ulong)stagedFiles.Sum(path => new FileInfo(path).Length);
            ulong installedSize = 0;
            List<string> installedTargets = new();
            List<string> createdDirectories = new();
            List<(string Backup, string Target)> backups = new();

            try
            {
                foreach (string stagedDirectory in stagedDirectories)
                {
                    string relativePath = Path.GetRelativePath(stageFolder, stagedDirectory);
                    string targetDirectory = Path.Combine(destinationFolder, relativePath);
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                        createdDirectories.Add(targetDirectory);
                    }
                }

                foreach (string stagedFile in stagedFiles)
                {
                    string relativePath = Path.GetRelativePath(stageFolder, stagedFile);
                    string targetPath = Path.Combine(destinationFolder, relativePath);
                    string targetDirectory = Path.GetDirectoryName(targetPath);
                    Directory.CreateDirectory(targetDirectory);

                    if (File.Exists(targetPath))
                    {
                        string backupPath = Path.Combine(backupFolder, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                        File.Move(targetPath, backupPath, true);
                        backups.Add((backupPath, targetPath));
                    }

                    File.Move(stagedFile, targetPath);
                    installedTargets.Add(targetPath);

                    installedSize += (ulong)new FileInfo(targetPath).Length;
                    if (totalSize != 0)
                    {
                        progress?.Report(installedSize * 100.0 / totalSize);
                    }
                }
            }
            catch
            {
                RollBack(installedTargets, backups, createdDirectories);
                throw;
            }

            return true;
        }
        catch (Exception e)
        {
            onError?.Invoke(e);
            return false;
        }
        finally
        {
            TryDeleteDirectory(stageFolder);
        }
    }

    private static void CopyTree(string sourceFolder, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);

        foreach (string directory in Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceFolder, directory);
            Directory.CreateDirectory(Path.Combine(destinationFolder, relativePath));
        }

        foreach (string file in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceFolder, file);
            string destinationPath = Path.Combine(destinationFolder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            File.Copy(file, destinationPath, true);
        }
    }

    private static void RollBack(
        IReadOnlyList<string> installedTargets,
        IReadOnlyList<(string Backup, string Target)> backups,
        IReadOnlyList<string> createdDirectories)
    {
        for (int i = installedTargets.Count - 1; i >= 0; i--)
        {
            try
            {
                if (File.Exists(installedTargets[i])) File.Delete(installedTargets[i]);
            }
            catch { }
        }

        for (int i = backups.Count - 1; i >= 0; i--)
        {
            try
            {
                var (backup, target) = backups[i];
                if (!File.Exists(backup)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Move(backup, target, true);
            }
            catch { }
        }

        for (int i = createdDirectories.Count - 1; i >= 0; i--)
        {
            try
            {
                if (Directory.Exists(createdDirectories[i]) &&
                    !Directory.EnumerateFileSystemEntries(createdDirectories[i]).Any())
                {
                    Directory.Delete(createdDirectories[i]);
                }
            }
            catch { }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch { }
    }
}
