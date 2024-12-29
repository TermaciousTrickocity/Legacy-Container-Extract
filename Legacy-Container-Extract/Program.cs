using System;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        Console.Write("Enter the directory of files: ");
        string directory = Console.ReadLine();

        if (!Directory.Exists(directory))
        {
            Console.WriteLine("Directory not found.");
            return;
        }

        string[] files = Directory.GetFiles(directory);
        string outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content");
        Directory.CreateDirectory(outputDirectory);

        string convertAllType = null;
        string ignoreAllType = null;

        foreach (var file in files)
        {
            try
            {
                byte[] fileBytes = File.ReadAllBytes(file);

                string fileContent = System.Text.Encoding.ASCII.GetString(fileBytes);
                int blfIndex = fileContent.IndexOf("_blf");

                if (blfIndex == -1)
                    continue;

                string content = System.Text.Encoding.ASCII.GetString(fileBytes.Skip(0xD0C0).Take(0x7F).ToArray()).Trim('\0');
                string sanitizedContent = string.Concat(content.Split(Path.GetInvalidFileNameChars()));
                string creatorName = System.Text.Encoding.ASCII.GetString(fileBytes.Skip(0xD088).Take(0x0F).ToArray()).Trim('\0');
                string creatorXUID = BitConverter.ToString(fileBytes.Skip(0xD080).Take(0x08).ToArray()).Replace("-", "");
                string modifierName = System.Text.Encoding.ASCII.GetString(fileBytes.Skip(0xD0AC).Take(0x0F).ToArray()).Trim('\0');
                string modifierXUID = BitConverter.ToString(fileBytes.Skip(0xD0A4).Take(0x08).ToArray()).Replace("-", "");
                string description = System.Text.Encoding.ASCII.GetString(fileBytes.Skip(0xD1C0).Take(0xFF).ToArray()).Trim('\0');
                string headerType = System.Text.Encoding.ASCII.GetString(fileBytes.Skip(0xD2F0).Take(0x04).ToArray()).Trim('\0');
                string contentType = null;

                switch (headerType)
                {
                    case "mpvr":
                        contentType = "Gametypes";
                        break;
                    case "mvar":
                        contentType = "Map variants";
                        break;
                    case "athr":
                        contentType = "Theater films";
                        break;
                    case "scnc":
                        contentType = "Screenshots";
                        break;
                }

                Console.WriteLine();
                Console.WriteLine($"File: {Path.GetFileName(file)}");
                Console.WriteLine($"Creator: {creatorName} (XUID: {creatorXUID})");
                Console.WriteLine($"Modifier: {modifierName} (XUID: {modifierXUID})");
                Console.WriteLine($"Content: {content}");
                Console.WriteLine($"Description: {description}");
                Console.WriteLine($"Header Type: {headerType}");

                if (headerType == ignoreAllType)
                {
                    Console.WriteLine($"Skipping all files of type: {headerType}");
                    continue;
                }

                if (headerType == convertAllType)
                {
                    Console.WriteLine($"Automatically converting all files of type: {headerType}");
                }
                else
                {
                    Console.Write("Would you like to convert this file? (y)es / (n)o / (a)ll of type / (i)gnore all of type: ");
                    string response = Console.ReadLine()?.ToLower();

                    if (response == "a")
                    {
                        convertAllType = headerType;
                    }
                    else if (response == "i")
                    {
                        ignoreAllType = headerType;
                        Console.WriteLine($"Skipping all files of type: {headerType}");
                        continue;
                    }
                    else if (response != "y")
                    {
                        continue;
                    }
                }

                if (headerType == "scnc")
                {
                    int jpgStartIndex = FindJpgStart(fileBytes, blfIndex);
                    int jpgEndIndex = FindJpgEnd(fileBytes, jpgStartIndex);

                    if (jpgStartIndex == -1 || jpgEndIndex == -1)
                    {
                        Console.WriteLine("Valid JPEG range not found. Skipping file.");
                        continue;
                    }

                    byte[] jpgBytes = fileBytes.Skip(jpgStartIndex).Take(jpgEndIndex - jpgStartIndex + 1).ToArray();

                    string headerDirectory = Path.Combine(outputDirectory, contentType);
                    Directory.CreateDirectory(headerDirectory);

                    string outputFile = Path.Combine(headerDirectory, sanitizedContent + ".jpg");
                    File.WriteAllBytes(outputFile, jpgBytes);
                    Console.WriteLine($"JPEG file saved: {outputFile}");
                    continue;
                }

                int stopSequenceIndex = FindStopSequence(fileBytes, blfIndex);

                if (stopSequenceIndex == -1)
                {
                    Console.WriteLine("Stop sequence not found. Skipping file.");
                    continue;
                }

                byte[] extractedBytes = fileBytes.Skip(blfIndex).Take(stopSequenceIndex - blfIndex).ToArray();

                string extension = null;
                if (headerType == "mpvr")
                {
                    extension = ".bin";
                }
                else if (headerType == "mvar")
                {
                    extension = ".mvar";
                }
                else if (headerType == "athr")
                {
                    extension = ".film";
                }

                string headerDirectoryForOthers = Path.Combine(outputDirectory, contentType);
                Directory.CreateDirectory(headerDirectoryForOthers);

                string outputFileForOthers = Path.Combine(headerDirectoryForOthers, sanitizedContent + extension);
                File.WriteAllBytes(outputFileForOthers, extractedBytes);
                Console.WriteLine($"File saved: {outputFileForOthers}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        Console.WriteLine("Processing complete.");
    }

    static int FindStopSequence(byte[] fileBytes, int startIndex)
    {
        string stopSequence = "_eof";
        int stopIndex = System.Text.Encoding.ASCII.GetString(fileBytes).IndexOf(stopSequence, startIndex);

        if (stopIndex != -1)
        {
            return stopIndex + stopSequence.Length + 0x0D;
        }

        return -1;
    }

    static int FindJpgStart(byte[] fileBytes, int startIndex)
    {
        byte[] jpgStartPattern = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        for (int i = startIndex; i <= fileBytes.Length - jpgStartPattern.Length; i++)
        {
            if (fileBytes.Skip(i).Take(jpgStartPattern.Length).SequenceEqual(jpgStartPattern))
            {
                return i;
            }
        }
        return -1;
    }

    static int FindJpgEnd(byte[] fileBytes, int startIndex)
    {
        byte[] jpgEndPattern = { 0xFF, 0xD9 };
        for (int i = startIndex; i <= fileBytes.Length - jpgEndPattern.Length; i++)
        {
            if (fileBytes.Skip(i).Take(jpgEndPattern.Length).SequenceEqual(jpgEndPattern))
            {
                return i + jpgEndPattern.Length - 1;
            }
        }
        return -1;
    }
}
