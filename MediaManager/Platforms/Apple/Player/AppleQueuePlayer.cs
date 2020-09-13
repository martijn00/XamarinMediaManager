using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AVFoundation;
using Foundation;

namespace MediaManager.Platforms.Apple.Player
{
    public class AppleQueuePlayer : AVQueuePlayer
    {
        private readonly List<AVPlayerItem> _itemsForPlayer;
        private int _nowPlayingIndex;
        private readonly NSObject _didFinishPlayingObserver;
        public IList<AVPlayerItem> AllItems => _itemsForPlayer;
        public AppleQueuePlayer(AVPlayerItem[] items) : base(items)
        {
            _itemsForPlayer = items.ToList();
        }
        public AppleQueuePlayer(IntPtr handle) : base(handle) {}
        public AppleQueuePlayer(NSObjectFlag t) : base(t) {}

        public AppleQueuePlayer() : base()
        {
            _itemsForPlayer = new List<AVPlayerItem>();
            _nowPlayingIndex = 0;
            _didFinishPlayingObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.DidPlayToEndTimeNotification, DidFinishPlaying);
        }

        private void DidFinishPlaying(NSNotification obj)
        {
            _nowPlayingIndex++;
        }


        public void InsertItem(AVPlayerItem item, int? index)
        {
            if (index.HasValue)
            {
                _itemsForPlayer.Insert(index.Value, item);
                if (index.Value > _nowPlayingIndex)
                {
                    if (CanInsert(item, _itemsForPlayer[index.Value - 1]))
                        base.InsertItem(item, _itemsForPlayer[index.Value - 1]);
                    else
                        RebuildQueue();
                }
            }
            else
            {
                _itemsForPlayer.Add(item);
                if (CanInsert(item, null))
                    base.InsertItem(item, null);
                else
                    RebuildQueue();
            }
        }

        private void RebuildQueue()
        {
            base.RemoveAllItems();
            for (var i = _nowPlayingIndex; i < _itemsForPlayer.Count; i++)
            {
                base.InsertItem(_itemsForPlayer[i], null);
            }
        }

        public override void InsertItem(AVPlayerItem item, AVPlayerItem? afterItem)
        {
            int? index = null;
            if (afterItem != null)
            {
                index = _itemsForPlayer.IndexOf(afterItem);
            }
            InsertItem(item, index);
        }

        public override void RemoveItem(AVPlayerItem item)
        {
            base.RemoveItem(item);
            _itemsForPlayer.Remove(item);
        }

        public void RemoveItem(int index)
        {
            var item = _itemsForPlayer[index];
            _itemsForPlayer.RemoveAt(index);
            if (index >= _nowPlayingIndex)
            {
                base.RemoveItem(item);
            }
        }

        public override void RemoveAllItems()
        {
            base.RemoveAllItems();
            _itemsForPlayer.Clear();
        }

        public override void AdvanceToNextItem()
        {
            base.AdvanceToNextItem();
            _nowPlayingIndex++;
        }

        public void AdvanceToPreviousItem()
        {
            _nowPlayingIndex--;
            RebuildQueue();
            Play();
        }

        public void PlayItemAtIndex(int index)
        {
            if (index >= _itemsForPlayer.Count)
                throw new IndexOutOfRangeException();
            _nowPlayingIndex = index;
            RebuildQueue();
            Play();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            NSNotificationCenter.DefaultCenter.RemoveObserver(_didFinishPlayingObserver);
            _didFinishPlayingObserver.Dispose();
        }
    }
}
