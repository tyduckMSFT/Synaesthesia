using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.Devices.Lights.Effects;

namespace Synaesthesia
{
    public abstract class LightingEffect
    {
        protected MainWindow m_mainWindow;
        protected LampArray m_lampArray;

        protected int m_currentlyPlayingRefreshFrameCount = 0;
        protected int m_effectFramesRemaining = 0;

        protected int[] m_lampIndices;

        public LightingEffect(MainWindow mainWindow, LampArray lampArray)
        {
            m_mainWindow = mainWindow;
            m_lampArray = lampArray;

            m_lampIndices = Enumerable.Range(0, m_lampArray.LampCount).ToArray();
        }

        public abstract ILampArrayEffect GetEffect();

        private static readonly object s_lastRequestTimeLock = new();
        private static DateTime s_lastRequestTime = DateTime.MinValue;

        public void RefreshNowPlayingIfNeeded()
        {
            bool updateNowPlaying = false;
            lock (s_lastRequestTimeLock)
            {
                var currentTime = DateTime.Now;
                if (s_lastRequestTime == DateTime.MinValue || currentTime - s_lastRequestTime > TimeSpan.FromSeconds(1))
                {
                    updateNowPlaying = true;
                    s_lastRequestTime = currentTime;
                }
            }

            if (updateNowPlaying)
            {
                m_mainWindow.NotifyNowPlayingRequested();
            }
        }

        public void UpdateRemainingFrameCount()
        {
            if (m_effectFramesRemaining == 0)
            {
                m_effectFramesRemaining = m_currentlyPlayingRefreshFrameCount;
            }
            else
            {
                m_effectFramesRemaining--;
            }
        }
    }
}
