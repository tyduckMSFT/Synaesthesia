using Microsoft.UI;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.Devices.Lights.Effects;

namespace LampArraySpotify
{
    class TempoAndMeterEffect : CustomLightingEffect
    {
        public enum TempoAndMeterEffectStyle
        {
            Pinwheel,
            Ripple,
            Flash
        };

        private TempoAndMeterEffectStyle m_style;
        private int m_meter = 4;
        private float m_tempo = 0;
        private int m_barCount = 0;

        private readonly object m_currentBarAccess = new object();
        private TimeInterval m_currentBar;
        private Section m_currentSection;
        private int m_currentBeatIndex = 0;

        private Dictionary<int, List<int>> m_beatPartitions = new();
        private Dictionary<int, Windows.UI.Color> m_beatPartitionColors = new();
        private Windows.UI.Color m_rippleColor;

        private Dictionary<int, double> m_distancesFromMidpoint = new();

        private readonly Dictionary<int, List<double>> m_beatPartitionAngles = new()
        {
            [3] = new List<double>() { (-1 * Math.PI) / 3, Math.PI / 3, Math.PI },
            [4] = new List<double>() { (-1 * Math.PI) / 2, 0, Math.PI / 2, Math.PI },
            [5] = new List<double>() { (-3 * Math.PI) / 5, (-1 * Math.PI) / 5, Math.PI / 5, (3 * Math.PI) / 5, Math.PI },
            [6] = new List<double>() { (-2 * Math.PI) / 3, (-1 * Math.PI) / 3, 0, Math.PI / 3, (2 * Math.PI) / 3, Math.PI },
            [7] = new List<double>() { (-5 * Math.PI) / 7, (-3 * Math.PI) / 7, (-1 * Math.PI) / 7, Math.PI / 7, (3 * Math.PI) / 7, (5 * Math.PI) / 7, Math.PI }
        };

        private Random m_random = new();

        public TempoAndMeterEffect(MainWindow mainWindow, LampArray lampArray, TempoAndMeterEffectStyle style) :
            base(mainWindow, lampArray)
        {
            m_style = style;

            if (m_style == TempoAndMeterEffectStyle.Pinwheel)
            {
                ResetPartitions();
            }
            else if (m_style == TempoAndMeterEffectStyle.Ripple)
            {
                m_rippleColor = GetRandomColor();

                double m_midpointX = lampArray.BoundingBox.X / 2;
                double m_midpointY = lampArray.BoundingBox.Y / 2;
                double maxDist = Math.Sqrt(Math.Pow(m_midpointX, 2) + Math.Pow(m_midpointY, 2));

                // TODO: Sort lamps by distances to midpoint
                for (int i = 0; i < lampArray.LampCount; i++)
                {
                    var lampPos = lampArray.GetLampInfo(i).Position;
                    double distX = Math.Abs(m_midpointX - lampPos.X);
                    double distY = Math.Abs(m_midpointY - lampPos.Y);

                    // Normalize the distance based on the bounding box
                    m_distancesFromMidpoint[i] = Math.Sqrt(Math.Pow(distX, 2) + Math.Pow(distY, 2)) / maxDist;
                }
            }
        }

        private int CalculateFadeoutFrameCount()
        {
            // Convert from beats per minute -> frames per beat
            var divisor = 60 * 30;
            if (m_style == TempoAndMeterEffectStyle.Pinwheel)
            {
                return (int)Math.Floor(divisor / (double)m_tempo) * 2;
            }
            else
            {
                return (int)Math.Floor(divisor / (double)m_tempo);
            }
        }

        private void ResetPartitions()
        {
            m_beatPartitions.Clear();

            var midpointX = m_lampArray.BoundingBox.X / 2;
            var midpointY = m_lampArray.BoundingBox.Y / 2;

            for (int i = 0; i < m_meter; i++)
            {
                m_beatPartitions.Add(i, new List<int>());
                m_beatPartitionColors[i] = GetRandomColor();
            }

            if (!m_beatPartitionAngles.ContainsKey(m_meter))
            {
                return;
            }

            var beatPartitionAngles = m_beatPartitionAngles[m_meter];
            for (int i = 0; i < m_lampArray.LampCount; i++)
            {
                var lamp = m_lampArray.GetLampInfo(i);
                var x = midpointX - lamp.Position.X;
                var y = midpointY - lamp.Position.Y;
                var angle = Math.Atan2(y, x);

                for (int j = 0; j < beatPartitionAngles.Count; j++)
                {
                    var partitionAngle = beatPartitionAngles[j];
                    if (angle <= partitionAngle)
                    {
                        m_beatPartitions[j].Add(i);
                        break;
                    }
                }
            }            
        }

        private Windows.UI.Color GetRandomColor()
        {
            byte[] bytes = new byte[3];
            m_random.NextBytes(bytes);
            return ColorHelper.FromArgb(0xFF, bytes[0], bytes[1], bytes[2]);
        }

        protected override void DoCustomEffectUpdate(LampArrayUpdateRequestedEventArgs args)
        {
            lock (m_currentBarAccess)
            {
                if (m_currentlyPlayingIsDirty)
                {
                    m_currentlyPlayingIsDirty = false;
                    m_currentBar = null;
                    m_barCount = 0;
                }

                var currentSpot = GetCurrentSpot();

                if (m_currentSection == null || currentSpot < m_currentSection.Start || (m_currentSection.Start + m_currentSection.Duration) < currentSpot)
                {
                    m_currentSection = GetCurrentSection(currentSpot);
                    if (m_currentSection == null)
                    {
                        return;
                    }

                    var meter = m_currentSection.TimeSignature;
                    if (meter != m_meter)
                    {
                        // Repartition lights based on the new time signature
                        m_meter = meter;

                        if (m_style == TempoAndMeterEffectStyle.Pinwheel)
                        {
                            ResetPartitions();
                        }
                    }

                    var tempo = m_currentSection.Tempo;
                    if (tempo != m_tempo)
                    {
                        m_tempo = tempo;
                        m_fadeoutFrameCount = CalculateFadeoutFrameCount() + 1;
                    }
                }

                if (m_currentBar == null || currentSpot < m_currentBar.Start || (m_currentBar.Start + m_currentBar.Duration) < currentSpot)
                {
                    m_currentBar = GetCurrentBar(currentSpot);
                    
                    if (m_currentBar != null)
                    {
                        m_rippleColor = GetRandomColor();
                        m_barCount++;
                    }
                }

                if (m_currentBar == null)
                {
                    return;
                }

                var beatDuration = m_currentBar.Duration / m_meter;
                // Subdivide the current bar and figure out which beat we're in
                var oldBeatIndex = m_currentBeatIndex;
                var newBeatIndex = 0;
                bool beatIndexUpdated = false;

                for (int i = 0; i < m_meter; i++)
                {
                    if (m_currentBar.Start + ((i + 1) * beatDuration) >= currentSpot)
                    {
                        newBeatIndex = i;

                        if (newBeatIndex != oldBeatIndex)
                        {
                            m_currentBeatIndex = newBeatIndex;
                            beatIndexUpdated = true;
                            m_rippleColor = GetRandomColor();
                        }

                        break;
                    }
                };

                if (m_style == TempoAndMeterEffectStyle.Pinwheel)
                {
                    if (beatIndexUpdated)
                    {
                        m_beatPartitionColors[newBeatIndex] = GetRandomColor();
                    }

                    var currentBeatIndices = m_beatPartitions[m_currentBeatIndex];
                    if (currentBeatIndices != null)
                    {
                        foreach (var index in currentBeatIndices)
                        {
                            if (!m_lampFadeouts.ContainsKey(index))
                            {
                                m_lampFadeouts[index] = new LampFadeout
                                {
                                    FramesLeft = m_fadeoutFrameCount,
                                    Color = m_beatPartitionColors[m_currentBeatIndex]
                                };
                            }
                        }
                    }
                }
                else if (m_style == TempoAndMeterEffectStyle.Ripple)
                {
                    var currentBeatStart = m_currentBar.Start + (m_currentBeatIndex * beatDuration);
                    var currentSpotBeatProgress = (currentSpot - currentBeatStart) / beatDuration;
                    for (int i = 0; i < m_lampArray.LampCount; i++)
                    {
                        if (m_distancesFromMidpoint[i] < currentSpotBeatProgress && !m_lampFadeouts.ContainsKey(i))
                        {
                            m_lampColors[i] = m_rippleColor;
                        }
                    }
                }
                else if (m_style == TempoAndMeterEffectStyle.Flash)
                {
                    if (beatIndexUpdated)
                    {
                        for (int i = 0; i < m_lampArray.LampCount; i++)
                        {
                            m_lampFadeouts[i] = new LampFadeout
                            {
                                FramesLeft = m_fadeoutFrameCount,
                                Color = m_rippleColor
                            };
                        }
                    }
                }

                if (m_style != TempoAndMeterEffectStyle.Ripple)
                {
                    if (m_lampArray.LampCount >= m_meter)
                    {
                        for (int i = 0; i < m_lampArray.LampCount; i++)
                        {
                            if (m_lampFadeouts.ContainsKey(i))
                            {
                                var fadeout = m_lampFadeouts[i];
                                if (fadeout.FramesLeft == 0)
                                {
                                    m_lampColors[i] = Colors.Black;
                                    m_lampFadeouts.Remove(i);
                                }
                                else
                                {
                                    float scale = (float)fadeout.FramesLeft / (float)m_fadeoutFrameCount;

                                    fadeout.FramesLeft--;
                                    m_lampColors[i] = ColorHelper.FromArgb((byte)(0xFF * scale), fadeout.Color.R, fadeout.Color.G, fadeout.Color.B);
                                    m_lampFadeouts[i] = fadeout;
                                }
                            }
                            else
                            {
                                m_lampColors[i] = Colors.Black;
                            }
                        }
                    }
                }
            }

            args.SetColorsForIndices(m_lampColors, m_lampIndices);
        }
    }
}
