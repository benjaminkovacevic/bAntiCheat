using MaterialSkin;
using MaterialSkin.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace bAntiCheat_Client
{
    public partial class Form1 : MaterialForm
    {
        private TcpClient socketConnection;
        private Thread clientReceiveThread;
        private Player p = new Player();
        private Anticheat AC;
        private static string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "bAntiCheat\\");

        public Form1()
        {
            InitializeComponent();

            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;
            materialSkinManager.ColorScheme = new ColorScheme(Primary.BlueGrey800, Primary.BlueGrey900, Primary.BlueGrey500, Accent.LightBlue200, TextShade.WHITE);

            if (!Directory.Exists(dataPath))
            {
                if (string.IsNullOrEmpty(Anticheat.GetGTAPath()))
                {
                    MessageBox.Show("Can't find the GTA installation directory. Please reinstall SAMP.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(-1);
                }

                Directory.CreateDirectory(dataPath);
            }

            // Always overwrite IP and port to these values
            string defaultIp = "code5lscnr.com";
            string defaultPort = "9014";
            File.WriteAllLines(Path.Combine(dataPath, "data.txt"), new string[] { defaultIp, defaultPort });

            string[] lines = File.ReadAllLines(Path.Combine(dataPath, "data.txt"));
            textBoxIp.Text = lines[0].Trim();
            textBoxPort.Text = lines[1].Trim();

            // Make IP text box read-only
            textBoxIp.ReadOnly = true;
            textBoxPort.ReadOnly = true;
            
            // Initially hide the join code until access is granted
            joinCodeLabel.Visible = false;
            
            // Set window size (Width, Height)
            this.Size = new System.Drawing.Size(450, 280);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void materialRaisedButton1_Click(object sender, EventArgs e)
        {
            try
            {
                File.WriteAllLines(Path.Combine(dataPath, "data.txt"), new string[] { textBoxIp.Text, textBoxPort.Text });
            }
            catch { }

            UpdateStatusLabel("Connecting to server...");

            clientReceiveThread = new Thread(new ThreadStart(ListenForData));
            clientReceiveThread.Start();

            ToggleConnectButton(false);
        }

        private void UpdateStatusLabel(string text, bool isAccessGranted = false)
        {
            MethodInvoker action = delegate { 
                statusLabel.Text = text; 
                statusLabel.Visible = true;
                
                // Set color and font based on message type
                if (isAccessGranted && text.Contains("Access granted"))
                {
                    statusLabel.ForeColor = System.Drawing.Color.Green;
                }
                else
                {
                    statusLabel.ForeColor = System.Drawing.Color.Black;
                }
                
                // Make text bold
                statusLabel.Font = new System.Drawing.Font(statusLabel.Font, System.Drawing.FontStyle.Bold);
            };
            statusLabel.BeginInvoke(action);
        }

        private void ToggleConnectButton(bool state)
        {
            MethodInvoker action = delegate { materialRaisedButton1.Enabled = state; };
            materialRaisedButton1.BeginInvoke(action);
        }

        private void UpdateUserInfoLabels(string name)
        {
            MethodInvoker action = delegate { labelPlayerName.Text = name; labelPlayerName.Visible = true; };
            labelPlayerName.BeginInvoke(action);

            action = delegate { label1.Visible = true; };
            label1.BeginInvoke(action);
        }

        private void UpdateJoinCodeLabel(string text)
        {
            MethodInvoker action = delegate { 
                // Only show join code when access is granted (text is not empty)
                if (!string.IsNullOrEmpty(text))
                {
                    joinCodeLabel.Text = text;
                    joinCodeLabel.Visible = true;
                }
                else
                {
                    joinCodeLabel.Text = "";
                    joinCodeLabel.Visible = false;
                }
                
                // Auto-copy to clipboard when join code is displayed
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        // Use Control.Invoke to ensure we're on the UI thread for clipboard
                        this.Invoke(new Action(() => {
                            Clipboard.SetText(text);
                        }));
                        UpdateStatusLabel($"Access granted. Code copied to clipboard.", true);
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"Failed to copy to clipboard: {ex.Message}");
                    }
                }
            };
            joinCodeLabel.BeginInvoke(action);
        }

        public static void WriteLog(string text)
        {
            try
            {
                if (!Directory.Exists(dataPath))
                    Directory.CreateDirectory(dataPath);
                    
                string logPath = Path.Combine(dataPath, "log.txt");
                File.AppendAllText(logPath, string.Format("\n---------------------[{0}] : {1}", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), text));
            }
            catch
            {
                // Silent fail for logging to prevent crashes
            }
        }

        private void SendMessage(string clientMessage)
        {
            if (socketConnection != null)
            {
                clientMessage = clientMessage.Trim();

                try
                {
                    NetworkStream stream = socketConnection.GetStream();
                    if (stream.CanWrite)
                    {
                        byte[] clientMessageAsByteArray = Encoding.ASCII.GetBytes(clientMessage);
                        stream.Write(clientMessageAsByteArray, 0, clientMessageAsByteArray.Length);
                        WriteLog("SENT: " + clientMessage);
                    }
                }
                catch (SocketException socketException)
                {
                    UpdateStatusLabel("Connection error occurred");
                    WriteLog("SendMessage error: " + socketException.ToString());
                }
                catch (Exception ex)
                {
                    WriteLog("SendMessage unexpected error: " + ex.ToString());
                }
            }
        }

        private void ListenForData()
        {
            try
            {
                socketConnection = new TcpClient(textBoxIp.Text, int.Parse(textBoxPort.Text));
                UpdateStatusLabel("Connected - Generating join code...");

                try
                {
                    p.generateNewJoinCode();
                    // Don't show join code yet - wait until access is granted
                    // UpdateJoinCodeLabel(p.joinCode.ToString());

                    string welcomeMsg = string.Format("CONNECTED:{0}|{1}|{2}", p.uniqueID, p.securityID, p.joinCode);
                    SendMessage(welcomeMsg);
                }
                catch (Exception ex)
                {
                    UpdateStatusLabel("Initialization failed");
                    WriteLog("Join code generation error: " + ex.ToString());
                    ToggleConnectButton(true);
                    return;
                }

                byte[] bytes = new byte[1024];

                while (socketConnection.Connected)
                {	
                    try
                    {
                        using (NetworkStream stream = socketConnection.GetStream())
                        {
                            int length;
                            while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                            {
                                var incommingData = new byte[length];
                                Array.Copy(bytes, 0, incommingData, 0, length);
                                string serverMessage = Encoding.ASCII.GetString(incommingData);
                                
                                WriteLog("RECEIVED: " + serverMessage);
                                Debug.WriteLine("RECEIVED: " + serverMessage);

                                if (serverMessage.Contains("CONNECTED"))
                                {
                                    UpdateStatusLabel("Validating game files...");

                                    string[] temp = serverMessage.Split('|');
                                    AC = new Anticheat(temp[1].Trim());

                                    if(!AC.CanConnect())
                                    {
                                        socketConnection.Close();
                                        UpdateStatusLabel("Authentication failed");
                                        UpdateJoinCodeLabel("");
                                        ToggleConnectButton(true);
                                        WriteLog("CanConnect() returned false - disconnecting");
                                    }
                                    else
                                    {
                                        // Show the join code when access is granted
                                        UpdateJoinCodeLabel(p.joinCode.ToString());
                                        WriteLog("CanConnect() passed - access granted");
                                    }
                                }
                                else if (serverMessage.Contains("WELCOME"))
                                {
                                    string[] temp = serverMessage.Split(':');
                                    UpdateStatusLabel("Connected to server");
                                    UpdateUserInfoLabels(temp[1]);
                                    WriteLog("Welcome message received - player: " + temp[1]);
                                }
                                else if (serverMessage.StartsWith("PING"))
                                {
                                    WriteLog("Received PING - starting validation checks");
                                    string pongMsg = string.Empty;

                                    try
                                    {
                                        // Add detailed logging for each check
                                        WriteLog($"PING Check 1: AC null check - AC: {(AC == null ? "null" : "OK")}, req: {(AC?.req == null ? "null" : "OK")}, info: {(AC?.req?.info == null ? "null" : "OK")}");
                                        
                                        if (AC?.req?.info == null)
                                        {
                                            pongMsg = string.Format("DROP:{0}", p.uniqueID);
                                            WriteLog("PING FAILED: AC.req.info is null");
                                        }
                                        else
                                        {
                                            WriteLog($"PING Check 2: GTA Running required: {AC.req.info.gtaRunning}");
                                            if (AC.req.info.gtaRunning)
                                            {
                                                bool gtaLegit = AC.IsRunningGTALegit();
                                                WriteLog($"PING Check 2 Result: GTA Legit: {gtaLegit}");
                                                if (!gtaLegit)
                                                {
                                                    pongMsg = string.Format("DROP:{0}", p.uniqueID);
                                                    WriteLog("PING FAILED: GTA not running legitimately");
                                                }
                                            }
                                            
                                            if (string.IsNullOrEmpty(pongMsg))
                                            {
                                                WriteLog($"PING Check 3: SAMP Running required: {AC.req.info.sampRunning}");
                                                if (AC.req.info.sampRunning)
                                                {
                                                    bool sampRunning = AC.IsSAMPRunning();
                                                    WriteLog($"PING Check 3 Result: SAMP Running: {sampRunning}");
                                                    if (!sampRunning)
                                                    {
                                                        pongMsg = string.Format("DROP:{0}", p.uniqueID);
                                                        WriteLog("PING FAILED: SAMP not running");
                                                    }
                                                }
                                            }
                                            
                                            if (string.IsNullOrEmpty(pongMsg))
                                            {
                                                WriteLog($"PING Check 4: Monitor processes constantly: {AC.req.info.monitorProcessesConstantly}");
                                                if (AC.req.info.monitorProcessesConstantly)
                                                {
                                                    bool processesClean = AC.ProcessesClean();
                                                    WriteLog($"PING Check 4 Result: Processes Clean: {processesClean}");
                                                    if (!processesClean)
                                                    {
                                                        pongMsg = string.Format("DROP:{0}", p.uniqueID);
                                                        WriteLog("PING FAILED: Forbidden processes detected");
                                                    }
                                                }
                                            }
                                            if (string.IsNullOrEmpty(pongMsg))
                                            {
                                                WriteLog("PING Check 5: AC.CanConnect()");
                                                if (!AC.CanConnect())
                                                {
                                                    pongMsg = string.Format("DROP:{0}", p.uniqueID);
                                                    WriteLog("PING FAILED: AC.CanConnect() returned false");
                                                }
                                            }
                                            if (string.IsNullOrEmpty(pongMsg))
                                            {
                                                pongMsg = string.Format("PONG:{0}", p.uniqueID);
                                                WriteLog("PING PASSED: All checks OK - sending PONG");
                                            }
                                        }

                                        WriteLog("Sending response: " + pongMsg);
                                        SendMessage(pongMsg);
                                    }
                                    catch (Exception ex)
                                    {
                                        WriteLog("PING ERROR: " + ex.ToString());
                                        pongMsg = string.Format("DROP:{0}", p.uniqueID);
                                        WriteLog("Sending DROP due to error: " + pongMsg);
                                        SendMessage(pongMsg);
                                    }
                                }
                                else if (serverMessage == "DSCN")
                                {
                                    WriteLog("Received DSCN - server disconnecting client");
                                    socketConnection.Close();
                                    UpdateStatusLabel("Disconnected by server");

                                    MethodInvoker action = delegate { labelPlayerName.Visible = false; };
                                    labelPlayerName.BeginInvoke(action);

                                    action = delegate { label1.Visible = false; };
                                    label1.BeginInvoke(action);

                                    UpdateJoinCodeLabel("");
                                    ToggleConnectButton(true);
                                    break; // Exit the inner while loop
                                }
                                else if(serverMessage == "WRONG_SEC_CODE")
                                {
                                    WriteLog("Received WRONG_SEC_CODE - client needs update");
                                    socketConnection.Close();
                                    UpdateStatusLabel("Version mismatch. Please update.");
                                    // Show update URL to user
                                    MessageBox.Show("Download the latest version at:\nhttps://code5lscnr.com/anticheat.php", "Update Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    UpdateJoinCodeLabel("");
                                    ToggleConnectButton(true);
                                    break; // Exit the inner while loop
                                }
                            }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        WriteLog("Socket disposed - connection closed");
                        break; // Expected when connection is closed
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Network error: " + ex.ToString());
                        UpdateStatusLabel("Connection lost");
                        ToggleConnectButton(true);
                        break;
                    }
                }
            }
            catch (SocketException socketException)
            {
                UpdateStatusLabel("Connection failed");
                ToggleConnectButton(true);
                WriteLog("Socket connection error: " + socketException.ToString());
            }
            finally
            {
                if (socketConnection != null && socketConnection.Connected)
                {
                    socketConnection.Close();
                }
            }
        }

        private void materialRaisedButton2_Click(object sender, EventArgs e)
        {
            MessageBox.Show("code5lscnr.com/anticheat.php\n\nVersion 2.1", "About", MessageBoxButtons.OK);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (socketConnection != null && socketConnection.Connected)
            {
                socketConnection.Close();
            }
            Environment.Exit(-1);
        }
    }
}