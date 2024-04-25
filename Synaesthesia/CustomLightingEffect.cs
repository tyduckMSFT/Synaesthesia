using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.Devices.Lights.Effects;

namespace Synaesthesia
{
    internal abstract class CustomLightingEffect : LightingEffect
    {
        private static readonly DateTime m_unixStart = new DateTime(1970, 1, 1);
        private LampArrayCustomEffect m_effect;

        // This value represents 30 FPS refresh rate for Custom effects
        private const int m_callbackIntervalMs = 33;

        protected struct LampFadeout
        {
            public Windows.UI.Color Color;
            public int FramesLeft;
        }

        protected Dictionary<int, LampFadeout> m_lampFadeouts = new();
        protected int m_fadeoutFrameCount = 10;

        protected CurrentlyPlaying m_currentlyPlaying;
        protected TrackAudioAnalysis m_audioAnalysis;
        protected string m_currentlyPlayingId = "";
        protected bool m_currentlyPlayingIsDirty = true;

        protected Windows.UI.Color[] m_lampColors;

        protected float GetCurrentSpot()
        {
            if (m_currentlyPlaying != null && m_currentlyPlaying.ProgressMs != null)
            {
                long msNow = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                long timestamp = m_mainWindow.CurrentlyPlayingTimestamp;

                var currentSpot = (msNow - timestamp) + m_currentlyPlaying.ProgressMs.Value;
                return (float)currentSpot / 1000.0f;
            }
            else
            {
                return 0;
            }
        }

#nullable enable

        protected Section? GetCurrentSection(float currentSpot)
        {
            if (m_audioAnalysis != null)
            {
                for (int i = 0; i < m_audioAnalysis.Sections.Count; i++)
                {
                    var section = m_audioAnalysis.Sections[i];
                    if (section.Start <= currentSpot && (section.Start + section.Duration) >= currentSpot)
                    {
                        return section;
                    }
                }
            }

            return null;
        }

        protected Segment? GetCurrentSegment(float currentSpot)
        {
            if (m_audioAnalysis != null)
            {
                for (int i = 0; i < m_audioAnalysis.Segments.Count; i++)
                {
                    var segment = m_audioAnalysis.Segments[i];
                    if (segment.Start <= currentSpot && (segment.Start + segment.Duration) >= currentSpot)
                    {
                        return segment;
                    }
                }
            }

            return null;
        }

        protected TimeInterval? GetCurrentBar(float currentSpot)
        {
            if (m_audioAnalysis != null && m_audioAnalysis.Bars != null)
            {
                for (int i = 0; i < m_audioAnalysis.Bars.Count; i++)
                {
                    var bar = m_audioAnalysis.Bars[i];
                    if (bar.Start <= currentSpot && (bar.Start + bar.Duration) >= currentSpot)
                    {
                        return bar;
                    }
                }
            }

            return null;
        }

#nullable disable

        public CustomLightingEffect(MainWindow mainWindow, LampArray lampArray) : base(mainWindow, lampArray)
        {
            m_currentlyPlayingRefreshFrameCount = 60;                       // every 2 sec
            m_effectFramesRemaining = m_currentlyPlayingRefreshFrameCount;

            m_effect = new LampArrayCustomEffect(m_lampArray, m_lampIndices);
            m_effect.UpdateRequested += CustomEffectUpdateRequested;
            m_effect.UpdateInterval = TimeSpan.FromMilliseconds(m_callbackIntervalMs);

            m_lampColors = new Windows.UI.Color[lampArray.LampCount];
        }

        public override ILampArrayEffect GetEffect()
        {
            return m_effect;
        }

        private void CustomEffectUpdateRequested(LampArrayCustomEffect sender, LampArrayUpdateRequestedEventArgs args)
        {
            if (sender == m_effect)
            {
                RefreshNowPlayingIfNeeded();

                DoCustomEffectUpdate(args);
                UpdateRemainingFrameCount();

                UpdateNowPlaying();
            }
        }

        protected abstract void DoCustomEffectUpdate(LampArrayUpdateRequestedEventArgs args);

        private async void UpdateNowPlaying()
        {
            m_currentlyPlaying = m_mainWindow.CurrentlyPlaying;

            if (m_mainWindow.CurrentlyPlayingId != m_currentlyPlayingId)
            {
                m_currentlyPlayingIsDirty = true;
                m_currentlyPlayingId = m_mainWindow.CurrentlyPlayingId;

                if (m_mainWindow.IsCurrentlyPlayingTrack)
                {
                    m_audioAnalysis = await m_mainWindow.SpotifyConnection.GetAudioAnalysis(m_currentlyPlayingId);
                }
                else
                {
                    m_audioAnalysis = null;
                }
            }
        }
    }
}
