using UnityEngine;
using ZXing.QrCode;
using ZXing;

public class QRCodeGenerator
{
    public static Texture2D GenerateQRCode(string text, int size)
    {
        Texture2D output = new(size, size);
        var color32 = Encode(text, output.width, output.height);
        output.SetPixels32(color32);
        output.Apply();
        return output;
    }

    public static void GenerateQRCode(ref Texture2D targetTexture, string text)
    {
        var color32 = Encode(text, targetTexture.width, targetTexture.height);
        targetTexture.SetPixels32(color32);
        targetTexture.Apply();
    }

    private static Color32[] Encode(string textForEncoding, int width, int height)
    {
        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Height = height,
                Width = width
            }
        };
        return writer.Write(textForEncoding);
    }
}
