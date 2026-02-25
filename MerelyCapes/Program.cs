using Microsoft.VisualBasic.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpGL;
using SharpGL.WinForms;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;
namespace MerelyCapes
{
    internal static class CapeEncryption
    {
        private struct contentsJson
        {
            public int version;
            public List<Object> content;
        }
        private struct contentKeys
        {
            public string key;
            public string path;
        }
        private struct content
        {
            public string path;
        }
        private struct signatureBlock
        {
            public string hash;
            public string path;
        }
        private static Random rng = new Random();
        private static string[] dontEncrypt = new string[] { "manifest.json", "contents.json", "texts", "pack_icon.png", "ui" };
        internal static void WriteString(Stream stream, string str, long totalLength)
        {
            byte[] data = Encoding.UTF8.GetBytes(str);
            long paddingLen = totalLength - data.Length;
            byte[] padding = new byte[paddingLen];
            stream.Write(data, 0, data.Length);
            stream.Write(padding, 0, padding.Length);
        }
        internal static byte[] Aes256CfbEncrypt(byte[] key, byte[] iv, byte[] data)
        {
            Aes aes = Aes.Create();
            aes.Mode = CipherMode.CFB;
            aes.Padding = PaddingMode.None;
            aes.BlockSize = 128;
            aes.KeySize = 256;
            ICryptoTransform aesEncryptor = aes.CreateEncryptor(key, iv);
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, aesEncryptor, CryptoStreamMode.Write))
                {
                    csEncrypt.Write(data, 0, data.Length);
                    long totalWritten = data.Length;
                    while ((totalWritten % 16 != 0))
                    {
                        csEncrypt.WriteByte(0);
                        totalWritten++;
                    }
                    msEncrypt.Seek(0x00, SeekOrigin.Begin);
                    return msEncrypt.ToArray();
                }
            }
        }
        internal static string GenerateKey()
        {
            string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            string key = "";
            for (int i = 0; i < 32; i++)
            {
                key += allowedChars[rng.Next(0, allowedChars.Length)];
            }
            return key;
        }
        internal static bool IsDirectory(string path)
        {
            if (Directory.Exists(path))
                return true;
            else if (File.Exists(path))
                return false;
            else
                throw new FileNotFoundException("Cannot find file: " + path);
        }
        private static bool shouldEncrypt(string relPath)
        {
            foreach (string part in relPath.Split('/'))
                if (dontEncrypt.Contains(part))
                    return false;
            return true;
        }
        internal static byte[] Sha256(byte[] data)
        {
            SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(data);
            sha256.Dispose();
            return hash;
        }
        public static string EncryptContents(string basePath, string uuid, string ContentKey)
        {
            string text = Path.Combine(basePath, "contents.json");
            contentsJson contentsJson = default(contentsJson);
            contentsJson.version = 1;
            contentsJson.content = new List<object>();
            foreach (string text2 in Directory.GetFileSystemEntries(basePath, "*", SearchOption.AllDirectories))
            {
                string text3 = text2.Remove(0, basePath.Length + 1);
                text3 = text3.Replace("\\", "/");
                bool flag = shouldEncrypt(text3);
                if (IsDirectory(text2))
                {
                    text3 += "/";
                    flag = false;
                }
                if (flag)
                {
                    contentKeys contentKeys = default(contentKeys);
                    contentKeys.path = text3;
                    contentKeys.key = GenerateKey();
                    byte[] bytes = Encoding.UTF8.GetBytes(contentKeys.key);
                    byte[] array = new byte[16];
                    Array.Copy(bytes, array, 16);
                    byte[] array2 = Aes256CfbEncrypt(bytes, array, File.ReadAllBytes(text2));
                    File.WriteAllBytes(text2, array2);
                    contentsJson.content.Add(contentKeys);
                }
                else
                {
                    content content = default(content);
                    content.path = text3;
                    contentsJson.content.Add(content);
                }
            }
            string text4 = JsonConvert.SerializeObject(contentsJson);
            byte[] bytes2 = Encoding.UTF8.GetBytes(ContentKey);
            byte[] array3 = new byte[16];
            Array.Copy(bytes2, array3, 16);
            byte[] array4 = Aes256CfbEncrypt(bytes2, array3, Encoding.UTF8.GetBytes(text4));
            FileStream fileStream = File.OpenWrite(text);
            BinaryWriter binaryWriter = new BinaryWriter(fileStream);
            binaryWriter.Write(0U);
            binaryWriter.Write(2614082044U);
            binaryWriter.Write(0UL);
            fileStream.WriteByte((byte)uuid.Length);
            WriteString(fileStream, uuid, 239L);
            fileStream.Write(array4, 0, array4.Length);
            fileStream.Close();
            return ContentKey;
        }
        public static void SignManifest(string basePath)
        {
            string manifestPath = Path.Combine(basePath, "manifest.json");
            signatureBlock signBlock = new signatureBlock();
            signBlock.path = manifestPath.Remove(0, basePath.Length + 1);
            signBlock.hash = Convert.ToBase64String(Sha256(File.ReadAllBytes(manifestPath)));
            List<signatureBlock> signatureData = new List<signatureBlock>();
            signatureData.Add(signBlock);
            string signatureJson = JsonConvert.SerializeObject(signatureData);
            string signaturesJsonFile = Path.Combine(basePath, "signatures.json");
            File.WriteAllText(signaturesJsonFile, signatureJson);
        }
        internal static class Pal
        {
            public static readonly Color BgDeep = Color.FromArgb(7, 5, 16);
            public static readonly Color BgMid = Color.FromArgb(11, 8, 24);
            public static readonly Color BgPanel = Color.FromArgb(15, 10, 32);
            public static readonly Color BgCard = Color.FromArgb(19, 13, 40);
            public static readonly Color BgCardHov = Color.FromArgb(25, 17, 52);
            public static readonly Color BgCardSel = Color.FromArgb(28, 20, 60);
            public static readonly Color Border = Color.FromArgb(36, 24, 70);
            public static readonly Color BorderHov = Color.FromArgb(58, 40, 110);
            public static readonly Color BorderSel = Color.FromArgb(75, 52, 140);
            public static readonly Color TxtPrim = Color.FromArgb(190, 180, 225);
            public static readonly Color TxtDim = Color.FromArgb(95, 88, 135);
            public static readonly Color TxtMuted = Color.FromArgb(52, 46, 80);
            public static readonly Color AccentVio = Color.FromArgb(100, 70, 185);
            public static readonly Color AccentBlue = Color.FromArgb(55, 145, 210);
            public static readonly Color AccentGrn = Color.FromArgb(55, 185, 130);
            public static readonly Color AccentRed = Color.FromArgb(195, 70, 85);
            public static readonly Color BtnBg = Color.FromArgb(32, 22, 68);
            public static readonly Color BtnHov = Color.FromArgb(44, 30, 88);
            public static readonly Color BtnStop = Color.FromArgb(105, 38, 50);
            public static Color Rarity(string r) => r.ToLower() switch
            {
                "legendary" => Color.FromArgb(200, 162, 58),
                "epic" => Color.FromArgb(150, 75, 200),
                "rare" => Color.FromArgb(50, 132, 200),
                _ => Color.FromArgb(125, 120, 150)
            };
        }
        public class RoundForm : Form
        {
            private const int R = 18;
            [DllImport("gdi32.dll")]
            private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);
            [DllImport("dwmapi.dll")]
            private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
            [DllImport("user32.dll")]
            protected static extern bool ReleaseCapture();
            [DllImport("user32.dll")]
            protected static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
            protected override void OnLoad(EventArgs e)
            {
                base.OnLoad(e);
                ApplyRegion();
                int round = 2;
                DwmSetWindowAttribute(Handle, 33, ref round, 4);
            }
            public void TitleMouseDown(object? sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            }
            protected override void OnResize(EventArgs e) { base.OnResize(e); ApplyRegion(); }
            private void ApplyRegion()
            {
                Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, R * 2, R * 2));
            }
        }
        internal class GlowCard : Panel
        {
            private bool _hov, _sel;
            public bool Selected { get => _sel; set { _sel = value; Invalidate(); } }
            public GlowCard()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                MouseEnter += (_, _) => { _hov = true; Invalidate(); };
                MouseLeave += (_, _) => { _hov = false; Invalidate(); };
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new RectangleF(1.5f, 1.5f, Width - 3, Height - 3);
                using var path = RR(rc, 10f);
                var fill = _sel ? Pal.BgCardSel : _hov ? Pal.BgCardHov : Pal.BgCard;
                using var fb = new SolidBrush(fill);
                g.FillPath(fb, path);
                var bc = _sel ? Pal.BorderSel : _hov ? Pal.BorderHov : Pal.Border;
                using var pen = new Pen(bc, 1f);
                g.DrawPath(pen, path);
                base.OnPaint(e);
            }
            private static GraphicsPath RR(RectangleF r, float rad)
            {
                var p = new GraphicsPath();
                p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
                p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
                p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
                p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
                p.CloseFigure();
                return p;
            }
            protected override void OnControlAdded(ControlEventArgs e)
            {
                base.OnControlAdded(e);
                SetTransparent(e.Control);
            }
            private static void SetTransparent(Control c)
            {
                if (c is TextBox || c is ComboBox || c is RichTextBox || c is Button) return;
                c.BackColor = Color.Transparent;
                foreach (Control ch in c.Controls) SetTransparent(ch);
            }
        }
        internal class GLCapePreview : Panel
        {
            private readonly OpenGL _gl = new();
            private uint[] _texId = new uint[1];
            private Bitmap? _texBmp;
            private bool _texDirty, _glReady;
            private float _yaw = 20f, _pitch = -10f;
            private bool _dragging, _userDragged;
            private Point _lastMouse;
            private readonly System.Windows.Forms.Timer _spinTimer;
            private const float CW = 0.625f, CH = 1.0f, CD = 0.0625f;
            private static float U(float px) => px / 64f;
            private static float V(float py) => py / 32f;
            public GLCapePreview()
            {
                SetStyle(ControlStyles.Opaque, true);
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.Opaque, true);
                BackColor = Color.FromArgb(10, 6, 22);
                _spinTimer = new System.Windows.Forms.Timer { Interval = 25 };
                _spinTimer.Tick += (_, _) =>
                {
                    if (!_userDragged) _yaw += 0.6f;
                    Invalidate();
                };
                _spinTimer.Start();
                MouseDown += (_, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    { _dragging = true; _userDragged = true; _lastMouse = e.Location; }
                };
                MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) _dragging = false; };
                MouseMove += (_, e) =>
                {
                    if (!_dragging) return;
                    _yaw += (e.X - _lastMouse.X) * 0.8f;
                    _pitch += (e.Y - _lastMouse.Y) * 0.8f;
                    _pitch = Math.Clamp(_pitch, -85f, 85f);
                    _lastMouse = e.Location;
                    Invalidate();
                };
                MouseDoubleClick += (_, _) => { _userDragged = false; _pitch = -10f; };
            }
            public Bitmap? CapeTexture
            {
                get => _texBmp;
                set { _texBmp = value; _texDirty = true; Invalidate(); }
            }
            private void EnsureGL()
            {
                if (_glReady) return;
                _gl.Create(SharpGL.Version.OpenGLVersion.OpenGL2_1,
                           SharpGL.RenderContextType.NativeWindow,
                           Width, Height, 32, Handle);
                _gl.MakeCurrent();
                _gl.Enable(OpenGL.GL_DEPTH_TEST);
                _gl.Enable(OpenGL.GL_TEXTURE_2D);
                _gl.Enable(OpenGL.GL_BLEND);
                _gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
                _gl.ClearColor(0.04f, 0.02f, 0.10f, 1f);
                _gl.GenTextures(1, _texId);
                _glReady = true;
            }
            protected override void OnPaintBackground(PaintEventArgs e) { }
            protected override void OnPaint(PaintEventArgs e)
            {
                if (!_glReady)
                {
                    EnsureGL();
                }
                _gl.MakeCurrent();
                _gl.SetDimensions(Width, Height);
                _gl.Viewport(0, 0, Width, Height);
                if (_texDirty && _texBmp != null)
                {
                    UploadTexture();
                    _texDirty = false;
                }
                _gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
                _gl.MatrixMode(OpenGL.GL_PROJECTION);
                _gl.LoadIdentity();
                _gl.Perspective(45.0, Width / (double)Math.Max(1, Height), 0.1, 100.0);
                _gl.MatrixMode(OpenGL.GL_MODELVIEW);
                _gl.LoadIdentity();
                _gl.Translate(0f, 0f, -3.2f);
                _gl.Rotate(_pitch, 1f, 0f, 0f);
                _gl.Rotate(_yaw, 0f, 1f, 0f);
                if (_texBmp != null)
                {
                    _gl.BindTexture(OpenGL.GL_TEXTURE_2D, _texId[0]);
                    DrawCape();
                }
                _gl.Flush();
                IntPtr hdc = e.Graphics.GetHdc();
                _gl.Blit(hdc);
                e.Graphics.ReleaseHdc(hdc);
            }
            private void DrawCape()
            {
                _gl.Begin(OpenGL.GL_QUADS);
                _gl.TexCoord(U(1), V(1)); _gl.Vertex(-CW, CH, CD);
                _gl.TexCoord(U(11), V(1)); _gl.Vertex(CW, CH, CD);
                _gl.TexCoord(U(11), V(17)); _gl.Vertex(CW, -CH, CD);
                _gl.TexCoord(U(1), V(17)); _gl.Vertex(-CW, -CH, CD);
                _gl.End();
                _gl.Begin(OpenGL.GL_QUADS);
                _gl.TexCoord(U(22), V(1)); _gl.Vertex(-CW, CH, -CD);
                _gl.TexCoord(U(12), V(1)); _gl.Vertex(CW, CH, -CD);
                _gl.TexCoord(U(12), V(17)); _gl.Vertex(CW, -CH, -CD);
                _gl.TexCoord(U(22), V(17)); _gl.Vertex(-CW, -CH, -CD);
                _gl.End();
                _gl.Begin(OpenGL.GL_QUADS);
                _gl.TexCoord(U(11), V(1)); _gl.Vertex(CW, CH, CD);
                _gl.TexCoord(U(12), V(1)); _gl.Vertex(CW, CH, -CD);
                _gl.TexCoord(U(12), V(17)); _gl.Vertex(CW, -CH, -CD);
                _gl.TexCoord(U(11), V(17)); _gl.Vertex(CW, -CH, CD);
                _gl.End();
                _gl.Begin(OpenGL.GL_QUADS);
                _gl.TexCoord(U(0), V(1)); _gl.Vertex(-CW, CH, -CD);
                _gl.TexCoord(U(1), V(1)); _gl.Vertex(-CW, CH, CD);
                _gl.TexCoord(U(1), V(17)); _gl.Vertex(-CW, -CH, CD);
                _gl.TexCoord(U(0), V(17)); _gl.Vertex(-CW, -CH, -CD);
                _gl.End();
                _gl.Begin(OpenGL.GL_QUADS);
                _gl.TexCoord(U(1), V(0)); _gl.Vertex(-CW, CH, -CD);
                _gl.TexCoord(U(11), V(0)); _gl.Vertex(CW, CH, -CD);
                _gl.TexCoord(U(11), V(1)); _gl.Vertex(CW, CH, CD);
                _gl.TexCoord(U(1), V(1)); _gl.Vertex(-CW, CH, CD);
                _gl.End();
                _gl.Begin(OpenGL.GL_QUADS);
                _gl.TexCoord(U(11), V(0)); _gl.Vertex(-CW, -CH, CD);
                _gl.TexCoord(U(21), V(0)); _gl.Vertex(CW, -CH, CD);
                _gl.TexCoord(U(21), V(1)); _gl.Vertex(CW, -CH, -CD);
                _gl.TexCoord(U(11), V(1)); _gl.Vertex(-CW, -CH, -CD);
                _gl.End();
            }
            private void UploadTexture()
            {
                var bmp = _texBmp!;
                var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                                       ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    _gl.BindTexture(OpenGL.GL_TEXTURE_2D, _texId[0]);
                    _gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST);
                    _gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);
                    _gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP_TO_EDGE);
                    _gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP_TO_EDGE);
                    _gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA,
                                   bmp.Width, bmp.Height, 0,
                                   0x80E1, OpenGL.GL_UNSIGNED_BYTE, bd.Scan0);
                }
                finally { bmp.UnlockBits(bd); }
            }
        }
        internal class ThumbPreview : Panel
        {
            private string? _url;
            private Image? _img;
            private bool _busy;
            public ThumbPreview()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }
            public string? Url
            {
                get => _url;
                set { _url = value; _img = null; _ = Load(); }
            }
            private async Task Load()
            {
                if (string.IsNullOrWhiteSpace(_url)) { Invalidate(); return; }
                _busy = true; Invalidate();
                try
                {
                    using var hc = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                    var bytes = await hc.GetByteArrayAsync(_url);
                    using var ms = new MemoryStream(bytes);
                    _img = Image.FromStream(ms);
                }
                catch { _img = null; }
                finally { _busy = false; if (IsHandleCreated) BeginInvoke(Invalidate); }
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var bg = new SolidBrush(Color.FromArgb(13, 8, 28));
                g.FillRectangle(bg, 0, 0, Width, Height);
                using var bd = new Pen(Pal.Border, 1f);
                g.DrawRectangle(bd, 0.5f, 0.5f, Width - 1, Height - 1);
                using var lf = new Font("Segoe UI", 6.5f);
                using var lb = new SolidBrush(Pal.AccentBlue);
                g.DrawString("THUMBNAIL", lf, lb, 4, 4);
                var ir = new Rectangle(3, 16, Width - 6, Height - 19);
                if (_img != null)
                    g.DrawImage(_img, ir);
                else
                {
                    using var ph = new SolidBrush(Color.FromArgb(22, 45, 36, 110));
                    g.FillRectangle(ph, ir);
                    string msg = _busy ? "Loading…" : (_url != null ? "Error" : "No URL");
                    using var mf = new Font("Segoe UI", 7.5f);
                    using var mb = new SolidBrush(Pal.TxtDim);
                    g.DrawString(msg, mf, mb, ir, new StringFormat
                    { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
            }
        }
        public class MainForm : RoundForm
        {
            private List<CapeDefinition> _capes = new();
            private CapeDefinition? _sel;
            private const string CfgPath = "cape_studio_config.json";
            private const string CapesJson = "capesv2.json";
            private const string OutPath = "output";
            private ProxyServer? _proxy;
            private bool _proxyOn;
            private const string TargetUrl = "https://store.mktpl.minecraft-services.net/api/v1.0/layout/pages/DressingRoom_Capes";
            private const string PlayfabUrl = "https://20ca2.playfabapi.com/Catalog/GetPublishedItem";
            private readonly Dictionary<SessionEventArgs, string> _pfPend = new();
            private readonly Dictionary<string, string> _zipItem = new();
            private readonly HashSet<string> _zipIds = new();
            private FlowLayoutPanel _cardFlow = null!;
            private Panel _editor = null!;
            private RichTextBox _log = null!;
            private Label _proxyStat = null!;
            private Button _proxyBtn = null!;
            public MainForm()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint, true);
                Text = "MerelyCapes";
                try { Icon = new Icon("capes.ico"); } catch { }
                Size = new Size(940, 640);
                MinimumSize = new Size(780, 520);
                StartPosition = FormStartPosition.CenterScreen;
                FormBorderStyle = FormBorderStyle.None;
                BackColor = Pal.BgDeep;
                FormClosing += (_, _) => { if (_proxyOn) StopProxy(); };
                Build();
                LoadCfg();
                RebuildCards();
                if (_sel != null) BuildEditor(_sel);
            }
            private void Build()
            {
                var bar = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 36,
                    BackColor = Color.FromArgb(9, 6, 20)
                };
                bar.MouseDown += TitleMouseDown;
                var titleLbl = L("MerelyCapes",
                                 new Font("Segoe UI", 10.5f, FontStyle.Bold), Pal.AccentVio);
                titleLbl.Dock = DockStyle.Left;
                titleLbl.Width = 220;
                titleLbl.Padding = new Padding(14, 0, 0, 0);
                titleLbl.MouseDown += TitleMouseDown;
                var btnM = TBar("─", Pal.TxtDim);
                btnM.Dock = DockStyle.Right;
                btnM.Click += (_, _) => WindowState = FormWindowState.Minimized;
                var btnX = TBar("✕", Pal.AccentRed);
                btnX.Dock = DockStyle.Right;
                btnX.Click += (_, _) => Application.Exit();
                bar.Controls.Add(titleLbl);
                bar.Controls.Add(btnM);
                bar.Controls.Add(btnX);
                var strip = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 28,
                    BackColor = Color.FromArgb(8, 5, 17)
                };
                _proxyStat = L("● Proxy off", new Font("Segoe UI", 8f), Pal.AccentRed);
                _proxyStat.Dock = DockStyle.Left; _proxyStat.Width = 220;
                _proxyStat.Padding = new Padding(10, 0, 0, 0);
                _proxyBtn = Pill("Start Proxy", 100);
                _proxyBtn.Dock = DockStyle.Right;
                _proxyBtn.Click += async (_, _) => { if (_proxyOn) StopProxy(); else await StartProxy(); };
                var clearBtn = FBtn("Clear Log", 72);
                clearBtn.Dock = DockStyle.Right;
                clearBtn.Click += (_, _) => _log.Clear();
                strip.Controls.AddRange(new Control[] { _proxyStat, _proxyBtn, clearBtn });
                var logBar = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 115,
                    BackColor = Color.FromArgb(7, 5, 16)
                };
                var logHead = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 20,
                    BackColor = Color.FromArgb(9, 6, 19)
                };
                var logTitle = L("  ▸  Proxy Log", new Font("Segoe UI", 7.5f, FontStyle.Bold), Pal.AccentBlue);
                logTitle.Dock = DockStyle.Fill;
                logHead.Controls.Add(logTitle);
                _log = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(6, 4, 13),
                    ForeColor = Pal.TxtPrim,
                    Font = new Font("Consolas", 8f),
                    ReadOnly = true,
                    BorderStyle = BorderStyle.None
                };
                logBar.Controls.Add(_log);
                logBar.Controls.Add(logHead);
                var content = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
                var side = new Panel
                {
                    Dock = DockStyle.Left,
                    Width = 215,
                    BackColor = Color.Transparent
                };
                var sideHead = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 40,
                    BackColor = Color.Transparent
                };
                var sideTitle = L("CAPES", new Font("Segoe UI", 7.5f, FontStyle.Bold), Pal.TxtDim);
                sideTitle.Dock = DockStyle.Left; sideTitle.Width = 60;
                sideTitle.Padding = new Padding(12, 0, 0, 0);
                var addBtn = Pill("+  Add", 80);
                addBtn.Dock = DockStyle.Right;
                addBtn.Margin = new Padding(0, 6, 8, 6);
                addBtn.Click += (_, _) => AddCape();
                sideHead.Controls.AddRange(new Control[] { sideTitle, addBtn });
                _cardFlow = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    BackColor = Color.Transparent,
                    Padding = new Padding(7, 4, 7, 4)
                };
                var genBtn = Pill("Generate All", 198);
                genBtn.Dock = DockStyle.Bottom;
                genBtn.Height = 32; genBtn.Margin = new Padding(8, 4, 8, 8);
                genBtn.Click += (_, _) => GenAll();
                side.Controls.Add(genBtn);
                side.Controls.Add(_cardFlow);
                side.Controls.Add(sideHead);
                var div = new Panel
                {
                    Dock = DockStyle.Left,
                    Width = 1,
                    BackColor = Pal.Border
                };
                _editor = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    AutoScroll = true
                };
                ShowPh();
                content.Controls.Add(_editor);
                content.Controls.Add(div);
                content.Controls.Add(side);
                Controls.Add(content);
                Controls.Add(logBar);
                Controls.Add(strip);
                Controls.Add(bar);
            }
            private void RebuildCards()
            {
                _cardFlow.SuspendLayout();
                _cardFlow.Controls.Clear();
                foreach (var c in _capes)
                {
                    var card = MakeCard(c);
                    if (c == _sel) card.Selected = true;
                    _cardFlow.Controls.Add(card);
                }
                _cardFlow.ResumeLayout();
            }
            private GlowCard MakeCard(CapeDefinition cape)
            {
                var card = new GlowCard
                {
                    Width = 198,
                    Height = 54,
                    Margin = new Padding(0, 0, 0, 3),
                    Cursor = Cursors.Hand
                };
                card.Tag = cape;
                var nameLbl = L(cape.Name, new Font("Segoe UI", 9f, FontStyle.Bold), Pal.TxtPrim);
                nameLbl.Location = new Point(10, 7); nameLbl.Size = new Size(156, 20);
                var rarLbl = L(cape.Rarity.ToUpper(), new Font("Segoe UI", 6.5f), Pal.Rarity(cape.Rarity));
                rarLbl.Location = new Point(10, 29); rarLbl.Size = new Size(120, 16);
                var del = new Button
                {
                    Text = "✕",
                    Size = new Size(22, 22),
                    Location = new Point(169, 16),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Pal.TxtMuted,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI", 8f)
                };
                del.FlatAppearance.BorderSize = 0;
                del.Click += (_, _) => DelCape(cape);
                del.MouseEnter += (_, _) => del.ForeColor = Pal.AccentRed;
                del.MouseLeave += (_, _) => del.ForeColor = Pal.TxtMuted;
                card.Controls.Add(nameLbl);
                card.Controls.Add(rarLbl);
                card.Controls.Add(del);
                EventHandler sel = (_, _) =>
                {
                    foreach (Control c in _cardFlow.Controls)
                        if (c is GlowCard gc) gc.Selected = false;
                    card.Selected = true;
                    SelCape(cape);
                };
                card.Click += sel; nameLbl.Click += sel; rarLbl.Click += sel;
                return card;
            }
            private void ShowPh()
            {
                _editor.Controls.Clear();
                var ph = L("Select or add a cape", new Font("Segoe UI", 11f), Pal.TxtMuted);
                ph.Dock = DockStyle.Fill; ph.TextAlign = ContentAlignment.MiddleCenter;
                _editor.Controls.Add(ph);
            }
            private void SelCape(CapeDefinition cape) { _sel = cape; BuildEditor(cape); }
            private void BuildEditor(CapeDefinition cape)
            {
                _editor.SuspendLayout();
                _editor.Controls.Clear();
                var outer = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = Color.Transparent
                };
                _editor.Controls.Add(outer);
                var inner = new Panel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = Color.Transparent,
                    Location = new Point(0, 0)
                };
                outer.SizeChanged += (_, _) => inner.Width = outer.Width - (outer.VerticalScroll.Visible ? 18 : 2);
                inner.Width = _editor.Width - 20;
                outer.Controls.Add(inner);
                int y = 16;
                const int lx = 16, fx = 132, fw_base = 300;
                var prev = new GLCapePreview { Size = new Size(180, 260), Location = new Point(lx, y) };
                if (!string.IsNullOrEmpty(cape.ImagePath) && File.Exists(cape.ImagePath))
                    try { prev.CapeTexture = new Bitmap(cape.ImagePath); } catch { }
                var thumb = new ThumbPreview { Size = new Size(105, 105), Location = new Point(lx + 190, y) };
                thumb.Url = cape.ThumbnailUrl;
                int btnY = y + 265;
                var texBtn = Pill("Texture", 108);
                texBtn.Location = new Point(lx, btnY);
                texBtn.Click += (_, _) => PickTex(cape, prev, thumb);
                inner.Controls.Add(prev);
                inner.Controls.Add(thumb);
                inner.Controls.Add(texBtn);
                y += 302;
                void Field(string label, string val, bool ro, Action<string>? changed)
                {
                    var lbl = L(label, new Font("Segoe UI", 7.5f, FontStyle.Bold), Pal.TxtDim);
                    lbl.Location = new Point(lx, y + 2); lbl.AutoSize = true;
                    var tb = new TextBox
                    {
                        Text = val,
                        ReadOnly = ro,
                        BackColor = ro ? Color.FromArgb(10, 6, 20) : Color.FromArgb(17, 11, 36),
                        ForeColor = ro ? Pal.TxtDim : Pal.TxtPrim,
                        BorderStyle = BorderStyle.FixedSingle,
                        Font = new Font("Segoe UI", 9f),
                        Location = new Point(fx, y),
                        Width = fw_base
                    };
                    if (changed != null) tb.TextChanged += (_, _) => changed(tb.Text);
                    inner.Controls.Add(lbl);
                    inner.Controls.Add(tb);
                    y += 28;
                }
                Field("Name", cape.Name, false, v =>
                {
                    cape.Name = v;
                    foreach (Control c in _cardFlow.Controls)
                    {
                        if (c is GlowCard gc && gc.Tag == (object)cape)
                        {
                            foreach (Control lc in gc.Controls)
                                if (lc is Label nl && nl.Font.Bold) { nl.Text = v; break; }
                            gc.Invalidate();
                            break;
                        }
                    }
                });
                Field("Creator", cape.CreatorName, false, v => cape.CreatorName = v);
                Field("Thumbnail URL", cape.ThumbnailUrl ?? "", false, v => { cape.ThumbnailUrl = v; thumb.Url = v; });
                var rLbl = L("Rarity", new Font("Segoe UI", 7.5f, FontStyle.Bold), Pal.TxtDim);
                rLbl.Location = new Point(lx, y + 2); rLbl.AutoSize = true;
                var combo = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = Color.FromArgb(17, 11, 36),
                    ForeColor = Pal.TxtPrim,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9f),
                    Location = new Point(fx, y),
                    Width = 145
                };
                combo.Items.AddRange(new[] { "common", "rare", "epic", "legendary" });
                combo.SelectedItem = cape.Rarity;
                combo.SelectedIndexChanged += (_, _) =>
                {
                    cape.Rarity = combo.SelectedItem?.ToString() ?? "rare";
                    foreach (Control c in _cardFlow.Controls)
                    {
                        if (c is GlowCard gc && gc.Tag == (object)cape)
                        {
                            foreach (Control lc in gc.Controls)
                                if (lc is Label rl && !rl.Font.Bold)
                                { rl.Text = cape.Rarity.ToUpper(); rl.ForeColor = Pal.Rarity(cape.Rarity); break; }
                            gc.Invalidate();
                            break;
                        }
                    }
                };
                inner.Controls.Add(rLbl); inner.Controls.Add(combo);
                y += 28;
                Field("Item ID", cape.ItemId, true, null);
                Field("Piece UUID", cape.PieceUuid, true, null);
                y += 6;
                var saveBtn = Pill("💾  Save", 112);
                saveBtn.Location = new Point(fx, y);
                saveBtn.Click += (_, _) => { SaveCfg(); Log("Saved: " + cape.Name, Pal.AccentGrn); };
                inner.Controls.Add(saveBtn);
                inner.Height = y + 50;
                _editor.ResumeLayout(true);
            }
            private void PickTex(CapeDefinition cape, GLCapePreview prev, ThumbPreview thumb)
            {
                using var dlg = new OpenFileDialog
                { Filter = "PNG Images|*.png", Title = "Select Cape Texture (64×32 recommended)" };
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var img = new Bitmap(dlg.FileName);
                    if (img.Width != 64 || img.Height != 32)
                        if (MessageBox.Show($"Image is {img.Width}×{img.Height}, expected 64×32. Continue?",
                            "Size mismatch", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        { img.Dispose(); return; }
                    string dest = Path.Combine(OutPath, "textures", $"{cape.ItemId}_cape.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    img.Save(dest, ImageFormat.Png);
                    cape.ImagePath = dest;
                    prev.CapeTexture = img;
                    Log("Texture set: " + cape.Name, Pal.AccentBlue);
                }
                catch (Exception ex) { Log("Texture error: " + ex.Message, Pal.AccentRed); }
            }
            private void AddCape()
            {
                var c = new CapeDefinition { Name = "New Cape", CreatorName = Environment.UserName };
                _capes.Add(c);
                SaveCfg();
                RebuildCards();
                if (_cardFlow.Controls.Count > 0)
                {
                    var last = _cardFlow.Controls[_cardFlow.Controls.Count - 1] as GlowCard;
                    if (last != null) { last.Selected = true; SelCape(c); }
                }
            }
            private void DelCape(CapeDefinition cape)
            {
                if (MessageBox.Show($"Remove \"{cape.Name}\"?", "Confirm",
                    MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                _capes.Remove(cape);
                if (_sel == cape) { _sel = null; ShowPh(); }
                SaveCfg(); RebuildCards();
            }
            private void GenAll()
            {
                if (_capes.Count == 0) { Log("No capes configured.", Pal.AccentRed); return; }
                try
                {
                    Cursor = Cursors.WaitCursor;
                    Log("═══ Generation started ═══", Pal.AccentBlue);
                    foreach (var c in _capes)
                    {
                        Log($"Building {c.Name}…", Pal.TxtPrim);
                        GenPkg(c);
                        Log($"✓ {c.Name}", Pal.AccentGrn);
                    }
                    GenCapesJson();
                    Log("✓ capesv2.json written", Pal.AccentGrn);
                    Log("═══ Done! ═══", Pal.AccentBlue);
                    MessageBox.Show($"{_capes.Count} cape(s) generated!",
                        "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log("✗ " + ex.Message, Pal.AccentRed);
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { Cursor = Cursors.Default; }
            }
            private void GenPkg(CapeDefinition cape)
            {
                string work = Path.Combine(OutPath, "temp", cape.ItemId);
                Directory.CreateDirectory(work);
                try
                {
                    var mf = new JObject
                    {
                        ["format_version"] = 1,
                        ["header"] = new JObject
                        {
                            ["description"] = cape.Description ?? "pack.description",
                            ["name"] = cape.Name,
                            ["uuid"] = cape.PieceUuid,
                            ["version"] = new JArray { 1, 1, 0 }
                        },
                        ["modules"] = new JArray { new JObject {
                        ["type"] = "persona_piece",
                        ["uuid"] = Guid.NewGuid().ToString(),
                        ["version"] = new JArray { 1, 1, 0 }
                    }}
                    };
                    File.WriteAllText(Path.Combine(work, "manifest.json"), mf.ToString(Formatting.Indented));
                    if (string.IsNullOrEmpty(cape.ImagePath) || !File.Exists(cape.ImagePath))
                        throw new FileNotFoundException("Cape image not found: " + cape.ImagePath);
                    string cf = $"{cape.ItemId}_cape.png";
                    File.Copy(cape.ImagePath, Path.Combine(work, cf), true);
                    var meta = new JObject
                    {
                        ["piece_id"] = cape.PieceUuid,
                        ["piece_name"] = $"{cape.ItemId}_cape",
                        ["piece_type"] = "persona_capes",
                        ["zone"] = new JArray { "body_back_upper", "body_back_lower" },
                        ["texture_sources"] = new JArray { new JObject { ["texture"] = cf } }
                    };
                    File.WriteAllText(Path.Combine(work, $"{cape.ItemId}_cape.meta.json"),
                        meta.ToString(Formatting.Indented));
                    string td = Path.Combine(work, "texts");
                    Directory.CreateDirectory(td);
                    File.WriteAllText(Path.Combine(td, "languages.json"), "[\"en_US\"]");
                    File.WriteAllText(Path.Combine(td, "en_US.lang"),
                        $"persona.{cape.ItemId}_cape.title={cape.Name}\n", Encoding.UTF8);
                    var cnt = new JObject
                    {
                        ["content"] = new JArray {
                    new JObject { ["path"] = cf },
                    new JObject { ["path"] = $"{cape.ItemId}_cape.meta.json" },
                    new JObject { ["path"] = "texts/languages.json" },
                    new JObject { ["path"] = "texts/en_US.lang" }
                }
                    };
                    File.WriteAllText(Path.Combine(work, "contents.json"), cnt.ToString(Formatting.Indented));
                    SignManifest(work);
                    CapeEncryption.EncryptContents(work, cape.PieceUuid, "s5s5ejuDru4uchuF2drUFuthaspAbepE");
                    string ppack = Path.Combine(work, "ppack0.zip");
                    using (var z = ZipFile.Open(ppack, ZipArchiveMode.Create))
                        foreach (string f in Directory.GetFiles(work, "*", SearchOption.AllDirectories))
                        { if (f == ppack) continue; z.CreateEntryFromFile(f, f[(work.Length + 1)..]); }
                    string pri = Path.Combine(OutPath, "zips", $"{cape.ItemId}_primary.zip");
                    Directory.CreateDirectory(Path.GetDirectoryName(pri)!);
                    if (File.Exists(pri)) File.Delete(pri);
                    using (var z2 = ZipFile.Open(pri, ZipArchiveMode.Create))
                        z2.CreateEntryFromFile(ppack, "ppack0.zip");
                    cape.ZipFilePath = pri; SaveCfg();
                }
                finally { try { Directory.Delete(work, true); } catch { } }
            }
            private void GenCapesJson()
            {
                var root = File.Exists(CapesJson)
                    ? JObject.Parse(File.ReadAllText(CapesJson))
                    : CapesTemplate();
                var rows = (JArray)root["result"]!["rows"]!;
                JArray? items = null;
                foreach (var row in rows)
                    if (row["controlId"]?.ToString() == "GridList")
                        foreach (var comp in (JArray)row["components"]!)
                            if (comp["type"]?.ToString() == "itemListComp")
                            { items = (JArray)comp["items"]!; break; }
                if (items == null) throw new Exception("Cannot find items array");
                var ids = new HashSet<string>(_capes.Select(c => c.ItemId));
                for (int i = items.Count - 1; i >= 0; i--)
                    if (ids.Contains(items[i]["id"]?.ToString() ?? "")) items.RemoveAt(i);
                foreach (var c in _capes) items.Add(MkItem(c));
                foreach (var row in rows)
                    if (row["controlId"]?.ToString() == "GridList")
                        foreach (var comp in (JArray)row["components"]!)
                            if (comp["type"]?.ToString() == "itemListComp")
                            {
                                comp["totalItems"] = items.Count;
                                if (comp["customStoreRowConfiguration"] is JObject config)
                                    config["maxOffers"] = items.Count;
                                break;
                            }
                File.WriteAllText(CapesJson, root.ToString(Formatting.Indented));
            }
            private static JObject CapesTemplate() => JObject.Parse(
                @"{""result"":{""id"":""9635ac1f-8ea3-4bb2-a43c-9b158b3382d1"",
""pageId"":""DressingRoom_Capes"",""addToRecentlyViewed"":false,""pageName"":""Home L1"",
""rows"":[{""controlId"":""Layout"",""components"":[]},{""controlId"":""GridList"",
""components"":[{""text"":{""value"":""dr.collector_title.owned""},""type"":""headerComp""},
{""items"":[],""totalItems"":0,""type"":""itemListComp""}]}],
""inventoryVersion"":""1/MTQ1"",""sidebarLayoutType"":""Persona""}}");
            private static JObject MkItem(CapeDefinition c) => new()
            {
                ["id"] = c.ItemId,
                ["contentType"] = "PersonaDurable",
                ["title"] = c.Name,
                ["description"] = c.Description,
                ["creatorName"] = c.CreatorName,
                ["thumbnail"] = new JObject
                {
                    ["tag"] = "Thumbnail",
                    ["type"] = "Thumbnail",
                    ["url"] = c.ThumbnailUrl ?? "",
                    ["urlWithResolution"] = c.ThumbnailUrl ?? ""
                },
                ["rating"] = new JObject { ["average"] = 4.5, ["totalCount"] = 10 },
                ["price"] = new JObject
                {
                    ["listPrice"] = 0,
                    ["realmsInfo"] = new JObject { ["inRealmsPlus"] = false },
                    ["currencyId"] = "ecd19d3c-7635-402c-a185-eb11cb6c6946",
                    ["virtualCurrencyType"] = "Minecoin"
                },
                ["linksTo"] = $"ItemDetail_{c.ItemId}?selectedItemId={c.ItemId}",
                ["pieceType"] = "persona_capes",
                ["rarity"] = c.Rarity,
                ["ownership"] = "Purchased",
                ["packType"] = "Persona",
                ["packIdentity"] = new JArray { new JObject {
                ["type"] = "persona_piece", ["uuid"] = c.PieceUuid, ["version"] = "1.2.0" }},
                ["tags"] = new JArray(),
                ["images"] = new JArray { new JObject {
                ["tag"] = "Thumbnail", ["type"] = "Thumbnail", ["url"] = c.ThumbnailUrl ?? "" }},
                ["contents"] = new JArray(),
                ["platformRestricted"] = false,
                ["startDate"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
            private async Task StartProxy()
            {
                try
                {
                    Log("Starting proxy…", Pal.AccentBlue);
                    _proxy = new ProxyServer();
                    _proxy.CertificateManager.SaveFakeCertificates = true;
                    _proxy.CertificateManager.CreateRootCertificate();
                    await InstallCert();
                    var ep = new ExplicitProxyEndPoint(IPAddress.Any, 8080, true);
                    _proxy.AddEndPoint(ep);
                    _proxy.BeforeRequest += OnReq;
                    _proxy.BeforeResponse += OnRes;
                    _proxy.Start();
                    _proxy.SetAsSystemProxy(ep, ProxyProtocolType.AllHttp);
                    _proxyOn = true;
                    UpdateProxyUI();
                    Log("System proxy set automatically (:8080)", Pal.AccentGrn);
                }
                catch (Exception ex) { Log("✗ " + ex.Message, Pal.AccentRed); StopProxy(); }
            }
            private void StopProxy()
            {
                try
                {
                    if (_proxy != null)
                    {
                        _proxy.DisableAllSystemProxies();
                        _proxy.BeforeRequest -= OnReq;
                        _proxy.BeforeResponse -= OnRes;
                        _proxy.Stop();
                        _proxy.Dispose();
                        _proxy = null;
                    }
                }
                catch { }
                _proxyOn = false;
                UpdateProxyUI();
                Log("Proxy stopped. System proxy restored automatically.", Pal.AccentRed);
            }
            private void UpdateProxyUI()
            {
                if (InvokeRequired) { Invoke(UpdateProxyUI); return; }
                if (_proxyOn)
                {
                    _proxyStat.Text = "● Active  :8080  (system-wide)";
                    _proxyStat.ForeColor = Pal.AccentGrn;
                    _proxyBtn.Text = "Stop Proxy";
                    _proxyBtn.BackColor = Pal.BtnStop;
                }
                else
                {
                    _proxyStat.Text = "● Proxy off";
                    _proxyStat.ForeColor = Pal.AccentRed;
                    _proxyBtn.Text = "Start Proxy";
                    _proxyBtn.BackColor = Pal.BtnBg;
                }
            }
            private async Task OnReq(object _, SessionEventArgs e)
            {
                string url = e.HttpClient.Request.RequestUri.AbsoluteUri;
                if (url == TargetUrl) { Log("[→] Capes store", Pal.AccentBlue); return; }
                if (url == PlayfabUrl)
                {
                    if (e.HttpClient.Request.Method == "POST" && e.HttpClient.Request.HasBody)
                        try
                        {
                            string id = JObject.Parse(await e.GetRequestBodyAsString())["ItemId"]?.ToString() ?? "";
                            if (_capes.Any(c => c.ItemId == id))
                            { Log($"[→] PlayFab {id}", Pal.AccentBlue); _pfPend[e] = id; }
                        }
                        catch { }
                    return;
                }
                if (url.Contains("xforgeassets") && url.EndsWith("/primary.zip"))
                {
                    var segs = new Uri(url).Segments;
                    string uid = segs.Length >= 2 ? segs[^2].TrimEnd('/') : "";
                    if (_zipIds.Contains(uid) && _zipItem.TryGetValue(uid, out string? iid))
                    {
                        var cap = _capes.Find(c => c.ItemId == iid);
                        if (cap?.ZipFilePath != null && File.Exists(cap.ZipFilePath))
                        {
                            byte[] dat = await File.ReadAllBytesAsync(cap.ZipFilePath);
                            var h = new HeaderCollection();
                            h.AddHeader("Content-Type", "application/zip");
                            h.AddHeader("Content-Length", dat.Length.ToString());
                            e.Ok(dat, h);
                            Log($"[✓] Served zip: {cap.Name}", Pal.AccentGrn);
                        }
                    }
                }
            }
            private async Task OnRes(object _, SessionEventArgs e)
            {
                string url = e.HttpClient.Request.RequestUri.AbsoluteUri;
                if (url == TargetUrl && File.Exists(CapesJson))
                {
                    e.Ok(await File.ReadAllTextAsync(CapesJson, Encoding.UTF8));
                    e.HttpClient.Response.Headers.AddHeader("Content-Type", "application/json");
                    Log("[✓] capesv2.json served", Pal.AccentGrn);
                    return;
                }
                if (url == PlayfabUrl && _pfPend.TryGetValue(e, out string? iid))
                {
                    var cap = _capes.Find(c => c.ItemId == iid);
                    if (cap != null)
                    {
                        string zu = Guid.NewGuid().ToString();
                        _zipIds.Add(zu); _zipItem[zu] = cap.ItemId;
                        var resp = new JObject
                        {
                            ["code"] = 200,
                            ["status"] = "OK",
                            ["data"] = new JObject
                            {
                                ["Item"] = new JObject
                                {
                                    ["Id"] = cap.ItemId,
                                    ["Type"] = "bundle",
                                    ["Title"] = new JObject { ["NEUTRAL"] = cap.Name },
                                    ["Description"] = new JObject { ["NEUTRAL"] = cap.Description },
                                    ["ContentType"] = "PersonaDurable",
                                    ["Contents"] = new JArray { new JObject {
                                ["Id"]  = zu,
                                ["Url"] = $"https://xforgeassets001.xboxlive.com/pf-namespace-MUEPXTH6QO/{zu}/primary.zip",
                                ["Type"] = "personabinary" }},
                                    ["DisplayProperties"] = new JObject
                                    {
                                        ["pieceType"] = "persona_capes",
                                        ["rarity"] = cap.Rarity
                                    }
                                }
                            }
                        };
                        e.Ok(resp.ToString());
                        e.HttpClient.Response.Headers.AddHeader("Content-Type", "application/json");
                        Log($"[✓] PlayFab served: {cap.Name}", Pal.AccentGrn);
                    }
                    _pfPend.Remove(e);
                }
            }
            private async Task InstallCert()
            {
                var cert = _proxy?.CertificateManager.RootCertificate;
                if (cert == null) return;
                try
                {
                    using var st = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                    st.Open(OpenFlags.ReadWrite);
                    if (st.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).Count == 0)
                    { st.Add(cert); Log("[cert] Installed (CurrentUser)", Pal.AccentBlue); }
                    else Log("[cert] Already trusted", Pal.TxtDim);
                    st.Close();
                }
                catch (Exception ex) { Log("[cert] " + ex.Message + " — try as Admin", Pal.AccentRed); }
            }
            private void LoadCfg()
            {
                if (!File.Exists(CfgPath)) return;
                try
                {
                    _capes = JsonConvert.DeserializeObject<List<CapeDefinition>>(
                                 File.ReadAllText(CfgPath)) ?? new();
                    if (_capes.Count > 0) _sel = _capes[0];
                }
                catch (Exception ex) { Log("Config error: " + ex.Message, Pal.AccentRed); }
            }
            private void SaveCfg()
            {
                try { File.WriteAllText(CfgPath, JsonConvert.SerializeObject(_capes, Formatting.Indented)); }
                catch (Exception ex) { Log("Save error: " + ex.Message, Pal.AccentRed); }
            }
            private void Log(string msg, Color col)
            {
                if (InvokeRequired) { Invoke(() => Log(msg, col)); return; }
                _log.SelectionStart = _log.TextLength;
                _log.SelectionColor = col;
                _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                _log.SelectionColor = _log.ForeColor;
                _log.ScrollToCaret();
            }
            private static Button TBar(string t, Color hov)
            {
                var b = new Button
                {
                    Text = t,
                    Width = 42,
                    Dock = DockStyle.Right,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Pal.TxtDim,
                    Font = new Font("Segoe UI", 10.5f),
                    Cursor = Cursors.Hand
                };
                b.FlatAppearance.BorderSize = 0;
                b.MouseEnter += (_, _) => b.ForeColor = hov;
                b.MouseLeave += (_, _) => b.ForeColor = Pal.TxtDim;
                return b;
            }
            private static Button Pill(string t, int w)
            {
                var b = new Button
                {
                    Text = t,
                    Width = w,
                    Height = 27,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Pal.BtnBg,
                    ForeColor = Pal.TxtPrim,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Pal.Border;
                b.MouseEnter += (_, _) => b.BackColor = Pal.BtnHov;
                b.MouseLeave += (_, _) => b.BackColor = Pal.BtnBg;
                return b;
            }
            private static Button FBtn(string t, int w)
            {
                var b = new Button
                {
                    Text = t,
                    Width = w,
                    Height = 27,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Pal.TxtDim,
                    Font = new Font("Segoe UI", 8f),
                    Cursor = Cursors.Hand
                };
                b.FlatAppearance.BorderSize = 0;
                b.MouseEnter += (_, _) => b.ForeColor = Pal.TxtPrim;
                b.MouseLeave += (_, _) => b.ForeColor = Pal.TxtDim;
                return b;
            }
            private static Label L(string text, Font font, Color fg) => new()
            {
                Text = text,
                Font = font,
                ForeColor = fg,
                BackColor = Color.Transparent,
                AutoSize = true
            };
        }
        public class CapeDefinition
        {
            public string ItemId { get; set; } = Guid.NewGuid().ToString();
            public string PieceUuid { get; set; } = Guid.NewGuid().ToString();
            public string Name { get; set; } = "New Cape";
            public string Description { get; set; } = "";
            public string CreatorName { get; set; } = "";
            public string? ThumbnailUrl { get; set; }
            public string Rarity { get; set; } = "rare";
            public string? ImagePath { get; set; }
            public string? ZipFilePath { get; set; }
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
}