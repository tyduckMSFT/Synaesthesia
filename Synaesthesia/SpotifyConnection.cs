using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synaesthesia
{
    public class SpotifyConnection
    {
        private static EmbedIOAuthServer m_server;

        // Replace this with the client ID registered to your app in the Spotify Developer Dashboard
        private static string m_clientId = "f0bd052478a04a5c8077c5abfdd2cfdb"; // "MY_CLIENT_ID"

        private static string m_verifier = "";

        private static SpotifyClient m_spotifyClient;
        private static MainWindow m_mainWindow;

        private static CurrentlyPlaying m_currentlyPlaying;

        public SpotifyConnection(MainWindow mainWindow)
        {
            InitializeAuthServer();
            m_mainWindow = mainWindow;
        }

        private async void InitializeAuthServer()
        {
            // Make sure "http://localhost:5642/callback" is in your spotify application as redirect uri!
            m_server = new EmbedIOAuthServer(new Uri("http://localhost:5642/callback"), 5642);
            await m_server.Start();

            m_server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
            m_server.ErrorReceived += OnErrorReceived;

            var (verifier, challenge) = PKCEUtil.GenerateCodes();
            m_verifier = verifier;
            var request = new LoginRequest(m_server.BaseUri, m_clientId, LoginRequest.ResponseType.Code)
            {
                CodeChallengeMethod = "S256",
                CodeChallenge = challenge,
                Scope = new List<string>
                {
                    Scopes.UserReadEmail,
                    Scopes.UserLibraryRead,
                    Scopes.UserReadPlaybackState,
                    Scopes.UserModifyPlaybackState,
                    Scopes.UserReadCurrentlyPlaying,
                    Scopes.UserReadPlaybackPosition
                }
            };

            BrowserUtil.Open(request.ToUri());
        }

        private static async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            var initialResponse = await new OAuthClient().RequestToken(
                new PKCETokenRequest(m_clientId, response.Code, new Uri("http://localhost:5642/callback"), m_verifier));

            var authenticator = new PKCEAuthenticator(m_clientId, initialResponse);

            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
            m_spotifyClient = new SpotifyClient(config);

            m_mainWindow.OnNowPlayingUpdated();
        }

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await m_server.Stop();
        }

        public CurrentlyPlaying GetCurrentlyPlaying()
        {
            return m_currentlyPlaying;
        }

        public async Task<CurrentlyPlaying> RequestCurrentlyPlaying()
        {
            if (m_spotifyClient != null)
            {
                try
                {
                    m_currentlyPlaying = await m_spotifyClient.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                    m_mainWindow.OnNowPlayingUpdated();
                }
                catch (SpotifyAPI.Web.APIException e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            return m_currentlyPlaying;
        }

        public async Task<TrackAudioAnalysis> GetAudioAnalysis(string id)
        {
            try
            {
                if (m_spotifyClient != null)
                {
                    return await m_spotifyClient.Tracks.GetAudioAnalysis(id);
                }
            }
            catch (SpotifyAPI.Web.APIException e)
            {
                Console.WriteLine(e.Message);
            }

            return null;
        }

        public async void PlayPause()
        {
            try
            {
                if (m_spotifyClient != null)
                {
                    m_currentlyPlaying = await RequestCurrentlyPlaying();
                    if (m_currentlyPlaying != null)
                    {
                        if (m_currentlyPlaying.IsPlaying)
                        {
                            await m_spotifyClient.Player.PausePlayback();
                        }
                        else
                        {
                            await m_spotifyClient.Player.ResumePlayback();
                        }
                    }
                }
            }
            catch (SpotifyAPI.Web.APIException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public async void NextTrack()
        {
            try
            {
                if (m_spotifyClient != null)
                {
                    await m_spotifyClient.Player.SkipNext();
                }

                await RequestCurrentlyPlaying();
            }
            catch (APIException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public async void PreviousTrack()
        {
            try
            {
                if (m_spotifyClient != null)
                {
                    await m_spotifyClient.Player.SkipPrevious();
                }

                await RequestCurrentlyPlaying();
            }
            catch (APIException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public void ChangeVolume()
        {
            try
            {
                if (m_spotifyClient != null)
                {
                    // m_spotifyClient.Player.SetVolume(new PlayerVolumeRequest());
                }
            }
            catch (APIException e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
