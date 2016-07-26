using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RiotApi.Net.RestClient;
using RiotApi.Net.RestClient.Configuration;
using RiotApi.Net.RestClient.Dto.CurrentGame;
using RiotApi.Net.RestClient.Dto.LolStaticData.Champion;
using RiotApi.Net.RestClient.Dto.Summoner;
using RiotApi.Net.RestClient.Helpers;
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

            //NotifyIcon.DoubleClick += (sender, args) =>
            //{
            //    Show();
            //    WindowState = WindowState.Normal;
            //};
            //Closing += (sender, args) =>
            //{
            //    NotifyIcon.Visible = false;
            //};

            //31c4ded7-3de9-423e-bdbf-7fd6665e011b
            RiotKey = ConfigurationManager.AppSettings["RiotKey"];
            if (string.IsNullOrEmpty(RiotKey))
            {
                MessageBox.Show("Riot API key not found.");
                Close();
            }

            //01e7b9c9dcc343f595d86c2517710e9f
            PushalotKey = ConfigurationManager.AppSettings["PushalotKey"];
            if (string.IsNullOrEmpty(PushalotKey))
            {
                MessageBox.Show("Pushalot API key not found.");
                Close();
            }

            List<string> names = new List<string>();
            foreach (ListBoxItem name in LbSummoners.Items)
            {
                names.Add(name.Content.ToString());
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

            PbStatus.Maximum = TimeSpan.FromMinutes(UpdateTime).TotalMilliseconds;
            TbName.Focus();
            Update();
        }

        private string RiotKey { get; }
        private string PushalotKey { get; }

        private Timer _timer;
        /// <summary>
        /// Timer para controle da próxima atualização
        /// </summary>
        private Timer Timer
        {
            get
            {
                if (_timer == null)
                {
                    _timer = new Timer {Interval = RefreshTime};
                    _timer.Elapsed += (sender, args) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if ((NextUpdate = NextUpdate.Subtract(TimeSpan.FromMilliseconds(RefreshTime))) == TimeSpan.Zero)
                            {
                                Update();
                                NextUpdate = TimeSpan.FromMinutes(UpdateTime);
                            }
                        });
                    };
                }
                return _timer;
            }
        }

        private static double UpdateTime => 5;
        private static double RefreshTime => 100; 

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
                    }
                }
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
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
        //public NotifyIcon NotifyIcon { get; } = new NotifyIcon
        //{
        //    Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().ManifestModule.Name),
        //    Visible = true
        //};

        //private IEnumerable<string> _summonerNames;
        ///// <summary>
        ///// Conjunto de nomes para pesquisa
        ///// </summary>
        //private IEnumerable<string> SummonerNames => _summonerNames ?? (_summonerNames = new[]
        //{
        //    "DG Skhar", "DG Space", "DG Power of Hue", "DG Odeio Toboco", "DG nekocora", "DG Wally", "DG Darkness", "DG Edge", "DG mzi", "DG Sampapito",
        //    "DG Sarinha", "DG Murphy", "DG Zar", "DG Hyukjae", "DG Papillon001", "DG Leonoak222"
        //    //"DG Skhar", "16 Tons de ADC",
        //    //"4LaN", "Aang1", "Abnegação", "Absolut209", "AdC Alot", "adc do autofill", "Ahri Main",
        //    //"Akatsuki Mills", "ALcT Top bosta", "all hail haunter", "andei trollando", "Areksuu", "astro",
        //    //"Avengerxs", "Ayk", "b g ô b", "BabyMisty", "Bd JooKeR", "beast mid laner", "BG Lenovo Rngr",
        //    //"BG Ranger", "bigpepe", "Bl4keouS", "Blinkerino", "boca no pico", "BoIinhoDeArroz",
        //    //"BRAVE Brevot", "BRAVE Cabuloso", "BRAVE Sarkis", "BRAVE Thulz", "brtw amorzinho",
        //    //"Cara Inteligente", "Carioca HOKAGE", "Caôs", "Chaser", "chavoso triste", "ChungHingSamLam",
        //    //"CITCRA SYEKNOM", "Cleyton Coxinha", "Cloyster DISNEY", "CNB HyperX Codpc", "CNB HyperX Deki",
        //    //"CNB HyperX Devo", "CNB HyperX Shy", "CNB HyperX Spek", "CNB HyperX Wos", "CNB HyperX Yampi",
        //    //"Codpiece", "Crywulf", "cute girI", "Céoszinho", "danmf", "danz0r408", "Darwin the fish",
        //    //"Days4fun", "Derick6", "Devios Sarrador", "devo boy", "DJ DlEGO", "dory a peixa azu",
        //    //"eChamp Dudao", "eChamp k0ga", "eChamp Krastyel", "EDG Awesome", "elf lvl 99", "Emp1rean",
        //    //"Envyyy", "Epilef safadão", "ESC Ever Tempt", "EzPrince", "FEBlVEN", "ferchu triste",
        //    //"fightingforlove", "FinalDeath", "FleekzKux", "fNbê", "Forget me", "Foxziim", "Frango Úmido",
        //    //"Fred O Temido", "Freedom2", "FREEKR", "FS Pinguin", "Gentha", "Glkko", "gofer", "Gonto1",
        //    //"Herminoso", "Hide on pussie", "Hit My Heart", "Holthedoor", "Hy0g4", "Hyrrokin",
        //    //"I AM BIXA Q BATE", "i wanna jungle", "IDM EzPrince", "IDM Fire", "IGUIN MANEIRIN", "im cheed",
        //    //"imabeast", "INTZ aC AlexKiD", "INTZ aC Goro", "INTZ Yang1", "ISG Newbie", "Its Me FraGioRlz",
        //    //"JAO DA MAIONESE", "jojothekiler", "Josh Dun", "JudenkiJuzo", "Jukes", "justt a sad guy",
        //    //"Juzinho", "Kales Myth", "kazhinzin", "KBM Zuao", "Kier", "KLG Julio", "Korea do Sertão",
        //    //"KsImorTal", "KTCHORO D KONOHA", "KTCHORO O WUKÓNG", "KTG Erasus", "KTG Kveeykva", "KTG xanad0",
        //    //"Kuroo", "kyolols", "L Watch", "leo1 aka boss", "Lesbian", "LGD Handsome", "lllllIIIll",
        //    //"lllllllIIIlll", "Loopi", "Luciana Genro", "LUSKKÃO", "Luuuuuuuuuukz", "Léozin1", "Lêozuxo",
        //    //"MANTEGA NA TEGA", "Marta Fogueteira", "Matsukaze", "MC DYNQUEDO", "Mechanics",
        //    //"Melhor do Prédio", "Minerva", "Mr Magician", "MrSuicideeSheep", "Mudkiipz", "MuShiBaS",
        //    //"Nemesis Hydro", "Nemozeiro", "Nice SoloQ Guy", "Ninja28", "nothing1", "Nyu", "Náru ",
        //    //"O Famoso Maso", "oBz Freddao", "OCK HBee", "Oh well", "OKG Wise Titan", "OPK Goku",
        //    //"OPK Professor", "OPK Skyerr", "paiN jÚc", "paiN KamiKat", "paiN TaeYeon", "PARARA TINBUM",
        //    //"PEDASO DE GATO", "Pequeno Asiatico", "Pichau Atlanta", "Pichau Leffer", "Pichau Paz",
        //    //"Pidgeot DISNEY ", "PNG Rakin1", "POLÃO", "Primoo S2 Mel", "q bom cara", "QR PÃO QUENTINHO",
        //    //"Rakyz", "Ranai", "RBT LG MANTARRAY", "RBT LG Slow", "RBT LG Zeypher", "RED Eryon", "RED SacyR",
        //    //"RelLucian", "Rey SkywaIker", "Rhydor", "Ricardownage", "rivAbene", "Robs", "Roex",
        //    //"ruan1 aka boss", "Satamonio", "sdkjaslkd1lulxd", "Sea Engineer", "SesshabecomoÉ", "SESSHODIA",
        //    //"Skeezy", "skt marinjuana", "Skyér", "snitnaZ", "Souaisei Riron ", "Stand Aside", "Stressado",
        //    //"Style o Genji", "Supp Me Carry U", "SuranoAscension", "Tadashl", "Tecnosh1", "The tp guy",
        //    //"TheMid itself", "tinowns2", "tinowns3", "Tio Sessh", "Tirano Otakinhu", "tomate1", "TomnaM",
        //    //"too many daddies", "TOOOLDFORLEAGUE", "TopOnSemMid", "Traitor", "UnderSky", "Uriinho",
        //    //"URUSAI URUSAI", "Vampyrus", "Van Joune", "VANESSA TRAVESSA", "VFK esinhA", "VFK Robo",
        //    //"Victinz", "Vinhemo", "Vini a Riven", "Volponi Senpai", "vVvert", "Warm Whispers", "Wild Pig",
        //    //"Wintero", "xObliverator", "YGÃO MANEIRÃO", "you even trying", "Zantins", "zAvenger", "Zelgius",
        //    //"ZeícrO", "Zuaozito", "zxcvbnm", "Émp"
        //});

        private IRiotClient _riotClient;
        /// <summary>
        /// Controle da API da Riot
        /// </summary>
        private IRiotClient RiotClient => _riotClient ?? (_riotClient = new RiotClient(RiotKey));

        /// <summary>
        /// Busca as informações para a lista de Summoners
        /// </summary>
        private void GetSummoners(params string[] names)
        {
            if (names.Length > 40)
            {
                throw new ArgumentOutOfRangeException(nameof(names), names.Length, @"limite máximo de 40 elementos");
            }

            foreach (KeyValuePair<string, SummonerDto> p in RiotClient.Summoner.GetSummonersByName(RiotApiConfig.Regions.BR, names.ToArray()))
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
                    Timer.Stop();
                    IsUpdating = true;
                    TbStatus.Text = "Updating...";
                    PbStatus.IsIndeterminate = true;
                    Cursor = Cursors.Wait;
                });
                try
                {
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

                        using (WebClient client = new WebClient())
                        {
                            NameValueCollection values = new NameValueCollection
                            {
                                ["AuthorizationToken"] = PushalotKey,
                                ["Body"] =
                                    $"{sum.Name} is playing as {GetChampion(cg.Participants.Single(c => c.SummonerId == sum.Id).ChampionId).Name}."
                            };

                            // client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                            client.UploadValues("https://pushalot.com/api/sendmessage", values);
                        }
                    }
                }
                finally
                {
                    Dispatcher.Invoke(() =>
                    {
                        Cursor = Cursors.Arrow;
                        PbStatus.IsIndeterminate = false;
                        IsUpdating = false;
                        Timer.Start();
                    });
                }
            }).Start();
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

        private void AddSumm()
        {
            LbSummoners.Items.Add(new ListBoxItem {Content = TbName.Text});
            GetSummoners(TbName.Text);
            TbName.Clear();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            AddSumm();
        }

        private void TbName_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddSumm();
            }
        }

        //private void PbStatus_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        //{
        //    Update();
        //}
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