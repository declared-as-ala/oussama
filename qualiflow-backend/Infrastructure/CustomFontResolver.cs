using System;
using System.IO;
using System.Runtime.InteropServices;
using PdfSharpCore.Fonts;

namespace DocApi.Infrastructure
{
    public sealed class CustomFontResolver : IFontResolver
    {
        public string DefaultFontName => "Arial";

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            string suffix = "";
            if (isBold && isItalic) suffix = "-BoldItalic";
            else if (isBold) suffix = "-Bold";
            else if (isItalic) suffix = "-Italic";
            else suffix = "-Regular";

            return new FontResolverInfo($"Arial{suffix}");
        }

        public byte[] GetFont(string faceName)
        {
            bool isBold = faceName.Contains("-Bold");
            bool isItalic = faceName.Contains("-Italic");
            bool isBoldItalic = faceName.Contains("-BoldItalic");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string fontFile = "arial.ttf";
                if (isBoldItalic) fontFile = "arialbi.ttf";
                else if (isBold) fontFile = "arialbd.ttf";
                else if (isItalic) fontFile = "ariali.ttf";

                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", fontFile);
                if (File.Exists(path))
                {
                    return File.ReadAllBytes(path);
                }
            }
            else
            {
                // Linux / Docker path (fonts-liberation package)
                string fontFile = "LiberationSans-Regular.ttf";
                if (isBoldItalic) fontFile = "LiberationSans-BoldItalic.ttf";
                else if (isBold) fontFile = "LiberationSans-Bold.ttf";
                else if (isItalic) fontFile = "LiberationSans-Italic.ttf";

                var linuxPaths = new[]
                {
                    Path.Combine("/usr/share/fonts/truetype/liberation", fontFile),
                    Path.Combine("/usr/share/fonts/liberation", fontFile),
                    Path.Combine("/usr/share/fonts/truetype/dejavu", "DejaVuSans.ttf")
                };

                foreach (var path in linuxPaths)
                {
                    if (File.Exists(path))
                    {
                        return File.ReadAllBytes(path);
                    }
                }
            }

            // Ultimate fallback for Linux to find ANY font in common paths
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var directories = new[] { "/usr/share/fonts", "/usr/local/share/fonts" };
                foreach (var dir in directories)
                {
                    if (Directory.Exists(dir))
                    {
                        var files = Directory.GetFiles(dir, "*.ttf", SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            return File.ReadAllBytes(files[0]);
                        }
                    }
                }
            }

            throw new InvalidOperationException($"Font face '{faceName}' could not be resolved or loaded.");
        }
    }
}
