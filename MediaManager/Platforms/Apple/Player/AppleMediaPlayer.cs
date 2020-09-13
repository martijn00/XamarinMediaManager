using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using AVFoundation;
using CoreMedia;
using Foundation;
using MediaManager.Library;
using MediaManager.Media;
using MediaManager.Platforms.Apple.Media;
using MediaManager.Platforms.Apple.Playback;
using MediaManager.Player;
using MediaManager.Queue;

namespace MediaManager.Platforms.Apple.Player
{
    public abstract class AppleMediaPlayer : MediaPlayerBase, IMediaPlayer<AppleQueuePlayer>
    {
        protected MediaManagerImplementation MediaManager = CrossMediaManager.Apple;

        public AppleMediaPlayer()
        {
        }

        private AppleQueuePlayer _player;
        public AppleQueuePlayer Player
        {
            get
            {
                if (_player == null)
                    Initialize();
                return _player;
            }
            set => SetProperty(ref _player, value);
        }

        public int TimeScale { get; set; } = 60;

        private NSObject didFinishPlayingObserver;
        private NSObject itemFailedToPlayToEndTimeObserver;
        private NSObject errorObserver;
        private NSObject playbackStalledObserver;
        private NSObject playbackTimeObserver;

        private IDisposable rateToken;
        private IDisposable statusToken;
        private IDisposable timeControlStatusToken;
        private IDisposable loadedTimeRangesToken;
        private IDisposable reasonForWaitingToPlayToken;
        private IDisposable playbackLikelyToKeepUpToken;
        private IDisposable playbackBufferFullToken;
        private IDisposable playbackBufferEmptyToken;
        private IDisposable presentationSizeToken;
        private IDisposable timedMetaDataToken;

        public override event BeforePlayingEventHandler BeforePlaying;
        public override event AfterPlayingEventHandler AfterPlaying;

        private IMediaQueue _currentQueue;

        protected virtual void Initialize()
        {
            Player = new AppleQueuePlayer();

            didFinishPlayingObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.DidPlayToEndTimeNotification, DidFinishPlaying);
            itemFailedToPlayToEndTimeObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.ItemFailedToPlayToEndTimeNotification, DidErrorOcurred);
            errorObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.NewErrorLogEntryNotification, DidErrorOcurred);
            playbackStalledObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.PlaybackStalledNotification, DidErrorOcurred);

            var options = NSKeyValueObservingOptions.Initial | NSKeyValueObservingOptions.New;
            rateToken = Player.AddObserver("rate", options, RateChanged);
            statusToken = Player.AddObserver("status", options, StatusChanged);
            timeControlStatusToken = Player.AddObserver("timeControlStatus", options, TimeControlStatusChanged);
            reasonForWaitingToPlayToken = Player.AddObserver("reasonForWaitingToPlay", options, ReasonForWaitingToPlayChanged);

            loadedTimeRangesToken = Player.AddObserver("currentItem.loadedTimeRanges", options, LoadedTimeRangesChanged);
            playbackLikelyToKeepUpToken = Player.AddObserver("currentItem.playbackLikelyToKeepUp", options, PlaybackLikelyToKeepUpChanged);
            playbackBufferFullToken = Player.AddObserver("currentItem.playbackBufferFull", options, PlaybackBufferFullChanged);
            playbackBufferEmptyToken = Player.AddObserver("currentItem.playbackBufferEmpty", options, PlaybackBufferEmptyChanged);
            presentationSizeToken = Player.AddObserver("currentItem.presentationSize", options, PresentationSizeChanged);
            timedMetaDataToken = Player.AddObserver("currentItem.timedMetadata", options, TimedMetaDataChanged);

            CrossMediaManager.Apple.PropertyChanged += MediaManager_PropertyChanged;

            _currentQueue = CrossMediaManager.Apple.Queue;
            _currentQueue.MediaItems.CollectionChanged += CurrentQueueOnCollectionChanged;
        }

        private void MediaManager_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CrossMediaManager.Apple.Queue))
            {
                OnQueueChanged();
            }
        }

        private void OnQueueChanged()
        {
            if (_currentQueue != null)
            {
                _currentQueue.MediaItems.CollectionChanged -= CurrentQueueOnCollectionChanged;
            }

            _currentQueue = CrossMediaManager.Apple.Queue;
            _currentQueue.MediaItems.CollectionChanged += CurrentQueueOnCollectionChanged;
        }

        private void CurrentQueueOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    if (Player.AllItems.Count != MediaManager.Queue.Count)
                    {
                        for (var i = 0; i < e.NewItems.Count; i++)
                        {
                            var mediaItem = (IMediaItem)e.NewItems[i];
                            Player.InsertItem(mediaItem.ToAVPlayerItem(), e.NewStartingIndex + i);
                        }
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                    for (var i = e.NewItems.Count - 1; i >= 0; i--)
                    {
                        var item = Player.Items[e.OldStartingIndex + i];
                        Player.RemoveItem(e.OldStartingIndex + i);
                        Player.InsertItem(item, Player.Items[e.NewStartingIndex - 1]);
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    if (e.NewItems.Count > 1)
                    {
                        for (int i = 0; i > e.NewItems.Count; i++)
                            Player.RemoveItem(i);
                    }
                    else
                        Player.RemoveItem(e.OldStartingIndex);
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                    for (var i = 0; i < e.NewItems.Count; i++)
                    {
                        var replace = Player.Items[e.OldStartingIndex + i];
                        var mediaItem = (IMediaItem)e.NewItems[i];
                        Player.InsertItem(mediaItem.ToAVPlayerItem(), replace);
                        Player.RemoveItem(replace);
                    }

                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    Player.RemoveAllItems();
                    break;
            }
        }

        protected virtual void PresentationSizeChanged(NSObservedChange obj)
        {
            if (Player.CurrentItem != null && !Player.CurrentItem.PresentationSize.IsEmpty)
            {
                VideoWidth = (int)Player.CurrentItem.PresentationSize.Width;
                VideoHeight = (int)Player.CurrentItem.PresentationSize.Height;
            }
        }

        protected virtual void TimedMetaDataChanged(NSObservedChange obj)
        {
            if (MediaManager.Queue.Current == null || MediaManager.Queue.Current.IsMetadataExtracted)
                return;

            if (obj.NewValue is NSArray array && array.Count > 0)
            {
                var avMetadataItem = array.GetItem<AVMetadataItem>(0);
                if (avMetadataItem != null && !string.IsNullOrEmpty(avMetadataItem.StringValue))
                {
                    var split = avMetadataItem.StringValue.Split(" - ");
                    MediaManager.Queue.Current.Artist = split.FirstOrDefault();

                    if (split.Length > 1)
                    {
                        MediaManager.Queue.Current.Title = split.LastOrDefault();
                    }
                }
            }
        }

        protected virtual void StatusChanged(NSObservedChange obj)
        {
            MediaManager.State = Player.Status.ToMediaPlayerState();
        }

        protected virtual void PlaybackBufferEmptyChanged(NSObservedChange obj)
        {
        }

        protected virtual void PlaybackBufferFullChanged(NSObservedChange obj)
        {
        }

        protected virtual void PlaybackLikelyToKeepUpChanged(NSObservedChange obj)
        {
        }

        protected virtual void ReasonForWaitingToPlayChanged(NSObservedChange obj)
        {
            var reason = Player.ReasonForWaitingToPlay;
            if (reason == null)
            {
            }
            else if (reason == AVPlayer.WaitingToMinimizeStallsReason)
            {
            }
            else if (reason == AVPlayer.WaitingWhileEvaluatingBufferingRateReason)
            {
            }
            else if (reason == AVPlayer.WaitingWithNoItemToPlayReason)
            {
            }
        }

        protected virtual void LoadedTimeRangesChanged(NSObservedChange obj)
        {
            var buffered = TimeSpan.Zero;
            if (Player?.CurrentItem != null && Player.CurrentItem.LoadedTimeRanges.Any())
            {
                buffered =
                    TimeSpan.FromSeconds(
                        Player.CurrentItem.LoadedTimeRanges.Select(
                            tr => tr.CMTimeRangeValue.Start.Seconds + tr.CMTimeRangeValue.Duration.Seconds).Max());

                MediaManager.Buffered = buffered;
            }
        }

        protected virtual void RateChanged(NSObservedChange obj)
        {
            //TODO: Maybe set the rate from here
        }

        protected virtual void TimeControlStatusChanged(NSObservedChange obj)
        {
            if (Player.Status != AVPlayerStatus.Unknown)
                MediaManager.State = Player.TimeControlStatus.ToMediaPlayerState();
        }

        protected virtual void DidErrorOcurred(NSNotification obj)
        {
            //TODO: Error should not be null after this or it will crash.
            var error = Player?.CurrentItem?.Error ?? new NSError();
            MediaManager.OnMediaItemFailed(this, new MediaItemFailedEventArgs(MediaManager.Queue?.Current, new NSErrorException(error), error?.LocalizedDescription));
        }

        protected virtual async void DidFinishPlaying(NSNotification obj)
        {
            MediaManager.OnMediaItemFinished(this, new MediaItemEventArgs(MediaManager.Queue.Current));

            //TODO: Android has its own queue and goes to next. Maybe use native apple queue
            /*var succesfullNext = await MediaManager.PlayNext();
            if (!succesfullNext)
            {
                await Stop();
            }*/
        }

        public override Task Pause()
        {
            Player.Pause();
            return Task.CompletedTask;
        }

        public override async Task Play(IMediaItem mediaItem)
        {
            BeforePlaying?.Invoke(this, new MediaPlayerEventArgs(mediaItem, this));
            var index = _currentQueue.IndexOf(mediaItem);
            if (index >= 0)
                await Play(index);
            else
            {
                Player.RemoveAllItems();
                Player.ReplaceCurrentItemWithPlayerItem(mediaItem.ToAVPlayerItem());
                await Play();
            }
            AfterPlaying?.Invoke(this, new MediaPlayerEventArgs(mediaItem, this));
        }

        public override async Task Play(IMediaItem mediaItem, TimeSpan startAt, TimeSpan? stopAt = null)
        {
            if (stopAt is TimeSpan endTime)
            {
                var values = new NSValue[]
                {
                NSValue.FromCMTime(CMTime.FromSeconds(endTime.TotalSeconds, TimeScale))
                };

                playbackTimeObserver = Player.AddBoundaryTimeObserver(values, null, OnPlayerBoundaryReached);
            }

            await Play(mediaItem);

            if (startAt != TimeSpan.Zero)
                await SeekTo(startAt);
        }

        protected virtual async void OnPlayerBoundaryReached()
        {
            await Pause();
            Player.RemoveTimeObserver(playbackTimeObserver);
        }

        public virtual async Task Play(int index)
        {
            Player.ActionAtItemEnd = AVPlayerActionAtItemEnd.Advance;
            Player.PlayItemAtIndex(index);
            await Play();
        }

        public override Task Play()
        {
            Player.Play();
            return Task.CompletedTask;
        }

        public override async Task SeekTo(TimeSpan position)
        {
            var scale = TimeScale;

            if (Player?.CurrentItem?.Duration != null && Player?.CurrentItem?.Duration != CMTime.Indefinite)
                scale = Player.CurrentItem.Duration.TimeScale;

            await Player?.SeekAsync(CMTime.FromSeconds(position.TotalSeconds, scale), CMTime.Zero, CMTime.Zero);
        }

        public override async Task Stop()
        {
            if (Player != null)
            {
                Player.Pause();
                await SeekTo(TimeSpan.Zero);
            }
            if (MediaManager != null)
                MediaManager.State = MediaPlayerState.Stopped;
        }

        protected override void Dispose(bool disposing)
        {
            NSNotificationCenter.DefaultCenter.RemoveObservers(new List<NSObject>(){
                didFinishPlayingObserver,
                itemFailedToPlayToEndTimeObserver,
                errorObserver,
                playbackStalledObserver
            });

            if (playbackTimeObserver != null)
                Player.RemoveTimeObserver(playbackTimeObserver);

            rateToken?.Dispose();
            statusToken?.Dispose();
            timeControlStatusToken?.Dispose();
            reasonForWaitingToPlayToken?.Dispose();
            playbackLikelyToKeepUpToken?.Dispose();
            loadedTimeRangesToken?.Dispose();
            playbackBufferFullToken?.Dispose();
            playbackBufferEmptyToken?.Dispose();
            presentationSizeToken?.Dispose();
            timedMetaDataToken?.Dispose();
        }
    }
}
