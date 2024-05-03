/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Threading;

namespace Goofbot
{
    internal class HairColorManager
    {
        private DSHook dsHook;

        private bool _currentlyShifting;
        private int _shiftIterationNum;
        private int _numIterationsPerShift = 60;

        private const int COLOR_ARR_LEN = 3;

        private float[] _currentColor;
        private Color _targetColor;
        private float[] _colorDeltas;

        private object locker = new object();
        private Thread _colorShiftThread;
        private CancellationTokenSource _threadCancellationSource;

        public HairColorManager()
        {
            dsHook = new DSHook(5000, 5000);
            dsHook.Start();
        }

        public void InitializeHairColorShift(Color targetColor)
        {
            StopColorShiftThread();

            lock (locker)
            {
                _currentlyShifting = true;
                _shiftIterationNum = 0;

                _currentColor = ReadHairColorFromMemory();
                _targetColor = targetColor;

                float[] targetColorArr = ColorToFloatArray(_targetColor);
                _colorDeltas = new float[COLOR_ARR_LEN];

                for (int i = 0; i < COLOR_ARR_LEN; i++)
                {
                    _colorDeltas[i] = (targetColorArr[i] - _currentColor[i]) / _numIterationsPerShift;
                }
            }

            StartColorShiftThread();
        }

        private float[] ReadHairColorFromMemory()
        {
            return new float[] { dsHook.HairRed, dsHook.HairGreen, dsHook.HairBlue };
        }

        private void WriteCurrentColorToMemory()
        {
            lock (locker)
            {
                dsHook.HairRed = _currentColor[0];
                dsHook.HairGreen = _currentColor[1];
                dsHook.HairBlue = _currentColor[2];
            }
        }

        private float[] ColorToFloatArray(Color color)
        {
            return new float[] { (float)color.R / 255.0f, (float)color.G / 255.0f, (float)color.B / 255.0f };
        }

        private void SetCurrentColor(Color color)
        {
            lock (locker)
            {
                _currentColor = new float[] { (float)color.R / 255.0f, (float)color.G / 255.0f, (float)color.B / 255.0f };
            }
        }

        private void HairColorShiftIteration()
        {
            lock (locker)
            {
                if (_currentlyShifting)
                {
                    if (_shiftIterationNum >= _numIterationsPerShift - 1)
                    {
                        SetCurrentColor(_targetColor);
                        _currentlyShifting = false;
                    }
                    else
                    {
                        for (int i = 0; i < COLOR_ARR_LEN; i++)
                        {
                            _currentColor[i] = _currentColor[i] + _colorDeltas[i];
                        }
                    }

                    _shiftIterationNum++;
                }
            }
        }

        private void ShiftHair(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HairColorShiftIteration();
                WriteCurrentColorToMemory();
                lock (locker)
                {
                    if (!_currentlyShifting)
                    {
                        break;
                    }
                }
                Thread.Sleep(15);
            }
        }

        private void StartColorShiftThread()
        {
            lock (locker)
            {
                if (_colorShiftThread == null)
                {
                    _threadCancellationSource = new CancellationTokenSource();
                    var threadStart = new ThreadStart(() => ShiftHair(_threadCancellationSource.Token));
                    _colorShiftThread = new Thread(threadStart);
                    _colorShiftThread.IsBackground = true;
                    _colorShiftThread.Start();
                }
            }
        }

        private void StopColorShiftThread()
        {
            lock (locker)
            {
                if (_colorShiftThread != null)
                {
                    _threadCancellationSource.Cancel();
                    _colorShiftThread = null;
                    _threadCancellationSource = null;
                }
            }
        }
    }
}
*/