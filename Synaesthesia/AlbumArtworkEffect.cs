using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.Devices.Lights.Effects;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Synaesthesia
{
    internal class AlbumArtworkEffect : LightingEffect
    {
        private readonly LampArrayBitmapEffect m_effect;

        // Album artwork mode
        private string m_currentArtworkUrl = "";
        private SoftwareBitmap m_currentArtworkSoftwareBitmap;

        public AlbumArtworkEffect(MainWindow mainWindow, LampArray lampArray) : base(mainWindow, lampArray)
        {
            m_currentlyPlayingRefreshFrameCount = 40;
            m_effectFramesRemaining = m_currentlyPlayingRefreshFrameCount;

            m_effect = new LampArrayBitmapEffect(m_lampArray, m_lampIndices);
            m_effect.BitmapRequested += BitmapEffectUpdateRequested;
            m_effect.UpdateInterval = TimeSpan.FromMilliseconds(500);
        }

        public override ILampArrayEffect GetEffect()
        {
            return m_effect;
        }

        private async void BitmapEffectUpdateRequested(LampArrayBitmapEffect sender, LampArrayBitmapRequestedEventArgs args)
        {
            RefreshNowPlayingIfNeeded();

            if (m_currentArtworkSoftwareBitmap != null)
            {
                args.UpdateBitmap(m_currentArtworkSoftwareBitmap);
            }

            // If the artwork URL has changed, schedule updating the bitmap for future callbacks
            // For a real implementation, we shouldn't download the image twice (Hackathon time constraints)
            var currentArtwork = m_mainWindow.CurrentArtworkUrl;
            if (currentArtwork != m_currentArtworkUrl)
            {
                m_currentArtworkUrl = currentArtwork;

                // Load image from web
                var response = await m_mainWindow.HttpClient.GetAsync(currentArtwork);
                response.EnsureSuccessStatusCode();

                var stream = response.Content.ReadAsStream();
                IRandomAccessStream randomAccessStream = stream.AsRandomAccessStream();
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
                m_currentArtworkSoftwareBitmap = await decoder.GetSoftwareBitmapAsync();
            }
        }
    }
}
