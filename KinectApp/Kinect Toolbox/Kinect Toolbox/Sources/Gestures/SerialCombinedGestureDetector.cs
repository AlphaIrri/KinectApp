using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kinect.Toolbox
{
    public class SerialCombinedGestureDetector : CombinedGestureDetector
    {
        DateTime? previousGestureTime;
        List<string> detectedGesturesName = new List<string>();

        public SerialCombinedGestureDetector(double epsilon = 1000)
            : base(epsilon)
        {
        }

        protected override void CheckGestures(string gesture)
        {
            if (!previousGestureTime.HasValue)
            {
                previousGestureTime = DateTime.Now;
                detectedGesturesName.Clear();
            }

            if (!detectedGesturesName.Contains(gesture))
            {
                var currentTime = DateTime.Now;

                if (currentTime.Subtract(previousGestureTime.Value).TotalMilliseconds > Epsilon)
                {
                    previousGestureTime = null;
                    return;
                }

                previousGestureTime = currentTime;

                detectedGesturesName.Add(gesture);
            }
            else
            {
                previousGestureTime = null;
                return;
            }

            if (detectedGesturesName.Count == GestureDetectorsCount)
            {
                RaiseGestureDetected(string.Join(">", detectedGesturesName));
                previousGestureTime = null;
            }
        }
    }
}
