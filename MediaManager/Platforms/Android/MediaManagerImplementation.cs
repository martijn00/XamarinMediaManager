using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Support.V4.Media.Session;
using Com.Google.Android.Exoplayer2;
using MediaManager.Library;
using MediaManager.Media;
using MediaManager.Notifications;
using MediaManager.Platforms.Android.Media;
using MediaManager.Platforms.Android.MediaSession;
using MediaManager.Platforms.Android.Player;
using MediaManager.Platforms.Android.Volume;
using MediaManager.Playback;
using MediaManager.Player;
using MediaManager.Queue;
using MediaManager.Volume;

[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessNetworkState)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessWifiState)]
[assembly: UsesPermission(Android.Manifest.Permission.ForegroundService)]
[assembly: UsesPermission(Android.Manifest.Permission.WakeLock)]
namespace MediaManager
{
    [global::Android.Runtime.Preserve(AllMembers = true)]
    public class MediaManagerImplementation : MediaManagerBase, IMediaManager<SimpleExoPlayer>
    {
        public MediaManagerImplementation()
        {
            IsInitialized = false;
        }

        private Context _context = Application.Context;
        public virtual Context Context
        {
            get => _context;
            set
            {
                if (SetProperty(ref _context, value))
                    SessionActivityPendingIntent = BuildSessionActivityPendingIntent();
            }
        }

        private int _notificationIconResource = Resource.Drawable.exo_notification_small_icon;
        public int NotificationIconResource
        {
            get => _notificationIconResource;
            set
            {
                SetProperty(ref _notificationIconResource, value);
                var playerNotificationManager = (Notification as MediaManager.Platforms.Android.Notifications.NotificationManager)?.PlayerNotificationManager;
                playerNotificationManager?.SetSmallIcon(_notificationIconResource);
            }
        }

        private MediaSessionCompat _mediaSession;
        public MediaSessionCompat MediaSession
        {
            get => _mediaSession;
            set => SetProperty(ref _mediaSession, value);
        }

        private MediaControllerCompat _mediaController;
        public MediaControllerCompat MediaController
        {
            get => _mediaController;
            set => SetProperty(ref _mediaController, value);
        }

        private PendingIntent _sessionActivityPendingIntent;
        public virtual PendingIntent SessionActivityPendingIntent
        {
            get
            {
                if (_sessionActivityPendingIntent == null)
                {
                    _sessionActivityPendingIntent = BuildSessionActivityPendingIntent();
                }
                return _sessionActivityPendingIntent;
            }
            set => SetProperty(ref _sessionActivityPendingIntent, value);
        }

        public virtual PendingIntent BuildSessionActivityPendingIntent()
        {
            Intent sessionIntent;
            // Build a PendingIntent that can be used to launch the UI.
            if (Context is Activity activity)
                sessionIntent = new Intent(Context, activity.GetType());
            else
                sessionIntent = Context.PackageManager.GetLaunchIntentForPackage(Context.PackageName);
            return PendingIntent.GetActivity(Context, 0, sessionIntent, 0);
        }

        public override Dictionary<string, string> RequestHeaders
        {
            get => base.RequestHeaders;
            set
            {
                //On Android we need to update the headers on the player instead of per item.
                if (SetProperty(ref _requestHeaders, value))
                    AndroidMediaPlayer.UpdateRequestHeaders();
            }
        }

        public override void Init()
        {
            EnsureInit();
        }

        public async Task EnsureInit()
        {
            IsInitialized = await MediaBrowserManager.Init();

            if (!IsInitialized)
                throw new Exception("Cannot Initialize MediaManager");
        }

        private MediaBrowserManager _mediaBrowserManager;
        public virtual MediaBrowserManager MediaBrowserManager
        {
            get
            {
                if (_mediaBrowserManager == null)
                    _mediaBrowserManager = new MediaBrowserManager();
                return _mediaBrowserManager;
            }
            set => SetProperty(ref _mediaBrowserManager, value);
        }

        public override TimeSpan StepSizeForward
        {
            get => _stepSizeForward;
            set
            {
                base.StepSizeForward = value;
                var playerNotificationManager = (Notification as MediaManager.Platforms.Android.Notifications.NotificationManager)?.PlayerNotificationManager;
                playerNotificationManager?.SetFastForwardIncrementMs((long)value.TotalMilliseconds);
            }
        }

        public override TimeSpan StepSizeBackward
        {
            get => _stepSizeBackward;
            set
            {
                base.StepSizeBackward = value;
                var playerNotificationManager = (Notification as MediaManager.Platforms.Android.Notifications.NotificationManager)?.PlayerNotificationManager;
                playerNotificationManager?.SetRewindIncrementMs((long)value.TotalMilliseconds);
            }
        }
        [Obsolete("Use StepSizeForward and StepSizeBackward properties instead.", true)]
        public virtual TimeSpan StepSize
        {
            get => throw new NotImplementedException("This property is obsolete. Use StepSizeForwards and StepSizeBackwards properties instead.");
            set => throw new NotImplementedException("This property is obsolete. Use StepSizeForwards and StepSizeBackwards properties instead.");
        }

        private IMediaPlayer _mediaPlayer;
        public override IMediaPlayer MediaPlayer
        {
            get
            {
                if (_mediaPlayer == null)
                    _mediaPlayer = new AndroidMediaPlayer();
                return _mediaPlayer;
            }
            set => SetProperty(ref _mediaPlayer, value);
        }

        public AndroidMediaPlayer AndroidMediaPlayer => (AndroidMediaPlayer)MediaPlayer;
        public SimpleExoPlayer Player => AndroidMediaPlayer?.Player;

        private IVolumeManager _volume;
        public override IVolumeManager Volume
        {
            get
            {
                if (_volume == null)
                    _volume = new VolumeManager();
                return _volume;
            }
            set => SetProperty(ref _volume, value);
        }

        private IMediaExtractor _extractor;
        public override IMediaExtractor Extractor
        {
            get
            {
                if (_extractor == null)
                    _extractor = new MediaExtractor();
                return _extractor;
            }
            set => SetProperty(ref _extractor, value);
        }


        private INotificationManager _notification;
        public override INotificationManager Notification
        {
            get
            {
                if (_notification == null)
                    _notification = new MediaManager.Platforms.Android.Notifications.NotificationManager();

                return _notification;
            }
            set => SetProperty(ref _notification, value);
        }

        public override TimeSpan Position => TimeSpan.FromMilliseconds(MediaController?.PlaybackState?.Position ?? 0);

        public override TimeSpan Duration => MediaController?.Metadata?.ToMediaItem()?.Duration ?? TimeSpan.Zero;

        public override float Speed
        {
            get => MediaController?.PlaybackState?.PlaybackSpeed ?? 0;
            set
            {
                var oldPlaybackParameters = Player.PlaybackParameters;
                if (MediaSession != null)
                    Player.PlaybackParameters = new PlaybackParameters(value, oldPlaybackParameters.Pitch);
            }
        }

        public override async Task PlayAsCurrent(IMediaItem mediaItem)
        {
            await EnsureInit();
            MediaController.GetTransportControls().Prepare();
        }

        public override async Task Pause()
        {
            await EnsureInit();
            MediaController.GetTransportControls().Pause();
        }

        public override async Task Play()
        {
            await EnsureInit();

            if (this.IsStopped())
                MediaController.GetTransportControls().Prepare();

            MediaController.GetTransportControls().Play();
        }

        public override async Task<bool> PlayNext()
        {
            await EnsureInit();

            // If we repeat just the single media item, we do that first
            if (RepeatMode == RepeatMode.One)
            {
                await SeekTo(TimeSpan.FromSeconds(0));
                return true;
            }
            // If we repeat all and there is no next in the Queue, we go back to the first
            else if (RepeatMode == RepeatMode.All && !Queue.HasNext)
            {
                var mediaItem = Queue.First();
                return await PlayQueueItem(mediaItem);
            }
            // Otherwise we try to play the next media item in the queue
            else if (Queue.HasNext)
            {
                MediaController.GetTransportControls().SkipToNext();
                return true;
            }
            return false;
        }

        public override async Task<bool> PlayPrevious()
        {
            await EnsureInit();

            if (Queue.HasPrevious)
            {
                MediaController.GetTransportControls().SkipToPrevious();
                return true;
            }
            return false;
        }

        public override async Task<bool> PlayQueueItem(IMediaItem mediaItem)
        {
            await EnsureInit();

            if (mediaItem == null || !Queue.Contains(mediaItem))
                return false;

            Queue.CurrentIndex = Queue.IndexOf(mediaItem);

            MediaController.GetTransportControls().SkipToQueueItem(Queue.IndexOf(mediaItem));
            return true;
        }

        public override async Task<bool> PlayQueueItem(int index)
        {
            await EnsureInit();

            var mediaItem = Queue.ElementAtOrDefault(index);
            if (mediaItem == null)
                return false;

            Queue.CurrentIndex = index;

            MediaController.GetTransportControls().SkipToQueueItem(index);
            return true;
        }

        public override async Task SeekTo(TimeSpan position)
        {
            await EnsureInit();
            MediaController.GetTransportControls().SeekTo((long)position.TotalMilliseconds);
        }

        public override async Task Stop()
        {
            await EnsureInit();

            MediaController.GetTransportControls().Stop();
            (Notification as MediaManager.Platforms.Android.Notifications.NotificationManager).Player = null;
        }

        public override RepeatMode RepeatMode
        {
            get
            {
                return base.RepeatMode;
            }
            set
            {
                base.RepeatMode = value;
                if (MediaController != null && MediaSession != null)
                {
                    MediaController.GetTransportControls()?.SetRepeatMode((int)value);
                    MediaSession.SetRepeatMode((int)value);
                    Player.RepeatMode = (int)value;
                }
            }
        }

        public override ShuffleMode ShuffleMode
        {
            get
            {
                return base.ShuffleMode;
            }
            set
            {
                base.ShuffleMode = value;
                MediaController?.GetTransportControls()?.SetShuffleMode((int)value);
                MediaSession?.SetShuffleMode((int)value);
            }
        }

        public override bool KeepScreenOn
        {
            get
            {
                return AndroidMediaPlayer?.PlayerView?.KeepScreenOn ?? false;
            }
            set
            {
                if (AndroidMediaPlayer.PlayerView != null)
                    AndroidMediaPlayer.PlayerView.KeepScreenOn = value;
            }
        }

        public virtual async Task<bool> PlayFromIntent(Intent intent)
        {
            if (intent == null)
                return false;

            var action = intent.Action;
            var type = intent.Type;

            if (action == Intent.ActionView)
            {
                string path = "";

                if (type.StartsWith("video/") || type.StartsWith("audio/"))
                {
                    path = intent.DataString;
                }
                if (!string.IsNullOrEmpty(path))
                {
                    await Play(path);
                    return true;
                }
            }
            else if (action == Intent.ActionSend)
            {
                string path = "";

                if (type.StartsWith("video/") || type.StartsWith("audio/"))
                {
                    var receivedUri = intent.GetParcelableExtra(Intent.ExtraStream) as global::Android.Net.Uri;
                    path = receivedUri?.ToString();
                }
                if (!string.IsNullOrEmpty(path))
                {
                    await Play(path);
                    return true;
                }
            }
            else if (action == Intent.ActionSendMultiple)
            {
                IEnumerable<string> mediaUrls = null;

                if (type.StartsWith("video/") || type.StartsWith("audio/"))
                {
                    var receivedUris = intent.GetParcelableArrayListExtra(Intent.ExtraStream);
                    mediaUrls = receivedUris.Cast<global::Android.Net.Uri>().Select(x => x?.ToString());
                }
                if (mediaUrls != null)
                {
                    await Play(mediaUrls);
                    return true;
                }
            }
            return false;
        }
    }
}
