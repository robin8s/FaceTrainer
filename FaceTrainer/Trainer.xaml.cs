using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FaceTrainer
{
    /// <summary>
    /// Interaction logic for Trainer.xaml
    /// </summary>
    public partial class Trainer : Page
    {
        private const int TrainingImageSize = 224;
        private const double FacePaddingPercent = 0.35;
        private const int MinimumFaceSize = 80;

        private string savePath = string.Empty;
        private readonly CascadeClassifier classifier;
        public CameraMode camera = null;
        public BitmapSource modified_image = null;
        private readonly List<BitmapImage> emoji;
        private readonly Stopwatch watch;

        private readonly int msPerEmoji = 2000;
        private readonly int msBetweenSaves = 333;
        private bool saveReady = false;
        private long lastElapsedMs = 0;
        private int saveImageNumber = 0;

        public event EventHandler TrainingCompleted;

        public Trainer()
        {
            InitializeComponent();

            string baseDirectory = AppContext.BaseDirectory;
            classifier = new CascadeClassifier(Path.Combine(baseDirectory, "haarcascade_frontalface_alt_tree.xml"));
            watch = new Stopwatch();
            emoji = new List<BitmapImage>();

            foreach (string filePath in Directory.GetFiles(Path.Combine(baseDirectory, "Emoji")))
            {
                emoji.Add(new BitmapImage(new Uri(filePath)));
            }

            if (camera == null)
            {
                camera = new CameraMode(timer_Tick);
            }
        }

        public void Start(string prefix)
        {
            string projectDirectory = ResolveProjectDirectory();
            string sanitizedPrefix = SanitizePrefix(prefix);
            savePath = Path.Combine(projectDirectory, "TrainingData", sanitizedPrefix);

            camera.startTimer();
            btnStart.IsEnabled = true;
            saveReady = false;
            saveImageNumber = 0;
        }

        public void Stop()
        {
            camera.stopTimer();
            watch.Stop();
            watch.Reset();
            btnStart.IsEnabled = true;
            saveReady = false;
            saveImageNumber = 0;
        }

        private string ResolveProjectDirectory()
        {
            DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                bool hasProjectFile = File.Exists(Path.Combine(current.FullName, "FaceTrainer.csproj"));
                bool hasTrainingData = Directory.Exists(Path.Combine(current.FullName, "TrainingData"));
                if (hasProjectFile || hasTrainingData)
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return Environment.CurrentDirectory;
        }

        private static string SanitizePrefix(string prefix)
        {
            string value = (prefix ?? string.Empty).Trim();
            value = Regex.Replace(value, "[^A-Za-z0-9_\\- ]", "_");
            value = value.Trim();
            return string.IsNullOrWhiteSpace(value) ? "UnknownUser" : value;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            long elapsed = watch.ElapsedMilliseconds;

            if (elapsed - lastElapsedMs > msBetweenSaves)
            {
                saveReady = true;
            }

            if (elapsed > msPerEmoji * emoji.Count)
            {
                Stop();
                TrainingCompleted?.Invoke(this, EventArgs.Empty);
            }

            progressTrainer.Value = (double)Math.Min(elapsed, msPerEmoji * emoji.Count) / (msPerEmoji * emoji.Count + 1) * 100;
            progressTrainer.UpdateLayout();

            using (Image<Bgr, byte> currentFrame = camera.queryFrame())
            {
                if (currentFrame != null)
                {
                    detectFaces(currentFrame);
                }
            }
        }

        private int currentEmoji()
        {
            return (int)Math.Min(emoji.Count - 1, watch.ElapsedMilliseconds / msPerEmoji);
        }

        private Rectangle[] DetectFaces(Image<Gray, byte> grayFrame)
        {
            return classifier.DetectMultiScale(grayFrame, 1.04, 5, System.Drawing.Size.Empty);
        }

        private Rectangle? SelectBestFace(Rectangle[] faces)
        {
            return faces
                .Where(face => face.Width >= MinimumFaceSize && face.Height >= MinimumFaceSize)
                .OrderByDescending(face => face.Width * face.Height)
                .Cast<Rectangle?>()
                .FirstOrDefault();
        }

        private Rectangle? BuildSquareCrop(Rectangle face, int imageWidth, int imageHeight)
        {
            int centerX = face.X + face.Width / 2;
            int centerY = face.Y + face.Height / 2;

            double sideWithPadding = Math.Max(face.Width, face.Height) * (1 + FacePaddingPercent * 2);
            int side = (int)Math.Round(sideWithPadding);
            side = Math.Min(side, Math.Min(imageWidth, imageHeight));

            if (side <= 0)
            {
                return null;
            }

            int x = centerX - side / 2;
            int y = centerY - side / 2;

            x = Math.Max(0, Math.Min(x, imageWidth - side));
            y = Math.Max(0, Math.Min(y, imageHeight - side));

            if (x < 0 || y < 0 || x + side > imageWidth || y + side > imageHeight)
            {
                return null;
            }

            return new Rectangle(x, y, side, side);
        }

        private Image<Bgr, byte> CropAndResizeFace(Image<Bgr, byte> frame, Rectangle face)
        {
            Rectangle? cropArea = BuildSquareCrop(face, frame.Width, frame.Height);
            if (cropArea == null)
            {
                return null;
            }

            using (Image<Bgr, byte> cropped = frame.Copy(cropArea.Value))
            {
                return cropped.Resize(TrainingImageSize, TrainingImageSize, Inter.Cubic);
            }
        }

        private bool TrySaveTrainingFace(Image<Bgr, byte> frame)
        {
            using (Image<Gray, byte> grayFrame = frame.Convert<Gray, byte>())
            {
                Rectangle[] faces = DetectFaces(grayFrame);
                Rectangle? bestFace = SelectBestFace(faces);
                if (bestFace == null)
                {
                    return false;
                }

                using (Image<Bgr, byte> resizedFace = CropAndResizeFace(frame, bestFace.Value))
                {
                    if (resizedFace == null)
                    {
                        return false;
                    }

                    if (!Directory.Exists(savePath))
                    {
                        Directory.CreateDirectory(savePath);
                    }

                    int nextImageNumber = saveImageNumber + 1;
                    string filename = Path.Combine(savePath, $"image{nextImageNumber:000}.png");
                    resizedFace.Save(filename);
                    saveImageNumber = nextImageNumber;
                    return true;
                }
            }
        }

        private void detectFaces(Image<Bgr, byte> image)
        {
            if (image == null)
            {
                return;
            }

            using (Image<Gray, byte> grayFrame = image.Convert<Gray, byte>())
            {
                Rectangle[] detectedFaces = DetectFaces(grayFrame);

                if (saveReady && watch.IsRunning)
                {
                    saveReady = false;
                    if (TrySaveTrainingFace(image))
                    {
                        lastElapsedMs = watch.ElapsedMilliseconds;
                    }
                }

                foreach (Rectangle face in detectedFaces)
                {
                    int paddingX = (int)(face.Width * .4);
                    int paddingY = (int)(face.Height * .4);
                    image.Draw(new Rectangle(face.X - paddingX, face.Y - paddingY, face.Width + paddingX * 2, face.Height + paddingY * 2), new Bgr(System.Drawing.Color.Blue), 3);
                }
            }

            modified_image = ToBitmapSource(image);

            FormattedText text = new FormattedText("", new CultureInfo("en-us"), FlowDirection.LeftToRight,
                new Typeface(this.FontFamily, FontStyles.Normal, FontWeights.Normal, new FontStretch()), this.FontSize,
                this.Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);

            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawImage(modified_image, new Rect(0, 0, modified_image.PixelWidth, modified_image.PixelHeight));
                drawingContext.PushOpacity(0.45);

                int eindex = currentEmoji();
                drawingContext.DrawImage(emoji[eindex], new Rect((imgCamera.Width - emoji[eindex].PixelWidth) / 2, 0, emoji[eindex].PixelWidth, emoji[eindex].PixelHeight));
                drawingContext.DrawText(text, new System.Windows.Point(2, 2));
            }

            RenderTargetBitmap bmp = new RenderTargetBitmap(modified_image.PixelWidth, modified_image.PixelHeight,
                modified_image.DpiX, modified_image.DpiY, PixelFormats.Pbgra32);
            bmp.Render(drawingVisual);

            imgCamera.Source = bmp;
        }

        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        public static BitmapSource ToBitmapSource(Image<Bgr, byte> image)
        {
            using (System.Drawing.Bitmap source = image.Mat.ToBitmap())
            {
                IntPtr ptr = source.GetHbitmap();
                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(ptr);
                return bs;
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            watch.Start();
            btnStart.IsEnabled = false;
        }
    }
}
