using AVFoundation;
using AVKit;
using MediaManager.Platforms.Apple.Player;
using MediaManager.Platforms.Mac.Video;
using MediaManager.Player;
using MediaManager.Video;

namespace MediaManager.Platforms.Mac.Player
{
    public class MacMediaPlayer : AppleMediaPlayer, IMediaPlayer<AppleQueuePlayer, VideoView>
    {
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
                    PlayerView.VideoGravity = AVLayerVideoGravity.Resize.ToString();
                    break;
                case VideoAspectMode.AspectFit:
                    PlayerView.VideoGravity = AVLayerVideoGravity.ResizeAspect.ToString();
                    break;
                case VideoAspectMode.AspectFill:
                    PlayerView.VideoGravity = AVLayerVideoGravity.ResizeAspectFill.ToString();
                    break;
                default:
                    PlayerView.VideoGravity = AVLayerVideoGravity.ResizeAspect.ToString();
                    break;
            }
        }

        public override void UpdateShowPlaybackControls(bool showPlaybackControls)
        {
            if (PlayerView == null)
                return;

            if (showPlaybackControls)
                PlayerView.ControlsStyle = AVPlayerViewControlsStyle.Default;
            else
                PlayerView.ControlsStyle = AVPlayerViewControlsStyle.None;
        }

        public override void UpdateVideoPlaceholder(object value)
        {
            if (PlayerView == null)
                return;

            //TODO: Implement placeholder
        }
    }
}
