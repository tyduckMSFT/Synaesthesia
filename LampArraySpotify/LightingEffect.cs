using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.Devices.Lights.Effects;

namespace LampArraySpotify
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

        public void RefreshNowPlayingIfNeeded()
        {
            if (m_effectFramesRemaining == 0)
            {
                m_effectFramesRemaining = m_currentlyPlayingRefreshFrameCount;
                m_mainWindow.NotifyNowPlayingRequested();
            }
            else
            {
                m_effectFramesRemaining--;
            }
        }
    }
}
