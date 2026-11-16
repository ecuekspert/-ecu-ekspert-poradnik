// ECU EKSPERT - Poradnik elektryka samochodowego
// Poradnik jest wbudowany w plik .exe, dziala bez internetu.
// Aktualizacja: z adresu w pliku adres-aktualizacji.txt (GitHub / Google Drive / Dropbox)
// Przy starcie program sprawdza nowa wersje i pyta uzytkownika o zgode na pobranie.
// albo recznie - wskazanie nowego pliku poradnik.html.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

class Program
{
    const string TITLE = "ECU EKSPERT - Poradnik elektryka samochodowego";
    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EcuEkspert", "Poradnik");
    static string Cur { get { return Path.Combine(Dir, "poradnik.html"); } }
    static string View { get { return Path.Combine(Dir, "widok.html"); } }
    static string AddrFile { get { return Path.Combine(Dir, "adres-aktualizacji.txt"); } }
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Directory.CreateDirectory(Dir);
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }

            // wersja wbudowana - jesli nowsza niz zapisana, uzyj jej
            string embedded = ReadEmbedded();
            if (!File.Exists(Cur) || Cmp(Ver(embedded), Ver(File.ReadAllText(Cur, Encoding.UTF8))) > 0)
                File.WriteAllText(Cur, embedded, Utf8);

            bool update = false;
            foreach (string a in args) if (a.ToLower().Contains("update")) update = true;
            if (update) DoUpdate();

            // 1) od razu otworz poradnik (dziala bez internetu)
            ShowGuide(null);
            if (update) return;

            // 2) w tle sprawdz, czy jest nowsza wersja; pobierz tylko za zgoda uzytkownika
            string cur = File.ReadAllText(Cur, Encoding.UTF8);
            string remote = Download(30000);
            if (remote == null || Cmp(Ver(remote), Ver(cur)) <= 0) return;

            string co = Changes(remote);
            string q = "Dostepna jest nowa wersja poradnika: " + Ver(remote) +
                       "\n(masz wersje " + Ver(cur) + ")" +
                       (co != null ? "\n\nCo nowego:\n" + co : "") +
                       "\n\nCzy pobrac i zainstalowac aktualizacje teraz?";
            if (Msg(q, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            File.WriteAllText(Cur, remote, Utf8);
            Msg("Zaktualizowano do wersji " + Ver(remote) + ".\n\nOtwieram nowa wersje - stare okno mozesz zamknac.",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            ShowGuide(null);
        }
        catch (Exception e)
        {
            Msg("Blad: " + e.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static void ShowGuide(string nowa)
    {
        string cur = File.ReadAllText(Cur, Encoding.UTF8);
        string inj = "<script>window.APP=1;" + (nowa != null ? "window.NOWA_WERSJA='" + nowa + "';" : "") + "</script>";
        int i = cur.IndexOf("</head>");
        string view = i >= 0 ? cur.Insert(i, inj) : inj + cur;
        File.WriteAllText(View, view, Utf8);
        Open(View);
    }

    // pierwszy wpis z listy zmian (ZMIANY) w nowej wersji
    static string Changes(string html)
    {
        Match m = Regex.Match(html, "const ZMIANY=\\[\\s*\\[\"[^\"]*\",\"((?:[^\"\\\\]|\\\\.)*)\"");
        if (!m.Success) return null;
        string t = m.Groups[1].Value.Replace("\\'", "'").Replace("\\\"", "\"");
        return t.Length > 600 ? t.Substring(0, 600) + "..." : t;
    }

    static void DoUpdate()
    {
        string cur = File.ReadAllText(Cur, Encoding.UTF8);
        string addr = Address();
        if (addr != null)
        {
            string remote = Download(20000);
            if (remote != null)
            {
                if (Cmp(Ver(remote), Ver(cur)) > 0)
                {
                    File.WriteAllText(Cur, remote, Utf8);
                    Msg("Zaktualizowano do wersji " + Ver(remote) + ".\n\nStare okno poradnika mozesz zamknac.",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    Msg("Masz najnowsza wersje (" + Ver(cur) + ").", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }
        }
        string why = addr == null
            ? "Nie ustawiono adresu aktualizacji."
            : "Nie udalo sie pobrac aktualizacji (brak internetu?).";
        if (Msg(why + "\n\nCzy chcesz wskazac nowy plik poradnik.html z dysku?",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        using (Form owner = Owner())
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Title = "Wybierz nowy plik poradnik.html";
            d.Filter = "Poradnik (*.html)|*.html";
            if (d.ShowDialog(owner) != DialogResult.OK) return;
            string t = File.ReadAllText(d.FileName, Encoding.UTF8);
            string v = Ver(t);
            if (v == null) { Msg("To nie jest plik poradnika.", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            File.WriteAllText(Cur, t, Utf8);
            Msg("Wgrano wersje " + v + ".\n\nStare okno poradnika mozesz zamknac.",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    static string Address()
    {
        if (!File.Exists(AddrFile)) return null;
        foreach (string line in File.ReadAllLines(AddrFile))
        {
            string l = line.Trim();
            if (l.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return l;
        }
        return null;
    }

    static string DirectUrl(string u)
    {
        if (u.Contains("drive.google.com"))
        {
            Match m = Regex.Match(u, @"/d/([A-Za-z0-9_-]+)");
            if (!m.Success) m = Regex.Match(u, @"[?&]id=([A-Za-z0-9_-]+)");
            if (m.Success) return "https://drive.google.com/uc?export=download&id=" + m.Groups[1].Value;
        }
        if (u.Contains("dropbox.com"))
        {
            u = u.Replace("dl=0", "dl=1");
            if (!u.Contains("dl=1")) u += (u.Contains("?") ? "&" : "?") + "dl=1";
        }
        Match g = Regex.Match(u, @"^https?://github\.com/([^/]+)/([^/]+)/blob/([^/]+)/(.+)$");
        if (g.Success) return "https://raw.githubusercontent.com/" + g.Groups[1].Value + "/" + g.Groups[2].Value + "/" + g.Groups[3].Value + "/" + g.Groups[4].Value;
        g = Regex.Match(u, @"^https?://github\.com/([^/]+)/([^/?#]+)/?$");
        if (g.Success) return "https://raw.githubusercontent.com/" + g.Groups[1].Value + "/" + g.Groups[2].Value.Replace(".git", "") + "/main/poradnik.html";
        return u;
    }

    static string Download(int timeoutMs)
    {
        string addr = Address();
        if (addr == null) return null;
        try
        {
            string url = DirectUrl(addr);
            url += (url.Contains("?") ? "&" : "?") + "t=" + DateTime.Now.Ticks; // bez cache
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.Timeout = timeoutMs;
            r.ReadWriteTimeout = timeoutMs;
            r.UserAgent = "EcuEkspertPoradnik";
            r.AllowAutoRedirect = true;
            using (WebResponse resp = r.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string t = sr.ReadToEnd();
                return Ver(t) != null ? t : null;
            }
        }
        catch { return null; }
    }

    static string ReadEmbedded()
    {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("poradnik.html"))
        using (StreamReader sr = new StreamReader(s, Encoding.UTF8))
            return sr.ReadToEnd();
    }

    static string Ver(string html)
    {
        if (html == null) return null;
        Match m = Regex.Match(html, "const WERSJA=\"([0-9]+(?:\\.[0-9]+)*)");
        return m.Success ? m.Groups[1].Value : null;
    }

    static int Cmp(string a, string b)
    {
        if (a == null) return b == null ? 0 : -1;
        if (b == null) return 1;
        string[] x = a.Split('.'), y = b.Split('.');
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            int p = i < x.Length ? int.Parse(x[i]) : 0, q = i < y.Length ? int.Parse(y[i]) : 0;
            if (p != q) return p.CompareTo(q);
        }
        return 0;
    }

    static Form Owner()
    {
        Form f = new Form();
        f.TopMost = true; f.ShowInTaskbar = false; f.FormBorderStyle = FormBorderStyle.None;
        f.StartPosition = FormStartPosition.CenterScreen; f.Size = new Size(1, 1); f.Opacity = 0;
        f.Show(); f.Activate();
        return f;
    }

    static DialogResult Msg(string text, MessageBoxButtons b, MessageBoxIcon i)
    {
        using (Form o = Owner()) return MessageBox.Show(o, text, TITLE, b, i);
    }

    static void Open(string path)
    {
        string url = new Uri(path).AbsoluteUri;
        string edge = FindEdge();
        if (edge != null)
        {
            ProcessStartInfo psi = new ProcessStartInfo(edge, "--app=\"" + url + "\" --window-size=1200,900");
            psi.UseShellExecute = false;
            Process.Start(psi);
        }
        else Process.Start(path);
    }

    static string FindEdge()
    {
        string[] bases = {
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        foreach (string b in bases)
        {
            if (string.IsNullOrEmpty(b)) continue;
            string p = Path.Combine(b, @"Microsoft\Edge\Application\msedge.exe");
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
