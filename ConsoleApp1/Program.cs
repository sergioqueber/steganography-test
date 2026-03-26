// See https://aka.ms/new-console-template for more information
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

if (!OperatingSystem.IsWindows())
{
    Console.WriteLine("This example requires Windows due to System.Drawing usage.");
    return;
}

string inputPath = "D:/Repos/steganography-test/ConsoleApp1/Data/input.bmp"; // Ensure this file exists in your output directory
string modifiedPath = "D:/Repos/steganography-test/ConsoleApp1/Data/modified_pixel.bmp";
string stegoPath = "D:/Repos/steganography-test/ConsoleApp1/Data/stego_message.bmp";

if (!File.Exists(inputPath))
{
    using (Bitmap bmp = new Bitmap(100, 100))
    {
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.FillRectangle(Brushes.Red, 10, 10, 50, 50);
        }
        bmp.Save(inputPath, ImageFormat.Bmp);
        Console.WriteLine($"Created dummy image at {inputPath}");
    }
}

Console.WriteLine($"Reading from {inputPath}");
using (Bitmap original = new Bitmap(inputPath))
{
    Color oldpixel = original.GetPixel(0, 0);
    Console.WriteLine($"Original pixel at (0,0): R={oldpixel.R}, G={oldpixel.G}, B={oldpixel.B}");
    
    original.SetPixel(0, 0, Color.Blue);
    original.Save(modifiedPath, ImageFormat.Bmp);
    Console.WriteLine($"Image saved to {modifiedPath}");

}

// Verification
using (Bitmap modified = new Bitmap(modifiedPath))
{
    Color newPixel = modified.GetPixel(0, 0);
    Console.WriteLine($"Modified Pixel (0,0): {newPixel}");
    if (newPixel.ToArgb() == Color.Blue.ToArgb())
        Console.WriteLine("Verification: SUCCESS");
    else
        Console.WriteLine("Verification: FAILED");
}

Console.WriteLine("\n--- Step 4: Encode Hidden Message ---");
string secretMessage = "Hello World!";
Console.WriteLine($"Hiding message: \"{secretMessage}\"");

using (Bitmap bmpToHide = new Bitmap(inputPath))
{
    byte[] messageBytes = Encoding.UTF8.GetBytes(secretMessage);
    byte[] lengthBytes = BitConverter.GetBytes(messageBytes.Length);
    
    // Combine length prefix and message
    byte[] payload = new byte[lengthBytes.Length + messageBytes.Length];
    Array.Copy(lengthBytes, 0, payload, 0, lengthBytes.Length);
    Array.Copy(messageBytes, 0, payload, lengthBytes.Length, messageBytes.Length);

    int bitIndex = 0;
    int totalBits = payload.Length * 8;

    if (totalBits > bmpToHide.Width * bmpToHide.Height)
    {
        Console.WriteLine("Image is too small to hold the message.");
        return;
    }

    for (int y = 0; y < bmpToHide.Height; y++)
    {
        for (int x = 0; x < bmpToHide.Width; x++)
        {
            if (bitIndex >= totalBits) break;

            Color pixel = bmpToHide.GetPixel(x, y);

            // Get current bit from payload (1 or 0)
            int byteIndex = bitIndex / 8;
            int bitOffset = bitIndex % 8;
            int bit = (payload[byteIndex] >> bitOffset) & 1;

            // Modify the Blue component's Least Significant Bit (LSB)
            // Clear LSB (pixel.B & 254) then OR with bit
            int newB = (pixel.B & 0xFE) | bit; 

            bmpToHide.SetPixel(x, y, Color.FromArgb(pixel.R, pixel.G, newB));

            bitIndex++;
        }
        if (bitIndex >= totalBits) break;
    }

    bmpToHide.Save(stegoPath, ImageFormat.Bmp);
    Console.WriteLine($"Encoded image saved to {stegoPath}");
}

Console.WriteLine("\n--- Step 5: Decode Hidden Message ---");

using (Bitmap stegoBmp = new Bitmap(stegoPath))
{
    int lengthBits = 32;
    int currentBitIndex = 0;
    byte[] lengthBuffer = new byte[4];

    // Helper function to extract byte from image stream
    // This assumes sequential reading of pixels
    void ExtractBits(byte[] buffer, int bitsToRead)
    {
        int localBitIndex = 0;
        int startX = (currentBitIndex) % stegoBmp.Width;
        int startY = (currentBitIndex) / stegoBmp.Width;
        
        // This simple loops restarts coordinate calculation for simplicity
        // Ideally you keep x,y state
        for (int y = 0; y < stegoBmp.Height; y++) 
        {
            for (int x = 0; x < stegoBmp.Width; x++)
            {
                // Skip pixels we already processed
                int absoluteIndex = y * stegoBmp.Width + x;
                if (absoluteIndex < currentBitIndex) continue;
                if (localBitIndex >= bitsToRead) return;

                Color pixel = stegoBmp.GetPixel(x, y);
                
                // Extract LSB from Blue channel
                int extractedBit = pixel.B & 1;

                int targetByteIndex = localBitIndex / 8;
                int targetBitOffset = localBitIndex % 8;

                if (extractedBit == 1)
                    buffer[targetByteIndex] |= (byte)(1 << targetBitOffset);
                else
                    buffer[targetByteIndex] &= (byte)~(1 << targetBitOffset);

                localBitIndex++;
            }
        }
    }

    // Extract length
    ExtractBits(lengthBuffer, 32);
    int messageLength = BitConverter.ToInt32(lengthBuffer);
    currentBitIndex += 32;

    Console.WriteLine($"Detected message length: {messageLength} bytes");

    // Extract message
    byte[] messageBuffer = new byte[messageLength];
    ExtractBits(messageBuffer, messageLength * 8);

    string decodedMessage = Encoding.UTF8.GetString(messageBuffer);
    Console.WriteLine($"Decoded Message: \"{decodedMessage}\"");
}