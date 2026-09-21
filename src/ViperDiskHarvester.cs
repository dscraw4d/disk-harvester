using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace ViperDiskHarvester
{
    public class MainForm : Form
    {
        private const string RootUrl = "https://mirrors.apple2.org.za/ftp.apple.asimov.net/";
        private const int CrawlDelayMs = 200;
        private const int DownloadDelayMs = 125;

        private static readonly string[] Extensions = new string[]
        {
            ".dsk", ".woz", ".po", ".nib", ".hdv"
        };

        private static readonly Dictionary<string, string> TypeFolderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".dsk", "DSK" },
            { ".woz", "WOZ" },
            { ".po",  "PO"  },
            { ".nib", "NIB" },
            { ".hdv", "HDV" }
        };

        private TextBox txtRoot;
        private TextBox txtDest;
        private Button btnDest;
        private Button btnScan;
        private Button btnDownload;
        private Button btnStop;
        private Button btnOpen;
        private Button btnClear;
        private Label lblStatus;
        private Label lblCounts;
        private ProgressBar progressOverall;
        private ProgressBar progressFile;
        private TextBox log;
        private PictureBox logo;

        private BackgroundWorker scanWorker;
        private BackgroundWorker downloadWorker;

        private Dictionary<string, List<string>> foundByExt = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private List<DownloadItem> downloadItems = new List<DownloadItem>();

        private static readonly Regex HrefRegex = new Regex(
            "<a\\s+(?:[^>]*?\\s+)?href\\s*=\\s*([\"'])(.*?)\\1",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public MainForm()
        {
            InitializeTheme();
            InitializeUi();
            LoadEmbeddedLogo();
            BuildWorkers();
        }

        private void InitializeTheme()
        {
            BackColor = Color.FromArgb(7, 10, 7);
            ForeColor = Color.FromArgb(95, 255, 95);
            Font = new Font("Consolas", 9F);
            Text = "Viper Disk Harvester v1.0.2";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(960, 760);
            MinimumSize = new Size(960, 760);
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
        }

        private Label MakeLabel(string text, int x, int y, float size = 9F, FontStyle style = FontStyle.Regular)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            label.ForeColor = Color.FromArgb(95, 255, 95);
            label.BackColor = Color.Transparent;
            label.Font = new Font("Consolas", size, style);
            return label;
        }

        private Button MakeButton(string text, int x, int y, int w, int h)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(0, 220, 0);
            b.FlatAppearance.BorderSize = 1;
            b.BackColor = Color.FromArgb(14, 20, 14);
            b.ForeColor = Color.FromArgb(95, 255, 95);
            b.Font = new Font("Consolas", 9F, FontStyle.Bold);
            b.UseVisualStyleBackColor = false;
            return b;
        }

        private TextBox MakeTextBox(int x, int y, int w, string text, bool readOnly = false)
        {
            TextBox tb = new TextBox();
            tb.Location = new Point(x, y);
            tb.Size = new Size(w, 24);
            tb.Text = text;
            tb.ReadOnly = readOnly;
            tb.BackColor = Color.FromArgb(0, 0, 0);
            tb.ForeColor = Color.FromArgb(120, 255, 120);
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Font = new Font("Consolas", 9F);
            return tb;
        }

        private void InitializeUi()
        {
            logo = new PictureBox();
            logo.Location = new Point(18, 12);
            logo.Size = new Size(900, 180);
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = Color.Transparent;
            Controls.Add(logo);

            Controls.Add(MakeLabel("Crawl the full Asimov Apple II mirror and harvest every .DSK, .WOZ, .PO, .NIB, and .HDV file.", 22, 190, 9.5F));
            Controls.Add(MakeLabel("Mirror root:", 20, 220, 9F, FontStyle.Bold));

            txtRoot = MakeTextBox(23, 242, 820, RootUrl, true);
            Controls.Add(txtRoot);

            Controls.Add(MakeLabel("Destination root (each file type is placed in its own subfolder):", 20, 278, 9F, FontStyle.Bold));
            txtDest = MakeTextBox(23, 300, 700, @"E:\Apple II\AsimovHarvest");
            Controls.Add(txtDest);

            btnDest = MakeButton("Browse...", 735, 297, 108, 30);
            btnDest.Click += delegate { BrowseDestination(); };
            Controls.Add(btnDest);

            btnScan = MakeButton("1. SCAN ENTIRE SITE", 23, 347, 195, 40);
            btnScan.Click += delegate { StartScan(); };
            Controls.Add(btnScan);

            btnDownload = MakeButton("2. DOWNLOAD ALL", 228, 347, 190, 40);
            btnDownload.Enabled = false;
            btnDownload.Click += delegate { StartDownload(); };
            Controls.Add(btnDownload);

            btnStop = MakeButton("STOP", 428, 347, 90, 40);
            btnStop.Enabled = false;
            btnStop.Click += delegate { StopWork(); };
            Controls.Add(btnStop);

            btnOpen = MakeButton("OPEN DESTINATION", 528, 347, 165, 40);
            btnOpen.Click += delegate { OpenDestination(); };
            Controls.Add(btnOpen);

            btnClear = MakeButton("CLEAR LOG", 703, 347, 140, 40);
            btnClear.Click += delegate { log.Clear(); };
            Controls.Add(btnClear);

            lblStatus = MakeLabel("READY.", 23, 400, 10F, FontStyle.Bold);
            Controls.Add(lblStatus);

            lblCounts = MakeLabel("Pages scanned: 0 | DSK: 0 | WOZ: 0 | PO: 0 | NIB: 0 | HDV: 0", 23, 425, 9F);
            Controls.Add(lblCounts);

            Controls.Add(MakeLabel("Overall:", 23, 455));
            progressOverall = new ProgressBar();
            progressOverall.Location = new Point(90, 453);
            progressOverall.Size = new Size(753, 22);
            progressOverall.Minimum = 0;
            progressOverall.Maximum = 100;
            Controls.Add(progressOverall);

            Controls.Add(MakeLabel("Current file:", 23, 486));
            progressFile = new ProgressBar();
            progressFile.Location = new Point(120, 484);
            progressFile.Size = new Size(723, 22);
            progressFile.Minimum = 0;
            progressFile.Maximum = 100;
            Controls.Add(progressFile);

            log = new TextBox();
            log.Location = new Point(23, 520);
            log.Size = new Size(820, 170);
            log.Multiline = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.ReadOnly = true;
            log.Font = new Font("Consolas", 9F);
            log.BackColor = Color.Black;
            log.ForeColor = Color.FromArgb(100, 255, 100);
            Controls.Add(log);

            Controls.Add(MakeLabel("APPLE II HACKER MODE: black background, green phosphor text, polite single-thread crawling, .partial download safety, no overwrites.", 23, 705, 8.5F));
        }

        private void LoadEmbeddedLogo()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream("ViperDiskHarvester.Branding.WozReaperLogo"))
                {
                    if (stream == null)
                    {
                        logo.Visible = false;
                        return;
                    }

                    using (Image image = Image.FromStream(stream))
                    {
                        logo.Image = new Bitmap(image);
                    }
                }
            }
            catch
            {
                logo.Visible = false;
            }
        }

        private void BuildWorkers()
        {
            scanWorker = new BackgroundWorker();
            scanWorker.WorkerReportsProgress = true;
            scanWorker.WorkerSupportsCancellation = true;
            scanWorker.DoWork += ScanWorker_DoWork;
            scanWorker.ProgressChanged += ScanWorker_ProgressChanged;
            scanWorker.RunWorkerCompleted += ScanWorker_Completed;

            downloadWorker = new BackgroundWorker();
            downloadWorker.WorkerReportsProgress = true;
            downloadWorker.WorkerSupportsCancellation = true;
            downloadWorker.DoWork += DownloadWorker_DoWork;
            downloadWorker.ProgressChanged += DownloadWorker_ProgressChanged;
            downloadWorker.RunWorkerCompleted += DownloadWorker_Completed;
        }

        private void BrowseDestination()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Choose the root folder that will receive DSK/WOZ/PO/NIB/HDV subfolders";
                if (Directory.Exists(txtDest.Text))
                    dlg.SelectedPath = txtDest.Text;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtDest.Text = dlg.SelectedPath;
            }
        }

        private void StartScan()
        {
            if (scanWorker.IsBusy || downloadWorker.IsBusy)
                return;

            foundByExt.Clear();
            downloadItems.Clear();
            foreach (string ext in Extensions)
                foundByExt[ext] = new List<string>();

            btnDownload.Enabled = false;
            progressOverall.Value = 0;
            progressFile.Value = 0;
            btnScan.Enabled = false;
            btnDest.Enabled = false;
            btnStop.Enabled = true;
            lblStatus.Text = "STARTING FULL-MIRROR SCAN...";
            UpdateCountLabel(0, 0);
            Log("VIPER DISK HARVESTER STARTING.");
            Log("Root: " + RootUrl);
            Log("Target extensions: " + String.Join(", ", Extensions));
            Log("Crawler is restricted to ftp.apple.asimov.net.");
            scanWorker.RunWorkerAsync();
        }

        private void ScanWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = (BackgroundWorker)sender;
            Uri baseUri = new Uri(RootUrl);
            string allowedHost = baseUri.Host;
            string allowedPath = baseUri.AbsolutePath;

            Queue<Uri> queue = new Queue<Uri>();
            HashSet<string> queued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HashSet<string>> filesByExt = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string ext in Extensions)
                filesByExt[ext] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            queue.Enqueue(baseUri);
            queued.Add(baseUri.AbsoluteUri);
            int pages = 0;
            int errors = 0;

            while (queue.Count > 0)
            {
                if (bw.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }

                Uri current = queue.Dequeue();
                if (!visited.Add(current.AbsoluteUri))
                    continue;

                string html = null;
                Exception last = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        html = DownloadText(current);
                        last = null;
                        break;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        Thread.Sleep(800 * attempt);
                    }
                }

                pages++;
                if (html == null)
                {
                    errors++;
                    bw.ReportProgress(0, new ScanProgress(pages, queue.Count, filesByExt, "Could not read directory after retries: " + current.AbsoluteUri + (last == null ? "" : " :: " + last.Message)));
                    continue;
                }

                MatchCollection matches = HrefRegex.Matches(html);
                foreach (Match m in matches)
                {
                    if (bw.CancellationPending)
                    {
                        e.Cancel = true;
                        break;
                    }

                    string href = WebUtility.HtmlDecode(m.Groups[2].Value).Trim();
                    if (href.Length == 0 || href.StartsWith("#") || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Uri child;
                    try { child = new Uri(current, href); }
                    catch { continue; }

                    if (!(child.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || child.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
                        continue;
                    if (!child.Host.Equals(allowedHost, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!child.AbsolutePath.StartsWith(allowedPath, StringComparison.Ordinal))
                        continue;
                    if (!String.IsNullOrEmpty(child.Query) || !String.IsNullOrEmpty(child.Fragment))
                        continue;
                    if (child.AbsoluteUri.Equals(current.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (child.AbsolutePath.EndsWith("/", StringComparison.Ordinal))
                    {
                        if (!visited.Contains(child.AbsoluteUri) && queued.Add(child.AbsoluteUri))
                            queue.Enqueue(child);
                    }
                    else
                    {
                        foreach (string ext in Extensions)
                        {
                            if (child.AbsolutePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                            {
                                filesByExt[ext].Add(child.AbsoluteUri);
                                break;
                            }
                        }
                    }
                }

                bw.ReportProgress(0, new ScanProgress(pages, queue.Count, filesByExt, "Scanned: " + current.AbsolutePath));
                Thread.Sleep(CrawlDelayMs);
            }

            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string ext in Extensions)
            {
                List<string> list = filesByExt[ext].ToList();
                list.Sort(StringComparer.OrdinalIgnoreCase);
                result[ext] = list;
            }
            e.Result = new ScanResult(result, pages, errors);
        }

        private void ScanWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ScanProgress p = e.UserState as ScanProgress;
            if (p == null) return;
            lblStatus.Text = p.Message.ToUpperInvariant();
            UpdateCountLabel(p.Pages, p.Queued, p.FilesByExt);
            if (p.Message.StartsWith("Could not read", StringComparison.OrdinalIgnoreCase))
                Log(p.Message);
        }

        private void ScanWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            btnScan.Enabled = true;
            btnDest.Enabled = true;
            btnStop.Enabled = false;

            if (e.Cancelled)
            {
                lblStatus.Text = "SCAN STOPPED.";
                Log("Scan stopped by user.");
                return;
            }
            if (e.Error != null)
            {
                lblStatus.Text = "SCAN FAILED.";
                Error(e.Error.Message);
                return;
            }

            ScanResult result = e.Result as ScanResult;
            if (result == null)
            {
                lblStatus.Text = "SCAN FAILED: NO RESULT.";
                return;
            }

            foundByExt = result.FilesByExt;
            downloadItems = BuildDownloadItems(foundByExt);
            int total = downloadItems.Count;
            lblStatus.Text = ("SCAN COMPLETE. FOUND " + total + " TARGET FILES.").ToUpperInvariant();
            UpdateCountLabel(result.Pages, 0, result.FilesByExt, result.Errors);
            Log("SCAN COMPLETE.");
            foreach (string ext in Extensions)
                Log(TypeFolderMap[ext] + ": " + result.FilesByExt[ext].Count + " file(s)");
            Log("TOTAL: " + total + " file(s)");
            if (result.Errors > 0)
                Log("Directory pages that failed after retries: " + result.Errors);
            btnDownload.Enabled = total > 0;

            try
            {
                string destRoot = NormalizeFolder(txtDest.Text);
                Directory.CreateDirectory(destRoot);
                SaveIndexFiles(destRoot, downloadItems);
                Log("Saved scan indexes to destination root.");
            }
            catch (Exception ex)
            {
                Log("Could not save index files yet: " + ex.Message);
            }

            MessageBox.Show(this,
                "Scan complete.\r\n\r\n" +
                "DSK: " + result.FilesByExt[".dsk"].Count + "\r\n" +
                "WOZ: " + result.FilesByExt[".woz"].Count + "\r\n" +
                "PO: " + result.FilesByExt[".po"].Count + "\r\n" +
                "NIB: " + result.FilesByExt[".nib"].Count + "\r\n" +
                "HDV: " + result.FilesByExt[".hdv"].Count + "\r\n\r\n" +
                "Total: " + total + "\r\n" +
                "Directory pages scanned: " + result.Pages + "\r\n" +
                "Directory errors after retries: " + result.Errors + "\r\n\r\n" +
                "Click DOWNLOAD ALL to begin.",
                "Viper Disk Harvester",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void StartDownload()
        {
            if (downloadWorker.IsBusy || scanWorker.IsBusy) return;
            if (downloadItems.Count == 0)
            {
                MessageBox.Show(this, "Scan the site first.", "Viper Disk Harvester", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string dest;
            try
            {
                dest = NormalizeFolder(txtDest.Text);
                Directory.CreateDirectory(dest);
                foreach (string sub in TypeFolderMap.Values)
                    Directory.CreateDirectory(Path.Combine(dest, sub));
            }
            catch (Exception ex)
            {
                Error("Cannot use destination folder: " + ex.Message);
                return;
            }

            DialogResult answer = MessageBox.Show(this,
                "Download " + downloadItems.Count + " file(s) into type subfolders under:\r\n\r\n" + dest + "\r\n\r\n" +
                "DSK, WOZ, PO, NIB, and HDV files will be separated into their own directories.\r\n" +
                "Existing completed files will be skipped. Name collisions are hash-suffixed.",
                "Start Download",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            btnScan.Enabled = false;
            btnDownload.Enabled = false;
            btnDest.Enabled = false;
            btnStop.Enabled = true;
            progressOverall.Value = 0;
            progressFile.Value = 0;
            downloadWorker.RunWorkerAsync(new DownloadJob(dest, new List<DownloadItem>(downloadItems)));
        }

        private void DownloadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = (BackgroundWorker)sender;
            DownloadJob job = (DownloadJob)e.Argument;
            int downloaded = 0, skipped = 0, failed = 0;
            int total = job.Items.Count;

            for (int i = 0; i < total; i++)
            {
                if (bw.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }

                DownloadItem item = job.Items[i];
                string targetDir = Path.Combine(job.Destination, item.TypeFolder);
                Directory.CreateDirectory(targetDir);
                string target = Path.Combine(targetDir, item.LocalName);
                string partial = target + ".partial";

                if (File.Exists(target) && new FileInfo(target).Length > 0)
                {
                    skipped++;
                    bw.ReportProgress(0, new DownloadProgress(i + 1, total, 100, downloaded, skipped, failed, "SKIP existing: " + item.TypeFolder + "\\" + item.LocalName));
                    continue;
                }

                bool ok = false;
                Exception last = null;
                for (int attempt = 1; attempt <= 3 && !ok; attempt++)
                {
                    if (bw.CancellationPending)
                    {
                        e.Cancel = true;
                        break;
                    }
                    try
                    {
                        if (File.Exists(partial)) File.Delete(partial);
                        DownloadOneFile(bw, item, partial, i + 1, total, downloaded, skipped, failed);
                        if (File.Exists(target)) throw new IOException("Target appeared during download and will not be overwritten.");
                        File.Move(partial, target);
                        ok = true;
                        downloaded++;
                        bw.ReportProgress(0, new DownloadProgress(i + 1, total, 100, downloaded, skipped, failed, "DOWNLOADED: " + item.TypeFolder + "\\" + item.LocalName));
                    }
                    catch (OperationCanceledException)
                    {
                        e.Cancel = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        if (File.Exists(partial)) { try { File.Delete(partial); } catch { } }
                        if (attempt < 3) Thread.Sleep(1000 * attempt);
                    }
                }

                if (e.Cancel) break;
                if (!ok)
                {
                    failed++;
                    bw.ReportProgress(0, new DownloadProgress(i + 1, total, 0, downloaded, skipped, failed, "FAILED: " + item.TypeFolder + "\\" + item.LocalName + (last == null ? "" : " :: " + last.Message)));
                }
                Thread.Sleep(DownloadDelayMs);
            }
            e.Result = new DownloadResult(downloaded, skipped, failed, total);
        }

        private void DownloadOneFile(BackgroundWorker bw, DownloadItem item, string partialPath, int itemNumber, int total, int downloaded, int skipped, int failed)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(item.Url);
            req.Method = "GET";
            req.UserAgent = "Viper-Disk-Harvester/1.0 (single-stream archival downloader)";
            req.AllowAutoRedirect = true;
            req.Timeout = 30000;
            req.ReadWriteTimeout = 30000;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                if ((int)resp.StatusCode < 200 || (int)resp.StatusCode >= 300)
                    throw new WebException("HTTP " + (int)resp.StatusCode + " " + resp.StatusDescription);
                long expected = resp.ContentLength;
                long written = 0;
                using (Stream input = resp.GetResponseStream())
                using (FileStream output = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                {
                    byte[] buffer = new byte[65536];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (bw.CancellationPending) throw new OperationCanceledException();
                        output.Write(buffer, 0, read);
                        written += read;
                        int pct = 0;
                        if (expected > 0) pct = (int)Math.Min(100L, (written * 100L) / expected);
                        bw.ReportProgress(0, new DownloadProgress(itemNumber, total, pct, downloaded, skipped, failed, "Downloading: " + item.TypeFolder + "\\" + item.LocalName));
                    }
                }
                if (expected >= 0 && written != expected)
                    throw new IOException("Size verification failed. Expected " + expected + " bytes but received " + written + ".");
            }
        }

        private void DownloadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            DownloadProgress p = e.UserState as DownloadProgress;
            if (p == null) return;
            int overall = p.Total > 0 ? (int)Math.Min(100L, ((long)p.Index * 100L) / p.Total) : 0;
            progressOverall.Value = Math.Max(0, Math.Min(100, overall));
            progressFile.Value = Math.Max(0, Math.Min(100, p.FilePercent));
            lblStatus.Text = p.Message.ToUpperInvariant();
            lblCounts.Text = "File " + p.Index + " of " + p.Total + " | Downloaded: " + p.Downloaded + " | Skipped: " + p.Skipped + " | Failed: " + p.Failed;
            if (p.Message.StartsWith("DOWNLOADED:", StringComparison.OrdinalIgnoreCase) || p.Message.StartsWith("FAILED:", StringComparison.OrdinalIgnoreCase) || p.Message.StartsWith("SKIP", StringComparison.OrdinalIgnoreCase))
                Log(p.Message);
        }

        private void DownloadWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            btnScan.Enabled = true;
            btnDest.Enabled = true;
            btnStop.Enabled = false;
            btnDownload.Enabled = downloadItems.Count > 0;
            progressFile.Value = 0;
            if (e.Cancelled)
            {
                lblStatus.Text = "DOWNLOAD STOPPED.";
                Log("Download stopped by user.");
                return;
            }
            if (e.Error != null)
            {
                lblStatus.Text = "DOWNLOAD FAILED.";
                Error(e.Error.Message);
                return;
            }
            DownloadResult r = e.Result as DownloadResult;
            if (r == null) return;
            progressOverall.Value = 100;
            lblStatus.Text = ("FINISHED. DOWNLOADED: " + r.Downloaded + " | SKIPPED: " + r.Skipped + " | FAILED: " + r.Failed).ToUpperInvariant();
            Log("DOWNLOAD JOB COMPLETE.");
            Log("Downloaded: " + r.Downloaded + " | Skipped existing: " + r.Skipped + " | Failed: " + r.Failed);
            MessageBox.Show(this,
                "Finished.\r\n\r\nDownloaded: " + r.Downloaded + "\r\nSkipped existing: " + r.Skipped + "\r\nFailed: " + r.Failed,
                "Viper Disk Harvester",
                MessageBoxButtons.OK,
                r.Failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void StopWork()
        {
            if (scanWorker.IsBusy)
            {
                scanWorker.CancelAsync();
                lblStatus.Text = "STOPPING SCAN...";
            }
            if (downloadWorker.IsBusy)
            {
                downloadWorker.CancelAsync();
                lblStatus.Text = "STOPPING AFTER CURRENT NETWORK READ...";
            }
        }

        private static string DownloadText(Uri uri)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
            req.Method = "GET";
            req.UserAgent = "Viper-Disk-Harvester/1.0 (polite single-thread crawler)";
            req.AllowAutoRedirect = true;
            req.Timeout = 30000;
            req.ReadWriteTimeout = 30000;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream input = resp.GetResponseStream())
            using (StreamReader reader = new StreamReader(input, Encoding.UTF8, true))
                return reader.ReadToEnd();
        }

        private List<DownloadItem> BuildDownloadItems(Dictionary<string, List<string>> byExt)
        {
            List<DownloadItem> result = new List<DownloadItem>();
            foreach (string ext in Extensions)
            {
                List<string> urls = byExt.ContainsKey(ext) ? byExt[ext] : new List<string>();
                Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                List<Tuple<string, string>> raw = new List<Tuple<string, string>>();
                foreach (string url in urls)
                {
                    Uri u = new Uri(url);
                    string name = Uri.UnescapeDataString(Path.GetFileName(u.AbsolutePath));
                    name = SanitizeFileName(name);
                    raw.Add(Tuple.Create(url, name));
                    if (!counts.ContainsKey(name)) counts[name] = 0;
                    counts[name]++;
                }
                foreach (Tuple<string, string> r in raw)
                {
                    string url = r.Item1;
                    string localName = r.Item2;
                    if (counts[localName] > 1)
                    {
                        string stem = Path.GetFileNameWithoutExtension(localName);
                        string extension = Path.GetExtension(localName);
                        string suffix = "__" + ShortHash(url);
                        localName = LimitFileName(stem, suffix, extension);
                    }
                    result.Add(new DownloadItem(url, localName, ext, TypeFolderMap[ext]));
                }
            }
            result.Sort(delegate(DownloadItem a, DownloadItem b)
            {
                int c = StringComparer.OrdinalIgnoreCase.Compare(a.TypeFolder, b.TypeFolder);
                if (c != 0) return c;
                return StringComparer.OrdinalIgnoreCase.Compare(a.LocalName, b.LocalName);
            });
            return result;
        }

        private static string SanitizeFileName(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) name = "unnamed";
            char[] bad = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name) sb.Append(bad.Contains(c) ? '_' : c);
            name = sb.ToString().Trim().TrimEnd('.', ' ');
            if (name.Length == 0) name = "unnamed";
            string stem = Path.GetFileNameWithoutExtension(name);
            string ext = Path.GetExtension(name);
            string upper = stem.ToUpperInvariant();
            string[] reserved = new string[] { "CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9" };
            if (reserved.Contains(upper)) stem = "_" + stem;
            return LimitFileName(stem, "", ext);
        }

        private static string LimitFileName(string stem, string suffix, string ext)
        {
            int max = 190;
            int available = max - suffix.Length - ext.Length;
            if (available < 10) available = 10;
            if (stem.Length > available) stem = stem.Substring(0, available);
            return stem + suffix + ext;
        }

        private static string ShortHash(string text)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 4; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static string NormalizeFolder(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("Destination folder is blank.");
            string full = Path.GetFullPath(path.Trim());
            string root = Path.GetPathRoot(full);
            while (full.Length > root.Length && (full.EndsWith(Path.DirectorySeparatorChar.ToString()) || full.EndsWith(Path.AltDirectorySeparatorChar.ToString())))
                full = full.Substring(0, full.Length - 1);
            return full;
        }

        private static void SaveIndexFiles(string destinationRoot, List<DownloadItem> items)
        {
            Directory.CreateDirectory(destinationRoot);
            string urlsPath = Path.Combine(destinationRoot, "ViperDiskHarvester-URLs.txt");
            File.WriteAllLines(urlsPath, items.Select(i => i.Url).ToArray(), Encoding.UTF8);
            string mapPath = Path.Combine(destinationRoot, "ViperDiskHarvester-filename-map.csv");
            using (StreamWriter sw = new StreamWriter(mapPath, false, new UTF8Encoding(true)))
            {
                sw.WriteLine("Type folder,Local filename,Source URL");
                foreach (DownloadItem item in items)
                    sw.WriteLine(Csv(item.TypeFolder) + "," + Csv(item.LocalName) + "," + Csv(item.Url));
            }
        }

        private static string Csv(string value)
        {
            if (value == null) value = "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private void OpenDestination()
        {
            try
            {
                string dest = NormalizeFolder(txtDest.Text);
                Directory.CreateDirectory(dest);
                Process.Start("explorer.exe", "\"" + dest + "\"");
            }
            catch (Exception ex)
            {
                Error(ex.Message);
            }
        }

        private void UpdateCountLabel(int pages, int queued)
        {
            lblCounts.Text = "Pages scanned: " + pages + " | Queued: " + queued + " | DSK: 0 | WOZ: 0 | PO: 0 | NIB: 0 | HDV: 0";
        }

        private void UpdateCountLabel(int pages, int queued, Dictionary<string, HashSet<string>> filesByExt)
        {
            lblCounts.Text =
                "Pages scanned: " + pages +
                " | Queued: " + queued +
                " | DSK: " + filesByExt[".dsk"].Count +
                " | WOZ: " + filesByExt[".woz"].Count +
                " | PO: " + filesByExt[".po"].Count +
                " | NIB: " + filesByExt[".nib"].Count +
                " | HDV: " + filesByExt[".hdv"].Count;
        }

        private void UpdateCountLabel(int pages, int queued, Dictionary<string, List<string>> filesByExt, int errors = -1)
        {
            lblCounts.Text =
                "Pages scanned: " + pages +
                (errors >= 0 ? " | Crawl errors: " + errors : " | Queued: " + queued) +
                " | DSK: " + (filesByExt.ContainsKey(".dsk") ? filesByExt[".dsk"].Count : 0) +
                " | WOZ: " + (filesByExt.ContainsKey(".woz") ? filesByExt[".woz"].Count : 0) +
                " | PO: " + (filesByExt.ContainsKey(".po") ? filesByExt[".po"].Count : 0) +
                " | NIB: " + (filesByExt.ContainsKey(".nib") ? filesByExt[".nib"].Count : 0) +
                " | HDV: " + (filesByExt.ContainsKey(".hdv") ? filesByExt[".hdv"].Count : 0);
        }

        private void Log(string message)
        {
            if (log.TextLength > 500000) log.Clear();
            log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }

        private void Error(string message)
        {
            Log("ERROR: " + message.Replace("\r", " ").Replace("\n", " "));
            MessageBox.Show(this, message, "Viper Disk Harvester", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private class ScanProgress
        {
            public int Pages; public int Queued; public Dictionary<string, HashSet<string>> FilesByExt; public string Message;
            public ScanProgress(int pages, int queued, Dictionary<string, HashSet<string>> filesByExt, string message)
            { Pages = pages; Queued = queued; FilesByExt = filesByExt; Message = message; }
        }

        private class ScanResult
        {
            public Dictionary<string, List<string>> FilesByExt; public int Pages; public int Errors;
            public ScanResult(Dictionary<string, List<string>> filesByExt, int pages, int errors)
            { FilesByExt = filesByExt; Pages = pages; Errors = errors; }
        }

        private class DownloadItem
        {
            public string Url; public string LocalName; public string Extension; public string TypeFolder;
            public DownloadItem(string url, string localName, string extension, string typeFolder)
            { Url = url; LocalName = localName; Extension = extension; TypeFolder = typeFolder; }
        }

        private class DownloadJob
        {
            public string Destination; public List<DownloadItem> Items;
            public DownloadJob(string destination, List<DownloadItem> items)
            { Destination = destination; Items = items; }
        }

        private class DownloadProgress
        {
            public int Index; public int Total; public int FilePercent; public int Downloaded; public int Skipped; public int Failed; public string Message;
            public DownloadProgress(int index, int total, int filePercent, int downloaded, int skipped, int failed, string message)
            { Index = index; Total = total; FilePercent = filePercent; Downloaded = downloaded; Skipped = skipped; Failed = failed; Message = message; }
        }

        private class DownloadResult
        {
            public int Downloaded; public int Skipped; public int Failed; public int Total;
            public DownloadResult(int downloaded, int skipped, int failed, int total)
            { Downloaded = downloaded; Skipped = skipped; Failed = failed; Total = total; }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}