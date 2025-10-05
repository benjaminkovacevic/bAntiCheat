using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq; // Add this at the top if not present
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

namespace bAntiCheat_Client
{
    class Anticheat
    {
        public Request req;
        private string schemaUrl { get; set; }

        public Anticheat(string schemaUrl)
        {
            this.schemaUrl = schemaUrl;
        }

        public bool ProcessesClean()
        {
            dynamic forbiddenProcessesResponse = CheckForbiddenProcesses();

            if (forbiddenProcessesResponse.passed == false)
            {
                ThreadPool.QueueUserWorkItem(delegate { // prevents not getting drop if player doesn't click on the message
                    MessageBox.Show("Forbidden process detected. Please kill it and click connect again" +
                    "\n\nProcess: " + forbiddenProcessesResponse.process.name + ".exe", "Alert", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                });

                return false;
            }

            return true;
        }
        
public bool CanConnect()
        {
            req = new Request(schemaUrl);
            bool clean = true;

            try
            {
                if (req.info != null)
                {
                    dynamic validateFilesResponse = ValidateFiles();
                    dynamic forbiddenFilesResponse = CheckForbiddenFiles();
                    dynamic forbiddenDirectoriesResponse = CheckForbiddenDirectories();
                    dynamic forbiddenProcessesResponse = CheckForbiddenProcesses();
                    dynamic forbiddenChecksumsResponse = CheckForbiddenChecksums();
                    dynamic whitelistedDirectoriesResponse = CheckWhitelistedDirectories();
                    dynamic asiCheck = CheckWhitelistedAsiFiles();

                    if (validateFilesResponse.passed == false)
                    {
                        if (validateFilesResponse.file.action == "PREVENT_CONNECT")
                        {
                            MessageBox.Show("Changed gamefiles detected. Please use the original ones." +
                            "\n\nFile: " + validateFilesResponse.file.path +
                            "\nReason: " + validateFilesResponse.reason, "Alert", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                            clean = false;
                        }
                    }

                    if (forbiddenDirectoriesResponse.passed == false)
                    {
                        if (forbiddenDirectoriesResponse.directory.action == "PREVENT_CONNECT")
                        {
                            MessageBox.Show("Forbidden directory detected. Please delete it and click connect again" +
                            "\n\nDirectory: " + forbiddenDirectoriesResponse.directory.path, "Alert", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                            clean = false;
                        }
                    }

                    if (forbiddenFilesResponse.passed == false)
                    {
                        if (forbiddenFilesResponse.file.action == "PREVENT_CONNECT")
                        {
                            MessageBox.Show("Forbidden file detected. Please delete it and click connect again" +
                            "\n\nFile: " + forbiddenFilesResponse.file.path, "Alert", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                            clean = false;
                        }
                    }

                    if (forbiddenProcessesResponse.passed == false)
                    {
                        if (forbiddenProcessesResponse.process.action == "PREVENT_CONNECT")
                        {
                            MessageBox.Show("Forbidden process detected. Please kill it and click connect again" +
                            "\n\nProcess: " + forbiddenProcessesResponse.process.name + ".exe", "Alert", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                            clean = false;
                        }
                    }

                    if (forbiddenChecksumsResponse.passed == false)
                    {
                        if (forbiddenChecksumsResponse.checksum.action == "PREVENT_CONNECT")
                        {
                            MessageBox.Show("Forbidden file detected. Please delete it and click connect again" +
                            "\n\nFile: " + forbiddenChecksumsResponse.filePath +
                            "\nDescription: " + forbiddenChecksumsResponse.checksum.description, "Alert", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                            clean = false;
                        }
                    }

                    if (whitelistedDirectoriesResponse.passed == false)
                    {
                        if (whitelistedDirectoriesResponse.directory.action == "PREVENT_CONNECT")
                        {
                            MessageBox.Show("Non-whitelisted file detected in cleo directory. Please remove it and click connect again" +
                            "\n\nFile: " + whitelistedDirectoriesResponse.invalidFile +
                            "\nDirectory: " + whitelistedDirectoriesResponse.directory.path, "Alert", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                            clean = false;
                        }
                    }

                    if (asiCheck.passed == false)
                    {
                        MessageBox.Show(
                            "Non-whitelisted ASI file detected!\n\nFile: " + asiCheck.asiFile + "\nPath: " + asiCheck.filePath,
                            "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        clean = false;
                    }
                }
                else
                {
                    Form1.WriteLog("ERROR: req.info je null! JSON nije učitan ili je neispravan.");
                    MessageBox.Show(
                        "Anticheat config nije učitan ili je neispravan. Konekcija nije dozvoljena.",
                        "Anticheat", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    clean = false;
                }
            }
            catch (Exception ex)
            {
                Form1.WriteLog(ex.ToString());
                clean = false;
            }

            return clean;
        }

public object CheckWhitelistedAsiFiles()
{
    
    string gtaPath = GetGTADirectory();
    dynamic response = new ExpandoObject();
    response.passed = true;
    response.asiFile = null;
    response.filePath = null;
    
    // LOG: Provera whitelist-e
            Form1.WriteLog("ASI whitelist null? " + (req.info.whitelistedAsiFiles == null));
    Form1.WriteLog("ASI whitelist length: " + (req.info.whitelistedAsiFiles == null ? "null" : req.info.whitelistedAsiFiles.Length.ToString()));
    if (req.info.whitelistedAsiFiles != null)
    {
        foreach (var w in req.info.whitelistedAsiFiles)
            Form1.WriteLog("Whitelist ASI: " + w.filename + " | " + w.hash);
    }

    // Ako nema whitelist-e, svi .asi fajlovi su zabranjeni
    if (req.info.whitelistedAsiFiles == null || req.info.whitelistedAsiFiles.Length == 0)
    {
        string[] asiFiles = Directory.GetFiles(gtaPath, "*.asi", SearchOption.TopDirectoryOnly);
        foreach (var file in asiFiles)
            Form1.WriteLog("Found ASI (no whitelist): " + Path.GetFileName(file));
        if (asiFiles.Length > 0)
        {
            response.passed = false;
            response.asiFile = Path.GetFileName(asiFiles[0]);
            response.filePath = asiFiles[0];
        }
        return response;
    }

    // Proveri svaki .asi fajl u GTA folderu
    string[] allAsiFiles = Directory.GetFiles(gtaPath, "*.asi", SearchOption.TopDirectoryOnly);
    foreach (string filePath in allAsiFiles)
    {
        string fileName = Path.GetFileName(filePath);
        string fileHash = GetChecksum(filePath).ToUpperInvariant();
        Form1.WriteLog("Found ASI: " + fileName + " | " + fileHash);

        // Da li je na whitelist-i?
        bool found = req.info.whitelistedAsiFiles.Any(w =>
            w.filename.Equals(fileName, StringComparison.OrdinalIgnoreCase) &&
            w.hash.Equals(fileHash, StringComparison.OrdinalIgnoreCase)
        );

        Form1.WriteLog("Is whitelisted: " + found);

        if (!found)
        {
            response.passed = false;
            response.asiFile = fileName;
            response.filePath = filePath;
            return response;
        }
    }

    return response;
}

        public static string GetChecksum(string file)
        {
            using (FileStream stream = File.OpenRead(file))
            {
                var sha = new SHA256Managed();
                byte[] checksum = sha.ComputeHash(stream);
                stream.Close();
                return BitConverter.ToString(checksum).Replace("-", String.Empty);
            }
        }

        public object ValidateFiles()
        {
            string gtaPath = GetGTADirectory();

            dynamic response = new ExpandoObject();
            response.passed = true;
            response.file = null;

            foreach (Validationfile file in req.info.validationFiles)
            {
                string filePath = Path.Combine(gtaPath, file.path);

                if (!File.Exists(filePath))
                {
                    response.passed = false;
                    response.file = file;
                    response.reason = "file doesn't exist";
                    break;
                }
                else
                {
                    string checksum = GetChecksum(filePath);
                    if (checksum != file.hash.ToUpper())
                    {
                        response.passed = false;
                        response.file = file;
                        response.reason = "checksum differs from original";
                        break;
                    }
                }
            }

            return response;
        }

        public object CheckForbiddenFiles()
        {
            dynamic response = new ExpandoObject();
            response.passed = true;
            response.file = null;

            // Provera da li je req, req.info ili req.info.forbiddenFiles null
            if (req == null || req.info == null || req.info.forbiddenFiles == null)
            {
                Form1.WriteLog("ERROR: req, req.info ili req.info.forbiddenFiles je null u CheckForbiddenFiles!");
                response.passed = false;
                response.error = "req, req.info ili req.info.forbiddenFiles je null";
                return response;
            }

            string gtaPath = GetGTADirectory();

            foreach (Forbiddenfile file in req.info.forbiddenFiles)
            {
                string filePath = Path.Combine(gtaPath, file.path);

                if (File.Exists(filePath))
                {
                    Form1.WriteLog("Forbidden file detected: " + filePath);
                    response.passed = false;
                    response.file = file;
                    break;
                }
            }

            return response;
        }

        public object CheckWhitelistedDirectories()
        {
            string gtaPath = GetGTADirectory();
            dynamic response = new ExpandoObject();
            response.passed = true;
            response.directory = null;
            response.invalidFile = null;

            if (req.info.whitelistedDirectories == null)
                return response;

            foreach (Whitelisteddirectory directory in req.info.whitelistedDirectories)
            {
                string directoryPath = Path.Combine(gtaPath, directory.path);

                if (Directory.Exists(directoryPath))
                {
                    // Get all files in the whitelisted directory
                    string[] allFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);

                    foreach (string filePath in allFiles)
                    {
                        string fileName = Path.GetFileName(filePath);
                        bool fileAllowed = false;

                        // Check if this file is in the allowed list
                        foreach (Allowedfile allowedFile in directory.allowedFiles)
                        {
                            if (fileName.Equals(allowedFile.filename, StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    string fileChecksum = GetChecksum(filePath);
                                    if (fileChecksum.Equals(allowedFile.hash, StringComparison.OrdinalIgnoreCase))
                                    {
                                        fileAllowed = true;
                                        break; // File is whitelisted and checksum matches
                                    }
                                }
                                catch
                                {
                                    // Skip files that can't be read
                                    continue;
                                }
                            }
                        }

                        // If file is not allowed, fail the check
                        if (!fileAllowed)
                        {
                            response.passed = false;
                            response.directory = directory;
                            response.invalidFile = filePath;
                            return response;
                        }
                    }
                }
            }

            return response;
        }

        public object CheckForbiddenProcesses()
        {
            dynamic response = new ExpandoObject();
            response.passed = true;
            response.process = null;

            try
            {
                foreach (Forbiddenprocess process in req.info.forbiddenProcesses)
                {
                    Process[] processes = Process.GetProcessesByName(process.name);
                    
                    if (processes.Length > 0)
                    {
                        Form1.WriteLog($"Forbidden process detected: {process.name}");
                        response.passed = false;
                        response.process = process;
                        
                        // Dispose all process objects
                        foreach (Process p in processes)
                        {
                            p?.Dispose();
                        }
                        
                        break;
                    }
                    
                    // Dispose process objects even if none found
                    foreach (Process p in processes)
                    {
                        p?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Form1.WriteLog($"Error in CheckForbiddenProcesses: {ex.Message}");
                // Return passed = true to avoid false positives on errors
            }

            return response;
        }

        public object CheckForbiddenDirectories()
        {
            string gtaPath = GetGTADirectory();

            dynamic response = new ExpandoObject();
            response.passed = true;
            response.directory = null;

            foreach (Forbiddenndirectory directory in req.info.forbiddenDirectories)
            {
                string directoryPath = Path.Combine(gtaPath, directory.path);

                if (Directory.Exists(directoryPath))
                {
                    response.passed = false;
                    response.directory = directory;
                    break;
                }
            }

            return response;
        }

        public object CheckForbiddenChecksums()
        {
            string gtaPath = GetGTADirectory();
            dynamic response = new ExpandoObject();
            response.passed = true;
            response.checksum = null;
            response.filePath = null;

            const long maxFileSize = 5 * 1024 * 1024; // 5MB in bytes

            foreach (Forbiddenchecksum checksumItem in req.info.forbiddenChecksums)
            {
                // Search through all files in GTA directory and subdirectories
                string[] allFiles = Directory.GetFiles(gtaPath, "*.*", SearchOption.AllDirectories);
                
                foreach (string filePath in allFiles)
                {
                    try
                    {
                        // Check file size before calculating checksum
                        FileInfo fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length > maxFileSize)
                        {
                            continue; // Skip files larger than 5MB
                        }

                        string fileChecksum = GetChecksum(filePath);
                        if (fileChecksum.Equals(checksumItem.hash, StringComparison.OrdinalIgnoreCase))
                        {
                            response.passed = false;
                            response.checksum = checksumItem;
                            response.filePath = filePath;
                            return response; // Exit immediately when found
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read (locked, permissions, etc.)
                        continue;
                    }
                }
            }

            return response;
        }

        public static string GetGTADirectory()
        {
            try
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\SAMP"))
                {
                    return registryKey.GetValue("gta_sa_exe").ToString().Trim().Replace("\\gta_sa.exe", "");
                }
            }
            catch
            {
                return null;
            }
        }

        public static string GetGTAPath()
        {
            try
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\SAMP"))
                {
                    return registryKey.GetValue("gta_sa_exe").ToString().Trim();
                }
            }
            catch
            {
                return null;
            }
        }

        public bool IsRunningGTALegit()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("gta_sa");
                
                if (processes.Length == 0)
                {
                    Form1.WriteLog("GTA process not found");
                    return false; // GTA nije pokrenut
                }

                string gtaPath = GetGTAPath();
                if (string.IsNullOrEmpty(gtaPath))
                {
                    Form1.WriteLog("Cannot get GTA path from registry");
                    return false;
                }

                foreach (Process process in processes)
                {
                    try
                    {
                        string processPath = process.MainModule.FileName;
                        bool isLegit = processPath.Equals(gtaPath, StringComparison.OrdinalIgnoreCase);
                        
                        Form1.WriteLog($"GTA process found: {processPath}");
                        Form1.WriteLog($"Expected path: {gtaPath}");
                        Form1.WriteLog($"Process legitimate: {isLegit}");
                        
                        return isLegit;
                    }
                    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5) // Access Denied
                    {
                        // Cannot access process - likely running with higher privileges
                        Form1.WriteLog($"Cannot verify GTA process path (Access Denied) - Process ID: {process.Id}. Assuming legitimate.");
                        return true; // Assume legitimate if we can't verify due to permissions
                    }
                    catch (InvalidOperationException ex)
                    {
                        // Process has exited
                        Form1.WriteLog($"GTA process has exited: {ex.Message}");
                        continue; // Try next process
                    }
                    catch (Exception ex)
                    {
                        Form1.WriteLog($"Error checking GTA process: {ex.Message}");
                        continue; // Try next process if multiple exist
                    }
                    finally
                    {
                        // Dispose process object to prevent handle leaks
                        process?.Dispose();
                    }
                }
                
                // If we get here, no valid processes were found
                Form1.WriteLog("No valid GTA processes found after checking all");
                return false;
            }
            catch (Exception ex)
            {
                Form1.WriteLog($"Error in IsRunningGTALegit: {ex.Message}");
                // In case of any unexpected error, allow connection to avoid false positives
                return true;
            }
        }

        public bool IsGTARunning()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("gta_sa");
                bool isRunning = processes.Length > 0;
                
                Form1.WriteLog($"GTA Running check: {isRunning} ({processes.Length} processes found)");
                
                // Dispose all process objects
                foreach (Process p in processes)
                {
                    p?.Dispose();
                }
                
                return isRunning;
            }
            catch (Exception ex)
            {
                Form1.WriteLog($"Error in IsGTARunning: {ex.Message}");
                return false;
            }
        }

        public bool IsSAMPRunning()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("samp");
                bool isRunning = processes.Length > 0;
                
                Form1.WriteLog($"SAMP Running check: {isRunning} ({processes.Length} processes found)");
                
                // Dispose all process objects
                foreach (Process p in processes)
                {
                    p?.Dispose();
                }
                
                return isRunning;
            }
            catch (Exception ex)
            {
                Form1.WriteLog($"Error in IsSAMPRunning: {ex.Message}");
                return false;
            }
        }
    }
}