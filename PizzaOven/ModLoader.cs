﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace PizzaOven
{
    public static class ModLoader
    {
        // Restore all backups created from previous build
        public static bool Restart()
        {
            // Restore all backups
            RestoreDirectory(Global.config.ModsFolder);
            // Delete all banks that aren't vanilla
            var banks = new List<string> (new string[] { "master.bank", "master.strings.bank", "music.bank", "sfx.bank" });
            foreach (var file in Directory.GetFiles($"{Global.config.ModsFolder}{Global.s}sound{Global.s}Desktop", "*", SearchOption.AllDirectories))
                if (!banks.Contains(Path.GetFileName(file).ToLowerInvariant()))
                    File.Delete(file);
            // Delete empty folders
            // Delete all dlls that aren't vanilla
            var dlls = new List<string>(new string[] { "fmod.dll", "fmod-gamemaker.dll", "fmodstudio.dll", "gameframe_x64.dll", "steam_api.dll",
            "steam_api64.dll", "steamworks_x64.dll"});
            foreach (var file in Directory.GetFiles($"{Global.config.ModsFolder}", "*", SearchOption.TopDirectoryOnly))
                if (Path.GetExtension(file).ToLowerInvariant() == ".dll" && !dlls.Contains(Path.GetFileName(file).ToLowerInvariant()))
                    File.Delete(file);
            foreach (var directory in Directory.GetDirectories($"{Global.config.ModsFolder}{Global.s}sound{Global.s}Desktop"))
                if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                    Directory.Delete(directory, false);
            // Delete .win from older version of Pizza Oven
            if (File.Exists($"{Global.config.ModsFolder}{Global.s}PizzaOven.win"))
                File.Delete($"{Global.config.ModsFolder}{Global.s}PizzaOven.win");
            return true;
        }
        // Copy over mod files in order of ModList
        public static bool Build(string mod)
        {
            var errors = 0;
            var successes = 0;
            var FilesToPatch = Directory.GetFiles($"{Global.config.ModsFolder}{Global.s}sound{Global.s}Desktop").ToList();
            FilesToPatch.Insert(0, $"{Global.config.ModsFolder}{Global.s}data.win");
            FilesToPatch.Insert(1, $"{Global.config.ModsFolder}{Global.s}PizzaTower.exe");
            var xdelta = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}xdelta.exe";
            if (!File.Exists(xdelta))
            {

                Global.logger.WriteLine($"{xdelta} is not found. Please try redownloading Pizza Oven", LoggerType.Error);
                return false;
            }
            foreach (var modFile in Directory.GetFiles(mod, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(modFile);
                // xdelta patches
                if (extension.Equals(".xdelta", StringComparison.InvariantCultureIgnoreCase))
                {
                    var success = false;
                    foreach (var file in FilesToPatch)
                    {
                        if (!File.Exists(file))
                        {
                            Global.logger.WriteLine($"{file} does not exist", LoggerType.Error);
                            continue;
                        }
                        try
                        {
                            // Attempt to patch file
                            Global.logger.WriteLine($"Attempting to patch {file} with {modFile}...", LoggerType.Info);
                            Patch(file, modFile, $"{Path.GetDirectoryName(file)}{Global.s}temp", xdelta);
                            // Only make backup if it doesn't already exist
                            if (!File.Exists($"{file}.po"))
                                File.Copy(file, $"{file}.po", true);
                            File.Move($"{Path.GetDirectoryName(file)}{Global.s}temp", file, true);
                            Global.logger.WriteLine($"Applied {modFile} to {file}.", LoggerType.Info);
                            successes++;
                        }
                        catch (Exception e)
                        {
                            Global.logger.WriteLine($"Unable to patch {file} with {modFile}", LoggerType.Warning);
                            continue;
                        }
                        // Stop trying to patch if it was successful
                        success = true;
                        break;
                    }
                    if (!success)
                    {
                        Global.logger.WriteLine($"{modFile} wasn't able to patch any file. Ensure that either the mod xdelta patch or your game version is up to date", LoggerType.Error);
                        errors++;
                    }
                }
                // Language .txt files
                else if (extension.Equals(".txt", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Verify .txt file is for language
                    if (File.ReadAllText(modFile).Contains("lang = ", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Copy over file to lang folder
                        File.Copy(modFile, $"{Global.config.ModsFolder}{Global.s}lang{Global.s}{Path.GetFileName(modFile)}", true);
                        Global.logger.WriteLine($"Copied over {modFile} to language folder", LoggerType.Info);
                        successes++;
                    }
                }
                // Font .png files
                else if (extension.Equals(".png", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Check if png is in fonts folder
                    if (modFile.Contains("fonts", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Create fonts folder
                        Directory.CreateDirectory($"{Global.config.ModsFolder}{Global.s}lang{Global.s}fonts");
                        // Copy over file to fonts folder
                        File.Copy(modFile, $"{Global.config.ModsFolder}{Global.s}lang{Global.s}fonts{Global.s}{Path.GetFileName(modFile)}", true);
                        Global.logger.WriteLine($"Copied over {modFile} to fonts folder", LoggerType.Info);
                        successes++;
                    }
                }
                // Copy over .win file in case modder provides entire file instead of .xdelta patch
                else if (extension.Equals(".win", StringComparison.InvariantCultureIgnoreCase))
                {
                    var dataWin = $"{Global.config.ModsFolder}{Global.s}data.win";
                    // Only make backup if it doesn't already exist
                    if (!File.Exists($"{dataWin}.po"))
                        File.Copy(dataWin, $"{dataWin}.po", true);
                    File.Copy(modFile, dataWin, true);
                    Global.logger.WriteLine($"Copied over {modFile} to use instead of data.win", LoggerType.Info);
                    successes++;
                }
                // Copy over .bank file in case modder provides entire file instead of .xdelta patch
                else if (extension.Equals(".bank", StringComparison.InvariantCultureIgnoreCase))
                {
                    var FileToReplace = $"{Global.config.ModsFolder}{Global.s}sound{Global.s}Desktop{Global.s}{Path.GetFileName(modFile)}";
                    if (File.Exists(FileToReplace))
                    {
                        // Only make backup if it doesn't already exist
                        if (!File.Exists($"{FileToReplace}.po"))
                            File.Copy(FileToReplace, $"{FileToReplace}.po", true);
                        File.Copy(modFile, FileToReplace, true);
                        Global.logger.WriteLine($"Copied over {modFile} to use in sound folder", LoggerType.Info);
                    }
                    // Copy the file over if its not vanilla
                    else
                    {
                        var FileToAdd = $"{Global.config.ModsFolder}{Global.s}sound{Global.s}Desktop{Global.s}{Path.GetFileName(modFile)}";
                        // Add subdirectory name if it's not the same name as the mod folder
                        if (!Path.GetFileName(Path.GetDirectoryName(modFile)).Equals(Path.GetFileName(mod), StringComparison.InvariantCultureIgnoreCase))
                            FileToAdd = $"{Global.config.ModsFolder}{Global.s}sound{Global.s}Desktop{Global.s}{Path.GetFileName(Path.GetDirectoryName(modFile))}{Global.s}{Path.GetFileName(modFile)}";
                        Directory.CreateDirectory(Path.GetDirectoryName(FileToAdd));
                        File.Copy(modFile, FileToAdd, true);

                    }
                    successes++;
                }
                // Extension .dll files
                else if (extension.Equals(".dll", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Copy over file to game folder
                    File.Copy(modFile, $"{Global.config.ModsFolder}{Global.s}{Path.GetFileName(modFile)}", true);
                    Global.logger.WriteLine($"Copied over {modFile} to game folder", LoggerType.Info);
                    successes++;
                }
                // Video Files
                else if (extension.Equals(".mp4", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Copy over file to game folder
                    File.Copy(modFile, $"{Global.config.ModsFolder}{Global.s}{Path.GetFileName(modFile)}", true);
                    Global.logger.WriteLine($"Copied over {modFile} to game folder", LoggerType.Info);
                    successes++;
                }
            }
            if (successes == 0)
                Global.logger.WriteLine($"No file was used from the current mod", LoggerType.Error);
            return errors == 0 && successes > 0;
        }

        private static void Patch(string file, string patch, string output, string xdelta)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = xdelta;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.WorkingDirectory = Path.GetDirectoryName(xdelta);
            startInfo.Arguments = $@"-d -s ""{file}"" ""{patch}"" ""{output}""";
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
        }
        private static void RestoreDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    if (File.Exists($"{file}.po"))
                        File.Move($"{file}.po", file, true);
            }
        }
    }
}
