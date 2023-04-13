using Microsoft.UI;
using Microsoft.UI.Dispatching;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.Devices.Lights.Effects;
using Windows.System;
using Windows.UI;

namespace Synaesthesia
{
    internal class PitchVisualizerEffect : CustomLightingEffect
    {
        private float m_pitchConfidenceThreshold = 0.7f;

        private enum Pitch : int
        {
            C = 0,
            CSharp = 1,
            D = 2,
            DSharp = 3,
            E = 4,
            F = 5,
            FSharp = 6,
            G = 7,
            GSharp = 8,
            A = 9,
            ASharp = 10,
            B = 11
        };

        private struct PitchVisualInfo
        {
            // public Color color;
            public readonly Windows.UI.Color Color;
            public readonly List<VirtualKey> Keys;

            public PitchVisualInfo(Windows.UI.Color colorIn, List<VirtualKey> vks)
            {
                Color = colorIn;
                Keys = vks;
            }
        };

        /* Assume en-US QWERTY for this effect */
        private static readonly List<PitchVisualInfo> s_pitchInfos = new()
        {
            new PitchVisualInfo(Colors.Tan, new List<VirtualKey>()
            {
                VirtualKey.Escape,
                VirtualKey.Tab,
                VirtualKey.Shift,
                VirtualKey.LeftShift,
                VirtualKey.RightShift,
                VirtualKey.Control,
                VirtualKey.LeftControl,
                VirtualKey.RightControl,
                VirtualKey.CapitalLock,
                VirtualKey.Enter,
                VirtualKey.Back,
                VirtualKey.F11,
                VirtualKey.F12,
                (VirtualKey) 0xC0, /* ~ key */
                (VirtualKey) 0xDC, /* \ key */
            }), /* C */ 
            new PitchVisualInfo(Colors.Magenta, new List<VirtualKey>()
            {
                VirtualKey.A,
                VirtualKey.Q,
                VirtualKey.Number1,
            }), /* C# */
            new PitchVisualInfo(Colors.Lime, new List<VirtualKey>()
            {
                VirtualKey.F1,
                VirtualKey.Number2,
                VirtualKey.S,
                VirtualKey.X,
                VirtualKey.Z,
                VirtualKey.W,
                VirtualKey.LeftWindows
            }), /* D */ 
            new PitchVisualInfo(Colors.White, new List<VirtualKey>()
            {
                VirtualKey.F2,
                VirtualKey.Number3,
                VirtualKey.D,
                VirtualKey.E
            }), /* D# */
            new PitchVisualInfo(Colors.Yellow, new List<VirtualKey>()
            {
                VirtualKey.F,
                VirtualKey.V,
                VirtualKey.C,
                VirtualKey.R,
                VirtualKey.Number4,
                VirtualKey.F3
            }), /* E */
            new PitchVisualInfo(Colors.Orange, new List<VirtualKey>()
            {
                VirtualKey.G,
                VirtualKey.B,
                VirtualKey.T,
                VirtualKey.Number5,
                VirtualKey.F4,
                VirtualKey.Space
            }), /* F */
            new PitchVisualInfo(Colors.Green, new List<VirtualKey>()
            {
                VirtualKey.F5,
                VirtualKey.Number6,
                VirtualKey.Number7,
                VirtualKey.Y,
                VirtualKey.H
            }), /* F# */
            new PitchVisualInfo(Colors.Cyan, new List<VirtualKey>()
            {
                VirtualKey.F6,
                VirtualKey.Number8,
                VirtualKey.U,
                VirtualKey.J,
                VirtualKey.N,
                VirtualKey.M
            }), /* G */
            new PitchVisualInfo(Colors.Maroon, new List<VirtualKey>()
            {
                VirtualKey.F7,
                VirtualKey.Number9,
                VirtualKey.I,
                VirtualKey.K
            }), /* G# */
            new PitchVisualInfo(Colors.Red, new List<VirtualKey>()
            {
                VirtualKey.F8,
                VirtualKey.Number0,
                VirtualKey.O,
                VirtualKey.L,
                (VirtualKey) 0xBC, /* , key */
                (VirtualKey) 0xBE, /* . key */
            }), /* A */
            new PitchVisualInfo(Colors.Blue, new List<VirtualKey>()
            {
                VirtualKey.F9,
                VirtualKey.P,
                (VirtualKey) 0xBD, /* - key */
                (VirtualKey) 0xBA, /* ; key */
            }), /* A# */
            new PitchVisualInfo(Colors.Violet, new List<VirtualKey>()
            {
                VirtualKey.F10,
                (VirtualKey) 0xBB, /* = key */
                (VirtualKey) 0xDB, /* [ key */
                (VirtualKey) 0xDD, /* ] key */
                (VirtualKey) 0xDE, /* ' key */               
                (VirtualKey) 0xBF /* / key */
            }) /* B */
        };

        private Dictionary<Pitch, List<int>> m_pitchMap = new();

        public PitchVisualizerEffect(MainWindow mainWindow, LampArray lampArray) : base(mainWindow, lampArray)
        {
            if (m_lampArray.SupportsVirtualKeys)
            {
                for (int i = 0; i < s_pitchInfos.Count; i++)
                {
                    var pitch = (Pitch)i;
                    var pitchInfo = s_pitchInfos[i];

                    List<int> indices = new();
                    foreach (var vk in pitchInfo.Keys)
                    {
                        indices.AddRange(m_lampArray.GetIndicesForKey(vk));
                    }

                    if (indices.Count > 0)
                    {
                        m_pitchMap[pitch] = indices;
                    }
                }
            }
        }

        protected override void DoCustomEffectUpdate(LampArrayUpdateRequestedEventArgs args)
        {
            if (m_audioAnalysis != null && m_currentlyPlaying != null)
            {
                m_currentlyPlayingIsDirty = false;
                var currentSpot = GetCurrentSpot();
                var currentSegment = GetCurrentSegment(currentSpot);
                if (currentSegment != null)
                {
                    var pitches = currentSegment.Pitches;
                    if (m_pitchMap.Count > 0)
                    {
                        for (var i = 0; i < pitches.Count; i++)
                        {
                            if (pitches[i] > m_pitchConfidenceThreshold)
                            {
                                var pitchMapKeys = m_pitchMap[(Pitch)i];
                                var pitchInfo = s_pitchInfos[i];

                                for (var j = 0; j < pitchMapKeys.Count; j++)
                                {
                                    var key = pitchMapKeys[j];
                                    var fadeout = new LampFadeout();
                                    fadeout.Color = pitchInfo.Color;
                                    fadeout.FramesLeft = m_fadeoutFrameCount;
                                    m_lampFadeouts[key] = fadeout;
                                }
                            }
                        }

                        for (int i = 0; i < m_lampArray.LampCount; i++)
                        {
                            if (m_lampFadeouts.ContainsKey(i))
                            {
                                var fadeout = m_lampFadeouts[i];
                                fadeout.FramesLeft--;

                                if (fadeout.FramesLeft == 0)
                                {
                                    m_lampColors[i] = Colors.Black;
                                    m_lampFadeouts.Remove(i);
                                }
                                else
                                {
                                    float scale = (float)fadeout.FramesLeft / (float)m_fadeoutFrameCount;
                                    byte r = (byte)(fadeout.Color.R * scale);
                                    byte g = (byte)(fadeout.Color.G * scale);
                                    byte b = (byte)(fadeout.Color.B * scale);

                                    m_lampColors[i] = ColorHelper.FromArgb(0xFF, r, g, b);
                                    m_lampFadeouts[i] = fadeout;
                                }
                            }
                            else
                            {
                                m_lampColors[i] = Colors.Black;
                            }
                        }
                    }

                    else
                    {
                        // TODO: Support for non-vk devices
                    }                    

                    args.SetColorsForIndices(m_lampColors, m_lampIndices);
                }                
            }
            else
            {
                args.SetColor(MainWindow.SpotifyGreenColor);
            }
        }
    }
}
