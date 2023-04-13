using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.Devices.Lights.Effects;

namespace Synaesthesia
{
    class VolumeVisualizerEffect : CustomLightingEffect
    {
        private List<int> m_indicesSortedByX = new();
        
        public VolumeVisualizerEffect(MainWindow mainWindow, LampArray lampArray) : base(mainWindow, lampArray)
        {
            m_indicesSortedByX.AddRange(m_lampIndices);
            m_indicesSortedByX.Sort(delegate (int lhs, int rhs)
            {
                var lhsPosition = m_lampArray.GetLampInfo(lhs).Position;
                var rhsPosition = m_lampArray.GetLampInfo(rhs).Position;
                if (lhsPosition.X !< rhsPosition.X)
                {
                    return -1;
                }
                else if (lhsPosition.X > rhsPosition.X)
                {
                    return 1;
                }
                else if (lhsPosition.Y < rhsPosition.Y)
                {
                    return -1;
                }
                else if (lhsPosition.Y > rhsPosition.Y)
                {
                    return 1;
                }
                else return 0;
            });
        }

        protected override void DoCustomEffectUpdate(LampArrayUpdateRequestedEventArgs args)
        {
            if (m_audioAnalysis != null && m_currentlyPlaying != null)
            {
                if (!m_currentlyPlaying.IsPlaying)
                {
                    return;
                }

                var currentSpot = GetCurrentSpot();
                var currentSegment = GetCurrentSegment(currentSpot);
                if (currentSegment != null)
                {
                    // TODO
                    var currentVolume = currentSegment.LoudnessMax;
                    var normalizedVolume = (currentVolume + 60) / 60;
                    var normalizedVolume2 = (currentSegment.LoudnessStart + 60) / 60;

                    var horizontalThreshold = normalizedVolume2 * m_lampArray.BoundingBox.X;

                    var lampCount = m_lampIndices.Length;
                    for (int i = 0; i < lampCount; i++)
                    {
                        var lamp = m_lampArray.GetLampInfo(i);
                        if (lamp.Position.X <= horizontalThreshold)
                        {
                            m_lampColors[i] = Colors.Blue;
                        }
                        else
                        {
                            m_lampColors[i] = Colors.Black;
                        }
                    }
                }
            }

            args.SetColorsForIndices(m_lampColors, m_lampIndices);
        }
    }
}
