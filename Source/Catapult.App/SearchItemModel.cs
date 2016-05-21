using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Catapult.App.Properties;
using Catapult.Core;
using Catapult.Core.Icons;
using Catapult.Core.Indexes;

namespace Catapult.App
{
    public class SearchItemModel : INotifyPropertyChanged
    {
        private BitmapFrame _icon;

        public SearchItemModel(string name, string details, double score, IIndexable targetItem, ImmutableHashSet<int> highlightIndexes, IIconResolver iconResolver)
        {
            Name = name;
            Details = details;
            Score = score;
            TargetItem = targetItem;
            HighlightIndexes = highlightIndexes;
            InitIcon = LoadIconAsync(iconResolver);
        }

        public SearchItemModel(SearchResult result) : this(result.Name, result.TargetItem.Details, result.Score, result.TargetItem, result.HighlightIndexes, result.TargetItem?.GetIconResolver())
        {
        }

        public string Name { get; set; }
        public string Details { get; set; }

        public double Score { get; set; }

        public IIndexable TargetItem { get; set; }

        public Guid Id { get; set; }

        public ImmutableHashSet<int> HighlightIndexes { get; set; }

        public BitmapFrame Icon
        {
            get { return _icon; }
            set
            {
                if (Equals(value, _icon)) return;
                _icon = value;
                OnPropertyChanged();
            }
        }

        public Task InitIcon { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private static readonly ConcurrentDictionary<string, BitmapFrame> IconCache = new ConcurrentDictionary<string, BitmapFrame>();

        private async Task LoadIconAsync(IIconResolver iconResolver)
        {
            BitmapFrame frame;
            var cacheKey = TargetItem.BoostIdentifier.IsSet() ? TargetItem.BoostIdentifier : $"{TargetItem.GetType()}: {TargetItem.Name}";

            if (IconCache.TryGetValue(cacheKey, out frame))
            {
                Icon = frame;
                return;
            }

            var bitmapFrame = await Task.Factory.StartNew(() =>
            {
                var icon = iconResolver?.Resolve();

                if (icon == null)
                {
                    return null;
                }

                using (var bmp = icon.ToBitmap())
                {
                    var stream = new MemoryStream();
                    bmp.Save(stream, ImageFormat.Png);
                    return BitmapFrame.Create(stream);
                }
            });

            if (bitmapFrame == null)
            {
                return;
            }

            IconCache.AddOrUpdate(cacheKey, bitmapFrame, (x, f) => bitmapFrame);

            Icon = bitmapFrame;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}