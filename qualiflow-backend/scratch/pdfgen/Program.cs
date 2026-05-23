using System;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;

class Program
{
    static void Main(string[] args)
    {
        var document = new PdfDocument();
        document.Info.Title = "Test PDF Document";
        
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 20, XFontStyle.Bold);
        
        gfx.DrawString("Hello, this is a test PDF document for QualiFlow!", font, XBrushes.Black, new XRect(0, 0, page.Width, page.Height), XStringFormats.Center);
        
        document.Save("test.pdf");
        Console.WriteLine("PDF generated successfully: test.pdf");
    }
}
