using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AVFoundation;
using MediaManager.Library;
using MediaManager.Media;
using MediaManager.Notifications;
using MediaManager.Platforms.Apple.Media;
using MediaManager.Platforms.Apple.Notifications;
using MediaManager.Platforms.Apple.Player;
using MediaManager.Platforms.Apple.Volume;
using MediaManager.Player;
using MediaManager.Volume;

namespace MediaManager
{
    public abstract class AppleMediaManagerBase<TMediaPlayer> : MediaManagerBase, IMediaManager<AppleQueuePlayer> where TMediaPlayer : AppleMediaPlayer, IMediaPlayer<AppleQueuePlayer>, new()
    {
        public AppleMediaManagerBase()
        {
        }

        private IMediaPlayer _mediaPlayer;
        public override IMediaPlayer MediaPlayer
        {
            get
            {
                if (_mediaPlayer == null)
                {
                    _mediaPlayer = new TMediaPlayer();
                }
                return _mediaPlayer;
            }
            set => SetProperty(ref _mediaPlayer, value);
        }

        public AppleMediaPlayer AppleMediaPlayer => (AppleMediaPlayer)MediaPlayer;

        public AppleQueuePlayer Player => ((AppleMediaPlayer)MediaPlayer).Player;

        private IMediaExtractor _mediaExtractor;
        public override IMediaExtractor Extractor
        {
            get
            {
                if (_mediaExtractor == null)
                {
                    _mediaExtractor = new AppleMediaExtractor();
                }
                return _mediaExtractor;
            }
            set => SetProperty(ref _mediaExtractor, value);
        }

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

        private INotificationManager _notification;
        public override INotificationManager Notification
        {
            get
            {
                if (_notification == null)
                    _notification = new NotificationManager();

                return _notification;
            }
            set => SetProperty(ref _notification, value);
        }

        public override TimeSpan Position
        {
            get
            {
                if (Player?.CurrentItem == null)
                {
                    return TimeSpan.Zero;
                }
                if (double.IsNaN(Player.CurrentTime.Seconds) || Player.CurrentTime.IsIndefinite)
                {
                    return TimeSpan.Zero;
                }
                return TimeSpan.FromSeconds(Player.CurrentTime.Seconds);
            }
        }

        public override TimeSpan Duration
        {
            get
            {
                if (AppleMediaPlayer?.Player?.CurrentItem == null)
                {
                    return TimeSpan.Zero;
                }
                if (Player.CurrentItem.Duration.IsIndefinite)
                    return TimeSpan.Zero;
                if (double.IsNaN(Player.CurrentItem.Duration.Seconds))
                {
                    return TimeSpan.Zero;
                }
                return TimeSpan.FromSeconds(Player.CurrentItem.Duration.Seconds);
            }
        }

        public override float Speed
        {
            get
            {
                if (AppleMediaPlayer?.Player != null)
                    return Player.Rate;
                return 0.0f;
            }
            set
            {
                if (AppleMediaPlayer?.Player != null)
                    Player.Rate = value;
            }
        }

        public override Task<bool> PlayQueueItem(IMediaItem mediaItem)
        {
            Queue.CurrentIndex = Queue.IndexOf(mediaItem);
            Player.PlayItemAtIndex(Queue.CurrentIndex);
            return Task.FromResult(true);
        }
    }
}
