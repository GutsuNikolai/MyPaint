using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Paint;

// GDI fonts are used by TextBox; GDI+ fonts are used when drawing into Bitmap.
internal sealed class TextFonts : IDisposable
{
    private const uint PrivateFont = 0x10;
    private readonly InstalledFontCollection installedFonts = new();
    private readonly PrivateFontCollection privateFonts = new();
    private readonly Dictionary<string, FontFamily> families = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FontFamily> ownedFamilies = new();
    private readonly List<string> registeredFiles = new();
    private bool disposed;

    public IEnumerable<string> Names => families.Keys.OrderBy(name => name);
    public static string FontFolder => AppSettings.FontsFolder;

    public TextFonts()
    {
        foreach (FontFamily family in installedFonts.Families)
        {
            families[family.Name] = family;
            ownedFamilies.Add(family);
        }
        CopyLegacyFonts();
        if (!Directory.Exists(FontFolder)) return;
        foreach (string file in Directory.EnumerateFiles(FontFolder))
        {
            if (Path.GetExtension(file).ToLowerInvariant() is not (".ttf" or ".otf")) continue;
            try { RegisterFile(file); }
            catch (Exception exception) when (exception is ArgumentException or IOException or ExternalException or NotSupportedException)
            {
                // A broken cached font must not prevent the editor from starting.
            }
        }
    }

    private static void CopyLegacyFonts()
    {
        string legacyFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyPaint", "Fonts");
        if (!Directory.Exists(legacyFolder)) return;
        try
        {
            foreach (string file in Directory.EnumerateFiles(legacyFolder))
            {
                if (Path.GetExtension(file).ToLowerInvariant() is not (".ttf" or ".otf")) continue;
                Directory.CreateDirectory(FontFolder);
                string target = Path.Combine(FontFolder, Path.GetFileName(file));
                if (!File.Exists(target)) File.Copy(file, target);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Keep the original font files if their copies cannot be created.
        }
    }

    public string LoadFile(string sourcePath)
    {
        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not (".ttf" or ".otf"))
            throw new ArgumentException("Выберите файл TTF или OTF.");
        Directory.CreateDirectory(FontFolder);
        string target = Path.Combine(FontFolder, Guid.NewGuid().ToString("N") + extension);
        File.Copy(sourcePath, target);
        try { return RegisterFile(target); }
        catch
        {
            File.Delete(target);
            throw;
        }
    }

    private string RegisterFile(string path)
    {
        if (AddFontResourceEx(path, PrivateFont, IntPtr.Zero) == 0)
            throw new ArgumentException("Файл не содержит поддерживаемый шрифт.");
        try
        {
            HashSet<string> previousNames = privateFonts.Families.Select(family =>
            {
                string name = family.Name;
                family.Dispose();
                return name;
            }).ToHashSet(StringComparer.OrdinalIgnoreCase);
            privateFonts.AddFontFile(path);
            string? selectedName = null;
            foreach (FontFamily family in privateFonts.Families)
            {
                if (!previousNames.Contains(family.Name)) selectedName ??= family.Name;
                families[family.Name] = family;
                ownedFamilies.Add(family);
            }
            registeredFiles.Add(path);
            return selectedName ?? privateFonts.Families.Select(family =>
            {
                string name = family.Name;
                family.Dispose();
                return name;
            }).First();
        }
        catch
        {
            RemoveFontResourceEx(path, PrivateFont, IntPtr.Zero);
            throw;
        }
    }

    public Font CreateFont(string name, float size)
    {
        FontFamily family = families.TryGetValue(name, out FontFamily? found) ? found : FontFamily.GenericSansSerif;
        FontStyle style = new[] { FontStyle.Regular, FontStyle.Bold, FontStyle.Italic, FontStyle.Bold | FontStyle.Italic }
            .First(family.IsStyleAvailable);
        return new Font(family, size, style, GraphicsUnit.Pixel);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (FontFamily family in ownedFamilies) family.Dispose();
        privateFonts.Dispose();
        installedFonts.Dispose();
        foreach (string path in registeredFiles) RemoveFontResourceEx(path, PrivateFont, IntPtr.Zero);
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResourceEx(string name, uint flags, IntPtr reserved);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveFontResourceEx(string name, uint flags, IntPtr reserved);
}
