using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using SpotifyAPI.Web;
using Swan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;
using Windows.Devices.Lights.Effects;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Synaesthesia
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        enum LightingMode
        {
            AlbumArtwork = 0,
            PitchVisualizer = 1,
            VolumeVisualizer = 2,
            Pinwheel = 3,
            Ripple = 4,
            Flash = 5
        };

        public class LightingDevice
        {
            public string m_deviceId;
            public string m_displayName;
            public LampArray m_lampArray;

            public LightingEffect m_effect;
        }

        public static Windows.UI.Color SpotifyGreenColor = ColorHelper.FromArgb(0xFF, 0x1E, 0xD7, 0x60);

        private readonly DeviceWatcher m_deviceWatcher = DeviceInformation.CreateWatcher(LampArray.GetDeviceSelector());
        private Dictionary<string, LightingDevice> m_lightingDevices;

        private readonly UISettings m_uiSettings = new();

        private readonly DispatcherQueue m_dispatcher = DispatcherQueue.GetForCurrentThread();
        public new DispatcherQueue DispatcherQueue { get => m_dispatcher; }

        private readonly HttpClient m_httpClient = new();
        public HttpClient HttpClient { get => m_httpClient; }

        private LampArrayEffectPlaylist m_playlist;

        private SpotifyConnection m_spotifyConnection;
        public SpotifyConnection SpotifyConnection { get => m_spotifyConnection; }

        private CurrentlyPlaying m_currentlyPlaying;
        public CurrentlyPlaying CurrentlyPlaying { get => m_currentlyPlaying; }

        private long m_lastCurrentlyPlayingRequestedTimestamp = 0;

        private long m_currentlyPlayingTimestamp = 0;
        public long CurrentlyPlayingTimestamp { get => m_currentlyPlayingTimestamp; }

        public bool IsCurrentlyPlayingTrack
        {
            get
            {
                if (m_currentlyPlaying != null && m_currentlyPlaying.Item != null)
                {
                    return m_currentlyPlaying.Item.Type == ItemType.Track;
                }
                else
                {
                    // Don't assume we're dealing with a track that will have audio data
                    return false;
                }
            }
        }


        private string m_currentlyPlayingId = "";
        public string CurrentlyPlayingId { get => m_currentlyPlayingId; }

        // Album artwork mode
        private string m_currentArtworkUrl = "";
        public string CurrentArtworkUrl { get => m_currentArtworkUrl; }

        private string m_selectedMode = "";

        public MainWindow()
        {
            this.InitializeComponent();

            m_lightingDevices = new Dictionary<string, LightingDevice>();

            m_uiSettings.ColorValuesChanged += Settings_ColorValuesChanged;
            EvaluateSystemTheme();

            var enumVals = Enum.GetNames(typeof(LightingMode));
            LightingModeSelector.ItemsSource = enumVals.ToList();
            LightingModeSelector.SelectedIndex = 0;

            m_deviceWatcher.Added += Watcher_Added;
            m_deviceWatcher.Removed += Watcher_Removed;
            m_deviceWatcher.Start();

            m_spotifyConnection = new SpotifyConnection(this);
        }

        private bool IsColorLight(Windows.UI.Color clr)
        {
            return (((5 * clr.G) + (2 * clr.R) + clr.B) > (8 * 128));
        }

        private void EvaluateSystemTheme()
        {
            var background = m_uiSettings.GetColorValue(UIColorType.Background);
            if (IsColorLight(background))
            {
                MainGrid.RequestedTheme = ElementTheme.Light;
            }
            else
            {
                MainGrid.RequestedTheme = ElementTheme.Dark;
            }
        }

        private void Settings_ColorValuesChanged(UISettings sender, object args)
        {
            m_dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                EvaluateSystemTheme();
            });
        }

        public void OnNowPlayingUpdated()
        {
            m_dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                RefreshNowPlayingUI();
            });
        }

        private async void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            if (m_playlist != null)
            {
                m_playlist.Stop();
            }

            try
            {
                LightingDevice device = new LightingDevice
                {
                    m_deviceId = args.Id,
                    m_displayName = args.Name,
                    m_lampArray = await LampArray.FromIdAsync(args.Id)
                };

                lock (m_lightingDevices)
                {
                    if (m_lightingDevices.TryAdd(args.Id, device))
                    {
                        UpdateConnectedDevicesSummary();
                    }
                }

                RefreshEffects();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            if (m_lightingDevices.ContainsKey(args.Id))
            {
                m_lightingDevices.Remove(args.Id);
                UpdateConnectedDevicesSummary();
            }
        }

        private void UpdateConnectedDevicesSummary()
        {
            m_dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                AttachedDevicesText.Text =  "Connected Devices (" + m_lightingDevices.Count + ")";
                if (m_lightingDevices.Count > 0)
                {
                    AttachedDevicesText.Text += ":";
                }

                AttachedDevicesText.Text += "\n";
                foreach (var device in m_lightingDevices)
                {
                    AttachedDevicesText.Text += "\t" + device.Value.m_displayName + "\n";
                }
            });
        }

        public void NotifyNowPlayingRequested()
        {
            _ = m_spotifyConnection.RequestCurrentlyPlaying();
        }

        public void RefreshNowPlayingUI()
        {
            var lastTrackId = m_currentlyPlayingId;

            // Save the current timestamp along with this call to help us know where we are in the track
            // (will help us not peg the Spotify servers for a constant progress report -- we can infer it
            // based on how many milliseconds we are beyond the last time we queried this)
            var currentlyPlaying = m_spotifyConnection.GetCurrentlyPlaying();

            lock (m_currentlyPlayingId)
            {
                if (currentlyPlaying == null)
                {
                    return;
                }

                if (currentlyPlaying.Timestamp > m_lastCurrentlyPlayingRequestedTimestamp)
                {
                    m_lastCurrentlyPlayingRequestedTimestamp = currentlyPlaying.Timestamp;

                    m_currentlyPlaying = currentlyPlaying;
                    m_currentlyPlayingTimestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                }

                if (m_currentlyPlaying != null)
                {
                    // Have we changed tracks? If so, update the UI.
                    var currentlyPlayingId = GetCurrentlyPlayingId();
                    if (lastTrackId != currentlyPlayingId)
                    {
                        m_currentlyPlayingId = currentlyPlayingId;

                        var currentTrackName = "";
                        var currentArtistName = "";
                        var currentAlbumName = "";
                        var currentArtwork = "";

                        switch (m_currentlyPlaying.CurrentlyPlayingType)
                        {
                            case "episode":
                                {
                                    var episode = m_currentlyPlaying.Item as FullEpisode;
                                    currentTrackName = episode.Name;
                                    currentArtistName = episode.Show.Name;
                                    currentAlbumName = episode.Show.Publisher;

                                    if (episode.Images.Count > 0)
                                    {
                                        currentArtwork = episode.Images[0].Url;
                                    }

                                    break;
                                }
                            case "track":
                                {
                                    var track = m_currentlyPlaying.Item as FullTrack;
                                    currentTrackName = track.Name;

                                    foreach (var artist in track.Artists)
                                    {
                                        if (currentArtistName != "")
                                        {
                                            currentArtistName += ", ";
                                        }

                                        currentArtistName += artist.Name;
                                    }

                                    currentAlbumName = track.Album.Name;

                                    if (track.Album.Images.Count > 0)
                                    {
                                        currentArtwork = track.Album.Images[0].Url;
                                    }

                                    break;
                                }
                            default:
                                break;
                        }

                        TrackNameText.Text = currentTrackName;
                        ArtistNameText.Text = currentArtistName;
                        AlbumNameText.Text = currentAlbumName;

                        if (currentArtwork != "")
                        {
                            try
                            {
                                if (m_currentArtworkUrl != currentArtwork)
                                {
                                    m_currentArtworkUrl = currentArtwork;
                                    AlbumArtwork.Source = new BitmapImage(new Uri(currentArtwork));
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                m_currentArtworkUrl = "";
                                AlbumArtwork.Source = null;
                            }
                        }
                    }
                }
                else
                {
                    AlbumArtwork.Source = null;
                }
            }
        }

        private void RefreshEffects()
        {
            lock (m_lightingDevices)
            {
                if (m_lightingDevices.Count > 0)
                {
                    if (m_playlist != null)
                    {
                        m_playlist.Stop();
                        m_playlist = null;
                    }

                    m_playlist = new LampArrayEffectPlaylist
                    {
                        EffectStartMode = LampArrayEffectStartMode.Simultaneous,
                        RepetitionMode = LampArrayRepetitionMode.Forever
                    };

                    foreach (var device in m_lightingDevices.Values)
                    {
                        device.m_effect = GetEffectForCurrentMode(device);
                        m_playlist.Append(device.m_effect.GetEffect());
                    }

                    m_playlist.Start();
                }
            }
        }

        private LightingEffect GetEffectForCurrentMode(LightingDevice device)
        {
            switch (m_selectedMode)
            {
                case "AlbumArtwork":
                    {
                        return new AlbumArtworkEffect(this, device.m_lampArray);
                    }
                case "PitchVisualizer":
                    {
                        return new PitchVisualizerEffect(this, device.m_lampArray);
                    }
                case "VolumeVisualizer":
                    {
                        return new VolumeVisualizerEffect(this, device.m_lampArray);
                    }
                case "Pinwheel":
                    {
                        return new TempoAndMeterEffect(this, device.m_lampArray, TempoAndMeterEffect.TempoAndMeterEffectStyle.Pinwheel);
                    }
                case "Ripple":
                    {
                        return new TempoAndMeterEffect(this, device.m_lampArray, TempoAndMeterEffect.TempoAndMeterEffectStyle.Ripple);
                    }
                case "Flash":
                    {
                        return new TempoAndMeterEffect(this, device.m_lampArray, TempoAndMeterEffect.TempoAndMeterEffectStyle.Flash);
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        private void LightingModeSelectorChanged(object sender, SelectionChangedEventArgs args)
        {
            var mode = args.AddedItems[0] as string;
            if (mode != "" && mode != m_selectedMode)
            {
                m_selectedMode = mode;
                RefreshEffects();
            }
        }

        private string GetCurrentlyPlayingId()
        {
            if (m_currentlyPlaying != null)
            {
                switch (m_currentlyPlaying.CurrentlyPlayingType)
                {
                    case "episode":
                        {
                            var episode = m_currentlyPlaying.Item as FullEpisode;
                            return episode.Id;
                        }
                    case "track":
                        {
                            var track = m_currentlyPlaying.Item as FullTrack;
                            return track.Id;
                        }
                    default:
                        break;
                }
            }

            return "";
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            m_spotifyConnection.PlayPause();
        }

        private void PrevTrackButton_Click(object sender, RoutedEventArgs e)
        {
            m_spotifyConnection.PreviousTrack();
        }

        private void NextTrackButton_Click(object sender, RoutedEventArgs e)
        {
            m_spotifyConnection.NextTrack();
        }
    }
}
