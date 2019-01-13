using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using IronOcr;
using IronOcr.Languages;
using Microsoft.Win32;
using Point = System.Drawing.Point;

namespace FindWordInImage
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private Bitmap _imgInput;
        private string fileName;


        public MainWindow()
        {
            InitializeComponent();
        }

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out WindowRect lpWindowRect);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

        private void MenuGetWindow_Click(object sender, RoutedEventArgs e)
        {
            var proc = Process.GetProcessById(5128);
            WindowRect wr;
            GetWindowRect(proc.MainWindowHandle, out wr);

            var bmp = new Bitmap(wr.Right - wr.Left, wr.Bottom - wr.Top, PixelFormat.Format32bppArgb);
            var gfxBmp = Graphics.FromImage(bmp);
            var hdcBitmap = gfxBmp.GetHdc();

            PrintWindow(proc.MainWindowHandle, hdcBitmap, 0);

            gfxBmp.ReleaseHdc(hdcBitmap);
            gfxBmp.Dispose();

            _imgInput = bmp;
            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(_imgInput.GetHbitmap(), IntPtr.Zero,
                Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            UploadedImage.Source = bitmapSource;
        }

        private void MenuOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp; *.png)|*.jpg; *.jpeg; *.gif; *.bmp; *.png"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                fileName = openFileDialog.FileName;
                _imgInput = new Bitmap(fileName);
                _imgInput.SetResolution(300, 300);
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(_imgInput.GetHbitmap(), IntPtr.Zero,
                    Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                UploadedImage.Source = bitmapSource;
            }
        }

        /// <summary>
        ///     Using Iron OCR, currently has the most success
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuGetText_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //setup the advanced optical character recognition in Iron OCR
                var ocr = new AdvancedOcr
                {
                    CleanBackgroundNoise = true,
                    EnhanceContrast = true,
                    EnhanceResolution = true,
                    Language = English.OcrLanguagePack,
                    Strategy = AdvancedOcr.OcrStrategy.Advanced,
                    ColorSpace = AdvancedOcr.OcrColorSpace.Color,
                    DetectWhiteTextOnDarkBackgrounds = true,
                    InputImageType = AdvancedOcr.InputTypes.AutoDetect,
                    RotateAndStraighten = true,
                    ReadBarCodes = false,
                    ColorDepth = 1
                };

                // read in the image and get the first page
                var readIn = ocr.Read(_imgInput);
                var orcResult = readIn.Pages;
                string result;
                var page1 = orcResult[0].Words;
                // declare lists to hold word data
                var sList = new List<string>();
                var rList = new List<Rectangle>();
                var pList = new List<Rectangle>();
                // iterate through and collect word data
                foreach (var word in page1)
                {
                    sList.Add(word.Text);
                    rList.Add(word.Location);
                    pList.Add(new Rectangle(word.X + word.Width / 2, word.Y + word.Height / 2, 5, 5));
                }

                // create with bounding boxes around text
                var newImage = orcResult[0].Image;
                result = string.Join(" | ", sList);

                using (var g = Graphics.FromImage(newImage))
                {
                    g.DrawRectangles(new Pen(Color.Red, 3), rList.ToArray());
                    g.DrawRectangles(new Pen(Color.Red, 2), rList.ToArray());
                    foreach (var ell in pList) g.DrawEllipse(new Pen(Color.Blue, 4), ell);
                }

                OutputImage.Source = Imaging.CreateBitmapSourceFromHBitmap(newImage.GetHbitmap(), IntPtr.Zero,
                    Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                TextBox.Text = result;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        ///     This method finds specific words in the image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FindWordButtonIron_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // list of words to find
                var findWord = FindWordTextBox.Text.ToLower().Split(' ');
                // setup advanced Iron OCR
                var ocr = new AdvancedOcr
                {
                    CleanBackgroundNoise = true,
                    EnhanceContrast = true,
                    EnhanceResolution = true,
                    Language = English.OcrLanguagePack,
                    Strategy = AdvancedOcr.OcrStrategy.Advanced,
                    ColorSpace = AdvancedOcr.OcrColorSpace.Color,
                    DetectWhiteTextOnDarkBackgrounds = true,
                    InputImageType = AdvancedOcr.InputTypes.AutoDetect,
                    RotateAndStraighten = true,
                    ReadBarCodes = false,
                    ColorDepth = 1
                };

                // read in the image and get the page to iterate on
                var readIn = ocr.Read(_imgInput);
                var orcResult = readIn.Pages;
                string result;
                var page1 = orcResult[0].Words;
                // setup list to hold word and rectangle data
                var rList = new List<Rectangle>();
                var wordObjectList = new List<WordObject>();
                // iterate through page and save word and rectangle data
                foreach (var word in page1)
                {
                    var currentText = word.Text.ToLower();

                    foreach (var w in findWord)
                        if (string.Equals(currentText, w))
                        {
                            rList.Add(word.Location);
                            wordObjectList.Add(new WordObject(new Point(word.X, word.Y), word.Width, word.Height,
                                currentText));
                        }
                }

                // if nothing is found return from method
                if (wordObjectList.Count == 0 || wordObjectList.Count < findWord.Length) return;

                // create new image to show where the word/s are
                var newImage = orcResult[0].Image;

                // order the list by distance in words
                wordObjectList = OrderByDistance(wordObjectList, findWord);
                // separate words in the list
                result = string.Join(" ", wordObjectList.Select(word => word.Word));
                // display the found word on the image
                using (var g = Graphics.FromImage(newImage))
                {
                    // find the correct rectangle to display
                    var correctRectangles = rList.FindAll(recta =>
                    {
                        foreach (var wordObject in wordObjectList)
                            if (wordObject.Point.X == recta.X && wordObject.Point.Y == recta.Y)
                                return true;

                        return false;
                    });

                    var xMin = correctRectangles.Min(s => s.X);
                    var yMin = correctRectangles.Min(s => s.Y);
                    var xMax = correctRectangles.Max(s => s.X + s.Width);
                    var yMax = correctRectangles.Max(s => s.Y + s.Height);
                    var middle = new Point(xMax / 2 + xMin / 2, yMax / 2 + yMin / 2);

                    g.DrawRectangle(new Pen(Color.Blue, 3), new Rectangle(xMin, yMin, xMax - xMin, yMax - yMin));

                    g.DrawEllipse(new Pen(Color.Red, 3), new Rectangle(middle.X, middle.Y, 5, 5));
                }

                OutputImage.Source = Imaging.CreateBitmapSourceFromHBitmap(newImage.GetHbitmap(), IntPtr.Zero,
                    Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                TextBox.Text = result;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.StackTrace);
            }
        }

        /// <summary>
        ///     This is a recursive method that finds the next closest word that is also in text order
        /// </summary>
        /// <param name="list"></param>
        /// <param name="words"></param>
        /// <param name="counter"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        private List<WordObject> DifferentOrderingStrategy2(List<WordObject> list, string[] words, int counter,
            WordObject x)
        {
            var newList = new List<WordObject>();
            // if there is words commence, otherwise clear list and return as the correct text hasn't been found
            if (counter < words.Length)
                foreach (var word in list)
                    // if the words is the correct word
                    if (word.Word == words[counter])
                    {
                        // check if the words contaain points that relate to eachother
                        var xRange1 = Enumerable.Range(word.Point.X, word.Width).Contains(x.Point.X);
                        var yRange1 = Enumerable.Range(word.Point.Y, word.Height).Contains(x.Point.Y);
                        var xRange2 = Enumerable.Range(x.Point.X, x.Width).Contains(word.Point.X);
                        var yRange2 = Enumerable.Range(x.Point.Y, x.Height).Contains(word.Point.Y);

                        // if some points relate increase the counter, if it is the end of the text return, otherwise call method on itself then return
                        if (yRange1 || yRange2 || xRange1 || xRange2)
                        {
                            counter++;
                            if (counter >= words.Length)
                            {
                                newList.Add(word);
                                return newList;
                            }

                            newList = DifferentOrderingStrategy2(list, words, counter, x);
                            newList.Add(word);
                            return newList;
                        }
                    }

            newList.Clear();
            return newList;
        }

        /// <summary>
        ///     Order the list by proximity
        /// </summary>
        /// <param name="list"></param>
        /// <param name="words"></param>
        /// <returns></returns>
        private List<WordObject> DifferentOrderingStrategy(List<WordObject> list, string[] words)
        {
            var counter = 0;
            var anotherList = new List<WordObject>();

            foreach (var word in list)
            {
                // if the word we are currently at is a word in the list at counters position call method, otherwise clear list
                if (word.Word == words[counter])
                {
                    counter++;
                    anotherList = DifferentOrderingStrategy2(list, words, counter, word);
                    anotherList.Add(word);
                    if (anotherList.Count == words.Length)
                    {
                        if (anotherList[0].Word != words[0]) anotherList.Reverse();
                        return anotherList;
                    }
                }

                anotherList.Clear();
                counter = 0;
            }

            return null;
        }

        private List<WordObject> OrderByDistance(List<WordObject> wordObjectList, string[] words)
        {
            // order words by Y
            var finalTest = DifferentOrderingStrategy(wordObjectList.OrderBy(wordObject => wordObject.Point.Y).ToList(),
                words);

            // if the words were found return the words, otherwise return null
            return finalTest.Count != 0 ? finalTest : null;
        }

        private class WordObject
        {
            public WordObject(Point point1, int width, int height, string word)
            {
                Point = point1;
                Width = width;
                Height = height;
                Word = word;
            }

            public Point Point { get; }
            public int Width { get; }
            public int Height { get; }
            public string Word { get; }
        }

        public struct WindowRect
        {
            public int Left { get; }

            public int Top { get; }

            public int Right { get; }

            public int Bottom { get; }
        }
    }
}