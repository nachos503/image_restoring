using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

class Program
{
    static void Main()
    {
        Console.WriteLine("Введите путь к изображению:");
        string imagePath = Console.ReadLine();

        if (File.Exists(imagePath))
        {
            DisplayImage(imagePath);
        }
        else
        {
            Console.WriteLine("Файл не найден.");
        }
    }

    static void DisplayImage(string imagePath)
    {
        using (var image = Image.Load<Rgba32>(imagePath))
        {

            ApplyInterlace(image);

            // Сохранение изображения
            string outputImagePath = "InterlacedImage.jpg";
            image.Save(outputImagePath);
            Console.WriteLine($"Изображение сохранено по пути: {outputImagePath}");
        }

        Console.ReadLine(); // Добавим задержку, чтобы консоль не закрылась сразу
    }

    static void ApplyInterlace(Image<Rgba32> image)
    {
        // Пример простого интерлейса - замена каждого второго пикселя на черный
        for (int y = 1; y < image.Height; y += 2)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = new Rgba32(0, 0, 0); // Черный цвет
            }
        }
    }
}