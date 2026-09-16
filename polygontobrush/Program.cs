using System;
using System.IO;
using System.Text;
using Weland;
using static LevelPolygonBuilder;

class Program
{
    static string levelName = string.Empty;
    static int levelNumber;
    static string levelPath = string.Empty;
    static string savePath = string.Empty;
    static Level? level = null;

    static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Console.Write("Enter a level name: ");
        levelName = Console.ReadLine() ?? string.Empty;

        if (string.IsNullOrEmpty(levelName))
        {
            Console.WriteLine("Invalid level name entered. Exiting.");
            return;
        }

        Console.Write("Enter a level number: ");
        if (!int.TryParse(Console.ReadLine(), out levelNumber))
        {
            Console.WriteLine("Invalid level number entered. Exiting.");
            return;
        }

        Console.Write("Enter the level file directory: ");
        levelPath = Console.ReadLine() ?? string.Empty;

        if (string.IsNullOrEmpty(levelPath))
        {
            Console.WriteLine("Invalid level path entered. Exiting.");
            return;
        }

        Console.Write("Enter where you want to save the polygons: ");
        savePath = Console.ReadLine() ?? string.Empty;

        if (string.IsNullOrEmpty(savePath))
        {
            Console.WriteLine("Invalid save path entered. Exiting.");
            return;
        }

        string outputFolder = Path.Combine(savePath, levelName);
        try
        {
            Directory.CreateDirectory(outputFolder);
        }
        catch (Exception exit)
        {
            Console.WriteLine($"Failed to create output directory: {exit.Message}");
            return;
        }

        level = LoadLevel(levelName, levelNumber);
        if (level == null || level.Polygons == null)
        {
            Console.WriteLine("Level data is null. Aborting process.");
            return;
        }

        Console.WriteLine($"Processing {level.Polygons.Count} polygons...");

        for (int p = 0; p < level.Polygons.Count; p++)
        {
            try
            {
                Level newLevel = BuildLevelWithSinglePolygon(level, p);
                Wadfile.DirectoryEntry entry = newLevel.Save();

                Wadfile wadfile = new Wadfile();
                wadfile.Directory.Add(0, entry);

                OBJExporter exporter = new OBJExporter(newLevel);

                string outPath = Path.Combine(outputFolder, $"Polygon ({p}).sceA");
                string objPath = Path.Combine(outputFolder, $"Polygon ({p}).obj");

                exporter.Export(objPath);
                wadfile.Save(outPath);

                Console.WriteLine($"Successfully exported Polygon ({p})");
            }
            catch (Exception exit)
            {
                Console.WriteLine($"Error processing polygon {p}: {exit.Message}");
            }
        }

        Console.WriteLine("Process complete!");
    }

    public static Level? LoadLevel(string Name, int LevelNumber)
    {
        MapFile map = new MapFile();
        Level level = new Level();

        try
        {
            map.Load(Path.Combine(levelPath, Name + ".sceA"));
            Console.WriteLine("Map loaded successfully!");
        }
        catch (Exception exit)
        {
            Console.WriteLine("Failed to load Map: " + exit.Message);
            return null;
        }

        try
        {
            level.Load(map.Directory[LevelNumber]);
            Console.WriteLine("Level loaded successfully!");
            return level;
        }
        catch (Exception exit)
        {
            Console.WriteLine("Failed to load level: " + exit.Message);
            return null;
        }
    }
}
