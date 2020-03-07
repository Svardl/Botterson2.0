using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Discord;
using MongoDB.Driver;
using Botteron2._0.Modules.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using System.Globalization;
using System.Threading;

namespace Botteron2._0 {
    class Program {

        public DiscordSocketClient _client;
        public CommandService _commands;
        public IServiceProvider _services;
        public static List<string> badWords;
        public bool dontChange = false;
        public static Riddles CurrRiddle;
        public static List<int> banList = new List<int>();
        private Object Lock = new Object();

        static void Main(string[] args) => new Program().RunBotterson().GetAwaiter().GetResult();
        public static MongoCrud db;

        public async Task RunBotterson() {
            _client = new DiscordSocketClient();
            _commands = new CommandService();
            _services = new ServiceCollection().AddSingleton(_client).AddSingleton(_commands).BuildServiceProvider();

            db = new MongoCrud("Botterson");

            string token = "";
            var mongo = new MongoClient("mongodb+srv://Botterson:Botterson@bottersondb-op6k5.mongodb.net/test?retryWrites=true&w=majority");

            //banList.Add(6962);
            db.SetRandomRiddle();


            string path = System.IO.Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName + "//bad.txt";
            badWords = File.ReadAllLines(path).ToList();

            await RegisterCommands();
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await CheckForNewUsers();

            await Task.Delay(-1);
        }

        private async Task CheckForNewUsers() {

            while (_client.Guilds.Count == 0) {
                await Task.Delay(20);
            }

            var DbUsers = db.GetAll().Select(u => u.UserID).ToList();
            var OnlineUsers = _client.Guilds.Select(u => u.Users.Where(p => !p.IsBot)).SelectMany(t => t);

            foreach (var online in OnlineUsers) {

                if (!DbUsers.Any(u => u == online.DiscriminatorValue)) {
                    //Create new user in database
                    UserDb newUser = new UserDb() {
                        UserID = online.DiscriminatorValue,
                        Username = online.Username,
                        Greeting = null,
                        RiddlePoints = 0,
                        Warnings = 0,
                        LastOnline = null,
                        DoWarn = true
                    };
                    db.AddNewUser(newUser);
                }
            }
        }

        public async Task RegisterCommands() {
            _client.MessageReceived += HandleCommand;
            _client.GuildMemberUpdated += HandleUserUpdate;
            _client.UserJoined += HandleNewUser;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private async Task HandleNewUser(SocketGuildUser user) {

            var allUsers = db.GetAll();
            if (!allUsers.Any(u => u.UserID == user.DiscriminatorValue)) {
                var userDb = new UserDb() {
                    UserID = user.DiscriminatorValue,
                    Username = user.Username,
                    Warnings = 0,
                    Greeting = null,
                    RiddlePoints = 0,
                    LastOnline = null,
                    DoWarn = true

                };
                db.AddNewUser(userDb);
            }
        }

        private async Task HandleUserUpdate(SocketGuildUser before, SocketGuildUser after) {

            var bannedUser = db.GetUserIfBanned(before.DiscriminatorValue);
            if (bannedUser != null && after.Nickname != bannedUser.NewName) {
                if (bannedUser != null && before.Nickname != after.Nickname) {
                    await after.ModifyAsync(u => u.Nickname = before.Nickname);
                    dontChange = true;
                    await after.SendMessageAsync("https://media3.giphy.com/media/9NLYiOUxnKAJLIycEv/giphy.gif?cid=790b761147c7a95c91265422f8e860a25f433fa2b5bba283&rid=giphy.gif");
                }
            }

            if (before.Status.ToString() == "Offline" && after.Status.ToString() != "Offline") {
                await HandleGreeting(before, after);
            }
            //stopped playing
            if (before.Activity != null && after.Activity == null) {
                HandleStoppedPlaying(before);
            }
        }

        private void HandleStoppedPlaying(SocketGuildUser before) {
            if (Monitor.TryEnter(Lock)) {
                try {
                    if (before.Activity.Type == Discord.ActivityType.Playing) {

                        var user = db.LoadRecordsById(before.DiscriminatorValue);

                        if (DateTime.Now > user.LastPlayed.AddSeconds(60)) {

                            var Game = before.Activity as RichGame;
                            TimeSpan? duration = DateTime.Now - Game.Timestamps.Start;

                            user.LastPlayed = DateTime.Now;
                            user.MinutesPlayed += (int)duration.Value.TotalMinutes;

                            if (!user.GameTime.ContainsKey(Game.Name)) {
                                user.GameTime.Add(Game.Name, 0);
                            }
                            user.GameTime[Game.Name] += (int)duration.Value.TotalMinutes;
                            db.Update(user);
                            Console.WriteLine("Increased MinutesPlayed by" + (int)duration.Value.TotalMinutes + " for " + Game.Name);
                        }
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine(ex.ToString());
                }
                finally {
                    Monitor.Exit(Lock);
                }
            }
        }

        private async Task HandleGreeting(SocketGuildUser before, SocketGuildUser after) {
            var user = db.LoadRecordsById(after.DiscriminatorValue);

            if (!user.LastOnline.HasValue || DateTime.Now > user.LastOnline.Value.AddHours(3)) {
                if (!user.Greeting.HasValue) {
                    await after.SendMessageAsync("Hello there! Would you like to subscribe and be greeted by me Botterson everytime you sign in? !yes or !nah");
                }
                else if (user.Greeting.Value) {
                    string greetPath = System.IO.Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName + "//gree2.txt";
                    var greetings = File.ReadAllLines(greetPath).ToList();
                    Random rand = new Random();
                    var r = rand.Next(greetings.Count);
                    await after.SendMessageAsync(greetings[r]);
                }
            }
            user.LastOnline = DateTime.Now;
            db.Update(user);

        }

        private async Task HandleCommand(SocketMessage arg) {

            var message = arg as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);
            int argpos2 = 0;
            if (message.Author.IsBot && !message.HasCharPrefix('!', ref argpos2)) return;

            int argPos = 0;
            if (message.HasStringPrefix("!", ref argPos)) {
                var result = await _commands.ExecuteAsync(context, argPos, _services);
                if (!result.IsSuccess)
                    Console.WriteLine(result.ErrorReason);
            }
            else {
                await HandleGeneralMessages(message);
                RiddleHandler(message);

            }

            if (message.Channel is SocketDMChannel) {
                var pmDump = _client.GetChannel(621271826710134824) as ISocketMessageChannel;
                await pmDump.SendMessageAsync(message.Author + "\n" + message.Content);
            }
        }

        private void RiddleHandler(SocketUserMessage message) {
            if (message.Content.ToLower().Contains(CurrRiddle.Answer.ToLower())) {
                var person = db.LoadRecordsById(message.Author.DiscriminatorValue);
                person.RiddlePoints++;
                message.Channel.SendMessageAsync(message.Author.Mention + " That is correct! You now have " + person.RiddlePoints + " point" + (person.RiddlePoints == 1 ? "." : "s."));
                db.SetRandomRiddle();
                db.Update(person);
            }
        }

        private async Task HandleGeneralMessages(SocketUserMessage message) {
            await BadWords(message);
            string msg = message.Content.ToLower();

            if (msg.Contains("skool") || msg.Contains("to skol") || msg.Contains("go to school")) {
                await message.Channel.SendMessageAsync("Yes, School is very important");
            }

            if ((msg.Contains("hey") || msg.Contains("hello") || msg.Contains("hi")) && msg.Contains("botterson")) {
                await message.Channel.SendMessageAsync("Hello " + message.Author.Mention);
            }

            if (msg.Contains("go to bed") || msg.Contains("good night") || msg.Contains("going to bed")) {
                await message.Channel.SendMessageAsync("Yeah man have good night's rest");
            }

        }
        private async Task BadWords(SocketUserMessage message) {
            List<string> resList = new List<string>();

            foreach (var bad in badWords) {
                if (WholeMatch(bad, message.Content)) {
                    resList.Add(bad);
                }
            }
            resList = resList.Distinct().ToList();
            string combined = "";
            if (resList.Count != 0) {
                for (int i = 0; i < resList.Count; i++) {
                    if (i != 0 && i == resList.Count - 1) {
                        combined = combined.TrimEnd(',');
                        combined += " and " + "\"" + resList[i] + "\"";
                    }
                    else {
                        combined += "" + "\"" + resList[i] + "\"";
                        if (resList.Count != 1)
                            combined += ", ";
                    }
                }
                await message.AddReactionAsync(new Emoji("😠"));
                int numWar = IncrementWarning(message.Author.DiscriminatorValue);

                if (numWar % 5 == 0) {
                    Random rand = new Random();
                    int badIndex = rand.Next(badWords.Count);
                    string name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(badWords[badIndex].ToLower());


                    try {
                        if (db.GetUserIfBanned(message.Author.DiscriminatorValue) == null) {
                            var author = message.Author as SocketGuildUser;
                            string currentName = string.IsNullOrEmpty(author.Nickname) ? author.Username : author.Nickname;
                            await (author).ModifyAsync(u => u.Nickname = name);
                            await message.Channel.SendMessageAsync("Alright " + currentName + ". I've had enough of your little outbursts. Enjoy your new nickname. It will be changed back when your peers have forgiven you. \n (!forgive " + author.Mention + ")");
                            AddToBannedDb(author, name);
                        }
                    }
                    catch (Exception ex) {
                        await message.Channel.SendMessageAsync("Felix, you absolute fuck");
                    }

                }
                else if (db.LoadRecordsById(message.Author.DiscriminatorValue).DoWarn)
                    await message.Channel.SendMessageAsync(message.Author.ToString() + " said " + combined + ". You think that makes you seem cool? You now have " + numWar + " warning" + (numWar == 1 ? "" : "s"));
            }
        }

        private void AddToBannedDb(SocketGuildUser author, string newName) {
            db.AddToBanned(new Banned() {
                BannedUserId = author.DiscriminatorValue,
                BanDate = DateTime.Now,
                OldName = author.Nickname ?? "",
                NewName = newName,
                ForgiveCount = 0,
                Forgivers = new List<int>()


            });
        }

        private async Task ChangeName(SocketGuildUser user, string name) {

            string oldNick = user.Nickname;
            await user.ModifyAsync(u => u.Nickname = name);

        }

        private int IncrementWarning(int code) {
            var person = db.LoadRecordsById(code);
            person.Warnings++;
            db.Update(person);
            return person.Warnings;
        }

        private bool WholeMatch(string bad, string content) {
            content = content.ToLower();
            bad = bad.ToLower();
            var regex = new Regex(@"\b" + bad + @"\b");
            return regex.IsMatch(content);
        }

    }

    public class MongoCrud {
        IMongoDatabase db;

        public MongoCrud(string dbName) {
            var mongo = new MongoClient("mongodb+srv://Botterson:Botterson@bottersondb-op6k5.mongodb.net/test?retryWrites=true&w=majority");
            db = mongo.GetDatabase("Botterson");
        }

        public void Update(UserDb person) {
            var id = person.UserID;
            var filter = Builders<UserDb>.Filter.Eq("UserID", id);
            var col = db.GetCollection<UserDb>("UserDb");
            col.ReplaceOne(filter, person);
        }
        public UserDb LoadRecordsById(int id) {
            try {
                var filter = Builders<UserDb>.Filter.Eq("UserID", id);
                var col = db.GetCollection<UserDb>("UserDb");
                return col.Find(filter).First();
            }
            catch (Exception ex) {
                return null;
            }
        }

        public List<UserDb> GetAll() {
            var col = db.GetCollection<UserDb>("UserDb");
            return col.AsQueryable().ToList();
        }

        public Riddles GetNextRiddle() {
            var col = db.GetCollection<Riddles>("Riddles");
            int max = col.AsQueryable().ToList().Count;

            int num = Program.CurrRiddle.RiddleNum + 1 == max ? 0 : Program.CurrRiddle.RiddleNum + 1;
            return col.Find(r => r.RiddleNum == num).First();

        }
        public void SetRandomRiddle() {
            Random rand = new Random();
            var col = db.GetCollection<Riddles>("Riddles");
            int num = rand.Next(col.AsQueryable().ToList().Count);
            Program.CurrRiddle = col.Find(r => r.RiddleNum == num).First();
        }

        internal void AddNewUser(UserDb newUser) {
            var col = db.GetCollection<UserDb>("UserDb");
            col.InsertOne(newUser);
        }

        public void AddToBanned(Banned user) {
            var col = db.GetCollection<Banned>("Banned");
            col.InsertOne(user);

        }

        public Banned GetUserIfBanned(int id) {
            var col = db.GetCollection<Banned>("Banned");
            var bannedUser = col.Find(b => b.BannedUserId == id).FirstOrDefault();
            return bannedUser;
        }

        public bool RemoveFromBanned(int id) {
            var col = db.GetCollection<Banned>("Banned");
            var bannedUser = col.Find(b => b.BannedUserId == id).FirstOrDefault();
            var deleteFilter = Builders<Banned>.Filter.Eq("BannedUserId", id);
            var you = col.DeleteOne(deleteFilter);

            return bannedUser != null;
        }

        public async Task<int> IncrementForgive(Banned user) {
            user.ForgiveCount++;
            var filter = Builders<Banned>.Filter.Eq("BannedUserId", user.BannedUserId);
            var col = db.GetCollection<Banned>("Banned");
            await col.ReplaceOneAsync(filter, user);
            return user.ForgiveCount;

        }

        public async void AddToForgiversList(int bannedUserId, int forgiverId) {
            var col = db.GetCollection<Banned>("Banned");
            var bannedUser = col.Find(b => b.BannedUserId == bannedUserId).FirstOrDefault();
            bannedUser.Forgivers.Add(forgiverId);

            var update = Builders<Banned>.Filter.Eq("BannedUserId", bannedUserId);
            await col.ReplaceOneAsync(update, bannedUser);
        }

        public async void addfield(int userID) {
            try {
                var too = this.GetAll();
                var col = db.GetCollection<UserDb>("UserDb");
                foreach (var dude in too) {
                    var filter = Builders<UserDb>.Filter.Eq("UserID", dude.UserID);
                    dude.MinutesPlayed = 0;
                    // user.GameTime.Add("GameTest", 4);
                    await col.ReplaceOneAsync(filter, dude);
                }
            }
            catch (Exception ex) {

            }

        }

    }
}
