using MediaManager.Notifications;
using MediaPlayer;

namespace MediaManager.Platforms.Apple.Notifications
{
    public class NotificationManager : NotificationManagerBase
    {
        public NotificationManager()
        {
            Enabled = true;
        }

        protected MediaManagerImplementation MediaManager = CrossMediaManager.Apple;
        protected MPRemoteCommandCenter CommandCenter = MPRemoteCommandCenter.Shared;

        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                ShowPlayPauseControls = value;
                ShowNavigationControls = value;
            }
        }

        public override bool ShowPlayPauseControls
        {
            get => base.ShowPlayPauseControls;
            set
            {
                base.ShowPlayPauseControls = value;
                if (ShowPlayPauseControls)
                {
                    CommandCenter.TogglePlayPauseCommand.Enabled = true;
                    CommandCenter.TogglePlayPauseCommand.AddTarget(PlayPauseCommand);

                    CommandCenter.PlayCommand.Enabled = true;
                    CommandCenter.PlayCommand.AddTarget(PlayCommand);

                    CommandCenter.PauseCommand.Enabled = true;
                    CommandCenter.PauseCommand.AddTarget(PauseCommand);

                    CommandCenter.StopCommand.Enabled = true;
                    CommandCenter.StopCommand.AddTarget(StopCommand);
                }
                else
                {
                    CommandCenter.TogglePlayPauseCommand.Enabled = false;

                    CommandCenter.PlayCommand.Enabled = false;

                    CommandCenter.PauseCommand.Enabled = false;

                    CommandCenter.StopCommand.Enabled = false;
                }
            }
        }

        public override bool ShowNavigationControls
        {
            get => base.ShowNavigationControls;
            set
            {
                base.ShowNavigationControls = value;
                if (ShowNavigationControls)
                {
                    CommandCenter.NextTrackCommand.Enabled = true;
                    CommandCenter.NextTrackCommand.AddTarget(NextCommand);

                    CommandCenter.PreviousTrackCommand.Enabled = true;
                    CommandCenter.PreviousTrackCommand.AddTarget(PreviousCommand);

                    CommandCenter.SeekBackwardCommand.Enabled = true;
                    CommandCenter.SeekBackwardCommand.AddTarget(SeekBackwardCommand);

                    CommandCenter.SeekForwardCommand.Enabled = true;
                    CommandCenter.SeekForwardCommand.AddTarget(SeekForwardCommand);

                    CommandCenter.SkipBackwardCommand.Enabled = true;
                    CommandCenter.SkipBackwardCommand.PreferredIntervals = new double[] { MediaManager.StepSizeBackward.TotalSeconds };
                    CommandCenter.SkipBackwardCommand.AddTarget(SkipBackwardCommand);

                    CommandCenter.SkipForwardCommand.Enabled = true;
                    CommandCenter.SkipForwardCommand.PreferredIntervals = new double[] { MediaManager.StepSizeForward.TotalSeconds };
                    CommandCenter.SkipForwardCommand.AddTarget(SkipForwardCommand);

                    CommandCenter.ChangeRepeatModeCommand.Enabled = true;
                    CommandCenter.ChangeRepeatModeCommand.AddTarget(RepeatCommand);

                    CommandCenter.ChangeShuffleModeCommand.Enabled = true;
                    CommandCenter.ChangeShuffleModeCommand.AddTarget(ShuffleCommand);
                }
                else
                {
                    CommandCenter.NextTrackCommand.Enabled = false;

                    CommandCenter.PreviousTrackCommand.Enabled = false;

                    CommandCenter.SeekBackwardCommand.Enabled = false;

                    CommandCenter.SeekForwardCommand.Enabled = false;

                    CommandCenter.SkipBackwardCommand.Enabled = false;

                    CommandCenter.SkipForwardCommand.Enabled = false;

                    CommandCenter.ChangeRepeatModeCommand.Enabled = false;

                    CommandCenter.ChangeShuffleModeCommand.Enabled = false;
                }
            }
        }

        public override void UpdateNotification()
        {
            var mediaItem = MediaManager.Queue.Current;

            if (mediaItem == null || !Enabled)
            {
                MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = null;
                return;
            }

            var nowPlayingInfo = new MPNowPlayingInfo
            {
                Title = mediaItem.DisplayTitle,
                AlbumTitle = mediaItem.Album,
                AlbumTrackNumber = mediaItem.TrackNumber,
                AlbumTrackCount = mediaItem.NumTracks,
                Artist = mediaItem.DisplaySubtitle,
                Composer = mediaItem.Composer,
                DiscNumber = mediaItem.DiscNumber,
                Genre = mediaItem.Genre,
                ElapsedPlaybackTime = MediaManager.Position.TotalSeconds,
                PlaybackDuration = MediaManager.Duration.TotalSeconds,
                PlaybackQueueIndex = MediaManager.Queue.CurrentIndex,
                PlaybackQueueCount = MediaManager.Queue.Count,
                IsLiveStream = mediaItem.IsLive
            };

            if (MediaManager.IsPlaying())
            {
                nowPlayingInfo.PlaybackRate = 1f; // MediaManager.Player.Rate?
            }
            else
            {
                nowPlayingInfo.PlaybackRate = 0f;
            }

#if __IOS__ || __TVOS__
            var cover = mediaItem.DisplayImage as UIKit.UIImage;

            if (cover != null)
            {
                //TODO: Why is this deprecated?
                nowPlayingInfo.Artwork = new MPMediaItemArtwork(cover);
            }
#endif
            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;
        }

        protected virtual MPRemoteCommandHandlerStatus SkipBackwardCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.StepBackward();
            return MPRemoteCommandHandlerStatus.Success;
        }

        protected virtual MPRemoteCommandHandlerStatus SkipForwardCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.StepForward();
            return MPRemoteCommandHandlerStatus.Success;
        }

        protected virtual MPRemoteCommandHandlerStatus StopCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.Stop();
            return MPRemoteCommandHandlerStatus.Success;
        }

        protected virtual MPRemoteCommandHandlerStatus SeekForwardCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.StepForward();
            return MPRemoteCommandHandlerStatus.Success;
        }

        protected virtual MPRemoteCommandHandlerStatus SeekBackwardCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.StepBackward();
            return MPRemoteCommandHandlerStatus.Success;
        }

        protected virtual MPRemoteCommandHandlerStatus PreviousCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.PlayPrevious();
            return MPRemoteCommandHandlerStatus.Success;
        }

        protected virtual MPRemoteCommandHandlerStatus PauseCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.Pause();
            return MPRemoteCommandHandlerStatus.Success;
        }

        protected virtual MPRemoteCommandHandlerStatus NextCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.PlayNext();
            return MPRemoteCommandHandlerStatus.Success;
        }

        protected virtual MPRemoteCommandHandlerStatus ShuffleCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.ToggleShuffle();
            return MPRemoteCommandHandlerStatus.Success;
        }

        protected virtual MPRemoteCommandHandlerStatus RepeatCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.ToggleRepeat();
            return MPRemoteCommandHandlerStatus.Success;
        }

        protected virtual MPRemoteCommandHandlerStatus PlayCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.Play();
            return MPRemoteCommandHandlerStatus.Success;
        }

        protected virtual MPRemoteCommandHandlerStatus PlayPauseCommand(MPRemoteCommandEvent arg)
        {
            MediaManager.PlayPause();
            return MPRemoteCommandHandlerStatus.Success;
        }
    }
}
