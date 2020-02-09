using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Botteron2._0.Modules.Helpers;
using Discord.Commands;
using Discord.WebSocket;


namespace Botteron2._0.Modules {
    public class Commands : ModuleBase<SocketCommandContext> {

     
    
        [Command("Hello")]
        public async Task Hello() {
            await ReplyAsync("Hello! I'm new Botterson");
        }


        [Command("nono")]
        public async Task nono() {
            Random rand = new Random();
            int badIndex = rand.Next(Program.badWords.Count);
            var onlineUsers = Context.Guild.Users.ToList().Where(u=> u.Status.ToString().Equals("Online"));
            int onlineIndex = rand.Next(onlineUsers.Count());

            var user = onlineUsers.ToArray()[onlineIndex];
            var oldName =  !String.IsNullOrEmpty(user.Nickname)? user.Nickname : user.Username;
            await user.ModifyAsync(m=> m.Nickname=Program.badWords[badIndex]);

            await ReplyAsync(user.Username + " is now called " + Program.badWords[badIndex]);
            Program.banList.Add(user.DiscriminatorValue);
            await Task.Delay(15000);
            await user.ModifyAsync(m => m.Nickname = oldName);
            await ReplyAsync("It was changed back");
            Program.banList.Remove(user.DiscriminatorValue);
        }

        [Command("Swear")]
        public async Task Swear() {
            var data = Program.db.GetAll();

            string formatted = "```";
            foreach (var person in data.Where(u => u.Warnings > 0).OrderBy(u=>u.Warnings)) {
                var user = Context.Guild.Users.FirstOrDefault(u => u.DiscriminatorValue == person.UserID);
                string nick = user.Nickname ?? user.Username;
                formatted += nick + " has said " + person.Warnings + " swear word" + (person.Warnings==1? ". \n": "s. \n"); 
            }
            formatted += "```";
            await ReplyAsync(formatted);
        }



        [Command("riddle")]
        public async Task riddle() {
            await ReplyAsync(Program.CurrRiddle.Riddle);

        }

        [Command("timer")]
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

        [Command("Yes")]
        public async Task Yes() {
            var user = Program.db.LoadRecordsById<UserDb>(Context.User.DiscriminatorValue);

            if (!user.Greeting.HasValue || !user.Greeting.Value) {
                user.Greeting = true;
                Program.db.Update(user);
                await ReplyAsync("Thanks! You will now receive a greeting when you log in to Discord");
            }
            else {
                await ReplyAsync("You were already subscribed to the greeting service but thanks for your continued support");
            }
        }

        [Command("nah")]
        public async Task Nah() {
            var user = Program.db.LoadRecordsById<UserDb>(Context.User.DiscriminatorValue);

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


        [Command("warning")]
        public async Task Warning() {

            var user = Program.db.LoadRecordsById<UserDb>(Context.User.DiscriminatorValue);
            user.DoWarn = !user.DoWarn;

            if (user.DoWarn) {
                await ReplyAsync("You will now receive warning messages from me when you use bad words. Here's to bettering ourselves!");
            }
            else {
                await ReplyAsync("You will not receive anymore warnings when using bad language");
            }
            Program.db.Update(user);
        
        }

    }
}
