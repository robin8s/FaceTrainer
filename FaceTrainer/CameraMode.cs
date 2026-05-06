using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Windows.Threading;

namespace FaceTrainer
{
    public class CameraMode
    {
        private readonly VideoCapture capture;
        private readonly DispatcherTimer timer;

        public CameraMode(EventHandler myEventHandler)
        {
            capture = new VideoCapture();
            capture.Set(CapProp.FrameWidth, 640);
            capture.Set(CapProp.FrameHeight, 480);

            timer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 0, 0, 1)
            };
            timer.Tick += myEventHandler;
        }

        public void stopTimer()
        {
            if (timer.IsEnabled)
            {
                timer.Stop();
            }
        }

        public void startTimer()
        {
            if (!timer.IsEnabled)
            {
                timer.Start();
            }
        }

        public Image<Bgr, byte> queryFrame()
        {
            using (Mat frame = capture.QueryFrame())
            {
                return frame?.ToImage<Bgr, byte>();
            }
        }
    }
}
