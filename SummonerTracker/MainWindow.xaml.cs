using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using RiotApi.Net.RestClient;
using RiotApi.Net.RestClient.Configuration;
using RiotApi.Net.RestClient.Dto.CurrentGame;
using RiotApi.Net.RestClient.Dto.LolStaticData.Champion;
using RiotApi.Net.RestClient.Dto.Summoner;
using RiotApi.Net.RestClient.Helpers;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Timers.Timer;

namespace SummonerTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            //Inicializa();

            NotifyIcon.DoubleClick += (sender, args) =>
            {
                Show();
                NotifyIcon.Visible = false;
                WindowState = WindowState.Normal;
            };
            Closing += (sender, args) =>
            {
                NotifyIcon.Visible = false;
            };

            //31c4ded7-3de9-423e-bdbf-7fd6665e011b
            RiotKey = ConfigurationManager.AppSettings["RiotKey"];
            if (string.IsNullOrEmpty(RiotKey))
            {
                MessageBox.Show("Riot API key not found.");
                Close();
            }
            else if (RiotKey.Equals("API_KEY"))
            {
                RiotKey = "31c4ded7-3de9-423e-bdbf-7fd6665e011b";
            }

            //01e7b9c9dcc343f595d86c2517710e9f
            PushalotKey = ConfigurationManager.AppSettings["PushalotKey"];
            //if (string.IsNullOrEmpty(PushalotKey))
            //{
            //    MessageBox.Show("Pushalot API key not found.");
            //    Close();
            //}
            //else if(PushalotKey.Equals("API_KEY"))
            //{
            //    PushalotKey = "01e7b9c9dcc343f595d86c2517710e9f";
            //}

            foreach (string s in ConfigurationManager.AppSettings["Summoners"].Split('|'))
            {
                if (LbSummoners.Items.OfType<ListBoxItem>().Any(it => it.Content.Equals(s)))
                {
                    continue;
                }
                LbSummoners.Items.Add(new ListBoxItem {Content = s});
            }

            //Controle de limite de requests
            LimitRate(TimeSpan.FromSeconds(15), 10); //10 requests every 10 seconds
            LimitRate(TimeSpan.FromMinutes(12), 500); //500 requests every 10 minutes

            PbStatus.Maximum = TimeSpan.FromMinutes(UpdateTime).TotalMilliseconds;
            TbName.Focus();

            DataVersion = RiotClient.LolStaticData.GetVersionData(RiotApiConfig.Regions.BR).First();
            Update();
        }

        private string DataVersion { get; }

        private string RiotKey { get; }
        private string PushalotKey { get; }

        private Timer _updateTimer;
        /// <summary>
        /// Timer para controle da próxima atualização
        /// </summary>
        private Timer UpdateTimer
        {
            get
            {
                if (_updateTimer == null)
                {
                    _updateTimer = new Timer {Interval = RefreshTime};
                    _updateTimer.Elapsed += (sender, args) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if ((NextUpdate = NextUpdate.Subtract(TimeSpan.FromMilliseconds(RefreshTime))) == TimeSpan.Zero)
                            {
                                UpdateAndResetTimer();
                            }
                        });
                    };
                }
                return _updateTimer;
            }
        }

        /// <summary>
        /// Realiza o update e reseta o timer para a próxima atualização.
        /// </summary>
        private void UpdateAndResetTimer()
        {
            Update();
            NextUpdate = TimeSpan.FromMinutes(UpdateTime);
        }

        private static double UpdateTime => 0.3;
        private static double RefreshTime => 100;
        //private static int RequestRateValue { get; set; }

        private TimeSpan _nextUpdate = TimeSpan.FromMinutes(UpdateTime);

        /// <summary>
        /// Time span to next update
        /// </summary>
        private TimeSpan NextUpdate
        {
            get { return _nextUpdate; }
            set
            {
                TimeSpan oldSpan = _nextUpdate;
                _nextUpdate = value;
                if (NextUpdate > TimeSpan.Zero)
                {
                    PbStatus.Value = TimeSpan.FromMinutes(UpdateTime).TotalMilliseconds - NextUpdate.TotalMilliseconds;
                    if (oldSpan.Seconds != NextUpdate.Seconds)
                    {
                        TbStatus.Text = $"Time to next update: {NextUpdate.Add(TimeSpan.FromSeconds(1)).ToString(@"mm\:ss")}";
                        //TbStatus.Text = $"Time to next update: {NextUpdate.ToString(@"mm\:ss")}";
                    }
                }
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                NotifyIcon.Visible = true;
                Hide();
            }

            base.OnStateChanged(e);
        }

        private List<SummonerDto> _summonerIDs;
        /// <summary>
        /// Lista de Summoners.
        /// </summary>
        private List<SummonerDto> Summoners => _summonerIDs ?? (_summonerIDs = new List<SummonerDto>());

        private Dictionary<long, ChampionDto> _champions;
        /// <summary>
        /// Lista de champions.
        /// </summary>
        private Dictionary<long, ChampionDto> Champions => _champions ?? (_champions = new Dictionary<long, ChampionDto>());

        /// <summary>
        /// Ícone de notificação na tray.
        /// </summary>
        public NotifyIcon NotifyIcon { get; } = new NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().ManifestModule.Name),
            Text = "SummonerTracker",
            Visible = false
        };

        private IRiotClient _riotClient;

        /// <summary>
        /// Controle da API da Riot
        /// </summary>
        private IRiotClient RiotClient
        {
            get
            {
                foreach (var pair in RateLimits)
                {
                    //RequestRateValue++;
                    //Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => TbValue.Text = RequestRateValue.ToString()));
                    //Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => TbRate.Text = RequestRate[pair.Key].ToString()));
                    //Dispatcher.Invoke(() => PbStatus.Foreground = Brushes.Red);
                    RequestRate[pair.Key]++;
                    while (RequestRate[pair.Key] >= pair.Value)
                    {
                        Thread.Sleep(1000);
                    }
                    //Dispatcher.Invoke(() => PbStatus.Foreground = Brushes.Green);
                }
                return _riotClient ?? (_riotClient = new RiotClient(RiotKey));
            }
        }

        /// <summary>
        /// Cria um novo limite para requisições e inicia um timer para controle.
        /// </summary>
        private void LimitRate(TimeSpan timeSpan, int maxRate)
        {
            //TbLimit.Text = maxRate.ToString();
            RateLimits.Add(timeSpan, maxRate);
            RequestRate.Add(timeSpan, 0);
            Timer timer = new Timer(timeSpan.TotalMilliseconds);
            timer.Elapsed += (sender, args) =>
            {
                RequestRate[timeSpan] = 0;
                //Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => TbRate.Text = RequestRate[timeSpan].ToString()));
            };
            timer.Start();
        }

        private Dictionary<TimeSpan, int> _rateLimits;
        /// <summary>
        /// Rate limits per Time span
        /// </summary>
        private Dictionary<TimeSpan, int> RateLimits => _rateLimits ?? (_rateLimits = new Dictionary<TimeSpan, int>());

        private Dictionary<TimeSpan, int> _requestRate;
        /// <summary>
        /// Request rate at the moment
        /// </summary>
        private Dictionary<TimeSpan, int> RequestRate => _requestRate ?? (_requestRate = new Dictionary<TimeSpan, int>());

        /// <summary>
        /// Busca as informações para a lista de Summoners
        /// </summary>
        private void GetSummoners(params string[] names)
        {
            if (names.Length > 40)
            {
                throw new ArgumentOutOfRangeException(nameof(names), names.Length, @"limite máximo de 40 elementos");
            }

            Dictionary<string, SummonerDto> summonerDtos = RiotClient.Summoner.GetSummonersByName(RiotApiConfig.Regions.BR, names.ToArray());
            foreach (KeyValuePair<string, SummonerDto> p in summonerDtos)
            {
                Summoners.Add(p.Value);
            }
        }

        //private void Inicializa()
        //{
            //31c4ded7-3de9-423e-bdbf-7fd6665e011b
            //List<string> names = new List<string>();
            //foreach (string name in SummonerNames)
            //{
            //    names.Add(name);
            //    if (names.Count == 40)
            //    {
            //        GetSummoners(names.ToArray());
            //        names.Clear();
            //    }
            //}
            //if (names.Any())
            //{
            //    GetSummoners(names.ToArray());
            //}

            ////retrieve all current free to play champions
            //ChampionListDto championList = riotClient.Champion.RetrieveAllChampions(RiotApiConfig.Regions.NA, true);
            ////print the number of free to play champions
            //Console.WriteLine($"There are {championList.Champions.Count()} free to play champions to play with!");

            ////retrieve xeyanord and fnatictop summoners with one call
            //Dictionary<string, SummonerDto> summoners = riotClient.Summoner.GetSummonersByName(RiotApiConfig.Regions.EUNE, "xeyanord", "fnatictop");
            //SummonerDto xeyanord = summoners["xeyanord"];
            //SummonerDto fnatictop = summoners["fnatictop"];
            ////print the following statement about the two summoners
            //Console.WriteLine($"{fnatictop.Name} is level {fnatictop.SummonerLevel} and {xeyanord.Name} is {xeyanord.SummonerLevel}, its because {xeyanord.Name} is a slacker!");

            ////get challeger tier league for ranked solo 5x5
            //LeagueDto challengers = riotClient.League.GetChallengerTierLeagues(RiotApiConfig.Regions.EUNE, Enums.GameQueueType.RANKED_SOLO_5x5);
            ////get top 5 leaderboard using LINQ
            //List<LeagueDto.LeagueEntryDto> top5 = challengers.Entries.OrderByDescending(x => x.LeaguePoints).Take(5).ToList();
            ////Print top 5 leaderboard
            //top5.ForEach(
            //topEntry =>
            //Console.WriteLine(
            //$"{topEntry.PlayerOrTeamName} - wins:{topEntry.Wins}  loss:{topEntry.Losses} points:{topEntry.LeaguePoints}"));

            //StringBuilder sb = new StringBuilder();
            //Dictionary<long, int> championsCount = new Dictionary<long, int>();
            //foreach (KeyValuePair<SummonerDto, DateTime> p in Summoners)
            //{
            //    sb.AppendLine(p.Key.Name);
            //    MatchListDto list = riotClient.MatchList.GetMatchListBySummonerId(RiotApiConfig.Regions.BR, p.Key.Id, beginTime: p.Value.ToUnixTime());
            //    foreach (MatchListDto.MatchReference match in list.Matches)
            //    {
            //        if (championsCount.ContainsKey(match.Champion))
            //        {
            //            championsCount[match.Champion]++;
            //        }
            //        else
            //        {
            //            championsCount.Add(match.Champion, 1);
            //        }
            //    }

            //    foreach (KeyValuePair<long, int> pair in championsCount.OrderByDescending(c => c.Value))
            //    {
            //        ChampionDto champion;
            //        int champId = Convert.ToInt32(pair.Key);
            //        if (Champions.ContainsKey(champId))
            //        {
            //            champion = Champions[champId];
            //        }
            //        else
            //        {
            //            champion = riotClient.LolStaticData.GetChampionById(RiotApiConfig.Regions.BR, Convert.ToInt32(pair.Key));
            //            Champions.Add(champion.Id, champion);
            //        }

            //        sb.AppendLine($"{champion.Name} = {pair.Value}");
            //    }
            //    sb.AppendLine();
            //}

            //TextBox.Text = sb.ToString();

            //Thread t = new Thread(() => new Timer());
            //foreach (SummonerDto sum in Summoners)
            //{
            //    CurrentGameInfo cg;
            //    try
            //    {
            //        cg = RiotClient.CurrentGame.GetCurrentGameInformationForSummonerId(RiotApiConfig.Platforms.BR1, sum.Id);
            //    }
            //    catch (RiotExceptionRaiser.RiotApiException)
            //    {
            //        continue;
            //    }

            //    using (WebClient client = new WebClient())
            //    {
            //        int champId = Convert.ToInt32(cg.Participants.Single(c => c.SummonerId == sum.Id).ChampionId);
            //        if (!Champions.ContainsKey(champId))
            //        {
            //            Champions.Add(champId, RiotClient.LolStaticData.GetChampionById(RiotApiConfig.Regions.NA, Convert.ToInt32(champId)));
            //        }

            //        NameValueCollection values = new NameValueCollection
            //        {
            //            ["AuthorizationToken"] = "01e7b9c9dcc343f595d86c2517710e9f",
            //            ["Body"] = $"{sum.Name} is playing as {Champions[champId].Name}."
            //        };

            //        // client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
            //        client.UploadValues("https://pushalot.com/api/sendmessage", values);
            //    }
            //}
        //}

        private ChampionDto GetChampion(long champId)
        {
            if (!Champions.ContainsKey(champId))
            {
                Champions.Add(champId, RiotClient.LolStaticData.GetChampionById(RiotApiConfig.Regions.NA, Convert.ToInt32(champId)));
            }
            return Champions[champId];
        }

        private bool IsUpdating { get; set; }

        private void Update()
        {
            if (IsUpdating)
            {
                return;
            }

            new Thread(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateTimer.Stop();
                    IsUpdating = true;
                    TbStatus.Text = "Updating...";
                    PbStatus.IsIndeterminate = true;
                    Cursor = Cursors.Wait;
                });
                try
                {
                    //Load Summoner information
                    List<string> names = new List<string>();
                    foreach (ListBoxItem item in LbSummoners.Items)
                    {
                        string name = Dispatcher.Invoke(() => item.Content.ToString());
                        if (Summoners.Any(s => s.Name.Equals(name)))
                        {
                            continue;
                        }

                        names.Add(name);
                        if (names.Count == 40)
                        {
                            GetSummoners(names.ToArray());
                            names.Clear();
                        }
                    }
                    if (names.Any())
                    {
                        GetSummoners(names.ToArray());
                    }

                    //Verify current game
                    foreach (SummonerDto sum in Summoners)
                    {
                        CurrentGameInfo cg;
                        try
                        {
                            cg = RiotClient.CurrentGame.GetCurrentGameInformationForSummonerId(RiotApiConfig.Platforms.BR1, sum.Id);
                        }
                        catch (RiotExceptionRaiser.RiotApiException)
                        {
                            continue;
                        }

                        ChampionDto champion = GetChampion(cg.Participants.Single(c => c.SummonerId == sum.Id).ChampionId);
                        Notify(sum.Name, champion.Name, champion.Key);
                    }
                }
                finally
                {
                    Dispatcher.Invoke(() =>
                    {
                        Cursor = Cursors.Arrow;
                        PbStatus.IsIndeterminate = false;
                        IsUpdating = false;
                        UpdateTimer.Start();
                    });
                }
            }) {IsBackground = true, Name = "Update Thread"}.Start();
        }

        private void Notify(string summonerName, string championName, string championKey)
        {
            string notificationText = $"{summonerName} is playing as {championName}.";
            if (string.IsNullOrEmpty(PushalotKey) || PushalotKey.Equals("API_KEY"))
            {
                //ToastGenerator.Instance.ShowToast(ToastTemplateType.ToastText01, notificationText);
                //ToastGenerator.Instance.ShowToast($"http://ddragon.leagueoflegends.com/cdn/{DataVersion}/img/champion/{championKey}.png", championName, notificationText);
                //ToastGenerator.Instance.ShowToast(
                //    "http://blogs.msdn.com/cfs-filesystemfile.ashx/__key/communityserver-blogs-components-weblogfiles/00-00-01-71-81-permanent/2727.happycanyon1_5B00_1_5D00_.jpg", championName,
                //    notificationText);
                //ToastGenerator.Instance.ShowToast(
                //    $"file:///C:\\Users\\Renato\\Downloads\\dragontail-6.15.1\\6.15.1\\img\\champion\\{championKey}.png",
                //    championName, notificationText);
                //ToastGenerator.Instance.ShowToast("ms-appx://Assets/Images/back.png", notificationText);
            }
            else
            {
                using (WebClient client = new WebClient())
                {
                    NameValueCollection values = new NameValueCollection
                    {
                        ["AuthorizationToken"] = PushalotKey,
                        ["Body"] = notificationText
                    };

                    // client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                    client.UploadValues("https://pushalot.com/api/sendmessage", values);
                }
            }
        }

        private void LbSummonersPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                ListBoxItem[] selectedItems = LbSummoners.SelectedItems.Cast<ListBoxItem>().ToArray();
                foreach (ListBoxItem selectedItem in selectedItems)
                {
                    LbSummoners.Items.Remove(selectedItem);
                    Summoners.Remove(Summoners.Single(s => s.Name.Equals(selectedItem.Content.ToString())));
                }
                e.Handled = true;
            }
        }

        private void AddSummoner(string summonerName)
        {
            LbSummoners.Items.Add(new ListBoxItem {Content = summonerName});
            GetSummoners(summonerName);
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            AddSummoner(TbName.Text);
            TbName.Clear();
        }

        private void TbName_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddSummoner(TbName.Text);
                TbName.Clear();
            }
        }

        private void PbStatus_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            UpdateAndResetTimer();
        }
    }

    public static class MyExtentions
    {
        public static DateTime FromUnixTime(this long unixTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime);
        }

        public static long ToUnixTime(this DateTime date)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Convert.ToInt64((date.ToUniversalTime() - epoch).TotalSeconds);
        }
    }
}