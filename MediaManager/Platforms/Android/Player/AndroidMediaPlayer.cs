using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Support.V4.Media.Session;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Ext.Mediasession;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Source.Dash;
using Com.Google.Android.Exoplayer2.Source.Smoothstreaming;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.UI;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2.Util;
using MediaManager.Library;
using MediaManager.Media;
using MediaManager.Platforms.Android.Media;
using MediaManager.Platforms.Android.MediaSession;
using MediaManager.Platforms.Android.Queue;
using MediaManager.Platforms.Android.Video;
using MediaManager.Player;
using MediaManager.Video;

namespace MediaManager.Platforms.Android.Player
{
    public class AndroidMediaPlayer : MediaPlayerBase, IMediaPlayer<SimpleExoPlayer, VideoView>
    {
        protected MediaManagerImplementation MediaManager => CrossMediaManager.Android;
        protected Dictionary<string, string> RequestHeaders => MediaManager.RequestHeaders;
        protected Context Context => MediaManager.Context;
        protected MediaSessionCompat MediaSession => MediaManager.MediaSession;

        protected string UserAgent { get; set; }
        protected DefaultHttpDataSourceFactory HttpDataSourceFactory { get; set; }
        public IDataSourceFactory DataSourceFactory { get; set; }
        public DefaultDashChunkSource.Factory DashChunkSourceFactory { get; set; }
        public DefaultSsChunkSource.Factory SsChunkSourceFactory { get; set; }

        protected DefaultBandwidthMeter BandwidthMeter { get; set; }
        protected AdaptiveTrackSelection.Factory TrackSelectionFactory { get; set; }
        protected DefaultTrackSelector TrackSelector { get; set; }

        protected MediaSessionConnector MediaSessionConnector { get; set; }
        public QueueNavigator QueueNavigator { get; protected set; }
        protected ConcatenatingMediaSource MediaSource { get; set; }
        protected QueueDataAdapter QueueDataAdapter { get; set; }
        protected QueueMediaSourceFactory MediaSourceFactory { get; set; }
        protected TimelineQueueEditor TimelineQueueEditor { get; set; }
        protected MediaSessionConnectorPlaybackPreparer PlaybackPreparer { get; set; }
        public PlayerEventListener PlayerEventListener { get; set; }
        protected RatingCallback RatingCallback { get; set; }

        private SimpleExoPlayer _player;
        public SimpleExoPlayer Player
        {
            get
            {
                if (_player == null)
                    Initialize();
                return _player;
            }

            set => SetProperty(ref _player, value);
        }

        public VideoView PlayerView => VideoView as VideoView;

        private IVideoView _videoView;
        public override IVideoView VideoView
        {
            get => _videoView;
            set
            {
                SetProperty(ref _videoView, value);
                if (PlayerView != null)
                {
                    PlayerView.RequestFocus();

                    //Use private field to prevent calling Initialize here
                    if (_player != null)
                        PlayerView.Player = Player;

                    UpdateVideoView();
                }
            }
        }

        public override void UpdateVideoAspect(VideoAspectMode videoAspectMode)
        {
            if (PlayerView == null)
                return;

            switch (videoAspectMode)
            {
                case VideoAspectMode.None:
                    PlayerView.ResizeMode = AspectRatioFrameLayout.ResizeModeZoom;
                    break;
                case VideoAspectMode.AspectFit:
                    PlayerView.ResizeMode = AspectRatioFrameLayout.ResizeModeFit;
                    break;
                case VideoAspectMode.AspectFill:
                    PlayerView.ResizeMode = AspectRatioFrameLayout.ResizeModeFill;
                    break;
                default:
                    PlayerView.ResizeMode = AspectRatioFrameLayout.ResizeModeZoom;
                    break;
            }
        }

        public override void UpdateShowPlaybackControls(bool showPlaybackControls)
        {
            if (PlayerView == null)
                return;

            PlayerView.UseController = showPlaybackControls;
        }

        public override void UpdateVideoPlaceholder(object value)
        {
            if (PlayerView == null)
                return;

            if (value is Drawable drawable)
            {
                PlayerView.UseArtwork = true;
                PlayerView.DefaultArtwork = drawable;
            }
            else if (value is Bitmap bmp)
            {
                PlayerView.UseArtwork = true;
                PlayerView.DefaultArtwork = new BitmapDrawable(Context.Resources, bmp);
            }
            else
                PlayerView.UseArtwork = false;
        }

        public override void UpdateIsFullWindow(bool isFullWindow)
        {
            if (PlayerView == null)
                return;

            //Player.VideoScalingMode = C.VideoScalingModeScaleToFit;
            if(isFullWindow)
                PlayerView.ResizeMode = AspectRatioFrameLayout.ResizeModeFill;
            else
                PlayerView.ResizeMode = AspectRatioFrameLayout.ResizeModeFit;
        }

        protected int lastWindowIndex = -1;

        protected virtual void Initialize()
        {
            if (RequestHeaders?.Count > 0 && RequestHeaders.TryGetValue("User-Agent", out var userAgent))
                UserAgent = userAgent;
            else
                UserAgent = Util.GetUserAgent(Context, Context.PackageName);

            HttpDataSourceFactory = new DefaultHttpDataSourceFactory(UserAgent);
            UpdateRequestHeaders();

            MediaSource = new ConcatenatingMediaSource();

            DataSourceFactory = new DefaultDataSourceFactory(Context, HttpDataSourceFactory);
            DashChunkSourceFactory = new DefaultDashChunkSource.Factory(DataSourceFactory);
            SsChunkSourceFactory = new DefaultSsChunkSource.Factory(DataSourceFactory);

            Player = new SimpleExoPlayer.Builder(Context).Build();
            Player.VideoSizeChanged += Player_VideoSizeChanged;

            var audioAttributes = new Com.Google.Android.Exoplayer2.Audio.AudioAttributes.Builder()
             .SetUsage(C.UsageMedia)
             .SetContentType(C.ContentTypeMusic)
             .Build();

            Player.SetAudioAttributes(audioAttributes, true);
            Player.SetHandleAudioBecomingNoisy(true);
            Player.SetWakeMode(C.WakeModeNetwork);

            PlayerEventListener = new PlayerEventListener()
            {
                OnPlayerErrorImpl = (ExoPlaybackException exception) =>
                {
                    switch (exception.Type)
                    {
                        case ExoPlaybackException.TypeRenderer:
                        case ExoPlaybackException.TypeSource:
                        case ExoPlaybackException.TypeUnexpected:
                            break;
                    }
                    MediaManager.OnMediaItemFailed(this, new MediaItemFailedEventArgs(MediaManager.Queue.Current, exception, exception.Message));
                },
                OnTracksChangedImpl = (trackGroups, trackSelections) =>
                {
                    InvokeBeforePlaying(this, new MediaPlayerEventArgs(MediaManager.Queue.Current, this));

                    MediaManager.Queue.CurrentIndex = Player.CurrentWindowIndex;

                    InvokeAfterPlaying(this, new MediaPlayerEventArgs(MediaManager.Queue.Current, this));
                },
                OnPlayerStateChangedImpl = (bool playWhenReady, int playbackState) =>
                {
                    switch (playbackState)
                    {
                        case Com.Google.Android.Exoplayer2.Player.StateEnded:
                            if (!Player.HasNext)
                                MediaManager.OnMediaItemFinished(this, new MediaItemEventArgs(MediaManager.Queue.Current));
                            //TODO: This means the whole list is finished. Should we fire an event?
                            break;
                        case Com.Google.Android.Exoplayer2.Player.StateIdle:
                            lastWindowIndex = -1;
                            break;
                        case Com.Google.Android.Exoplayer2.Player.StateBuffering:
                            //MediaManager.Buffered = TimeSpan.FromMilliseconds(Player.BufferedPosition);
                            break;
                        case Com.Google.Android.Exoplayer2.Player.StateReady:
                        default:
                            break;
                    }
                },
                OnPositionDiscontinuityImpl = (int reason) =>
                {
                    switch (reason)
                    {
                        case Com.Google.Android.Exoplayer2.Player.DiscontinuityReasonAdInsertion:
                        case Com.Google.Android.Exoplayer2.Player.DiscontinuityReasonSeek:
                        case Com.Google.Android.Exoplayer2.Player.DiscontinuityReasonSeekAdjustment:
                            break;
                        case Com.Google.Android.Exoplayer2.Player.DiscontinuityReasonPeriodTransition:
                            var currentWindowIndex = Player.CurrentWindowIndex;
                            if (SetProperty(ref lastWindowIndex, currentWindowIndex))
                            {
                                MediaManager.OnMediaItemFinished(this, new MediaItemEventArgs(MediaManager.Queue.Current));
                            }
                            break;
                        case Com.Google.Android.Exoplayer2.Player.DiscontinuityReasonInternal:
                            break;
                    }
                },
                OnLoadingChangedImpl = (bool isLoading) =>
                {
                    if (isLoading && Player.BufferedPosition >= 0)
                        MediaManager.Buffered = TimeSpan.FromMilliseconds(Player.BufferedPosition);
                },
                OnIsPlayingChangedImpl = (bool isPlaying) =>
                {
                    //TODO: Maybe call playing changed event
                },
                OnPlaybackSuppressionReasonChangedImpl = (int playbackSuppressionReason) =>
                {
                    //TODO: Maybe call event
                }
            };
            Player.AddListener(PlayerEventListener);

            ConnectMediaSession();

            if (PlayerView != null && PlayerView.Player == null)
            {
                PlayerView.Player = Player;
            }
        }

        protected virtual void Player_VideoSizeChanged(object sender, Com.Google.Android.Exoplayer2.Video.VideoSizeChangedEventArgs e)
        {
            VideoWidth = e.Width;
            VideoHeight = e.Height;
        }

        public virtual void UpdateRequestHeaders()
        {
            if (RequestHeaders?.Count > 0)
            {
                foreach (var item in RequestHeaders)
                {
                    HttpDataSourceFactory?.DefaultRequestProperties.Set(item.Key, item.Value);
                }
            }
        }

        public virtual void ConnectMediaSession()
        {
            if (MediaSession == null)
                throw new ArgumentNullException(nameof(MediaSession), $"{nameof(MediaSession)} cannot be null. Make sure the {nameof(MediaBrowserService)} sets it up");

            MediaSessionConnector = new MediaSessionConnector(MediaSession);

            QueueNavigator = new QueueNavigator(MediaSession);
            MediaSessionConnector.SetQueueNavigator(QueueNavigator);

            QueueDataAdapter = new QueueDataAdapter(MediaSource);
            MediaSourceFactory = new QueueMediaSourceFactory();
            TimelineQueueEditor = new TimelineQueueEditor(MediaSession.Controller, MediaSource, QueueDataAdapter, MediaSourceFactory);
            MediaSessionConnector.SetQueueEditor(TimelineQueueEditor);

            RatingCallback = new RatingCallback();
            MediaSessionConnector.SetRatingCallback(RatingCallback);

            PlaybackPreparer = new MediaSessionConnectorPlaybackPreparer(Player, MediaSource);
            MediaSessionConnector.SetPlayer(Player);
            MediaSessionConnector.SetPlaybackPreparer(PlaybackPreparer);
        }

        public override async Task Play(IMediaItem mediaItem)
        {
            InvokeBeforePlaying(this, new MediaPlayerEventArgs(mediaItem, this));
            await Play(mediaItem.ToMediaSource());
            InvokeAfterPlaying(this, new MediaPlayerEventArgs(mediaItem, this));
        }

        public override async Task Play(IMediaItem mediaItem, TimeSpan startAt, TimeSpan? stopAt = null)
        {
            InvokeBeforePlaying(this, new MediaPlayerEventArgs(mediaItem, this));


            var mediaSource = stopAt.HasValue ? mediaItem.ToClippingMediaSource(stopAt.Value) : mediaItem.ToMediaSource();
            MediaSource.Clear();
            MediaSource.AddMediaSource(mediaSource);

            Player.Prepare(MediaSource);
            if (startAt != TimeSpan.Zero)
                await SeekTo(startAt);

            await Play();

            InvokeAfterPlaying(this, new MediaPlayerEventArgs(mediaItem, this));
        }

        public virtual async Task Play(IMediaSource mediaSource)
        {
            MediaSource.Clear();
            MediaSource.AddMediaSource(mediaSource);
            Player.Prepare(MediaSource);
            await Play();
        }

        public override Task Play()
        {
            Player.PlayWhenReady = true;
            return Task.CompletedTask;
        }

        public override Task Pause()
        {
            Player.Stop();
            return Task.CompletedTask;
        }

        public override Task SeekTo(TimeSpan position)
        {
            Player.SeekTo((long)position.TotalMilliseconds);
            return Task.CompletedTask;
        }

        public override Task Stop()
        {
            Player.Stop(true);
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (Player != null)
            {
                Player.VideoSizeChanged -= Player_VideoSizeChanged;
                Player.RemoveListener(PlayerEventListener);
                Player.Release();
                Player = null;
            }
        }
    }
}
