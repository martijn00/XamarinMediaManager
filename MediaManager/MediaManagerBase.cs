using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using MediaManager.Library;
using MediaManager.Media;
using MediaManager.Notifications;
using MediaManager.Playback;
using MediaManager.Player;
using MediaManager.Queue;
using MediaManager.Volume;

namespace MediaManager
{
    public abstract class MediaManagerBase : NotifyPropertyChangedBase, IMediaManager
    {
        public MediaManagerBase()
        {
            Timer.AutoReset = true;
            Timer.Elapsed += Timer_Elapsed;
            Timer.Start();
        }

        private bool _isInitialized = true;
        public bool IsInitialized
        {
            get => _isInitialized;
            protected set => SetProperty(ref _isInitialized, value);
        }

        public Timer Timer { get; protected set; } = new Timer(TimerInterval);

        public static double TimerInterval { get; set; } = 1000;
        [Obsolete("Use StepSizeForward and StepSizeBackward properties instead.", true)]
        public virtual TimeSpan StepSize
        {
            get => throw new NotImplementedException("This property is obsolete. Use StepSizeForwards and StepSizeBackwards properties instead.");
            set => throw new NotImplementedException("This property is obsolete. Use StepSizeForwards and StepSizeBackwards properties instead.");
        }

        protected TimeSpan _stepSizeForward = TimeSpan.FromSeconds(10);
        public virtual TimeSpan StepSizeForward
        {
            get => _stepSizeForward;
            set => SetProperty(ref _stepSizeForward, value);
        }

        protected TimeSpan _stepSizeBackward = TimeSpan.FromSeconds(10);
        public virtual TimeSpan StepSizeBackward
        {
            get => _stepSizeBackward;
            set => SetProperty(ref _stepSizeBackward, value);
        }

        protected Dictionary<string, string> _requestHeaders = new Dictionary<string, string>();
        public virtual Dictionary<string, string> RequestHeaders
        {
            get => _requestHeaders;
            set => SetProperty(ref _requestHeaders, value);
        }

        protected IMediaLibrary _library;
        public virtual IMediaLibrary Library
        {
            get
            {
                if (_library == null)
                    _library = new Media.MediaLibrary();

                return _library;
            }
            set => SetProperty(ref _library, value);
        }

        protected IMediaQueue _mediaQueue;
        public virtual IMediaQueue Queue
        {
            get
            {
                if (_mediaQueue == null)
                    _mediaQueue = new MediaQueue();

                return _mediaQueue;
            }
            set => SetProperty(ref _mediaQueue, value);
        }

        public virtual void Init()
        {
            IsInitialized = true;
        }

        public abstract IMediaPlayer MediaPlayer { get; set; }
        public abstract IMediaExtractor Extractor { get; set; }
        public abstract IVolumeManager Volume { get; set; }
        public abstract INotificationManager Notification { get; set; }

        protected MediaPlayerState _state = MediaPlayerState.Stopped;
        public MediaPlayerState State
        {
            get => _state;
            internal set
            {
                if (SetProperty(ref _state, value))
                    OnStateChanged(this, new StateChangedEventArgs(State));
            }
        }

        protected TimeSpan _buffered;
        public TimeSpan Buffered
        {
            get => _buffered;
            internal set
            {
                if (SetProperty(ref _buffered, value))
                    OnBufferedChanged(this, new BufferedChangedEventArgs(Buffered));
            }
        }

        public abstract TimeSpan Position { get; }
        public abstract TimeSpan Duration { get; }
        public abstract float Speed { get; set; }
        public abstract bool KeepScreenOn { get; set; }

        private RepeatMode _repeatMode = RepeatMode.Off;
        public virtual RepeatMode RepeatMode
        {
            get => _repeatMode;
            set => SetProperty(ref _repeatMode, value);
        }

        private ShuffleMode _shuffleMode = ShuffleMode.Off;
        public virtual ShuffleMode ShuffleMode
        {
            get => _shuffleMode;
            set => SetProperty(ref _shuffleMode, value);
        }

        private bool _clearQueueOnPlay = true;
        public bool ClearQueueOnPlay
        {
            get => _clearQueueOnPlay;
            set => SetProperty(ref _clearQueueOnPlay, value);
        }

        private bool _autoPlay = true;
        public bool AutoPlay
        {
            get => _autoPlay;
            set => SetProperty(ref _autoPlay, value);
        }

        private bool _retryPlayOnFailed = true;
        public bool RetryPlayOnFailed
        {
            get => _retryPlayOnFailed;
            set => SetProperty(ref _retryPlayOnFailed, value);
        }

        private bool _playNextOnFailed = true;
        public bool PlayNextOnFailed
        {
            get => _playNextOnFailed;
            set => SetProperty(ref _playNextOnFailed, value);
        }

        private int _maxRetryCount = 1;
        public int MaxRetryCount
        {
            get => _maxRetryCount;
            set => SetProperty(ref _maxRetryCount, value);
        }

        public virtual Task Play()
        {
            return MediaPlayer.Play();
        }

        public virtual Task Pause()
        {
            return MediaPlayer.Pause();
        }

        public virtual Task SeekTo(TimeSpan position)
        {
            return MediaPlayer.SeekTo(position);
        }

        public virtual Task Stop()
        {
            return MediaPlayer.Stop();
        }

        public virtual async Task<IMediaItem> Play(IMediaItem mediaItem)
        {
            var mediaItemToPlay = await PrepareQueueForPlayback(mediaItem);
            await PlayAsCurrent(mediaItemToPlay);
            return mediaItemToPlay;
        }

        public virtual async Task<IMediaItem> Play(string uri)
        {
            var mediaItem = await Extractor.CreateMediaItem(uri).ConfigureAwait(false);
            var mediaItemToPlay = await PrepareQueueForPlayback(mediaItem);

            await PlayAsCurrent(mediaItemToPlay);
            return mediaItem;
        }

        public virtual async Task<IMediaItem> PlayFromAssembly(string resourceName, Assembly assembly = null)
        {
            var mediaItem = await Extractor.CreateMediaItemFromAssembly(resourceName, assembly).ConfigureAwait(false);
            var mediaItemToPlay = await PrepareQueueForPlayback(mediaItem);

            await PlayAsCurrent(mediaItemToPlay);
            return mediaItem;
        }

        public virtual async Task<IMediaItem> PlayFromResource(string resourceName)
        {
            var mediaItem = await Extractor.CreateMediaItemFromResource(resourceName).ConfigureAwait(false);
            var mediaItemToPlay = await PrepareQueueForPlayback(mediaItem);

            await PlayAsCurrent(mediaItemToPlay);
            return mediaItem;
        }

        public virtual async Task<IMediaItem> Play(Stream stream, string cacheName)
        {
            //TODO: Probably better to do everything in memory. On Android use ByteArrayDataSource
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), cacheName);
            var fileStream = File.Create(path);
            await stream.CopyToAsync(fileStream);
            fileStream.Close();

            var mediaItem = await Extractor.CreateMediaItem(path).ConfigureAwait(false);
            var mediaItemToPlay = await PrepareQueueForPlayback(mediaItem);

            await PlayAsCurrent(mediaItemToPlay);
            return mediaItem;
        }

        public virtual async Task<IMediaItem> Play(IEnumerable<IMediaItem> mediaItems)
        {
            var mediaItemToPlay = await PrepareQueueForPlayback(mediaItems);

            await PlayAsCurrent(mediaItemToPlay);
            return mediaItemToPlay;
        }

        public virtual async Task<IMediaItem> Play(IEnumerable<string> items)
        {
            var mediaItems = await items.CreateMediaItems();
            var mediaItemToPlay = await PrepareQueueForPlayback(mediaItems);

            await PlayAsCurrent(mediaItemToPlay);
            return mediaItemToPlay;
        }

        public virtual async Task<IMediaItem> Play(FileInfo file)
        {
            var mediaItem = await Extractor.CreateMediaItem(file).ConfigureAwait(false);
            var mediaItemToPlay = await PrepareQueueForPlayback(mediaItem);

            await PlayAsCurrent(mediaItemToPlay);
            return mediaItem;
        }

        public virtual async Task<IMediaItem> Play(DirectoryInfo directoryInfo)
        {
            var mediaItems = await directoryInfo.GetFiles().CreateMediaItems();
            var mediaItemToPlay = await PrepareQueueForPlayback(mediaItems);
            await PlayAsCurrent(mediaItemToPlay);
            return mediaItemToPlay;
        }

        public virtual async Task PlayAsCurrent(IMediaItem mediaItem)
        {
            if (AutoPlay)
                await MediaPlayer.Play(mediaItem);
        }

        public virtual Task<IMediaItem> PrepareQueueForPlayback(IMediaItem mediaItem)
        {
            return PrepareQueueForPlayback(new[] { mediaItem });
        }

        public virtual Task<IMediaItem> PrepareQueueForPlayback(IEnumerable<IMediaItem> mediaItems)
        {
            if (ClearQueueOnPlay)
            {
                Queue.Clear();
                Queue.CurrentIndex = 0;
            }

            foreach (var item in mediaItems)
            {
                Queue.Add(item);
            }

            return Task.FromResult(Queue.Current);
        }

        public virtual async Task<bool> PlayNext()
        {
            IMediaItem mediaItem = null;
            // If we repeat just the single media item, we do that first
            if (RepeatMode == RepeatMode.One)
            {
                mediaItem = Queue.Current;
            }
            // If we repeat all and there is no next in the Queue, we go back to the first
            else if (RepeatMode == RepeatMode.All && !Queue.HasNext)
            {
                mediaItem = Queue.First();
            }
            // Otherwise we try to play the next media item in the queue
            else if (Queue.HasNext)
            {
                mediaItem = Queue.Next;
            }

            return await PlayQueueItem(mediaItem);
        }

        public virtual async Task<bool> PlayPrevious()
        {
            if (Queue.HasPrevious)
            {
                return await PlayQueueItem(Queue.Previous);
            }
            return false;
        }

        public virtual async Task<bool> PlayQueueItem(IMediaItem mediaItem)
        {
            if (mediaItem == null || !Queue.Contains(mediaItem))
                return false;

            Queue.CurrentIndex = Queue.IndexOf(mediaItem);
            await MediaPlayer.Play(mediaItem);
            return true;
        }

        public virtual async Task<bool> PlayQueueItem(int index)
        {
            var mediaItem = Queue.ElementAtOrDefault(index);
            if (mediaItem == null)
                return false;

            Queue.CurrentIndex = index;
            await MediaPlayer.Play(mediaItem);
            return true;
        }

        public virtual Task StepBackward()
        {
            var seekTo = SeekTo(TimeSpan.FromSeconds(double.IsNaN(Position.TotalSeconds) ? 0 : ((Position.TotalSeconds < StepSizeBackward.TotalSeconds) ? 0 : Position.TotalSeconds - StepSizeBackward.TotalSeconds)));
            Timer_Elapsed(null, null);
            return seekTo;
        }

        public virtual Task StepForward()
        {
            var seekTo = SeekTo(TimeSpan.FromSeconds(double.IsNaN(Position.TotalSeconds) ? 0 : Position.TotalSeconds + StepSizeForward.TotalSeconds));
            Timer_Elapsed(null, null);
            return seekTo;
        }

        public event StateChangedEventHandler StateChanged;
        public event BufferedChangedEventHandler BufferedChanged;
        public event PositionChangedEventHandler PositionChanged;

        public event MediaItemFinishedEventHandler MediaItemFinished;
        public event MediaItemChangedEventHandler MediaItemChanged;
        public event MediaItemFailedEventHandler MediaItemFailed;

        protected IMediaItem _currentSource;

        internal void OnBufferedChanged(object sender, BufferedChangedEventArgs e) => BufferedChanged?.Invoke(sender, e);
        internal void OnMediaItemChanged(object sender, MediaItemEventArgs e)
        {
            if (SetProperty(ref _currentSource, e.MediaItem))
                MediaItemChanged?.Invoke(sender, e);
        }

        protected int RetryCount = 0;
        internal async void OnMediaItemFailed(object sender, MediaItemFailedEventArgs e)
        {
            MediaItemFailed?.Invoke(sender, e);

            if (RetryPlayOnFailed && RetryCount < MaxRetryCount)
            {
                RetryCount++;
                await Play();
            }
            else
            {
                RetryCount = 0;
                if (PlayNextOnFailed)
                    await PlayNext();
            }
        }

        internal void OnMediaItemFinished(object sender, MediaItemEventArgs e) => MediaItemFinished?.Invoke(sender, e);
        internal void OnPositionChanged(object sender, PositionChangedEventArgs e) => PositionChanged?.Invoke(sender, e);

        internal void OnStateChanged(object sender, StateChangedEventArgs e)
        {
            StateChanged?.Invoke(sender, e);

            //TODO: Find a better way to trigger some changes.
            OnPropertyChanged(nameof(Duration));

            Notification?.UpdateNotification();
        }

        protected TimeSpan _previousPosition = new TimeSpan();
        protected TimeSpan PreviousPosition
        {
            get => _previousPosition;
            set
            {
                if (SetProperty(ref _previousPosition, value))
                    OnPositionChanged(this, new PositionChangedEventArgs(Position));
            }
        }

        protected virtual void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!IsInitialized)
                return;

            PreviousPosition = Position;
        }

        public virtual void Dispose()
        {
            Timer.Elapsed -= Timer_Elapsed;
            Timer.Dispose();
        }
    }
}
