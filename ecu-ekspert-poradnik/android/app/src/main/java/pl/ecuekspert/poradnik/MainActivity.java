package pl.ecuekspert.poradnik;

import android.app.Activity;
import android.app.AlertDialog;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class MainActivity extends Activity {
    // Skad aplikacja pobiera nowa wersje poradnika
    private static final String ADRES =
            "https://raw.githubusercontent.com/ecuekspert/-ecu-ekspert-poradnik/main/poradnik.html";

    private WebView web;
    private String wersja;          // wersja aktualnie wyswietlana
    private boolean sprawdzono = false;
    private final Handler ui = new Handler(Looper.getMainLooper());

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        web = new WebView(this);
        WebSettings s = web.getSettings();
        s.setJavaScriptEnabled(true);
        s.setDomStorageEnabled(true);
        s.setDatabaseEnabled(true);
        s.setAllowFileAccess(true);
        web.setWebViewClient(new WebViewClient());
        setContentView(web);

        wersja = ver(czytajAsset());
        File f = plik();
        String wPliku = f.exists() ? ver(czytajPlik(f)) : null;
        boolean zPliku = wPliku != null && cmp(wPliku, wersja) > 0;
        if (zPliku) wersja = wPliku;

        if (savedInstanceState != null) {
            web.restoreState(savedInstanceState);
            sprawdzono = savedInstanceState.getBoolean("sprawdzono", false);
        } else {
            web.loadUrl(zPliku ? "file://" + f.getAbsolutePath() : "file:///android_asset/poradnik.html");
        }
        if (!sprawdzono) sprawdzAktualizacje();
    }

    private File plik() { return new File(getFilesDir(), "poradnik.html"); }

    // ---------- aktualizacja ----------
    private void sprawdzAktualizacje() {
        sprawdzono = true;
        new Thread(() -> {
            final byte[] dane = pobierz();
            if (dane == null) return;                       // brak internetu - nic nie robimy
            final String html = new String(dane, StandardCharsets.UTF_8);
            final String nowa = ver(html);
            if (nowa == null || cmp(nowa, wersja) <= 0) return;  // brak nowszej wersji
            ui.post(() -> pokazPytanie(dane, nowa, zmiany(html)));
        }).start();
    }

    private void pokazPytanie(final byte[] dane, final String nowa, String co) {
        if (isFinishing()) return;
        String tekst = "Dostępna jest nowa wersja poradnika: " + nowa
                + "\n(masz wersję " + wersja + ")"
                + (co != null ? "\n\nCo nowego:\n" + co : "")
                + "\n\nCzy pobrać i zainstalować aktualizację teraz?";
        new AlertDialog.Builder(this)
                .setTitle("Aktualizacja poradnika")
                .setMessage(tekst)
                .setPositiveButton("Pobierz i aktualizuj", (d, w) -> zainstaluj(dane, nowa))
                .setNegativeButton("Później", null)
                .show();
    }

    private void zainstaluj(byte[] dane, String nowa) {
        File f = plik();
        File tmp = new File(getFilesDir(), "poradnik.tmp");
        try (FileOutputStream o = new FileOutputStream(tmp)) {
            o.write(dane);
        } catch (Exception e) {
            Toast.makeText(this, "Nie udało się zapisać aktualizacji.", Toast.LENGTH_LONG).show();
            return;
        }
        if (f.exists()) f.delete();
        if (!tmp.renameTo(f)) {
            Toast.makeText(this, "Nie udało się zapisać aktualizacji.", Toast.LENGTH_LONG).show();
            return;
        }
        wersja = nowa;
        web.clearCache(true);
        web.loadUrl("file://" + f.getAbsolutePath());
        Toast.makeText(this, "Zaktualizowano do wersji " + nowa, Toast.LENGTH_LONG).show();
    }

    private static byte[] pobierz() {
        HttpURLConnection c = null;
        try {
            c = (HttpURLConnection) new URL(ADRES + "?t=" + System.currentTimeMillis()).openConnection();
            c.setConnectTimeout(15000);
            c.setReadTimeout(30000);
            c.setUseCaches(false);
            if (c.getResponseCode() != 200) return null;
            try (InputStream in = c.getInputStream()) { return czytaj(in); }
        } catch (Exception e) {
            return null;
        } finally {
            if (c != null) c.disconnect();
        }
    }

    // ---------- pomocnicze ----------
    private String czytajAsset() {
        try (InputStream in = getAssets().open("poradnik.html")) {
            return new String(czytaj(in), StandardCharsets.UTF_8);
        } catch (Exception e) { return null; }
    }

    private static String czytajPlik(File f) {
        try (InputStream in = new java.io.FileInputStream(f)) {
            return new String(czytaj(in), StandardCharsets.UTF_8);
        } catch (Exception e) { return null; }
    }

    private static byte[] czytaj(InputStream in) throws java.io.IOException {
        ByteArrayOutputStream b = new ByteArrayOutputStream();
        byte[] buf = new byte[16384];
        int n;
        while ((n = in.read(buf)) > 0) b.write(buf, 0, n);
        return b.toByteArray();
    }

    static String ver(String html) {
        if (html == null) return null;
        Matcher m = Pattern.compile("const WERSJA=\"([0-9]+(?:\\.[0-9]+)*)").matcher(html);
        return m.find() ? m.group(1) : null;
    }

    static int cmp(String a, String b) {
        if (a == null) return b == null ? 0 : -1;
        if (b == null) return 1;
        String[] x = a.split("\\."), y = b.split("\\.");
        for (int i = 0; i < Math.max(x.length, y.length); i++) {
            int p = i < x.length ? Integer.parseInt(x[i]) : 0;
            int q = i < y.length ? Integer.parseInt(y[i]) : 0;
            if (p != q) return Integer.compare(p, q);
        }
        return 0;
    }

    static String zmiany(String html) {
        Matcher m = Pattern.compile("const ZMIANY=\\[\\s*\\[\"[^\"]*\",\"((?:[^\"\\\\]|\\\\.)*)\"").matcher(html);
        if (!m.find()) return null;
        String t = m.group(1).replace("\\'", "'").replace("\\\"", "\"");
        return t.length() > 600 ? t.substring(0, 600) + "…" : t;
    }

    @Override
    protected void onSaveInstanceState(Bundle out) {
        super.onSaveInstanceState(out);
        web.saveState(out);
        out.putBoolean("sprawdzono", sprawdzono);
    }

    @Override
    public void onBackPressed() {
        if (web.canGoBack()) web.goBack(); else super.onBackPressed();
    }
}
