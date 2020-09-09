using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaManager;
using MediaManager.Library;
using MvvmCross.Commands;
using MvvmCross.Logging;
using MvvmCross.Navigation;
using MvvmCross.ViewModels;

namespace ElementPlayer.Core.ViewModels
{
    public class HomeViewModel : BaseViewModel
    {
        public HomeViewModel(IMvxLogProvider logProvider, IMvxNavigationService navigationService, IMediaManager mediaManager) : base(logProvider, navigationService)
        {
            MediaManager = mediaManager;
        }

        public override string Title => "Home";
        public readonly IMediaManager MediaManager;
        public MvxObservableCollection<IMediaItem> Items { get; set; } = new MvxObservableCollection<IMediaItem>();

        public IMvxAsyncCommand<IMediaItem> ItemSelected => new MvxAsyncCommand<IMediaItem>(SelectItem);
        //new MvxAsyncCommand<IMediaItem>(async (item) => await this.NavigationService.Navigate<PlayerViewModel, IMediaItem>(item));

        public override async Task Initialize()
        {
            var items = await MediaManager.Library.GetAll<IMediaItem>().ConfigureAwait(false);
            Items.ReplaceWith(items);
        }

        public IList<string> Mp3UrlList => new[]{
            "https://ia800806.us.archive.org/15/items/Mp3Playlist_555/AaronNeville-CrazyLove.mp3",
            "https://ia800605.us.archive.org/32/items/Mp3Playlist_555/CelineDion-IfICould.mp3",
            "https://ia800605.us.archive.org/32/items/Mp3Playlist_555/Daughtry-Homeacoustic.mp3",
            "https://storage.googleapis.com/uamp/The_Kyoto_Connection_-_Wake_Up/01_-_Intro_-_The_Way_Of_Waking_Up_feat_Alan_Watts.mp3",
            "https://aphid.fireside.fm/d/1437767933/02d84890-e58d-43eb-ab4c-26bcc8524289/d9b38b7f-5ede-4ca7-a5d6-a18d5605aba1.mp3"
            };


        private async Task SelectItem(IMediaItem mediaItem)
        {
            //CrossMediaManager.Current.StepSizeBackward = TimeSpan.FromSeconds(5);
            //CrossMediaManager.Current.StepSizeForward = TimeSpan.FromSeconds(60);

            await NavigationService.Navigate<PlayerViewModel>();

            await MediaManager.Play(mediaItem);
            //await MediaManager.Play(mediaItem, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(35));
            //await MediaManager.Play(Mp3UrlList);
        }
    }
}
