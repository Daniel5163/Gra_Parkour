using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Diagnostics;

public class AddMap : MonoBehaviour
{
    public Button btnOpenGoogleEarth;
    public Text labelInfo;

    private const string GOOGLE_EARTH_URL =
        "https://earth.google.com/web/@4.55018835,2.83183527,-3731.41789506a," +
        "18163478.06449413d,35y,-0h,0t,0r/data=CgRCAggBOgMKATBCAggASg0I____________ARAA";

    private string tempPath;
    private bool waitingForClipboard = false;
    private float checkTimer = 0f;
    private float checkInterval = 0.8f;
    private int lastClipboardCount = 0;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetClipboardSequenceNumber();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(System.IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);

    void Start()
    {
        tempPath = Path.Combine(Application.persistentDataPath, "clipboard_map.png");
        btnOpenGoogleEarth.onClick.AddListener(OnOpenGoogleEarth);
    }

    public void OnOpenGoogleEarth()
    {
        lastClipboardCount = GetClipboardSequenceNumber();
        waitingForClipboard = true;
        checkTimer = 0f;
        Application.OpenURL(GOOGLE_EARTH_URL);
        SetLabel("Użyj Win+Shift+S i zaznacz fragment mapy.");
    }

    void Update()
    {
        if (!waitingForClipboard) return;

        checkTimer += Time.deltaTime;
        if (checkTimer < checkInterval) return;
        checkTimer = 0f;

        int current = GetClipboardSequenceNumber();
        if (current == lastClipboardCount) return;
        lastClipboardCount = current;

        SetLabel("Wykryto zmianę schowka, sprawdzam...");
        TrySaveClipboard();
    }

    void TrySaveClipboard()
    {
        string safePath = tempPath.Replace("\\", "\\\\");

        string script =
            "Add-Type -AssemblyName System.Windows.Forms; " +
            "Add-Type -AssemblyName System.Drawing; " +
            "try { " +
            "  $hasImg = [System.Windows.Forms.Clipboard]::ContainsImage(); " +
            "  if ($hasImg) { " +
            $"    $img = [System.Windows.Forms.Clipboard]::GetImage(); " +
            $"    $bmp = New-Object System.Drawing.Bitmap($img); " +
            $"    $bmp.Save('{safePath}', [System.Drawing.Imaging.ImageFormat]::Png); " +
            
            "    Get-Process | Where-Object { $_.Name -in 'chrome','msedge','firefox','opera','brave' } | Stop-Process -Force; " +
            "    Write-Output 'OK' " +
            "  } else { " +
            "    $formats = [System.Windows.Forms.Clipboard]::GetDataObject().GetFormats(); " +
            "    Write-Output ('EMPTY:' + ($formats -join ',')); " +
            "  } " +
            "} catch { Write-Output ('ERROR:' + $_.Exception.Message) }";

        string result = RunPowerShell(script);
        UnityEngine.Debug.Log("[Clipboard RAW] " + result);

        if (result.StartsWith("OK"))
        {
            LoadTextureFromFile();
        }
        else if (result.StartsWith("EMPTY:"))
        {
            SetLabel("Czekam na obrazek...\n(Zrób Win+Shift+S i zaznacz fragment)");
            waitingForClipboard = true;
        }
        else if (result.StartsWith("ERROR:"))
        {
            SetLabel("Błąd: " + result.Substring(6));
            waitingForClipboard = true;
        }
        else
        {
            SetLabel("Czekam...");
            waitingForClipboard = true;
        }
    }

    void BringUnityToFront()
    {
        var hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        ShowWindow(hwnd, 9);
        SetForegroundWindow(hwnd);
    }

    string RunPowerShell(string script)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            var process = Process.Start(psi);
            process.WaitForExit(5000);
            string stdout = process.StandardOutput.ReadToEnd().Trim();
            string stderr = process.StandardError.ReadToEnd().Trim();
            if (!string.IsNullOrEmpty(stderr))
                UnityEngine.Debug.LogWarning("[PS STDERR] " + stderr);
            return stdout;
        }
        catch (System.Exception e)
        {
            return "ERROR:" + e.Message;
        }
    }

    void LoadTextureFromFile()
    {
        if (!File.Exists(tempPath))
        {
            SetLabel("Brak pliku tymczasowego.");
            return;
        }

        byte[] bytes = File.ReadAllBytes(tempPath);
        Texture2D tex = new Texture2D(2, 2);

        if (!tex.LoadImage(bytes))
        {
            SetLabel("Nie udało się wczytać obrazu.");
            return;
        }

        SelectedPhotoData.selectedTexture = tex;
        SetLabel($"Wczytano! ({tex.width}x{tex.height}px)\nGenerowanie mapy...");
        UnityEngine.SceneManagement.SceneManager.LoadScene("Mapa");
    }

    void SetLabel(string msg)
    {
        if (labelInfo != null)
            labelInfo.text = msg;
        UnityEngine.Debug.Log("[GoogleEarthCapture] " + msg);
    }
}