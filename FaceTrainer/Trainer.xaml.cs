using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FaceTrainer
{
    /// <summary>
    /// Interaction logic for Trainer.xaml
    /// </summary>
    public partial class Trainer : Page
    {
        private String savePath = "";
        private CascadeClassifier classifier;
        public CameraMode camera = null;
        public BitmapSource modified_image = null;
        private List<BitmapImage> emoji;
        private Stopwatch watch;

        private int msPerEmoji = 2000;
        private int msBetweenSaves = 333;
        private bool saveReady = false;
        private long lastElapsedMs = 0;
        private int saveImageNumber = 0;

        public event EventHandler TrainingCompleted;

        public Trainer()
        {
            InitializeComponent();

            classifier = new CascadeClassifier(System.Windows.Forms.Application.StartupPath + "/haarcascade_frontalface_alt_tree.xml");
            watch = new Stopwatch();
            emoji = new List<BitmapImage>();

            List<string> filePathList = Directory.GetFiles(System.Windows.Forms.Application.StartupPath + @"\Emoji\").ToList();
            foreach (string filePath in filePathList)
            {
                emoji.Add(new BitmapImage(new Uri(filePath)));
            }

            if (camera == null)
            {
                /* initialize the cameramode object and pass it the event handler */
                camera = new CameraMode(timer_Tick);
            }
            
        }

        public void Start(String prefix)
        {
            this.savePath = System.Windows.Forms.Application.StartupPath + @"\TrainingData\" + prefix + "\\";
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

        /* The threaded function that takes care of the drawing of rectangles. */
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

                if (TrainingCompleted != null)
                    TrainingCompleted(this, EventArgs.Empty);
            }

            progressTrainer.Value = (double)Math.Min(elapsed, msPerEmoji * emoji.Count) / (msPerEmoji * emoji.Count + 1) * 100;
            progressTrainer.UpdateLayout();

            /* Grab a frame from the camera */
            Image<Bgr, Byte> currentFrame = camera.queryFrame();

            /* Check to see that there was a frame collected */
            if (currentFrame != null)
            {
                detectFaces(currentFrame);
                currentFrame.Dispose();
            }


            //GC.Collect();
        }

        private int currentEmoji ()
        {
            return (int)Math.Min(emoji.Count-1, (watch.ElapsedMilliseconds / msPerEmoji));
        }

        /* Save the image with the specified name */
        public void saveImage(BitmapSource source)
        {
            saveImageNumber++;
            String filename = savePath + "image" + saveImageNumber.ToString().PadLeft(3, '0') + ".png";

            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            FileStream stream = new FileStream(filename, FileMode.Create);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Interlace = PngInterlaceOption.Off;
            // see notes below about BitmapFrame.Create()
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(stream);
            stream.Close();
        }

        private void detectFaces(Image<Bgr, Byte> image)
        {
            /* Check to see that there was a frame collected */
            if (image != null)
            {

                /* convert the frame from the camera to a transformed Image that improves facial detection */
                Image<Gray, Byte> grayFrame = image.Convert<Gray, Byte>();

                /* Detect how many faces are there on the image */
                if (saveReady)
                {
                    saveReady = false;
                    lastElapsedMs = watch.ElapsedMilliseconds;
                    saveImage(ToBitmapSource(image));
                }

                if (!watch.IsRunning)
                {
                    var detectedFaces = classifier.DetectMultiScale(grayFrame, 1.04, 5, System.Drawing.Size.Empty);

                    // loop through all faces that were detected and draw a rectangle 
                    foreach (var face in detectedFaces)
                    {
                        int paddingX = (int)(face.Width * .4);
                        int paddingY = (int)(face.Height * .4);

                        image.Draw(new System.Drawing.Rectangle(face.X - paddingX, face.Y - paddingY, face.Width + paddingX * 2, face.Height + paddingY * 2), new Bgr(System.Drawing.Color.Blue), 3);
                    }

                    if (detectedFaces.Length == 0)
                    {
                        grayFrame = grayFrame.Rotate(20, new Gray(0));
                        detectedFaces = classifier.DetectMultiScale(grayFrame, 1.1, 5, System.Drawing.Size.Empty);

                        // loop through all faces that were detected and draw a rectangle 
                        foreach (var face in detectedFaces)
                        {
                            int paddingX = (int)(face.Width * .4);
                            int paddingY = (int)(face.Height * .4);

                            image.Draw(new System.Drawing.Rectangle(face.X - paddingX, face.Y - paddingY, face.Width + paddingX * 2, face.Height + paddingY * 2), new Bgr(System.Drawing.Color.DarkGreen), 3);
                        }
                    }

                }

                modified_image = ToBitmapSource(image);



                FormattedText text = new FormattedText("",
                                 new System.Globalization.CultureInfo("en-us"),
                                 System.Windows.FlowDirection.LeftToRight,
                                 new Typeface(this.FontFamily, FontStyles.Normal, FontWeights.Normal, new FontStretch()),
                                 this.FontSize,
                                 this.Foreground);

                DrawingVisual drawingVisual = new DrawingVisual();
                DrawingContext drawingContext = drawingVisual.RenderOpen();
                drawingContext.DrawImage(modified_image, new Rect(0, 0, modified_image.PixelWidth, modified_image.PixelHeight));
                drawingContext.PushOpacity(0.45);

                int eindex = currentEmoji();
                drawingContext.DrawImage(emoji[eindex], new Rect(0 + (imgCamera.Width-emoji[eindex].PixelWidth)/2, 0, emoji[eindex].PixelWidth, emoji[eindex].PixelHeight));
                drawingContext.DrawText(text, new Point(2, 2));
                drawingContext.Close();

                RenderTargetBitmap bmp = new RenderTargetBitmap(modified_image.PixelWidth, modified_image.PixelHeight, modified_image.DpiX, modified_image.DpiY, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);


                /* set the transformed image to the image1 object */
                imgCamera.Source = bmp;
            }
        }

        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        /* Function to convert an IImage to a BitmapSource. This is needed because
         * otherwise I have to use a hardcoded ImageBox object that is defined in EmguCV
         * but this will hault my design so I decided to convert the Image object to the proper
         * format to use a normal Image object that is used in WPF
         */
        public static BitmapSource ToBitmapSource(IImage image)
        {
            using (System.Drawing.Bitmap source = image.Bitmap)
            {
                IntPtr ptr = source.GetHbitmap(); /* obtain the Hbitmap */

                /* Transform the IImage to a BitmapSource */
                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(ptr); /* release the HBitmap */
                return bs; /* return the newly converted BitmapSource */
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            watch.Start();
            btnStart.IsEnabled = false;
        }
    }
}
