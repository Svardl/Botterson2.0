﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Botteron2._0.Modules.Helpers {
    public class UserDb {

        [BsonId]
        public ObjectId _id { get; set; }
        public int UserID { get; set; }
        public string Username { get; set; }
        public int Warnings { get; set; }
        public int RiddlePoints { get; set; }
        public bool? Greeting { get; set; }
        public DateTime? LastOnline { get; set; }
        public bool DoWarn { get; set; }
        public int MinutesPlayed { get; set; }
        public DateTime LastPlayed { get; set; }
        public Dictionary<string, int> GameTime {get; set;}
    }

    public class Riddles {
        public ObjectId _id { get; set; }
        public int RiddleNum { get; set; }
        public string Riddle { get; set; }
        public string Answer { get; set; }

    }

    public class Banned {
        public ObjectId _id { get; set; }
        public int BannedUserId { get; set; }
        public DateTime BanDate { get; set; }
        public int ForgiveCount { get; set; }
        public string OldName { get; set; }
        public string NewName { get; set; }
        public List<int> Forgivers { get; set; }

    }


}
