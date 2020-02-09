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

namespace Botteron2._0 {
    class Program {

        public DiscordSocketClient _client;
        public CommandService _commands;
        public IServiceProvider _services;
        public static List<string> badWords;
        public bool dontChange = false;
        public static List<int> banList = new List<int>();
        public static Riddles CurrRiddle;
        public bool isProd = true;

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
              await Task.Delay(2);
            }

            var DbUsers = db.GetAll().Select(u=> u.UserID).ToList();
            var OnlineUsers = _client.Guilds.Select(u => u.Users.Where(p=>!p.IsBot)).SelectMany(t=>t);

            foreach(var online in OnlineUsers) {

                if(!DbUsers.Any(u=> u == online.DiscriminatorValue)) {
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
            if (!allUsers.Any(u=> u.UserID == user.DiscriminatorValue)) 
            {
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
            if (!dontChange) {
                if (banList.Any(id => id == before.DiscriminatorValue) && before.Nickname != after.Nickname) {
                    await after.ModifyAsync(u => u.Nickname = before.Nickname);
                    dontChange = true;
                    await after.SendMessageAsync("https://media3.giphy.com/media/9NLYiOUxnKAJLIycEv/giphy.gif?cid=790b761147c7a95c91265422f8e860a25f433fa2b5bba283&rid=giphy.gif");
                }
            }
            else {
                dontChange = false;
            }

            if (before.Status.ToString() != "Online" && after.Status.ToString() == "Online") {
                await HandleGreeting(before, after);
            }
        }

        private async Task HandleGreeting(SocketGuildUser before, SocketGuildUser after) {
            var user = db.LoadRecordsById<UserDb>(after.DiscriminatorValue);

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
            if (!isProd) {
                var id = (message.Channel as SocketGuildChannel).Guild.Id;
                if (id == 150219519166513152) {
                    return;
                }
            }

            var context = new SocketCommandContext(_client, message);
            if (message.Author.IsBot) return;

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
        }

        private void RiddleHandler(SocketUserMessage message) {
            if (message.Content.Contains(CurrRiddle.Answer)) {
               var person= db.LoadRecordsById<UserDb>(message.Author.DiscriminatorValue);
                person.RiddlePoints++;
                message.Channel.SendMessageAsync(message.Author.Mention + " That is correct! You now have "+ person.RiddlePoints+" points." );
                db.SetRandomRiddle();
                db.Update(person);
            }
        }

        private async Task HandleGeneralMessages(SocketUserMessage message) {
          await BadWords(message);
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
                        combined += " and " +"\""+resList[i]+"\"";
                    }
                    else {
                        combined += ""+"\""+resList[i]+"\",";
                    }
                }
                int numWar = IncrementWarning(message.Author.DiscriminatorValue);

                if(numWar % 5 == 0) {
                    banList.Add(message.Author.DiscriminatorValue);
                    Random rand = new Random();
                    int badIndex = rand.Next(badWords.Count);

                    try {
                        await ChangeName((SocketGuildUser)message.Author, badWords[badIndex], 10);
                        await message.Channel.SendMessageAsync("Aright " + message.Author.Username + ". I've had enough of your little outbursts. Enjoy your new nickname. It might be changed back in an hour, who knows");
                        await message.Channel.SendMessageAsync(message.Author.ToString() + " your punishment is over");
                    }
                    catch (Exception ex) {
                        await message.Channel.SendMessageAsync("Felix, you absolute fuck");
                    }
                   
                }
                else if(db.LoadRecordsById<UserDb>(message.Author.DiscriminatorValue).DoWarn)
                    await message.Channel.SendMessageAsync(message.Author.ToString() + " said " + combined + ". You think that makes you seem cool? You now have "+ numWar+ " warnings");
            } 
        }
        private async Task ChangeName(SocketGuildUser user, string name, int time=-1) {

            string oldNick = user.Nickname;
            await user.ModifyAsync(u => u.Nickname = name);

            if (time != -1) {

                await Task.Delay(time * 1000);
                await user.ModifyAsync(u => u.Nickname = oldNick);
            }   
        }

        private int IncrementWarning(int code) {
            var person = db.LoadRecordsById<UserDb>(code);
            person.Warnings++;
            db.Update(person);
            return person.Warnings;
        }

        private bool WholeMatch(string bad, string content) {
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
        public T LoadRecordsById<T>(int id) {
            try {
                var filter = Builders<T>.Filter.Eq("UserID", id);
                var col = db.GetCollection<T>("UserDb");
                return col.Find(filter).First();
            }
            catch (Exception ex) {
                return default(T);
            }
        }

        public List<UserDb> GetAll() {
            var col = db.GetCollection<UserDb>("UserDb");
            return col.AsQueryable().ToList();
        }

        public Riddles GetNextRiddle() {
            var col = db.GetCollection<Riddles>("Riddles");
            int max = col.AsQueryable().ToList().Count;

            int num = Program.CurrRiddle.RiddleNum + 1 == max ? 0: Program.CurrRiddle.RiddleNum + 1;
            return col.Find(r => r.RiddleNum == num).First();

        }
        public void SetRandomRiddle() {
            Random rand = new Random();
            var col = db.GetCollection<Riddles>("Riddles");
            int num= rand.Next(col.AsQueryable().ToList().Count);
            Program.CurrRiddle = col.Find(r => r.RiddleNum == num).First();
        }

        internal void AddNewUser(UserDb newUser) {
            var col = db.GetCollection<UserDb>("UserDb");
            col.InsertOne(newUser);
        }
    }
}
