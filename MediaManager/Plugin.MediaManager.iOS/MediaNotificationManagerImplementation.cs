﻿using MediaPlayer;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using UIKit;

namespace Plugin.MediaManager
{
    public class MediaNotificationManagerImplementation : RemoteControlNotificationManager
    {
        private readonly IMediaManager _mediaManager;

        private IMediaQueue Queue => _mediaManager.MediaQueue;

        private MPNowPlayingInfo NowPlaying
        {
            set { MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = value; }
        }

        public MediaNotificationManagerImplementation(IMediaManager mediaManager)
        {
            _mediaManager = mediaManager;
        }

        public override void StartNotification(IMediaFile mediaFile)
        {
            NowPlaying = CreateNowPlayingInfo(mediaFile);

            base.StartNotification(mediaFile);
        }

        public override void UpdateNotifications(IMediaFile mediaFile, MediaPlayerStatus status)
        {
            NowPlaying = CreateNowPlayingInfo(mediaFile);

            base.UpdateNotifications(mediaFile, status);
        }

        public override void StopNotifications()
        {
            NowPlaying = null;

            base.StopNotifications();
        }

        private MPNowPlayingInfo CreateNowPlayingInfo(IMediaFile mediaFile)
        {
            var metadata = mediaFile.Metadata;

            var nowPlayingInfo = new MPNowPlayingInfo
            {
                Title = metadata.Title,
                AlbumTitle = metadata.Album,
                AlbumTrackNumber = metadata.TrackNumber,
                AlbumTrackCount = metadata.NumTracks,
                Artist = metadata.Artist,
                Composer = metadata.Composer,
                DiscNumber = metadata.DiscNumber,
                Genre = metadata.Genre,
                ElapsedPlaybackTime = _mediaManager.Position.TotalSeconds,
                PlaybackDuration = _mediaManager.Duration.TotalSeconds,
                PlaybackQueueIndex = Queue.Index,
                PlaybackQueueCount = Queue.Count
            };

            if (_mediaManager.Status == MediaPlayerStatus.Playing)
            {
                nowPlayingInfo.PlaybackRate = 1f;
            }
            else
            {
                nowPlayingInfo.PlaybackRate = 0f;
            }

            if (metadata.AlbumArt != null)
            {
                var cover = (UIImage) metadata.AlbumArt;
                nowPlayingInfo.Artwork = new MPMediaItemArtwork(cover);
            }

            return nowPlayingInfo;
        }
    }
}
