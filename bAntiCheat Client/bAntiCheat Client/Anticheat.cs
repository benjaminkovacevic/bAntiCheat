using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

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
                    dynamic whitelistedDirectoriesResponse = CheckWhitelistedDirectories(); // Add this line


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
                }
            }
            catch (Exception ex)
            {
                Form1.WriteLog(ex.ToString());
            }

            return clean;
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
            string gtaPath = GetGTADirectory();

            dynamic response = new ExpandoObject();
            response.passed = true;
            response.file = null;

            foreach (Forbiddenfile file in req.info.forbiddenFiles)
            {
                string filePath = Path.Combine(gtaPath, file.path);

                if (File.Exists(filePath))
                {
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

            foreach (Forbiddenprocess process in req.info.forbiddenProcesses)
            {
                foreach (Process p in Process.GetProcessesByName(process.name))
                {
                    response.passed = false;
                    response.process = process;
                    break;
                }
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
            foreach (Process p in Process.GetProcessesByName("gta_sa"))
            {
                if(p.MainModule.FileName != GetGTAPath())
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsGTARunning()
        {
            foreach(Process p in Process.GetProcessesByName("gta_sa"))
            {
                return true;
            }

            return false;
        }

        public bool IsSAMPRunning()
        {
            foreach (Process p in Process.GetProcessesByName("samp"))
            {
                return true;
            }

            return false;
        }
    }
}