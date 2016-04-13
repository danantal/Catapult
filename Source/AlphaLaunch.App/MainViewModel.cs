using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using AlphaLaunch.Core.Actions;
using AlphaLaunch.Core.Frecency;
using AlphaLaunch.Core.Indexes;
using AlphaLaunch.Core.Selecta;
using AlphaLaunch.Spotify;
using Serilog;

namespace AlphaLaunch.App
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ActionRegistry _actionRegistry;

        private readonly ListViewModel _mainListModel = new ListViewModel();
        private Searcher _selectaSeacher;
        private readonly List<IIndexable> _actions = new List<IIndexable>();
        private readonly FrecencyStorage _frecencyStorage;



        public MainViewModel()
        {
            _actionRegistry = new ActionRegistry();

            _actionRegistry.RegisterAction<OpenAction>();
            _actionRegistry.RegisterAction<OpenAsAdminAction>();

            var frecencyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlphaLaunch", "frecency.json");
            _frecencyStorage = new FrecencyStorage(frecencyPath);

            RegisterAction<SpotifyNextTrackAction>();
            RegisterAction<SpotifyPlayPauseAction>();
            RegisterAction<SpotifyPreviousTrackAction>();
            RegisterAction<SpotifyStopAction>();

            RegisterAction<KillProcessAction>();
            RegisterAction<OpenLastLogAction>();
            RegisterAction<OpenLogFolderAction>();

            StartIntentService(Dispatcher.CurrentDispatcher);
        }

        private void RegisterAction<T>() where T : IIndexable, new()
        {
            _actionRegistry.RegisterAction<T>();
            //IndexStore.Instance.IndexAction(new T());

            _actions.Add(new T());
        }

        //public async Task UpdateSearchAsync(string search, CancellationToken token)
        //{
        //var searchItemModel = _mainListModel.SelectedSearchItem;

        //if (Search.Contains(" ") && searchItemModel != null && searchItemModel.TargetItem is KillProcessAction)
        //{
        //    MainListModel = _processListModel;

        //    var processes = Process
        //        .GetProcesses()
        //        .Select(x => new RunningProcessInfo(x.ProcessName, x.MainWindowTitle, x.Id));

        //    var searcher = new FuzzySearcher();

        //    searcher.IndexItems(processes);

        //    var searchResults = searcher.Search(Search.Split(new[] { ' ' })[1], ImmutableDictionary.Create<string, EntryBoost>()).Take(10);

        //    _processListModel.Items.Clear();

        //    foreach (var searchResult in searchResults)
        //    {
        //        _processListModel.Items.Add(new SearchItemModel(searchResult.Name, searchResult.Score, searchResult.TargetItem, searchResult.HighlightIndexes));
        //    }

        //    _processListModel.SelectedIndex = 0;

        //    return;
        //}

        //var items = await Task.Factory.StartNew(() =>
        //{
        //    var frecencyData = _frecencyStorage.GetFrecencyData();
        //    Func<IIndexable, int> boosterFunc = x => frecencyData.ContainsKey(x.BoostIdentifier) ? frecencyData[x.BoostIdentifier] : 0;

        //    _selectaSeacher = _selectaSeacher ?? Searcher.Create(SearchResources.GetFiles().Concat(_actions).ToArray());
        //    _selectaSeacher = _selectaSeacher.Search(search, boosterFunc);
        //    var searchResults = _selectaSeacher.SearchResults.Take(10);
        //    var searchItemModels = searchResults.Select(x => new SearchItemModel(x.Name, x.Score, x.TargetItem, x.HighlightIndexes, x.TargetItem.GetIconResolver())).ToArray();
        //    return searchItemModels;
        //}, token);

        //token.ThrowIfCancellationRequested();

        //UpdateSearchItems(items);
        //}

        private void UpdateSearchItems(SearchItemModel[] searchItemModels)
        {
            MainListModel.Items.Reset(searchItemModels);
            MainListModel.SelectedIndex = 0;
        }


        public ListViewModel MainListModel
        {
            get { return _mainListModel; }
        }

        private void OpenSelected(string search)
        {
            if (!_mainListModel.Items.Any())
            {
                return;
            }

            //if (_mainListModel.SelectedSearchItem != null)
            //{
            //    var killProcessAction = _mainListModel.SelectedSearchItem.TargetItem as KillProcessAction;

            //    if (killProcessAction != null)
            //    {
            //        var processItem = _processListModel.SelectedSearchItem.TargetItem as RunningProcessInfo;

            //        if (processItem == null)
            //        {
            //            return;
            //        }

            //        killProcessAction.RunAction(processItem);
            //        return;
            //    }
            //}

            var searchItemModel = _mainListModel.Items[_mainListModel.SelectedIndex];

            _frecencyStorage.AddUse(searchItemModel.TargetItem.BoostIdentifier, search, _mainListModel.SelectedIndex);

            var standaloneAction = searchItemModel.TargetItem as IStandaloneAction;
            if (standaloneAction != null)
            {
                Log.Information("Launching {@TargetItem} with {ActionType}", searchItemModel.TargetItem, standaloneAction.GetType());

                try
                {
                    standaloneAction.RunAction();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception launching {@TargetItem} with {ActionType}", searchItemModel.TargetItem, standaloneAction.GetType());
                }

                return;
            }


            var actionList = _actionRegistry.GetActionFor(searchItemModel.TargetItem.GetType());
            var firstActionType = actionList.First();

            try
            {
                var actionInstance = Activator.CreateInstance(firstActionType);
                var runMethod = firstActionType.GetMethod("RunAction");

                Log.Information("Launching {@TargetItem} with {ActionType}", searchItemModel.TargetItem, firstActionType);
                runMethod.Invoke(actionInstance, new[] { searchItemModel.TargetItem });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception launching {@TargetItem} with {ActionType}", searchItemModel.TargetItem, firstActionType);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private BlockingCollection<IIntent> _queue;
        private Dispatcher _dispatcher;

        public void StartIntentService(Dispatcher dispatcher)
        {
            _queue = new BlockingCollection<IIntent>(new ConcurrentQueue<IIntent>());
            _dispatcher = dispatcher;

            var thread = new Thread(Process) { IsBackground = true };
            thread.Start();
        }

        public void AddIntent(IIntent intent)
        {
            _queue.Add(intent);
        }

        private void Process()
        {
            foreach (var intent in _queue.GetConsumingEnumerable())
            {
                if (intent is SearchIntent)
                {
                    var searchIntent = intent as SearchIntent;

                    var frecencyData = _frecencyStorage.GetFrecencyData();
                    Func<IIndexable, int> boosterFunc = x => frecencyData.ContainsKey(x.BoostIdentifier) ? frecencyData[x.BoostIdentifier] : 0;

                    _selectaSeacher = _selectaSeacher ?? Searcher.Create(SearchResources.GetFiles().Concat(_actions).ToArray());
                    _selectaSeacher = _selectaSeacher.Search(searchIntent.Search, boosterFunc);
                    var searchResults = _selectaSeacher.SearchResults.Take(10);
                    var searchItemModels = searchResults.Select(x => new SearchItemModel(x.Name, x.Score, x.TargetItem, x.HighlightIndexes, x.TargetItem.GetIconResolver())).ToArray();

                    _dispatcher.Invoke(() => UpdateSearchItems(searchItemModels));
                }
                else if (intent is ExecuteIntent)
                {
                    var executeIntent = intent as ExecuteIntent;

                    OpenSelected(executeIntent.Search);
                }
                else if (intent is MoveSelectionIntent)
                {
                    var moveSelectionIntent = intent as MoveSelectionIntent;

                    _dispatcher.Invoke(() =>
                    {
                        if (moveSelectionIntent.Direction == MoveDirection.Down)
                        {
                            MainListModel.SelectedIndex = Math.Min(MainListModel.Items.Count, MainListModel.SelectedIndex + 1);
                        }
                        else
                        {
                            MainListModel.SelectedIndex = Math.Max(0, MainListModel.SelectedIndex - 1);
                        }
                    });
                }
                else if (intent is ShutdownIntent)
                {
                    var shutdownIntent = intent as ShutdownIntent;
                    _dispatcher.Invoke(shutdownIntent.ShutdownAction);
                }
            }
        }
    }

    public class ExecuteIntent : IIntent
    {
        public string Search { get; set; }

        public ExecuteIntent(string search)
        {
            Search = search;
        }
    }

    public class ShutdownIntent : IIntent
    {
        public Action ShutdownAction { get; }

        public ShutdownIntent(Action shutdownAction)
        {
            ShutdownAction = shutdownAction;
        }
    }

    public class MoveSelectionIntent : IIntent
    {
        public MoveDirection Direction { get; set; }

        public MoveSelectionIntent(MoveDirection direction)
        {
            Direction = direction;
        }
    }

    public enum MoveDirection
    {
        Up,
        Down
    }

    public class SearchIntent : IIntent
    {
        public string Search { get; set; }

        public SearchIntent(string search)
        {
            Search = search;
        }
    }

    public interface IIntent
    {

    }
}
