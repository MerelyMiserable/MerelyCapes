using McCrypt;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace MerelyCapes
{
    public partial class MainForm : Form
    {
        private List<CapeDefinition> _capes = new List<CapeDefinition>();
        private CapeDefinition _selectedCape;
        private readonly string _configPath = "cape_studio_config.json";
        private readonly string _capesJsonPath = "capesv2.json";
        private readonly string _outputPath = "output";

        // Proxy components
        private ProxyServer _proxyServer;
        private ExplicitProxyEndPoint _explicitEndPoint;
        private bool _isProxyRunning = false;
        private readonly string _targetUrl = "https://store.mktpl.minecraft-services.net/api/v1.0/layout/pages/DressingRoom_Capes";
        private readonly string _playfabApiUrl = "https://20ca2.playfabapi.com/Catalog/GetPublishedItem";
        private readonly Dictionary<SessionEventArgs, string> _pendingPlayFabRequests = new Dictionary<SessionEventArgs, string>();
        private readonly Dictionary<string, string> _zipUuidToItemId = new Dictionary<string, string>();
        private readonly HashSet<string> _generatedZipUuids = new HashSet<string>();

        private Label _lblProxyStatus;
        private Button _btnToggleProxy;
        private RichTextBox _txtProxyLog;

        public MainForm()
        {
            InitializeComponent();
            LoadConfiguration();
            RefreshCapeList();
            UpdateProxyUI();
        }

        private void InitializeComponent()
        {
            this.Text = "MerelyCapes";
            this.Icon = new Icon("capes.ico");
            this.Size = new Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormClosing += MainForm_FormClosing;
            this.FormBorderStyle = FormBorderStyle.None; // For rounded edges
            this.BackColor = Color.FromArgb(15, 10, 25); // Deep purple-black

            // Enable rounded corners
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));

            // Custom title bar
            var titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = Color.FromArgb(25, 15, 40),
                Padding = new Padding(10, 0, 0, 0)
            };

            var lblTitle = new Label
            {
                Text = "⚡ MerelyCapes",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(138, 43, 226), // Electric purple
                Dock = DockStyle.Left,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 7, 0, 0)
            };

            var btnClose = new Button
            {
                Text = "✕",
                Dock = DockStyle.Right,
                Width = 45,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(25, 15, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Application.Exit();
            btnClose.MouseEnter += (s, e) => btnClose.BackColor = Color.FromArgb(138, 43, 226);
            btnClose.MouseLeave += (s, e) => btnClose.BackColor = Color.FromArgb(25, 15, 40);

            var btnMinimize = new Button
            {
                Text = "─",
                Dock = DockStyle.Right,
                Width = 45,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(25, 15, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            btnMinimize.MouseEnter += (s, e) => btnMinimize.BackColor = Color.FromArgb(80, 40, 120);
            btnMinimize.MouseLeave += (s, e) => btnMinimize.BackColor = Color.FromArgb(25, 15, 40);

            titleBar.Controls.AddRange(new Control[] { lblTitle, btnMinimize, btnClose });

            // Main layout - vertical split
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 550,
                BackColor = Color.FromArgb(138, 43, 226),
                SplitterWidth = 2
            };

            // Top panel - horizontal split for capes and editor
            var topSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 300,
                BackColor = Color.FromArgb(138, 43, 226),
                SplitterWidth = 2
            };

            // Left panel - Cape list
            var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 15, 35) };

            var lblCapes = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Color.FromArgb(30, 20, 50),
                ForeColor = Color.FromArgb(138, 43, 226)
            };

            var capeListBox = new ListBox
            {
                Name = "capeListBox",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                DisplayMember = "Name",
                BackColor = Color.FromArgb(20, 15, 35),
                ForeColor = Color.FromArgb(200, 180, 255),
                BorderStyle = BorderStyle.None
            };
            capeListBox.SelectedIndexChanged += CapeListBox_SelectedIndexChanged;

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5),
                BackColor = Color.FromArgb(25, 15, 40)
            };

            var btnAdd = CreateButton("Add Cape", 90);
            btnAdd.Click += BtnAdd_Click;
            var btnRemove = CreateButton("Remove", 90);
            btnRemove.Click += BtnRemove_Click;
            var btnGenerate = CreateButton("Generate All", 110);
            btnGenerate.Click += BtnGenerate_Click;

            buttonPanel.Controls.AddRange(new Control[] { btnAdd, btnRemove, btnGenerate });
            leftPanel.Controls.Add(capeListBox);
            leftPanel.Controls.Add(lblCapes);
            leftPanel.Controls.Add(buttonPanel);

            topSplit.Panel1.Controls.Add(leftPanel);

            // Right panel - Cape editor (SCROLLABLE)
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(20, 15, 35)
            };

            var editorPanel = new TableLayoutPanel
            {
                Location = new Point(0, 0),
                Width = rightPanel.Width - 25, // Account for scrollbar
                ColumnCount = 2,
                AutoSize = true,
                Padding = new Padding(20),
                BackColor = Color.FromArgb(20, 15, 35)
            };
            editorPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            editorPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Cape Name
            AddLabelTextBox(editorPanel, "Cape Name:", "txtName");

            // Description
            AddLabelTextBox(editorPanel, "Description:", "txtDescription");

            // Creator Name
            AddLabelTextBox(editorPanel, "Creator Name:", "txtCreator");

            // Thumbnail URL
            AddLabelTextBox(editorPanel, "Thumbnail URL:", "txtThumbnail");

            // Rarity
            var lblRarity = CreateLabel("Rarity:");
            var cboRarity = new ComboBox
            {
                Name = "cboRarity",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200,
                BackColor = Color.FromArgb(35, 25, 55),
                ForeColor = Color.FromArgb(200, 180, 255),
                FlatStyle = FlatStyle.Flat
            };
            cboRarity.Items.AddRange(new[] { "common", "rare", "epic", "legendary" });
            cboRarity.SelectedIndex = 1;
            editorPanel.Controls.Add(lblRarity);
            editorPanel.Controls.Add(cboRarity);

            // Cape Image
            var lblImage = CreateLabel("Cape Image:");
            var imagePanel = new Panel { Height = 350, Width = 500, BackColor = Color.FromArgb(15, 10, 25) };
            var picBox = new PictureBox
            {
                Name = "picCape",
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 10, 25)
            };
            var btnSelectImage = CreateButton("Select Image (64x32)", 0);
            btnSelectImage.Dock = DockStyle.Bottom;
            btnSelectImage.Height = 35;
            btnSelectImage.Click += BtnSelectImage_Click;
            imagePanel.Controls.Add(picBox);
            imagePanel.Controls.Add(btnSelectImage);
            editorPanel.Controls.Add(lblImage);
            editorPanel.Controls.Add(imagePanel);

            // IDs Display (read-only)
            AddLabelTextBox(editorPanel, "Item ID:", "txtItemId", true);
            AddLabelTextBox(editorPanel, "Piece UUID:", "txtPieceUuid", true);

            // Save button
            var btnSave = CreateButton("Save Cape", 150);
            btnSave.Height = 40;
            btnSave.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnSave.Margin = new Padding(0, 20, 0, 0);
            btnSave.Click += BtnSave_Click;
            editorPanel.Controls.Add(new Label());
            editorPanel.Controls.Add(btnSave);

            rightPanel.Controls.Add(editorPanel);
            topSplit.Panel2.Controls.Add(rightPanel);

            mainSplit.Panel1.Controls.Add(topSplit);

            // Bottom panel - Proxy controls and log (SCROLLABLE)
            var proxyPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 15, 35) };

            var proxyHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(30, 20, 50),
                Padding = new Padding(10)
            };

            var lblProxyTitle = new Label
            {
                Text = "⚡ Proxy Server",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(138, 43, 226),
                AutoSize = true,
                Location = new Point(10, 10)
            };

            _lblProxyStatus = new Label
            {
                Text = "Status: Stopped",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(255, 100, 100),
                AutoSize = true,
                Location = new Point(10, 40)
            };

            _btnToggleProxy = CreateButton("Start Proxy", 120);
            _btnToggleProxy.Location = new Point(proxyHeader.Width - 140, 25);
            _btnToggleProxy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnToggleProxy.Click += BtnToggleProxy_Click;

            proxyHeader.Controls.AddRange(new Control[] { lblProxyTitle, _lblProxyStatus, _btnToggleProxy });

            _txtProxyLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 10, 25),
                ForeColor = Color.FromArgb(180, 160, 220),
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };

            proxyPanel.Controls.Add(_txtProxyLog);
            proxyPanel.Controls.Add(proxyHeader);

            mainSplit.Panel2.Controls.Add(proxyPanel);

            // Menu bar
            var menuStrip = new MenuStrip
            {
                BackColor = Color.FromArgb(25, 15, 40),
                ForeColor = Color.FromArgb(200, 180, 255),
                Renderer = new CustomMenuRenderer()
            };

            var fileMenu = new ToolStripMenuItem("File");
            var exportMenu = new ToolStripMenuItem("Export capesv2.json");
            exportMenu.Click += ExportMenu_Click;
            var importMenu = new ToolStripMenuItem("Import Config");
            importMenu.Click += ImportMenu_Click;
            var exitMenu = new ToolStripMenuItem("Exit");
            exitMenu.Click += (s, e) => Application.Exit();
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { exportMenu, importMenu, new ToolStripSeparator(), exitMenu });

            var proxyMenu = new ToolStripMenuItem("Proxy");
            var proxySettingsMenu = new ToolStripMenuItem("Open Windows Proxy Settings");
            proxySettingsMenu.Click += ProxySettingsMenu_Click;
            var clearLogMenu = new ToolStripMenuItem("Clear Log");
            clearLogMenu.Click += (s, e) => _txtProxyLog.Clear();
            proxyMenu.DropDownItems.AddRange(new ToolStripItem[] { proxySettingsMenu, clearLogMenu });

            menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, proxyMenu });

            this.MainMenuStrip = menuStrip;
            this.Controls.Add(mainSplit);
            this.Controls.Add(titleBar);
            this.Controls.Add(menuStrip);

            this.BackColor = Color.FromArgb(15, 10, 25);
        }

        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
    int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private Button CreateButton(string text, int width)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 35,
                BackColor = Color.FromArgb(138, 43, 226), // Electric purple
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(160, 70, 255);
            btn.MouseLeave += (s, e) => btn.BackColor = Color.FromArgb(138, 43, 226);
            return btn;
        }


        private Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 30,
                ForeColor = Color.FromArgb(138, 43, 226)
            };
        }


        private void AddLabelTextBox(TableLayoutPanel panel, string labelText, string textBoxName, bool readOnly = false)
        {
            var lbl = CreateLabel(labelText);
            var txt = new TextBox
            {
                Name = textBoxName,
                Width = 400,
                ReadOnly = readOnly,
                BackColor = readOnly ? Color.FromArgb(25, 20, 40) : Color.FromArgb(35, 25, 55),
                ForeColor = Color.FromArgb(200, 180, 255),
                BorderStyle = BorderStyle.FixedSingle
            };
            panel.Controls.Add(lbl);
            panel.Controls.Add(txt);
        }


        #region Proxy Management

        private async void BtnToggleProxy_Click(object sender, EventArgs e)
        {
            if (_isProxyRunning)
            {
                StopProxy();
            }
            else
            {
                await StartProxy();
            }
        }

        private async Task StartProxy()
        {
            try
            {
                LogProxy("Starting proxy server...", Color.Cyan);

                _proxyServer = new ProxyServer();
                _proxyServer.CertificateManager.SaveFakeCertificates = true;
                _proxyServer.CertificateManager.CreateRootCertificate();

                await InstallRootCertificateAsync();

                _explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8080, true);
                _proxyServer.AddEndPoint(_explicitEndPoint);

                _proxyServer.BeforeRequest += OnProxyRequest;
                _proxyServer.BeforeResponse += OnProxyResponse;

                _proxyServer.Start();
                _isProxyRunning = true;

                UpdateProxyUI();

                LogProxy("✓ Proxy server started on port 8080", Color.LimeGreen);
                LogProxy($"✓ Intercepting: {_targetUrl}", Color.Yellow);
                LogProxy($"✓ Intercepting: {_playfabApiUrl}", Color.Yellow);
                LogProxy($"✓ Configured {_capes.Count} cape(s)", Color.Cyan);
                LogProxy("", Color.White);
                LogProxy("IMPORTANT: Set Windows proxy to 127.0.0.1:8080", Color.Orange);
                LogProxy("Go to: Settings > Network & Internet > Proxy", Color.Orange);
            }
            catch (Exception ex)
            {
                LogProxy($"✗ Error starting proxy: {ex.Message}", Color.Red);
                StopProxy();
            }
        }

        private void StopProxy()
        {
            try
            {
                if (_proxyServer != null)
                {
                    _proxyServer.BeforeRequest -= OnProxyRequest;
                    _proxyServer.BeforeResponse -= OnProxyResponse;
                    _proxyServer.Stop();
                    _proxyServer.Dispose();
                    _proxyServer = null;
                }

                _isProxyRunning = false;
                UpdateProxyUI();

                LogProxy("✓ Proxy server stopped", Color.Orange);
                LogProxy("Remember to disable proxy in Windows settings!", Color.Yellow);
            }
            catch (Exception ex)
            {
                LogProxy($"✗ Error stopping proxy: {ex.Message}", Color.Red);
            }
        }

        private async Task OnProxyRequest(object sender, SessionEventArgs e)
        {
            string requestUrl = e.HttpClient.Request.RequestUri.AbsoluteUri;

            if (requestUrl == _targetUrl)
            {
                LogProxy($"[DETECTED] Minecraft Capes URL", Color.Cyan);
            }
            else if (requestUrl == _playfabApiUrl)
            {
                LogProxy($"[DETECTED] PlayFab API Request", Color.Cyan);

                if (e.HttpClient.Request.Method == "POST" && e.HttpClient.Request.HasBody)
                {
                    try
                    {
                        string requestBody = await e.GetRequestBodyAsString();
                        if (!string.IsNullOrEmpty(requestBody))
                        {
                            var requestJson = JObject.Parse(requestBody);
                            string itemId = requestJson["ItemId"]?.ToString();

                            if (!string.IsNullOrEmpty(itemId))
                            {
                                var matchingCape = _capes.Find(c => c.ItemId == itemId);
                                if (matchingCape != null)
                                {
                                    LogProxy($"[TARGET] Item ID: {itemId} (Cape: {matchingCape.Name})", Color.LimeGreen);
                                    _pendingPlayFabRequests[e] = itemId;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogProxy($"[ERROR] Reading PlayFab request: {ex.Message}", Color.Red);
                    }
                }
            }
            else if (requestUrl.Contains("xforgeassets") && requestUrl.EndsWith("/primary.zip"))
            {
                try
                {
                    string urlPath = new Uri(requestUrl).AbsolutePath;
                    string[] pathParts = urlPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                    if (pathParts.Length >= 2)
                    {
                        string potentialUuid = pathParts[pathParts.Length - 2];

                        if (_generatedZipUuids.Contains(potentialUuid) && _zipUuidToItemId.ContainsKey(potentialUuid))
                        {
                            string itemId = _zipUuidToItemId[potentialUuid];
                            var matchingCape = _capes.Find(c => c.ItemId == itemId);

                            if (matchingCape != null && !string.IsNullOrEmpty(matchingCape.ZipFilePath) && File.Exists(matchingCape.ZipFilePath))
                            {
                                LogProxy($"[INTERCEPT] Serving {matchingCape.Name}", Color.Yellow);

                                byte[] zipContent = await File.ReadAllBytesAsync(matchingCape.ZipFilePath);

                                var headers = new HeaderCollection();
                                headers.AddHeader("Content-Type", "application/zip");
                                headers.AddHeader("Content-Length", zipContent.Length.ToString());

                                e.Ok(zipContent, headers);

                                LogProxy($"[SUCCESS] Served {matchingCape.Name} ({zipContent.Length} bytes)", Color.LimeGreen);
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogProxy($"[ERROR] Intercepting zip: {ex.Message}", Color.Red);
                }
            }
        }

        private async Task OnProxyResponse(object sender, SessionEventArgs e)
        {
            string requestUrl = e.HttpClient.Request.RequestUri.AbsoluteUri;

            if (requestUrl == _targetUrl)
            {
                await HandleCapesStoreResponse(e);
            }
            else if (requestUrl == _playfabApiUrl)
            {
                await HandlePlayFabResponse(e);
            }
        }

        private async Task HandleCapesStoreResponse(SessionEventArgs e)
        {
            try
            {
                if (File.Exists(_capesJsonPath))
                {
                    string capesContent = await File.ReadAllTextAsync(_capesJsonPath, Encoding.UTF8);
                    e.Ok(capesContent);
                    e.HttpClient.Response.Headers.AddHeader("Content-Type", "application/json");
                    LogProxy($"[SUCCESS] Served capesv2.json", Color.LimeGreen);
                }
                else
                {
                    LogProxy($"[WARNING] capesv2.json not found!", Color.Orange);
                }
            }
            catch (Exception ex)
            {
                LogProxy($"[ERROR] Serving capesv2.json: {ex.Message}", Color.Red);
            }
        }

        private async Task HandlePlayFabResponse(SessionEventArgs e)
        {
            try
            {
                if (_pendingPlayFabRequests.ContainsKey(e))
                {
                    string itemId = _pendingPlayFabRequests[e];
                    var matchingCape = _capes.Find(c => c.ItemId == itemId);

                    if (matchingCape != null)
                    {
                        LogProxy($"[RESPONSE] Generating custom PlayFab response for {matchingCape.Name}", Color.Yellow);

                        string customResponse = GenerateCustomPlayFabResponse(matchingCape);
                        e.Ok(customResponse);
                        e.HttpClient.Response.Headers.AddHeader("Content-Type", "application/json");

                        LogProxy($"[SUCCESS] Served PlayFab response for {matchingCape.Name}", Color.LimeGreen);
                    }

                    _pendingPlayFabRequests.Remove(e);
                }
            }
            catch (Exception ex)
            {
                LogProxy($"[ERROR] PlayFab response: {ex.Message}", Color.Red);
            }
        }

        private string GenerateCustomPlayFabResponse(CapeDefinition cape)
        {
            string zipUuid = Guid.NewGuid().ToString();
            _generatedZipUuids.Add(zipUuid);
            _zipUuidToItemId[zipUuid] = cape.ItemId;

            var response = new JObject
            {
                ["code"] = 200,
                ["status"] = "OK",
                ["data"] = new JObject
                {
                    ["Item"] = new JObject
                    {
                        ["Id"] = cape.ItemId,
                        ["Type"] = "bundle",
                        ["Title"] = new JObject { ["NEUTRAL"] = cape.Name },
                        ["Description"] = new JObject { ["NEUTRAL"] = cape.Description },
                        ["ContentType"] = "PersonaDurable",
                        ["Contents"] = new JArray
                        {
                            new JObject
                            {
                                ["Id"] = zipUuid,
                                ["Url"] = $"https://xforgeassets001.xboxlive.com/pf-namespace-MUEPXTH6QO/{zipUuid}/primary.zip",
                                ["Type"] = "personabinary"
                            }
                        },
                        ["DisplayProperties"] = new JObject
                        {
                            ["pieceType"] = "persona_capes",
                            ["rarity"] = cape.Rarity
                        }
                    }
                }
            };

            return response.ToString(Formatting.Indented);
        }

        private async Task InstallRootCertificateAsync()
        {
            try
            {
                if (!IsRunningAsAdmin())
                {
                    LogProxy("[WARNING] Not running as admin - certificate may not install", Color.Orange);
                    return;
                }

                var rootCert = _proxyServer?.CertificateManager.RootCertificate;
                if (rootCert == null) return;

                using (var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadWrite);
                    var existing = store.Certificates.Find(X509FindType.FindByThumbprint, rootCert.Thumbprint, false);

                    if (existing.Count == 0)
                    {
                        store.Add(rootCert);
                        LogProxy("[CERT] Root certificate installed", Color.Cyan);
                    }
                    else
                    {
                        LogProxy("[CERT] Root certificate already installed", Color.Gray);
                    }

                    store.Close();
                }
            }
            catch (Exception ex)
            {
                LogProxy($"[WARNING] Certificate install failed: {ex.Message}", Color.Orange);
            }
        }

        private static bool IsRunningAsAdmin()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = "session",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                });
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch { return false; }
        }

        private void UpdateProxyUI()
        {
            if (_isProxyRunning)
            {
                _lblProxyStatus.Text = "Status: Running on port 8080";
                _lblProxyStatus.ForeColor = Color.FromArgb(100, 255, 150);
                _btnToggleProxy.Text = "Stop Proxy";
                _btnToggleProxy.BackColor = Color.FromArgb(200, 50, 80);
            }
            else
            {
                _lblProxyStatus.Text = "Status: Stopped";
                _lblProxyStatus.ForeColor = Color.FromArgb(255, 100, 100);
                _btnToggleProxy.Text = "Start Proxy";
                _btnToggleProxy.BackColor = Color.FromArgb(138, 43, 226);
            }
        }


        private void LogProxy(string message, Color color)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => LogProxy(message, color)));
                return;
            }

            _txtProxyLog.SelectionStart = _txtProxyLog.TextLength;
            _txtProxyLog.SelectionLength = 0;
            _txtProxyLog.SelectionColor = color;
            _txtProxyLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            _txtProxyLog.SelectionColor = _txtProxyLog.ForeColor;
            _txtProxyLog.ScrollToCaret();
        }

        private void ProxySettingsMenu_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("ms-settings:network-proxy");
            }
            catch
            {
                MessageBox.Show("Could not open proxy settings. Please open manually:\nSettings > Network & Internet > Proxy",
                    "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isProxyRunning)
            {
                StopProxy();
            }
        }

        #endregion

        #region Cape Management

        private void CapeListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox?.SelectedItem is CapeDefinition cape)
            {
                _selectedCape = cape;
                LoadCapeToEditor(cape);
            }
        }

        private void LoadCapeToEditor(CapeDefinition cape)
        {
            FindControl<TextBox>("txtName").Text = cape.Name;
            FindControl<TextBox>("txtDescription").Text = cape.Description;
            FindControl<TextBox>("txtCreator").Text = cape.CreatorName;
            FindControl<TextBox>("txtThumbnail").Text = cape.ThumbnailUrl;
            FindControl<ComboBox>("cboRarity").SelectedItem = cape.Rarity;
            FindControl<TextBox>("txtItemId").Text = cape.ItemId;
            FindControl<TextBox>("txtPieceUuid").Text = cape.PieceUuid;

            var picBox = FindControl<PictureBox>("picCape");
            if (!string.IsNullOrEmpty(cape.ImagePath) && File.Exists(cape.ImagePath))
            {
                picBox.Image = Image.FromFile(cape.ImagePath);
            }
            else
            {
                picBox.Image = null;
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            var cape = new CapeDefinition
            {
                ItemId = Guid.NewGuid().ToString(),
                PieceUuid = Guid.NewGuid().ToString(),
                Name = "New Cape",
                Description = "Custom cape",
                CreatorName = Environment.UserName,
                Rarity = "rare"
            };
            _capes.Add(cape);
            RefreshCapeList();
            FindControl<ListBox>("capeListBox").SelectedItem = cape;
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (_selectedCape != null)
            {
                _capes.Remove(_selectedCape);
                _selectedCape = null;
                RefreshCapeList();
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_selectedCape != null)
            {
                _selectedCape.Name = FindControl<TextBox>("txtName").Text;
                _selectedCape.Description = FindControl<TextBox>("txtDescription").Text;
                _selectedCape.CreatorName = FindControl<TextBox>("txtCreator").Text;
                _selectedCape.ThumbnailUrl = FindControl<TextBox>("txtThumbnail").Text;
                _selectedCape.Rarity = FindControl<ComboBox>("cboRarity").SelectedItem?.ToString() ?? "rare";

                SaveConfiguration();
                RefreshCapeList();
                MessageBox.Show("Cape saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnSelectImage_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "PNG Images|*.png|All Files|*.*";
                ofd.Title = "Select Cape Texture (64x32)";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var img = Image.FromFile(ofd.FileName);

                        if (img.Width != 64 || img.Height != 32)
                        {
                            var result = MessageBox.Show(
                                $"Cape texture should be 64x32 pixels. Selected image is {img.Width}x{img.Height}.\n\nUse anyway?",
                                "Invalid Dimensions",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning
                            );

                            if (result != DialogResult.Yes)
                            {
                                img.Dispose();
                                return;
                            }
                        }

                        if (_selectedCape != null)
                        {
                            var fileName = $"{_selectedCape.ItemId}_cape.png";
                            var destPath = Path.Combine(_outputPath, "textures", fileName);
                            Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                            img.Save(destPath, ImageFormat.Png);
                            _selectedCape.ImagePath = destPath;

                            FindControl<PictureBox>("picCape").Image = img;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            if (_capes.Count == 0)
            {
                MessageBox.Show("No capes to generate!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                LogProxy("", Color.White);
                LogProxy("=== GENERATION STARTED ===", Color.Cyan);

                foreach (var cape in _capes)
                {
                    LogProxy($"Generating {cape.Name}...", Color.Yellow);
                    //delete contents of zip folder
                    var zipDir = Path.Combine(_outputPath, "zips");
                    if (Directory.Exists(zipDir))
                    {
                        foreach (var file in Directory.GetFiles(zipDir, $"{cape.ItemId}_primary.zip"))
                        {
                            try
                            {
                                File.Delete(file);
                                LogProxy($"✓ Deleted existing zip: {file}", Color.Gray);
                            }
                            catch (Exception ex)
                            {
                                LogProxy($"✗ Error deleting existing zip: {ex.Message}", Color.Red);
                            }
                        }
                    }                       
                    GenerateCapePackage(cape);
                    LogProxy($"✓ Generated {cape.Name}", Color.LimeGreen);
                }

                LogProxy("Generating capesv2.json...", Color.Yellow);
                GenerateCapesJson();
                LogProxy("✓ Generated capesv2.json", Color.LimeGreen);

                LogProxy("=== GENERATION COMPLETE ===", Color.Cyan);
                LogProxy($"✓ {_capes.Count} cape(s) ready to use!", Color.LimeGreen);

                Cursor = Cursors.Default;
                MessageBox.Show($"Successfully generated {_capes.Count} cape packages and capesv2.json!",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                LogProxy($"✗ GENERATION FAILED: {ex.Message}", Color.Red);
                MessageBox.Show($"Error generating capes: {ex.Message}\n\n{ex.StackTrace}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GenerateCapePackage(CapeDefinition cape)
        {
            var workDir = Path.Combine(_outputPath, "temp", cape.ItemId);
            Directory.CreateDirectory(workDir);

            try
            {
                // Create manifest.json
                var manifest = CreateManifest(cape);
                File.WriteAllText(Path.Combine(workDir, "manifest.json"),
                    JsonConvert.SerializeObject(manifest, Formatting.Indented));

                // Copy cape texture
                if (!string.IsNullOrEmpty(cape.ImagePath) && File.Exists(cape.ImagePath))
                {
                    var capeFileName = $"{cape.ItemId}_cape.png";
                    var capeDestPath = Path.Combine(workDir, capeFileName);
                    File.Copy(cape.ImagePath, capeDestPath, true);

                    // Create meta.json for the cape
                    var metaJson = new JObject
                    {
                        ["piece_id"] = cape.PieceUuid,
                        ["piece_name"] = $"{cape.ItemId}_cape",
                        ["piece_type"] = "persona_capes",
                        ["zone"] = new JArray { "body_back_upper", "body_back_lower" },
                        ["texture_sources"] = new JArray
        {
            new JObject
            {
                ["texture"] = capeFileName
            }
        }
                    };

                    var metaPath = Path.Combine(workDir, $"{cape.ItemId}_cape.meta.json");
                    File.WriteAllText(metaPath, metaJson.ToString(Formatting.Indented));
                }
                else
                {
                    throw new FileNotFoundException($"Cape image not found: {cape.ImagePath}");
                }

                // Create texts directory with lang files
                CreateLanguageFiles(workDir, cape);

                // Create contents.json manually (McCrypt may auto-generate, but we'll create it)
                CreateContentsJson(workDir, cape);

                // Sign manifest
                Manifest.SignManifest(workDir);

                // Encrypt with McCrypt
                var contentKey = "s5s5ejuDru4uchuF2drUFuthaspAbepE";
                Marketplace.EncryptContents(workDir, cape.PieceUuid, contentKey);

                // Create ppack0.zip from the encrypted contents
                var ppackPath = Path.Combine(workDir, "ppack0.zip");
                using (var ppackZip = ZipFile.Open(ppackPath, ZipArchiveMode.Create))
                {
                    foreach (string file in Directory.GetFiles(workDir, "*", SearchOption.AllDirectories))
                    {
                        if (file == ppackPath) continue; // Skip ppack0.zip itself

                        string relPath = file.Substring(workDir.Length + 1);
                        ppackZip.CreateEntryFromFile(file, relPath);
                    }
                }
                var primaryPath = Path.Combine(_outputPath, "zips", $"{cape.ItemId}_primary.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(primaryPath));

                if (File.Exists(ppackPath))
                {
                    using (var primaryZip = ZipFile.Open(primaryPath, ZipArchiveMode.Create))
                    {
                        primaryZip.CreateEntryFromFile(ppackPath, "ppack0.zip");
                    }

                    cape.ZipFilePath = primaryPath;
                    SaveConfiguration();
                }
                else
                {
                    throw new FileNotFoundException("ppack0.zip was not created by McCrypt");
                }
            }
            finally
            {
                // Cleanup temp directory
                try { Directory.Delete(workDir, true); } catch { }
            }
        }

        private JObject CreateManifest(CapeDefinition cape)
        {
            return new JObject
            {
                ["format_version"] = 1,
                ["header"] = new JObject
                {
                    ["description"] = cape.Description ?? "pack.description",
                    ["name"] = cape.Name ?? "pack.name",
                    ["uuid"] = cape.PieceUuid,   // assumed equivalent
                    ["version"] = new JArray { 1, 1, 0 }
                },
                ["modules"] = new JArray
        {
            new JObject
            {
                ["type"] = "persona_piece",
                ["uuid"] = Guid.NewGuid().ToString(),
                ["version"] = new JArray { 1, 1, 0 }
            }
                }
            };
        }

        private void CreateContentsJson(string workDir, CapeDefinition cape)
        {
            var contents = new JObject
            {
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["path"] = $"{cape.ItemId}_cape.png"
                    },
                    new JObject
                    {
                        ["path"] = $"{cape.ItemId}_cape.meta.json"
                    },
                    new JObject
                    {
                        ["path"] = "texts/languages.json"
                    },
                    new JObject
                    {
                        ["path"] = "texts/en_US.lang"
                    }
                }
            };

            File.WriteAllText(Path.Combine(workDir, "contents.json"),
                contents.ToString(Formatting.Indented));
        }

        private void CreateLanguageFiles(string workDir, CapeDefinition cape)
        {
            var textsDir = Path.Combine(workDir, "texts");
            Directory.CreateDirectory(textsDir);

            // languages.json
            var languagesJson = new JArray { "en_US" };
            File.WriteAllText(Path.Combine(textsDir, "languages.json"),
                languagesJson.ToString(Formatting.Indented));

            // en_US.lang
            var langContent = $"persona.{cape.ItemId}_cape.title={cape.Name}\n";
            File.WriteAllText(Path.Combine(textsDir, "en_US.lang"), langContent, Encoding.UTF8);
        }

        private void GenerateCapesJson()
        {
            JObject capesData;

            // Load existing capesv2.json if it exists, otherwise create from template
            if (File.Exists(_capesJsonPath))
            {
                string existingJson = File.ReadAllText(_capesJsonPath);
                capesData = JObject.Parse(existingJson);
            }
            else
            {
                capesData = LoadCapesTemplate();
            }

            // Navigate to items array
            var rows = capesData["result"]["rows"] as JArray;
            JArray itemsList = null;

            foreach (var row in rows)
            {
                if (row["controlId"]?.ToString() == "GridList")
                {
                    var components = row["components"] as JArray;
                    foreach (var component in components)
                    {
                        if (component["type"]?.ToString() == "itemListComp")
                        {
                            itemsList = component["items"] as JArray;
                            break;
                        }
                    }
                    break;
                }
            }

            if (itemsList == null)
            {
                throw new Exception("Could not find items list in capesv2.json template");
            }

            // Remove only custom capes (ones with IDs that match our _capes list)
            var customCapeIds = new HashSet<string>(_capes.Select(c => c.ItemId));
            for (int i = itemsList.Count - 1; i >= 0; i--)
            {
                string itemId = itemsList[i]["id"]?.ToString();
                if (!string.IsNullOrEmpty(itemId) && customCapeIds.Contains(itemId))
                {
                    itemsList.RemoveAt(i);
                }
            }

            // Add custom capes to the existing list
            foreach (var cape in _capes)
            {
                var capeItem = CreateCapeItem(cape);
                itemsList.Add(capeItem);
            }

            // Update totalItems count
            foreach (var row in rows)
            {
                if (row["controlId"]?.ToString() == "GridList")
                {
                    var components = row["components"] as JArray;
                    foreach (var component in components)
                    {
                        if (component["type"]?.ToString() == "itemListComp")
                        {
                            component["totalItems"] = itemsList.Count;

                            // Update maxOffers if it exists
                            if (component["customStoreRowConfiguration"] != null)
                            {
                                component["customStoreRowConfiguration"]["maxOffers"] = itemsList.Count;
                            }
                            break;
                        }
                    }
                    break;
                }
            }

            File.WriteAllText(_capesJsonPath, capesData.ToString(Formatting.Indented));
        }

        private JObject LoadCapesTemplate()
        {
            return JObject.Parse(@"{
  ""result"": {
    ""id"": ""9635ac1f-8ea3-4bb2-a43c-9b158b3382d1"",
    ""pageId"": ""DressingRoom_Capes"",
    ""addToRecentlyViewed"": false,
    ""pageName"": ""Home L1"",
    ""rows"": [
      {
        ""controlId"": ""Layout"",
        ""components"": []
      },
      {
        ""controlId"": ""GridList"",
        ""components"": [
          {
            ""text"": {
              ""value"": ""dr.collector_title.owned""
            },
            ""type"": ""headerComp"",
            ""$type"": ""HeaderComponent""
          },
          {
            ""items"": [],
            ""totalItems"": 0,
            ""type"": ""itemListComp"",
            ""$type"": ""ItemListComponent""
          }
        ]
      }
    ],
    ""inventoryVersion"": ""1/MTQ1"",
    ""sidebarLayoutType"": ""Persona""
  }
}");
        }

        private JObject CreateCapeItem(CapeDefinition cape)
        {
            return new JObject
            {
                ["id"] = cape.ItemId,
                ["contentType"] = "PersonaDurable",
                ["title"] = cape.Name,
                ["description"] = cape.Description,
                ["creatorName"] = cape.CreatorName,
                ["thumbnail"] = new JObject
                {
                    ["tag"] = "Thumbnail",
                    ["type"] = "Thumbnail",
                    ["url"] = cape.ThumbnailUrl ?? "",
                    ["urlWithResolution"] = cape.ThumbnailUrl ?? ""
                },
                ["rating"] = new JObject
                {
                    ["average"] = 4.5,
                    ["totalCount"] = 10
                },
                ["price"] = new JObject
                {
                    ["listPrice"] = 0,
                    ["realmsInfo"] = new JObject { ["inRealmsPlus"] = false },
                    ["currencyId"] = "ecd19d3c-7635-402c-a185-eb11cb6c6946",
                    ["virtualCurrencyType"] = "Minecoin"
                },
                ["linksTo"] = $"ItemDetail_{cape.ItemId}?selectedItemId={cape.ItemId}",
                ["linksToInfo"] = new JObject
                {
                    ["linksTo"] = $"ItemDetail_{cape.ItemId}?selectedItemId={cape.ItemId}",
                    ["linkType"] = "pageId",
                    ["displayType"] = "store_layout.character_creator_screen",
                    ["navigateInPlace"] = false
                },
                ["creatorPage"] = "CreatorPage_master_player_account!H8MB8R7GTF",
                ["statistics"] = new JObject
                {
                    ["skins"] = 0,
                    ["worlds"] = 0,
                    ["textures"] = 0,
                    ["behaviors"] = 0
                },
                ["flags"] = new JArray(),
                ["pieceType"] = "persona_capes",
                ["rarity"] = cape.Rarity,
                ["ownership"] = "Purchased",
                ["packType"] = "Persona",
                ["packIdentity"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "persona_piece",
                        ["uuid"] = cape.PieceUuid,
                        ["version"] = "1.2.0"
                    }
                },
                ["tags"] = new JArray(),
                ["images"] = new JArray
                {
                    new JObject
                    {
                        ["tag"] = "Thumbnail",
                        ["type"] = "Thumbnail",
                        ["url"] = cape.ThumbnailUrl ?? ""
                    }
                },
                ["contents"] = new JArray(),
                ["platformRestricted"] = false,
                ["iconOverlay"] = new JArray(),
                ["descriptionLineByLine"] = new JArray(),
                ["startDate"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                ["subscription"] = new JArray(),
                ["thumbnailPreviewOnly"] = false
            };
        }

        private void LoadConfiguration()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    _capes = JsonConvert.DeserializeObject<List<CapeDefinition>>(json) ?? new List<CapeDefinition>();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading config: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_capes, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving config: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshCapeList()
        {
            var listBox = FindControl<ListBox>("capeListBox");
            listBox.DataSource = null;
            listBox.DataSource = _capes;
        }

        private T FindControl<T>(string name) where T : Control
        {
            return this.Controls.Find(name, true).FirstOrDefault() as T;
        }

        private void ExportMenu_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "JSON Files|*.json";
                sfd.FileName = "capesv2.json";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    GenerateCapesJson();
                    File.Copy(_capesJsonPath, sfd.FileName, true);
                    MessageBox.Show("Exported successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ImportMenu_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON Files|*.json";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    LoadConfiguration();
                    RefreshCapeList();
                }
            }
        }

        #endregion
    }

    public class CustomMenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(80, 40, 120)), e.Item.ContentRectangle);
            }
            else
            {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(25, 15, 40)), e.Item.ContentRectangle);
            }
        }
    }


    public class CapeDefinition
    {
        public string ItemId { get; set; }
        public string PieceUuid { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string CreatorName { get; set; }
        public string ThumbnailUrl { get; set; }
        public string Rarity { get; set; } = "rare";
        public string ImagePath { get; set; }
        public string ZipFilePath { get; set; }

        public override string ToString() => Name;
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}