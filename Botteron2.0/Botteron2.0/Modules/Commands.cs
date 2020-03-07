using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Botteron2._0.Modules.Helpers;
using System.IO;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Globalization;


namespace Botteron2._0.Modules {
    public class Commands : ModuleBase<SocketCommandContext> {
        public static bool nonoGo = true;


        [Command("Hello", RunMode = RunMode.Async)]
        public async Task Hello() {
            await ReplyAsync("Hello! I'm new Botterson");
        }


        [Command("nono", RunMode = RunMode.Async)]
        public async Task nono() {

            if (nonoGo) {
                nonoGo = false;
                Random rand = new Random();
                int badIndex = rand.Next(Program.badWords.Count);
                var onlineUsers = Context.Guild.Users.ToList().Where(u => u.Status.ToString().Equals("Online") && !(u.Roles.Count() == 2 && u.Roles.Any(p => p.Name.ToLower() != "bambinos")));

                try {
                    int onlineIndex = rand.Next(onlineUsers.Count());

                    var user = onlineUsers.ToArray()[onlineIndex];
                    var oldName = user.Nickname;
                    await user.ModifyAsync(m => m.Nickname = Program.badWords[badIndex]);

                    await ReplyAsync(user.Username + " is now called " + "\"" + Program.badWords[badIndex] + "\"");
                    Program.banList.Add(user.DiscriminatorValue);
                    await Task.Delay(600000);
                    Program.banList.Remove(user.DiscriminatorValue);
                    await user.ModifyAsync(m => m.Nickname = oldName);
                    nonoGo = true;
                }
                catch (Exception ex) {
                    nonoGo = true;
                }
            }
            else {
                await ReplyAsync("That command is on cooldown");
            }
        }

        [Command("swear", RunMode = RunMode.Async)]
        public async Task Swear() {

            try {
                var data = Program.db.GetAll();

                string formatted = "```";
                foreach (var person in data.Where(u => u.Warnings > 0).OrderByDescending(u => u.Warnings)) {
                    var user = Context.Guild.Users.FirstOrDefault(u => u.DiscriminatorValue == person.UserID);
                    if (user == null)
                        continue;
                    string nick = user.Nickname ?? user.Username;
                    formatted += nick + " has said " + person.Warnings + " swear word" + (person.Warnings == 1 ? ". \n\n" : "s. \n\n");

                }
                formatted += "```";
                await ReplyAsync(formatted);
            }
            catch (Exception ex) {
                Console.WriteLine();
            }
        }



        [Command("riddle", RunMode = RunMode.Async)]
        public async Task riddle() {
            await ReplyAsync(Program.CurrRiddle.Riddle);

        }

        [Command("timer", RunMode = RunMode.Async)]
        public async Task Timer(string time, string message = null) {
            //check time formatting
            if (time == null) {
                await ReplyAsync("esf");
                return;
            }
            time = time.ToLower().Replace(" ", "");
            if (time.EndsWith("h") || time.EndsWith("m") || time.EndsWith("s")) {
                string timeNum = time.Substring(0, time.Count() - 1);

                int num;
                try {
                    num = Convert.ToInt32(timeNum);
                }
                catch {
                    await ReplyAsync("That doesn't seem like a proper number there buddy");
                    return;
                }
                string unit = "";
                int multiplier = 0;
                if (time.EndsWith("h")) {
                    multiplier = 3600000;
                    unit = "hour";
                }
                else if (time.EndsWith("m")) {
                    multiplier = 60000;
                    unit = "minute";
                }
                else if (time.EndsWith("s")) {
                    multiplier = 1000;
                    unit = "second";
                }
                await ReplyAsync("Copy that I'll ping you in " + num + " " + (num == 1 ? unit : unit + "s"));
                await Task.Delay(num * multiplier);
                await ReplyAsync(Context.User.Mention + " pinging you as requested");

            }
            else {
                await ReplyAsync("Must end with either a 's', 'm' or 'h'");
            }

        }

        [Command("Yes", RunMode = RunMode.Async)]
        public async Task Yes() {
            var user = Program.db.LoadRecordsById(Context.User.DiscriminatorValue);

            if (!user.Greeting.HasValue || !user.Greeting.Value) {
                user.Greeting = true;
                Program.db.Update(user);
                await ReplyAsync("Thanks! You will now receive a greeting when you log in to Discord");
            }
            else {
                await ReplyAsync("You were already subscribed to the greeting service but thanks for your continued support");
            }
        }

        [Command("nah", RunMode = RunMode.Async)]
        public async Task Nah() {
            var user = Program.db.LoadRecordsById(Context.User.DiscriminatorValue);

            if (!user.Greeting.HasValue || user.Greeting.Value) {
                user.Greeting = false;
                Program.db.Update(user);
                await ReplyAsync("Oh okay, I see how it is... (Botterson will remember this)");
            }
            else {
                await ReplyAsync("You were already not subscribed to the greeting service");
                await Task.Delay(3000);
                await ReplyAsync("Prick");
            }
        }


        [Command("warning", RunMode = RunMode.Async)]
        public async Task Warning() {

            var user = Program.db.LoadRecordsById(Context.User.DiscriminatorValue);
            user.DoWarn = !user.DoWarn;

            if (user.DoWarn) {
                await ReplyAsync("You will now receive warning messages from me when you use bad words. Here's to bettering ourselves!");
            }
            else {
                await ReplyAsync("You will not receive anymore warnings when using bad language");
            }
            Program.db.Update(user);

        }

        [Command("say", RunMode = RunMode.Async)]
        public async Task say(string message) {
            var channel = Context.Client.GetChannel(150219519166513152);
            await (channel as IMessageChannel).SendMessageAsync(message);
        }

        [Command("clearNick", RunMode = RunMode.Async)]
        public async Task clearNick(string sid) {
            int id = Convert.ToInt32(sid);
            var User = Context.Client.Guilds.SelectMany(u => u.Users).FirstOrDefault(p => p.DiscriminatorValue == id);
            var dbUser = Program.db.GetUserIfBanned(User.DiscriminatorValue);
            Program.db.RemoveFromBanned(User.DiscriminatorValue);
            await Task.Delay(500);
            await User.ModifyAsync(u => u.Nickname = dbUser?.OldName);
        }

        [Command("forgive", RunMode = RunMode.Async)]
        public async Task forgive(SocketGuildUser user) {

            if (user.DiscriminatorValue != Context.User.DiscriminatorValue) {
                var bannedUser = Program.db.GetUserIfBanned(user.DiscriminatorValue);
                if (!bannedUser.Forgivers.Contains(Context.User.DiscriminatorValue)) {

                    if (bannedUser != null) {
                        int forgiveCount = await Program.db.IncrementForgive(bannedUser);
                        Program.db.AddToForgiversList(bannedUser.BannedUserId, Context.User.DiscriminatorValue);
                        if (forgiveCount > 1) {
                            Program.db.RemoveFromBanned(bannedUser.BannedUserId);
                            await user.ModifyAsync(u => u.Nickname = bannedUser.OldName);
                            await ReplyAsync("Your friends have shown you forgiveness. Your nickname is " + (bannedUser.OldName != "" ? bannedUser.OldName : " set to nothing" + " again."));
                        }
                        else {
                            await ReplyAsync("You have forgiven " + user.Username + ". They've now been forgiven " + forgiveCount + "/2 times");
                        }
                    }
                    else {
                        await ReplyAsync("That user is not banned");
                    }
                }
                else {
                    await ReplyAsync("You've already forgiven this person");
                }
            }
            else {
                await ReplyAsync("Yes sure you can forgive yourself, that's for sure allowed");
            }
        }

        [Command("secret", RunMode = RunMode.Async)]
        public async Task Secret() {
            var hasGreeting = Program.db.LoadRecordsById(Context.User.DiscriminatorValue).Greeting;
            if (Context.Message.Channel is SocketDMChannel && hasGreeting.HasValue && hasGreeting.Value) {
                Random rand = new Random();
                var Users = (Context.Client.GetChannel(150219519166513152) as SocketGuildChannel).Users.ToList().Where(u => u.DiscriminatorValue != Context.User.DiscriminatorValue);
                Users = Users.Where(u => u.Roles.Any(x => x.Name.ToLower() != "bambinos"));
                var randUser = Users.ToArray()[rand.Next(Users.Count())];
                int GoodOrBad = rand.Next(2);

                if (GoodOrBad == 1) {
                    string nameCall = Program.badWords[rand.Next(Program.badWords.Count)];

                    await randUser.SendMessageAsync("Hey so I hate to be a tattletale but I thought you should know that " + Context.User.Username + " said you were a " + nameCall);
                    await Context.User.SendMessageAsync("I just told " + randUser.Username + " that you thought they were a " + nameCall + ". Sorry about that.");
                }
                else {
                    string path = System.IO.Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName + "//great.txt";
                    var compliments = File.ReadAllLines(path).ToList();
                    string randCompliment = compliments[rand.Next(compliments.Count)];

                    await randUser.SendMessageAsync("I just though you should know that " + Context.User.Username + " said that " + randCompliment);
                    await Context.User.SendMessageAsync("I just told " + randUser.Username + " that you said \"" + randCompliment + "\" You're welcome");
                }
            }
        }
        [Command("coolname", RunMode = RunMode.Async)]
        public async Task CoolName() {
            string path = System.IO.Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName + "//blue.csv";
            List<string> colors = new List<string>();
            List<string> adjs = new List<string>();
            List<string> weapons = new List<string>();
            List<string> animals = new List<string>();
            List<string> things = new List<string>();
            using (var reader = new StreamReader(path)) {

                while (!reader.EndOfStream) {
                    var line = reader.ReadLine();
                    var values = line.Split(',');
                    colors.Add(values[0]);
                    adjs.Add(values[1]);
                    weapons.Add(values[2]);
                    animals.Add(values[3]);
                    things.Add(values[4]);
                }
            }
            string coolName = "";
            do {
                coolName = GetRandom(colors) + GetRandom(animals) + GetRandom(weapons) + GetRandom(adjs) + GetRandom(things);
            }
            while (coolName.Length > 32);
            await ReplyAsync("Your cool new name is " + coolName + "!");

            try {
                await (Context.User as SocketGuildUser).ModifyAsync(u => u.Nickname = coolName);
            }
            catch (Exception ex) {
                await ReplyAsync("Or it would have been if I had the rights to change your name");
            }

        }

        private string GetRandom(List<string> entry) {
            Random rand = new Random();
            entry = entry.Where(r => !string.IsNullOrEmpty(r)).ToList();
            int randIndex = rand.Next(entry.Count());
            string name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(entry[randIndex]);

            return name.Trim().Replace(" ", "");
        }
        [Command("gamer", RunMode = RunMode.Async)]
        public async Task Gamer() {
            string formatted = "```";
            var data = Program.db.GetAll();

            foreach (var person in data.Where(u => u.MinutesPlayed > 0).OrderByDescending(u => u.MinutesPlayed)) {

                var user = Context.Client.Guilds.SelectMany(u => u.Users).FirstOrDefault(p => p.DiscriminatorValue == person.UserID);
                if (user == null)
                    continue;

                string time = person.MinutesPlayed > 60 ? ((double)person.MinutesPlayed / 60).ToString("0.0")+" hours" : person.MinutesPlayed+" minutes"; 
                string nick =  user.Username;
                formatted += nick + " has played " + time + "\n";

                foreach (var game in person.GameTime.OrderByDescending(v => v.Value).ToDictionary(x => x.Key, x => x.Value).Keys) {

                    string gameTime = person.GameTime[game] > 60 ? ((double)person.GameTime[game] / 60).ToString("0.0") + " hours" : person.GameTime[game] + " minutes";
                    formatted += "           " + game + ": " + gameTime+"\n";
                }
                formatted += "\n";
            }
            formatted += "```";
            await ReplyAsync(formatted);
        }
        [Command("addfield", RunMode = RunMode.Async)]
        public async Task addField(int userId) {
            Program.db.addfield(userId);
        }

        [Command("get", RunMode = RunMode.Async)]
        public async Task get(int id) {
            var User = Context.Client.Guilds.SelectMany(u => u.Users).FirstOrDefault(p => p.DiscriminatorValue == id);
            var test= User.Activity as IActivity;
            var test1 = User.Activity as Game;
            Console.WriteLine();
        }


        [Command("speak", RunMode = RunMode.Async)]
        public async Task speak(string text) {
            //var channel = (Context.Client.GetChannel(150219519166513152) as SocketTextChannel);
            var channel = (Context.Client.GetChannel(618063069121216512) as SocketTextChannel);
            //SendTTSMessages, 
            var msg =await channel.SendMessageAsync(text, true);
            
            await msg.DeleteAsync();
            try {
                await Context.Message.DeleteAsync();
            }
            catch (Exception ex) {
                Console.WriteLine("vad");
            }


        }


    }
}
