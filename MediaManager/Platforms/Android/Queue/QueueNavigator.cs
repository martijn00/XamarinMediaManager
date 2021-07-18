using System;
using System.Linq;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Ext.Mediasession;
using MediaManager.Library;
using MediaManager.Platforms.Android.Media;

namespace MediaManager.Platforms.Android.Queue
{
    public class QueueNavigator : TimelineQueueNavigator
    {
        protected MediaSessionCompat MediaSession { get; }
        protected MediaManagerImplementation MediaManager => CrossMediaManager.Android;

        public Func<IPlayer, int, IMediaItem> MediaDescriptionRetriever { get; set; }

        public QueueNavigator(MediaSessionCompat mediaSession) : base(mediaSession)
        {
            MediaSession = mediaSession;
        }

        public QueueNavigator(MediaSessionCompat mediaSession, int maxQueueSize) : base(mediaSession, maxQueueSize)
        {
            MediaSession = mediaSession;
        }

        protected QueueNavigator(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public override MediaDescriptionCompat GetMediaDescription(IPlayer player, int windowIndex)
        {
            var mediaItem = MediaManager.Queue.ElementAtOrDefault(windowIndex);
            if(mediaItem == null && MediaDescriptionRetriever != null)
            {
                mediaItem = MediaDescriptionRetriever.Invoke(player, windowIndex);
            }
            return mediaItem?.ToMediaDescription();
        }
    }
}
