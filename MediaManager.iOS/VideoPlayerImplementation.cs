using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AVFoundation;
using CoreFoundation;
using CoreMedia;
using Foundation;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using Plugin.MediaManager.Abstractions.EventArguments;
using Plugin.MediaManager.Abstractions.Implementations;

namespace Plugin.MediaManager
{
    public class VideoPlayerImplementation : NSObject, IVideoPlayer
    {
        public static readonly NSString StatusObservationContext =
            new NSString("AVCustomEditPlayerViewControllerStatusObservationContext");

        public static NSString RateObservationContext =
            new NSString("AVCustomEditPlayerViewControllerRateObservationContext");

        private AVPlayer _player;
        private MediaPlayerStatus _status;
        private AVPlayerLayer _videoLayer;

        public Dictionary<string, string> RequestHeaders { get; set; }

        public VideoPlayerImplementation(IVolumeManager volumeManager)
        {
            _volumeManager = volumeManager;
            _status = MediaPlayerStatus.Stopped;

            // Watch the buffering status. If it changes, we may have to resume because the playing stopped because of bad network-conditions.
            BufferingChanged += (sender, e) =>
            {
                // If the player is ready to play, it's paused and the status is still on PLAYING, go on!
                if ((Player.Status == AVPlayerStatus.ReadyToPlay) && (Rate == 0.0f) &&
                    (Status == MediaPlayerStatus.Playing))
                    Player.Play();
            };
            _volumeManager.Mute = Player.Muted;
            _volumeManager.CurrentVolume = Player.Volume;
            _volumeManager.MaxVolume = 1;
            _volumeManager.VolumeChanged += VolumeManagerOnVolumeChanged;
        }

        private void VolumeManagerOnVolumeChanged(object sender, VolumeChangedEventArgs e)
        {
            _player.Volume = (float) e.Volume;
            _player.Muted = e.Mute;
        }

        private AVPlayer Player
        {
            get
            {
                if (_player == null)
                    InitializePlayer();

                return _player;
            }
        }

        private NSUrl nsUrl { get; set; }

        public float Rate
        {
            get
            {
                if (Player != null)
                    return Player.Rate;
                return 0.0f;
            }
            set
            {
                if (Player != null)
                    Player.Rate = value;
            }
        }

        public TimeSpan Position
        {
            get
            {
                if (Player.CurrentItem == null)
                    return TimeSpan.Zero;
                return TimeSpan.FromSeconds(Player.CurrentItem.CurrentTime.Seconds);
            }
        }

        public TimeSpan Duration
        {
            get
            {
                if (Player.CurrentItem == null)
                    return TimeSpan.Zero;
				if (double.IsNaN(Player.CurrentItem.Duration.Seconds))
					return TimeSpan.Zero;
                return TimeSpan.FromSeconds(Player.CurrentItem.Duration.Seconds);
            }
        }

        public TimeSpan Buffered
        {
            get
            {
                var buffered = TimeSpan.Zero;
                if (Player.CurrentItem != null)
                    buffered =
                        TimeSpan.FromSeconds(
                            Player.CurrentItem.LoadedTimeRanges.Select(
                                tr => tr.CMTimeRangeValue.Start.Seconds + tr.CMTimeRangeValue.Duration.Seconds).Max());

                Console.WriteLine("Buffered size: " + buffered);

                return buffered;
            }
        }

        public async Task Stop()
        {
            await Task.Run(() =>
            {
                if (Player.CurrentItem == null)
                    return;

                if (Player.Rate != 0.0)
                    Player.Pause();

                Player.CurrentItem.Seek(CMTime.FromSeconds(0d, 1));

                Status = MediaPlayerStatus.Stopped;
            });
        }

        public async Task Pause()
        {
            await Task.Run(() =>
            {
                Status = MediaPlayerStatus.Paused;

                if (Player.CurrentItem == null)
                    return;

                if (Player.Rate != 0.0)
                    Player.Pause();
            });
        }

        public MediaPlayerStatus Status
        {
            get { return _status; }
            private set
            {
                _status = value;
                StatusChanged?.Invoke(this, new StatusChangedEventArgs(_status));
            }
        }

        public event StatusChangedEventHandler StatusChanged;
        public event PlayingChangedEventHandler PlayingChanged;
        public event BufferingChangedEventHandler BufferingChanged;
        public event MediaFinishedEventHandler MediaFinished;
        public event MediaFailedEventHandler MediaFailed;

        public async Task Seek(TimeSpan position)
        {
            await Task.Run(() => { Player.CurrentItem?.Seek(CMTime.FromSeconds(position.TotalSeconds, 1)); });
        }

        private void InitializePlayer()
        {
            _player = new AVPlayer();
            _videoLayer = AVPlayerLayer.FromPlayer(_player);

            #if __IOS__ || __TVOS__
            var avSession = AVAudioSession.SharedInstance();

            // By setting the Audio Session category to AVAudioSessionCategorPlayback,
            //audio will continue to play when the silent switch is enabled, or when the screen is locked.
            avSession.SetCategory(AVAudioSessionCategory.Playback);

            NSError activationError = null;
            avSession.SetActive(true, out activationError);
            if (activationError != null)
                Console.WriteLine("Could not activate audio session {0}", activationError.LocalizedDescription);
            #endif

            Player.AddPeriodicTimeObserver(new CMTime(1, 4), DispatchQueue.MainQueue, delegate
            {
				double totalProgress = 0;
				if (!double.IsNaN(_player.CurrentItem.Duration.Seconds))
				{
					var totalDuration = TimeSpan.FromSeconds(_player.CurrentItem.Duration.Seconds);
					totalProgress = Position.TotalMilliseconds /
										totalDuration.TotalMilliseconds;
				}
                PlayingChanged?.Invoke(this, new PlayingChangedEventArgs(
                    !double.IsInfinity(totalProgress) ? totalProgress : 0,
                    Position,
                    Duration));
            });            
        }

        public async Task Play(IMediaFile mediaFile = null)
        {
            if (mediaFile != null)
                nsUrl = new NSUrl(mediaFile.Url);

            if (Status == MediaPlayerStatus.Paused)
            {
                Status = MediaPlayerStatus.Playing;
                //We are simply paused so just start again
                Player.Play();
                return;
            }

            try
            {
                // Start off with the status LOADING.
                Status = MediaPlayerStatus.Buffering;

                var nsAsset = AVAsset.FromUrl(nsUrl);
                var streamingItem = AVPlayerItem.FromAsset(nsAsset);

                Player.CurrentItem?.RemoveObserver(this, new NSString("status"));

                Player.ReplaceCurrentItemWithPlayerItem(streamingItem);
                streamingItem.AddObserver(this, new NSString("status"), NSKeyValueObservingOptions.New, Player.Handle);
                streamingItem.AddObserver(this, new NSString("loadedTimeRanges"),
                    NSKeyValueObservingOptions.Initial | NSKeyValueObservingOptions.New, Player.Handle);

                Player.CurrentItem.SeekingWaitsForVideoCompositionRendering = true;
                Player.CurrentItem.AddObserver(this, (NSString)"status", NSKeyValueObservingOptions.New |
                                                                          NSKeyValueObservingOptions.Initial,
                    StatusObservationContext.Handle);

                NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.DidPlayToEndTimeNotification,
                                                               notification => MediaFinished?.Invoke(this, new MediaFinishedEventArgs(mediaFile)), Player.CurrentItem);
                
                _TrackInfoList = null;
                _lastSelectedTrackIndex = null;

                List<IMediaTrackInfo> temp = ExtractTrackInfo(nsAsset);
                _TrackInfoList = temp == null ? null : new ReadOnlyCollection<IMediaTrackInfo>(temp);

                Player.Play();
            }
            catch (Exception ex)
            {
                OnMediaFailed();
                Status = MediaPlayerStatus.Stopped;

                //unable to start playback log error
                Console.WriteLine("Unable to start playback: " + ex);
            }

            await Task.CompletedTask;
        }

        public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
        {
            Console.WriteLine("Observer triggered for {0}", keyPath);

            switch ((string)keyPath)
            {
                case "status":
                    ObserveStatus();
                    return;

                case "loadedTimeRanges":
                    ObserveLoadedTimeRanges();
                    return;

                default:
                    Console.WriteLine("Observer triggered for {0} not resolved ...", keyPath);
                    return;
            }
        }

        private void ObserveStatus()
        {
            Console.WriteLine("Status Observed Method {0}", Player.Status);
            
            if ((Player.Status == AVPlayerStatus.ReadyToPlay) && (Status == MediaPlayerStatus.Buffering))
            {                
                Status = MediaPlayerStatus.Playing;
                Player.Play();
            }
            else if (Player.Status == AVPlayerStatus.Failed)
            {
                OnMediaFailed();
                Status = MediaPlayerStatus.Stopped;
            }
        }

        private void OnMediaFailed()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Description: {Player.Error.LocalizedDescription}");
            builder.AppendLine($"Reason: {Player.Error.LocalizedFailureReason}");
            builder.AppendLine($"Recovery Options: {Player.Error.LocalizedRecoveryOptions}");
            builder.AppendLine($"Recovery Suggestion: {Player.Error.LocalizedRecoverySuggestion}");
            MediaFailed?.Invoke(this, new MediaFailedEventArgs(builder.ToString(), new NSErrorException(Player.Error)));
        }

        private void ObserveLoadedTimeRanges()
        {
            var loadedTimeRanges = _player.CurrentItem.LoadedTimeRanges;
            if (loadedTimeRanges.Length > 0)
            {
                var range = loadedTimeRanges[0].CMTimeRangeValue;
                var duration = double.IsNaN(range.Duration.Seconds) ? TimeSpan.Zero : TimeSpan.FromSeconds(range.Duration.Seconds);
                var totalDuration = _player.CurrentItem.Duration;
                var bufferProgress = duration.TotalSeconds / totalDuration.Seconds;
                BufferingChanged?.Invoke(this, new BufferingChangedEventArgs(
                    !double.IsInfinity(bufferProgress) ? bufferProgress : 0,
                    duration
                ));
            }
            else
            {
                BufferingChanged?.Invoke(this, new BufferingChangedEventArgs(0, TimeSpan.Zero));
            }
        }

        /// <summary>
        /// True when RenderSurface has been initialized and ready for rendering
        /// </summary>
        public bool IsReadyRendering => RenderSurface != null && !RenderSurface.IsDisposed;

        private IVideoSurface _renderSurface;
        public IVideoSurface RenderSurface
        {
            get
            {
                return _renderSurface;
            }
            set
            {
                var view = (VideoSurface)value;
                if (view == null)
                    throw new ArgumentException("VideoSurface must be a UIView");               

                _renderSurface = value;
                _videoLayer = AVPlayerLayer.FromPlayer(Player);
                _videoLayer.Frame = view.Frame;

                //_videoLayer.VideoGravity = AVLayerVideoGravity.ResizeAspect;
                _videoLayer.VideoGravity = ToAVLayerVideoGravity(_aspectMode);

                view.Layer.AddSublayer(_videoLayer);
            }
        }                

        public int TrackCount => _TrackInfoList?.Count ?? -1;

        public int GetSelectedTrack(Abstractions.Enums.MediaTrackType trackType)
        {
            if (trackType != Abstractions.Enums.MediaTrackType.Audio)
                throw new NotSupportedException($"{trackType}");

            int result = -1;
            try
            {                
                AVPlayerItem item = Player.CurrentItem;
                AVAsset asset = item.Asset;
                AVMediaSelection selection = item.CurrentMediaSelection;
                AVMediaSelectionGroup group = asset.MediaSelectionGroupForMediaCharacteristic(AVMediaCharacteristic.Audible);
                AVMediaSelectionOption option = selection.GetSelectedMediaOption(group);
                
                foreach (var track in _TrackInfoList)
                {
                    if (track.TrackIndex != null && track.Tag == option)
                    {
                        result = track.TrackIndex.Value;
                        break;
                    }
                }
                return result;
            }
            catch (Exception e)
            {                
                throw;
            }
        }

        private int? _lastSelectedTrackIndex = null;
        /// <summary>
        /// Do NOT call this in UI thread otherwise it will freeze the video rendering
        /// </summary>
        /// <param name="trackIndex"></param>
        /// <returns></returns>
        public bool SetTrack(int trackIndex)
        {
            if (_lastSelectedTrackIndex != null && _lastSelectedTrackIndex == trackIndex)
                return true;
            
            try
            {
                int count = TrackCount;
                if (count <= 0 || trackIndex >= count)
                    //throw new ArgumentOutOfRangeException($"Track index {trackIndex} is out of range [0, {count})");
                    return false;

                AVPlayerItem item = Player.CurrentItem;
                AVAsset asset = item.Asset;

                AVMediaSelectionGroup group = asset.MediaSelectionGroupForMediaCharacteristic(AVMediaCharacteristic.Audible);

                AVMediaSelectionOption option = (AVMediaSelectionOption) _TrackInfoList[trackIndex].Tag;

                item.SelectMediaOption(option, group);
                
                _lastSelectedTrackIndex = trackIndex;

                return true;
            }
            catch
            {
                throw;
            }
        }

        private ReadOnlyCollection<IMediaTrackInfo> _TrackInfoList;
        public IReadOnlyCollection<IMediaTrackInfo> TrackInfoList => _TrackInfoList;

        private IVolumeManager _volumeManager;

        private VideoAspectMode _aspectMode = VideoAspectMode.None;
        public VideoAspectMode AspectMode
        { 
            get { return _aspectMode; }
            set
            {                
                _videoLayer.VideoGravity = ToAVLayerVideoGravity(value);
                _aspectMode = value;
            }
        }

        #region Helpers
        private static AVLayerVideoGravity ToAVLayerVideoGravity(VideoAspectMode input)
        {
            AVLayerVideoGravity output = AVLayerVideoGravity.Resize;
            switch (input)
            {
                case VideoAspectMode.None:
                    output = AVLayerVideoGravity.Resize;
                    break;
                case VideoAspectMode.AspectFit:
                    output = AVLayerVideoGravity.ResizeAspect;
                    break;
                case VideoAspectMode.AspectFill:
                    output = AVLayerVideoGravity.ResizeAspectFill;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return output;
        }

        private static List<IMediaTrackInfo> ExtractTrackInfo(AVAsset asset)
        {
            AVMediaSelectionGroup group = asset?.MediaSelectionGroupForMediaCharacteristic(AVMediaCharacteristic.Audible);
            if (group == null)
                return null;

            AVMediaSelectionOption[] options = group.Options;

            List<IMediaTrackInfo> result = null;
            if (options != null && options.Any())
            {
                result = new List<IMediaTrackInfo>();

                for (int i = 0; i < options.Length; i++)
                {
                    AVMediaSelectionOption option = options[i];
                    
                    var audioTrack = new MediaTrackInfo()
                    {
                        Tag = option,
                        DisplayName=option.DisplayName,
                        TrackIndex = i,
                        TrackId = i.ToString(),
                        //LanguageCode = 
                        LanguageTag = option.ExtendedLanguageTag,
                        TrackType = ToMediaTrackTypeAsbtract(option.MediaType)
                    };
                    result.Add(audioTrack);
                }
            }           
            return result;
        }

        private static Abstractions.Enums.MediaTrackType ToMediaTrackTypeAsbtract(string avMediaType)
        {
            MediaTrackType typeOut = MediaTrackType.Unknown;
            if (string.IsNullOrWhiteSpace(avMediaType))
                return typeOut;
            
            avMediaType = avMediaType.ToLower();            
            if (string.CompareOrdinal(avMediaType, AVMediaType.Video.ToLower(NSLocale.SystemLocale)) == 0)
                typeOut = MediaTrackType.Video;
            else if (string.CompareOrdinal(avMediaType, AVMediaType.Audio.ToLower(NSLocale.SystemLocale)) == 0)
                typeOut = MediaTrackType.Audio;
            else if (string.CompareOrdinal(avMediaType, AVMediaType.Timecode.ToLower(NSLocale.SystemLocale)) == 0)
                typeOut = MediaTrackType.Timedtext;
            else if (string.CompareOrdinal(avMediaType, AVMediaType.Subtitle.ToLower(NSLocale.SystemLocale)) == 0)
                typeOut = MediaTrackType.Subtitle;
            else if (string.CompareOrdinal(avMediaType, AVMediaType.Metadata.ToLower(NSLocale.SystemLocale)) == 0)
                typeOut = MediaTrackType.Metadata;
            else
            {
                throw new NotImplementedException($"ToMediaTrackTypeAsbtract conversion not found for {avMediaType}");
            }
            return typeOut;
        }
        #endregion
    }
}