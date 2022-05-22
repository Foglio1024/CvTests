using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Nostrum.WPF;
using Nostrum.WPF.ThreadSafe;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace CvTests
{
    public class ViewModel : ThreadSafeObservableObject
    {
        #region Fields

        readonly VideoCapture _videoCapture = new();
        readonly GpuMat _gpuFrame = new();
        readonly GpuMat _gpuGray = new();
        readonly GpuMat _facesMat = new();
        readonly Mat _frame = new();
        readonly Mat _gray = new();
        readonly CascadeClassifier _cpuCascadeClassifier = new(@".\haarcascade_frontalface_alt.xml");
        readonly CudaCascadeClassifier _gpuCascadeClassifier = new(@".\haarcascade_frontalface_alt_cuda.xml")
        {
            MinObjectSize = new Size(200, 200)
        };

        readonly Stopwatch _totalStopwatch = new();
        readonly MCvScalar _cpuCol;
        readonly MCvScalar _gpuCol;

        #endregion

        #region Properties

        bool _useCPU = true;
        bool _useGPU = true;
        WriteableBitmap? _imageSource;

        public bool UseCPU
        {
            get => _useCPU;
            set
            {
                if (_useCPU == value) return;
                _useCPU = value;
                N();
            }
        }
        public bool UseGPU
        {
            get => _useGPU;
            set
            {
                if (_useGPU == value) return;
                _useGPU = value;
                N();
            }
        }
        public WriteableBitmap? ImageSource
        {
            get => _imageSource;
            set
            {
                if (_imageSource == value) return;
                _imageSource = value;
                N();
            }
        }

        public PerformanceMonitor TotalCpuPerfMon { get; } = new();
        public PerformanceMonitor TotalGpuPerfMon { get; } = new();
        public ICommand StartCaptureCommand { get; }

        #endregion

        public ViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            StartCaptureCommand = new RelayCommand(StartCapture);

            _videoCapture.ImageGrabbed += OnFrame;

            _cpuCol = new Bgr(Color.IndianRed).MCvScalar;
            _gpuCol = new Bgr(Color.MediumSeaGreen).MCvScalar;
        }

        void StartCapture()
        {
            _videoCapture.Start();
        }

        void OnFrame(object? sender, EventArgs e)
        {
            // retrieve frame from camera
            if(!_videoCapture.Retrieve(_frame)) return;

            // resize it for better performance
            CvInvoke.Resize(_frame, _frame, new Size(_frame.Width / 2, _frame.Height / 2));

            var cpuResults = Array.Empty<Rectangle>();
            var gpuResults = Array.Empty<Rectangle>();

            if (UseCPU)
            {
                // detect using CPU
                _totalStopwatch.Restart();
                cpuResults = DetectFacesCPU();
                _totalStopwatch.Stop();
                TotalCpuPerfMon.AddSample(_totalStopwatch.ElapsedMilliseconds);
            }

            if (UseGPU)
            {
                // detect using GPU
                _totalStopwatch.Restart();
                gpuResults = DetectFacesGPU();
                _totalStopwatch.Stop();
                TotalGpuPerfMon.AddSample(_totalStopwatch.ElapsedMilliseconds);
            }

            // draw rectangles to original frame
            foreach (var rect in cpuResults)
            {
                CvInvoke.Rectangle(_frame, rect, _cpuCol, 2);
                CvInvoke.PutText(_frame, $"CPU avg: {TotalCpuPerfMon.Average:N1}ms", new Point(rect.X, rect.Y + rect.Height + 20), FontFace.HersheyPlain, 1.5, _cpuCol, 2);
            }
            foreach (var rect in gpuResults)
            {
                CvInvoke.Rectangle(_frame, rect, _gpuCol, 2);
                CvInvoke.PutText(_frame, $"GPU avg: {TotalGpuPerfMon.Average:N1}ms", new Point(rect.X, rect.Y - 10), FontFace.HersheyPlain, 1.5, _gpuCol,2);
            }

            UpdatePreviewImage();
        }

        Rectangle[] DetectFacesCPU()
        {
            // convert to gray image, equalize hist
            CvInvoke.CvtColor(_frame, _gray, ColorConversion.Bgr2Gray);
            CvInvoke.EqualizeHist(_gray, _gray);

            // detect using CPU classifier
            return _cpuCascadeClassifier.DetectMultiScale(_gray, minSize: new Size(200, 200));
        }

        Rectangle[] DetectFacesGPU()
        {
            // upload frame to GPU
            _gpuFrame.Upload(_frame);

            // convert to gray image, equalize hist and save it to GPU
            CudaInvoke.CvtColor(_gpuFrame, _gpuGray, ColorConversion.Bgr2Gray);
            CudaInvoke.EqualizeHist(_gpuGray, _gpuGray);

            // detect using GPU classifier and put results in _facesMat (GPU Mat)
            try
            {
                _gpuCascadeClassifier.DetectMultiScale(_gpuGray, _facesMat);
            }
            catch { return Array.Empty<Rectangle>(); }

            // convert GPU Mat to Rectangle[]
            return _gpuCascadeClassifier.Convert(_facesMat);
        }

        void UpdatePreviewImage()
        {
            _dispatcher?.Invoke(() =>
            {
                // initialize ImageSource (for UI binding) if it's still null 
                if (ImageSource == null)
                {
                    ImageSource = new(_frame.Width, _frame.Height, 96, 96, PixelFormats.Bgr24, null);
                }

                // update image in UI
                ImageSource?.WritePixels(sourceRect: new Int32Rect(0, 0, _frame.Cols, _frame.Rows),
                                         buffer: _frame.DataPointer,
                                         bufferSize: _frame.Rows * _frame.Cols * _frame.NumberOfChannels,
                                         stride: _frame.Cols * _frame.NumberOfChannels);
            });
        }

        internal void StopCapture()
        {
            _videoCapture.ImageGrabbed -= OnFrame;
            _videoCapture.Stop();
        }
    }
}
