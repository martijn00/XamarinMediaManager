using System;
using MediaManager.Library;
using MediaManager.Media;
using MediaManager.Playback;
using MediaManager.Player;
using MediaManager.Queue;
using MediaManager.Video;
using Xamarin.Forms;

namespace MediaManager.Forms
{
    public class VideoView : View, IDisposable
    {
        protected static IMediaManager MediaManager => CrossMediaManager.Current;
        protected static IMediaPlayer MediaPlayer => MediaManager.MediaPlayer;
        protected static IVideoView PlayerView => MediaPlayer.VideoView;

        public VideoView()
        {
            MediaManager.BufferedChanged += MediaManager_BufferedChanged;
            MediaManager.PositionChanged += MediaManager_PositionChanged;
            MediaManager.StateChanged += MediaManager_StateChanged;
            MediaManager.MediaItemChanged += MediaManager_MediaItemChanged;

            MediaManager.Queue.QueueChanged += MediaQueue_QueueChanged;

            MediaManager.PropertyChanged += MediaManager_PropertyChanged;
            MediaManager.MediaPlayer.PropertyChanged += MediaPlayer_PropertyChanged;
        }

        protected virtual void MediaPlayer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MediaPlayer.ShowPlaybackControls):
                    ShowControls = MediaPlayer.ShowPlaybackControls;
                    break;
                case nameof(MediaPlayer.VideoAspect):
                    VideoAspect = MediaPlayer.VideoAspect;
                    break;
                case nameof(MediaPlayer.VideoHeight):
                    VideoHeight = MediaPlayer.VideoHeight;
                    break;
                case nameof(MediaPlayer.VideoWidth):
                    VideoWidth = MediaPlayer.VideoWidth;
                    break;
                case nameof(MediaPlayer.VideoPlaceholder):
                    if (MediaPlayer.VideoPlaceholder is ImageSource imageSource)
                        VideoPlaceholder = imageSource;
                    break;           
                case nameof(MediaPlayer.IsFullWindow):
                        IsFullWindow = MediaPlayer.IsFullWindow;
                    break;
                default:
                    break;
            }
        }

        protected virtual void MediaManager_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MediaManager.Duration):
                    Duration = MediaManager.Duration;
                    break;
                case nameof(MediaManager.AutoPlay):
                    AutoPlay = MediaManager.AutoPlay;
                    break;                
                case nameof(MediaManager.IsFullWindow):
                    IsFullWindow = MediaManager.IsFullWindow;
                    break;
                case nameof(MediaManager.RepeatMode):
                    Repeat = MediaManager.RepeatMode;
                    break;
                case nameof(MediaManager.ShuffleMode):
                    Shuffle = MediaManager.ShuffleMode;
                    break;
                case nameof(MediaManager.Speed):
                    Speed = MediaManager.Speed;
                    break;
                default:
                    break;
            }
        }

        protected virtual void MediaQueue_QueueChanged(object sender, QueueChangedEventArgs e)
        {
            //var queue = MediaManager.MediaQueue.ToList();
            //if (Source != queue)
            //    Source = queue;
        }

        protected virtual void MediaManager_MediaItemChanged(object sender, MediaItemEventArgs e)
        {
            Current = e.MediaItem;
        }

        protected virtual void MediaManager_StateChanged(object sender, StateChangedEventArgs e)
        {
            State = e.State;
        }

        protected virtual void MediaManager_PositionChanged(object sender, Playback.PositionChangedEventArgs e)
        {
            Position = e.Position;
        }

        protected virtual void MediaManager_BufferedChanged(object sender, BufferedChangedEventArgs e)
        {
            Buffered = e.Buffered;
        }

        public bool ExitsFullScreenWhenPlaybackEnds { get; set; }

        public static readonly BindableProperty VideoAspectProperty =
            BindableProperty.Create(nameof(VideoAspect), typeof(VideoAspectMode), typeof(VideoView), VideoAspectMode.AspectFit, propertyChanged: OnVideoAspectPropertyChanged, defaultValueCreator: x => MediaManager.MediaPlayer.VideoAspect);

        public static readonly BindableProperty AutoPlayProperty =
            BindableProperty.Create(nameof(AutoPlay), typeof(bool), typeof(VideoView), true, propertyChanged: OnAutoPlayPropertyChanged, defaultValueCreator: x => MediaManager.AutoPlay);

        public static readonly BindableProperty BufferedProperty =
            BindableProperty.Create(nameof(Buffered), typeof(TimeSpan), typeof(VideoView), TimeSpan.Zero, defaultValueCreator: x => MediaManager.Buffered);

        public static readonly BindableProperty StateProperty =
            BindableProperty.Create(nameof(State), typeof(MediaPlayerState), typeof(VideoView), MediaPlayerState.Stopped, defaultValueCreator: x => MediaManager.State);

        public static readonly BindableProperty DurationProperty =
            BindableProperty.Create(nameof(Duration), typeof(TimeSpan), typeof(VideoView), null, defaultValueCreator: x => MediaManager.Duration);

        public static readonly BindableProperty PositionProperty =
            BindableProperty.Create(nameof(Position), typeof(TimeSpan), typeof(VideoView), TimeSpan.Zero, defaultValueCreator: x => MediaManager.Position);

        public static readonly BindableProperty ShowControlsProperty =
            BindableProperty.Create(nameof(ShowControls), typeof(bool), typeof(VideoView), false, propertyChanged: OnShowControlsPropertyChanged, defaultValueCreator: x => MediaManager.MediaPlayer.ShowPlaybackControls);

        public static readonly BindableProperty SourceProperty =
            BindableProperty.Create(nameof(Source), typeof(object), typeof(VideoView), propertyChanged: OnSourcePropertyChanged);

        public static readonly BindableProperty CurrentProperty =
            BindableProperty.Create(nameof(Current), typeof(IMediaItem), typeof(VideoView));

        public static readonly BindableProperty RepeatProperty =
            BindableProperty.Create(nameof(Repeat), typeof(RepeatMode), typeof(VideoView), RepeatMode.Off, propertyChanged: OnRepeatPropertyChanged, defaultValueCreator: x => MediaManager.RepeatMode);

        public static readonly BindableProperty ShuffleProperty =
            BindableProperty.Create(nameof(Shuffle), typeof(ShuffleMode), typeof(VideoView), ShuffleMode.Off, propertyChanged: OnShufflePropertyChanged, defaultValueCreator: x => MediaManager.ShuffleMode);

        public static readonly BindableProperty VideoHeightProperty =
            BindableProperty.Create(nameof(VideoHeight), typeof(int), typeof(VideoView), defaultValueCreator: x => MediaManager.MediaPlayer.VideoHeight);

        public static readonly BindableProperty VideoWidthProperty =
            BindableProperty.Create(nameof(VideoWidth), typeof(int), typeof(VideoView), defaultValueCreator: x => MediaManager.MediaPlayer.VideoWidth);

        public static readonly BindableProperty VolumeProperty =
            BindableProperty.Create(nameof(Volume), typeof(int), typeof(VideoView), 1, propertyChanged: OnVolumePropertyChanged, defaultValueCreator: x => MediaManager.Volume.CurrentVolume);

        public static readonly BindableProperty SpeedProperty =
            BindableProperty.Create(nameof(Speed), typeof(float), typeof(VideoView), 1.0f, propertyChanged: OnSpeedPropertyChanged, defaultValueCreator: x => MediaManager.Speed);

        public static readonly BindableProperty VideoPlaceholderProperty =
            BindableProperty.Create(nameof(VideoPlaceholder), typeof(ImageSource), typeof(VideoView), null, propertyChanged: OnVideoPlaceholderPropertyChanged, defaultValueCreator: x => MediaManager.MediaPlayer.VideoPlaceholder?.ToImageSource());              
        
        public static readonly BindableProperty IsFullWindowProperty =
            BindableProperty.Create(nameof(IsFullWindow), typeof(bool), typeof(VideoView), false, propertyChanged: OnIsFullWindowPropertyChanged, defaultValueCreator: x => MediaManager.MediaPlayer.IsFullWindow);

        public VideoAspectMode VideoAspect
        {
            get => (VideoAspectMode)GetValue(VideoAspectProperty);
            set => SetValue(VideoAspectProperty, value);
        }

        public RepeatMode Repeat
        {
            get => (RepeatMode)GetValue(RepeatProperty);
            set => SetValue(RepeatProperty, value);
        }

        public ShuffleMode Shuffle
        {
            get => (ShuffleMode)GetValue(ShuffleProperty);
            set => SetValue(ShuffleProperty, value);
        }

        public bool AutoPlay
        {
            get { return (bool)GetValue(AutoPlayProperty); }
            set { SetValue(AutoPlayProperty, value); }
        }

        public TimeSpan Buffered
        {
            get { return (TimeSpan)GetValue(BufferedProperty); }
            internal set { SetValue(BufferedProperty, value); }
        }

        public MediaPlayerState State
        {
            get { return (MediaPlayerState)GetValue(StateProperty); }
            internal set { SetValue(StateProperty, value); }
        }

        public TimeSpan Duration
        {
            get { return (TimeSpan)GetValue(DurationProperty); }
            internal set { SetValue(DurationProperty, value); }
        }

        public bool ShowControls
        {
            get { return (bool)GetValue(ShowControlsProperty); }
            set { SetValue(ShowControlsProperty, value); }
        }

        public TimeSpan Position
        {
            get { return (TimeSpan)GetValue(PositionProperty); }
            internal set { SetValue(PositionProperty, value); }
        }

        public IMediaItem Current
        {
            get { return (IMediaItem)GetValue(CurrentProperty); }
            internal set { SetValue(CurrentProperty, value); }
        }

        public object Source
        {
            get { return (object)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public int VideoHeight
        {
            get { return (int)GetValue(VideoHeightProperty); }
            internal set { SetValue(VideoHeightProperty, value); }
        }

        public int VideoWidth
        {
            get { return (int)GetValue(VideoWidthProperty); }
            internal set { SetValue(VideoWidthProperty, value); }
        }

        public int Volume
        {
            get { return (int)GetValue(VolumeProperty); }
            set { SetValue(VolumeProperty, value); }
        }

        public float Speed
        {
            get { return (float)GetValue(SpeedProperty); }
            set { SetValue(SpeedProperty, value); }
        }

        public ImageSource VideoPlaceholder
        {
            get { return (ImageSource)GetValue(VideoPlaceholderProperty); }
            set { SetValue(VideoPlaceholderProperty, value); }
        }        
        
        public bool IsFullWindow
        {
            get { return (bool)GetValue(IsFullWindowProperty); }
            set { SetValue(IsFullWindowProperty, value); }
        }

        private static async void OnSourcePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            //Prevent loop with MediaQueue_QueueChanged
            //var queue = MediaManager.MediaQueue.ToList();
            //if (queue == newValue)
            //    return;

            await CrossMediaManager.Current.Play(newValue);
        }

        private static void OnShowControlsPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            MediaManager.MediaPlayer.ShowPlaybackControls = (bool)newValue;
        }

        private static void OnVideoAspectPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            MediaManager.MediaPlayer.VideoAspect = (VideoAspectMode)newValue;
        }

        private static void OnRepeatPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            MediaManager.RepeatMode = (RepeatMode)newValue;
        }

        private static void OnShufflePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            MediaManager.ShuffleMode = (ShuffleMode)newValue;
        }

        private static void OnVolumePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            MediaManager.Volume.CurrentVolume = (int)newValue;
        }

        private static void OnSpeedPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            MediaManager.Speed = (float)newValue;
        }

        private static void OnAutoPlayPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            MediaManager.AutoPlay = (bool)newValue;
        }

        private static async void OnVideoPlaceholderPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
#if !NETSTANDARD
            if (newValue is Xamarin.Forms.ImageSource imageSource)
                MediaManager.MediaPlayer.VideoPlaceholder = await imageSource.ToNative().ConfigureAwait(false);
#endif
        }

        private static void OnIsFullWindowPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            MediaManager.MediaPlayer.IsFullWindow = (bool)newValue;
        }        

        public virtual void Dispose()
        {
            MediaManager.BufferedChanged -= MediaManager_BufferedChanged;
            MediaManager.PositionChanged -= MediaManager_PositionChanged;
            MediaManager.StateChanged -= MediaManager_StateChanged;
            MediaManager.MediaItemChanged -= MediaManager_MediaItemChanged;

            MediaManager.Queue.QueueChanged -= MediaQueue_QueueChanged;

            MediaManager.PropertyChanged -= MediaManager_PropertyChanged;
            MediaManager.MediaPlayer.PropertyChanged -= MediaPlayer_PropertyChanged;
        }
    }
}
