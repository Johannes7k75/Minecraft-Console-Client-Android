﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using ConsoleIO = Android.MainActivity;

namespace MinecraftClient
{
    ///
    /// Welcome to the Bot API file !
    /// The virtual class "ChatBot" contains anything you need for creating chat bots
    /// Inherit from this class while adding your bot class to the folder "ChatBots".
    /// Override the methods you want for handling events: Initialize, Update, GetText.
    /// Once your bot is created, read the explanations below to start using it in the MinecraftClient app.
    ///
    /// Pieces of code to add in other parts of the program for your bot. Line numbers are approximative.
    /// Settings.cs:73  | public static bool YourBot_Enabled = false;
    /// Settings.cs:74  | private enum ParseMode { /* [...] */, YourBot };
    /// Settings.cs:106 | case "yourbot": pMode = ParseMode.YourBot; break;
    /// Settings.cs:197 | case ParseMode.YourBot: switch (argName.ToLower()) { case "enabled": YourBot_Enabled = str2bool(argValue); break; } break;
    /// Settings.cs:267 | + "[YourBot]\r\n" + "enabled=false\r\n"
    /// McTcpClient:110 | if (Settings.YourBot_Enabled) { handler.BotLoad(new ChatBots.YourBot()); }
    /// Here your are. Now you will have a setting in MinecraftClient.ini for enabling your brand new bot.
    /// Delete MinecraftClient.ini to re-generate it or add the lines [YourBot] and enabled=true to the existing one.
    ///

    /// <summary>
    /// The virtual class containing anything you need for creating chat bots.
    /// </summary>

    public abstract class ChatBot
    {
        public enum DisconnectReason { InGameKick, LoginRejected, ConnectionLost };

        //Handler will be automatically set on bot loading, don't worry about this
        public void SetHandler(McTcpClient handler) { this._handler = handler; }
        protected void SetMaster(ChatBot master) { this.master = master; }
        protected void LoadBot(ChatBot bot) { Handler.BotUnLoad(bot); Handler.BotLoad(bot); }
        private McTcpClient Handler { get { return master != null ? master.Handler : _handler; } }
        private McTcpClient _handler = null;
        private ChatBot master = null;
        private List<string> registeredPluginChannels = new List<String>();
        private Queue<string> chatQueue = new Queue<string>();
        private DateTime lastMessageSentTime = DateTime.MinValue;
        private bool CanSendTextNow
        {
            get
            {
                return DateTime.Now > lastMessageSentTime + Settings.botMessageDelay;
            }
        }

        /// <summary>
        /// Processes the current chat message queue, displaying a message after enough time passes.
        /// </summary>
        internal void ProcessQueuedText()
        {
            if (chatQueue.Count > 0)
            {
                if (CanSendTextNow)
                {
                    string text = chatQueue.Dequeue();
                    LogToConsole("Sending '" + text + "'");
                    lastMessageSentTime = DateTime.Now;
                    Handler.SendText(text);
                }
            }
        }

        /* ================================================== */
        /*   Main methods to override for creating your bot   */
        /* ================================================== */

        /// <summary>
        /// Anything you want to initialize your bot, will be called on load by MinecraftCom
        ///
        /// NOTE: Chat messages cannot be sent at this point in the login process.  If you want to send
        /// a message when the bot is loaded, use AfterGameJoined.
        /// </summary>

        public virtual void Initialize() { }

        /// <summary>
        /// Called after the server has been joined successfully and chat messages are able to be sent.
        ///
        /// NOTE: This is not always right after joining the server - if the bot was loaded after logging
        /// in this is still called.
        /// </summary>

        public virtual void AfterGameJoined() { }

        /// <summary>
        /// Will be called every ~100ms (10fps) if loaded in MinecraftCom
        /// </summary>

        public virtual void Update() { }

        /// <summary>
        /// Any text sent by the server will be sent here by MinecraftCom
        /// </summary>
        /// <param name="text">Text from the server</param>

        public virtual void GetText(string text) { }

        /// <summary>
        /// Is called when the client has been disconnected fom the server
        /// </summary>
        /// <param name="reason">Disconnect Reason</param>
        /// <param name="message">Kick message, if any</param>
        /// <returns>Return TRUE if the client is about to restart</returns>

        public virtual bool OnDisconnect(DisconnectReason reason, string message) { return false; }

        /// <summary>
        /// Called when a plugin channel message is received.
        /// The given channel must have previously been registered with RegisterPluginChannel.
        /// This can be used to communicate with server mods or plugins.  See wiki.vg for more
        /// information about plugin channels: http://wiki.vg/Plugin_channel
        /// </summary>
        /// <param name="channel">The name of the channel</param>
        /// <param name="data">The payload for the message</param>

        public virtual void OnPluginMessage(string channel, byte[] data) { }

        /* =================================================================== */
        /*  ToolBox - Methods below might be useful while creating your bot.   */
        /*  You should not need to interact with other classes of the program. */
        /*  All the methods in this ChatBot class should do the job for you.   */
        /* =================================================================== */

        /// <summary>
        /// Send text to the server. Can be anything such as chat messages or commands
        /// </summary>
        /// <param name="text">Text to send to the server</param>
        /// <param name="sendImmediately">Whether the message should be sent immediately rather than being queued to avoid chat spam</param>
        /// <returns>True if the text was sent with no error</returns>

        protected bool SendText(string text, bool sendImmediately = false)
        {
            if (Settings.botMessageDelay.TotalSeconds > 0 && !sendImmediately)
            {
                if (!CanSendTextNow)
                {
                    chatQueue.Enqueue(text);
                    // TODO: We don't know whether there was an error at this point, so we assume there isn't.
                    // Might not be the best idea.
                    return true;
                }
            }

            LogToConsole("Sending '" + text + "'");
            lastMessageSentTime = DateTime.Now;
            return Handler.SendText(text);
        }

        /// <summary>
        /// Perform an internal MCC command (not a server command, use SendText() instead for that!)
        /// </summary>
        /// <param name="command">The command to process</param>
        /// <returns>TRUE if the command was indeed an internal MCC command</returns>

        protected bool PerformInternalCommand(string command)
        {
            string temp = "";
            return Handler.PerformInternalCommand(command, ref temp);
        }

        /// <summary>
        /// Perform an internal MCC command (not a server command, use SendText() instead for that!)
        /// </summary>
        /// <param name="command">The command to process</param>
        /// <param name="response_msg">May contain a confirmation or error message after processing the command, or "" otherwise.</param>
        /// <returns>TRUE if the command was indeed an internal MCC command</returns>

        protected bool PerformInternalCommand(string command, ref string response_msg)
        {
            return Handler.PerformInternalCommand(command, ref response_msg);
        }

        /// <summary>
        /// Remove color codes ("§c") from a text message received from the server
        /// </summary>

        protected static string GetVerbatim(string text)
        {
            if ( String.IsNullOrEmpty(text) )
                return String.Empty;

            int idx = 0;
            var data = new char[text.Length];

            for ( int i = 0; i < text.Length; i++ )
                if ( text[i] != '§' )
                    data[idx++] = text[i];
                else
                    i++;

            return new string(data, 0, idx);
        }

        /// <summary>
        /// Verify that a string contains only a-z A-Z 0-9 and _ characters.
        /// </summary>

        protected static bool IsValidName(string username)
        {
            if (String.IsNullOrEmpty(username))
                return false;

            foreach (char c in username)
                if (!((c >= 'a' && c <= 'z')
                        || (c >= 'A' && c <= 'Z')
                        || (c >= '0' && c <= '9')
                        || c == '_') )
                    return false;

            return true;
        }

        /// <summary>
        /// Returns true if the text passed is a private message sent to the bot
        /// </summary>
        /// <param name="text">text to test</param>
        /// <param name="message">if it's a private message, this will contain the message</param>
        /// <param name="sender">if it's a private message, this will contain the player name that sends the message</param>
        /// <returns>Returns true if the text is a private message</returns>

        protected static bool IsPrivateMessage(string text, ref string message, ref string sender)
        {
            if (String.IsNullOrEmpty(text))
                return false;

            text = GetVerbatim(text);

            //Built-in detection routine for private messages
            if (Settings.ChatFormat_Builtins)
            {
                string[] tmp = text.Split(' ');
                try
                {
                    //Detect vanilla /tell messages
                    //Someone whispers message (MC 1.5)
                    //Someone whispers to you: message (MC 1.7)
                    if (tmp.Length > 2 && tmp[1] == "whispers")
                    {
                        if (tmp.Length > 4 && tmp[2] == "to" && tmp[3] == "you:")
                        {
                            message = text.Substring(tmp[0].Length + 18); //MC 1.7
                        }
                        else message = text.Substring(tmp[0].Length + 10); //MC 1.5
                        sender = tmp[0];
                        return IsValidName(sender);
                    }

                    //Detect Essentials (Bukkit) /m messages
                    //[Someone -> me] message
                    //[~Someone -> me] message
                    else if (text[0] == '[' && tmp.Length > 3 && tmp[1] == "->"
                            && (tmp[2].ToLower() == "me]" || tmp[2].ToLower() == "moi]")) //'me' is replaced by 'moi' in french servers
                    {
                        message = text.Substring(tmp[0].Length + 4 + tmp[2].Length + 1);
                        sender = tmp[0].Substring(1);
                        if (sender[0] == '~') { sender = sender.Substring(1); }
                        return IsValidName(sender);
                    }

                    //Detect Modified server messages. /m
                    //[Someone @ me] message
                    else if (text[0] == '[' && tmp.Length > 3 && tmp[1] == "@"
                            && (tmp[2].ToLower() == "me]" || tmp[2].ToLower() == "moi]")) //'me' is replaced by 'moi' in french servers
                    {
                        message = text.Substring(tmp[0].Length + 4 + tmp[2].Length + 0);
                        sender = tmp[0].Substring(1);
                        if (sender[0] == '~') { sender = sender.Substring(1); }
                        return IsValidName(sender);
                    }

                    //Detect Essentials (Bukkit) /me messages with some custom prefix
                    //[Prefix] [Someone -> me] message
                    //[Prefix] [~Someone -> me] message
                    else if (text[0] == '[' && tmp[0][tmp[0].Length - 1] == ']'
                            && tmp[1][0] == '[' && tmp.Length > 4 && tmp[2] == "->"
                            && (tmp[3].ToLower() == "me]" || tmp[3].ToLower() == "moi]"))
                    {
                        message = text.Substring(tmp[0].Length + 1 + tmp[1].Length + 4 + tmp[3].Length + 1);
                        sender = tmp[1].Substring(1);
                        if (sender[0] == '~') { sender = sender.Substring(1); }
                        return IsValidName(sender);
                    }

                    //Detect Essentials (Bukkit) /me messages with some custom rank
                    //[Someone [rank] -> me] message
                    //[~Someone [rank] -> me] message
                    else if (text[0] == '[' && tmp.Length > 3 && tmp[2] == "->"
                            && (tmp[3].ToLower() == "me]" || tmp[3].ToLower() == "moi]"))
                    {
                        message = text.Substring(tmp[0].Length + 1 + tmp[1].Length + 4 + tmp[2].Length + 1);
                        sender = tmp[0].Substring(1);
                        if (sender[0] == '~') { sender = sender.Substring(1); }
                        return IsValidName(sender);
                    }

                    //Detect HeroChat PMsend
                    //From Someone: message
                    else if (text.StartsWith("From "))
                    {
                        sender = text.Substring(5).Split(':')[0];
                        message = text.Substring(text.IndexOf(':') + 2);
                        return IsValidName(sender);
                    }
                    else return false;
                }
                catch (IndexOutOfRangeException) { /* Not an expected chat format */ }
                catch (ArgumentOutOfRangeException) { /* Same here */ }
            }

            //User-defined regex for private chat messages
            if (Settings.ChatFormat_Private != null)
            {
                Match regexMatch = Settings.ChatFormat_Private.Match(text);
                if (regexMatch.Success && regexMatch.Groups.Count >= 3)
                {
                    sender = regexMatch.Groups[1].Value;
                    message = regexMatch.Groups[2].Value;
                    return IsValidName(sender);
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if the text passed is a public message written by a player on the chat
        /// </summary>
        /// <param name="text">text to test</param>
        /// <param name="message">if it's message, this will contain the message</param>
        /// <param name="sender">if it's message, this will contain the player name that sends the message</param>
        /// <returns>Returns true if the text is a chat message</returns>

        protected static bool IsChatMessage(string text, ref string message, ref string sender)
        {
            if (String.IsNullOrEmpty(text))
                return false;

            text = GetVerbatim(text);
            
            //Built-in detection routine for public messages
            if (Settings.ChatFormat_Builtins)
            {
                string[] tmp = text.Split(' ');

                //Detect vanilla/factions Messages
                //<Someone> message
                //<*Faction Someone> message
                //<*Faction Someone>: message
                //<*Faction ~Nicknamed>: message
                if (text[0] == '<')
                {
                    try
                    {
                        text = text.Substring(1);
                        string[] tmp2 = text.Split('>');
                        sender = tmp2[0];
                        message = text.Substring(sender.Length + 2);
                        if (message.Length > 1 && message[0] == ' ')
                        { message = message.Substring(1); }
                        tmp2 = sender.Split(' ');
                        sender = tmp2[tmp2.Length - 1];
                        if (sender[0] == '~') { sender = sender.Substring(1); }
                        return IsValidName(sender);
                    }
                    catch (IndexOutOfRangeException) { /* Not a vanilla/faction message */ }
                    catch (ArgumentOutOfRangeException) { /* Same here */ }
                }

                //Detect HeroChat Messages
                //Public chat messages
                //[Channel] [Rank] User: Message
                else if (text[0] == '[' && text.Contains(':') && tmp.Length > 2)
                {
                    try
                    {
                        int name_end = text.IndexOf(':');
                        int name_start = text.Substring(0, name_end).LastIndexOf(']') + 2;
                        sender = text.Substring(name_start, name_end - name_start);
                        message = text.Substring(name_end + 2);
                        return IsValidName(sender);
                    }
                    catch (IndexOutOfRangeException) { /* Not a herochat message */ }
                    catch (ArgumentOutOfRangeException) { /* Same here */ }
                }

                //Detect (Unknown Plugin) Messages
                //**Faction<Rank> User : Message
                else if (text[0] == '*'
                    && text.Length > 1
                    && text[1] != ' '
                    && text.Contains('<') && text.Contains('>')
                    && text.Contains(' ') && text.Contains(':')
                    && text.IndexOf('*') < text.IndexOf('<')
                    && text.IndexOf('<') < text.IndexOf('>')
                    && text.IndexOf('>') < text.IndexOf(' ')
                    && text.IndexOf(' ') < text.IndexOf(':'))
                {
                    try
                    {
                        string prefix = tmp[0];
                        string user = tmp[1];
                        string semicolon = tmp[2];
                        if (prefix.All(c => char.IsLetterOrDigit(c) || new char[] { '*', '<', '>', '_' }.Contains(c))
                            && semicolon == ":")
                        {
                            message = text.Substring(prefix.Length + user.Length + 4);
                            return IsValidName(user);
                        }
                    }
                    catch (IndexOutOfRangeException) { /* Not a <unknown plugin> message */ }
                    catch (ArgumentOutOfRangeException) { /* Same here */ }
                }
            }

            //User-defined regex for public chat messages
            if (Settings.ChatFormat_Public != null)
            {
                Match regexMatch = Settings.ChatFormat_Public.Match(text);
                if (regexMatch.Success && regexMatch.Groups.Count >= 3)
                {
                    sender = regexMatch.Groups[1].Value;
                    message = regexMatch.Groups[2].Value;
                    return IsValidName(sender);
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if the text passed is a teleport request (Essentials)
        /// </summary>
        /// <param name="text">Text to parse</param>
        /// <param name="sender">Will contain the sender's username, if it's a teleport request</param>
        /// <returns>Returns true if the text is a teleport request</returns>

        protected static bool IsTeleportRequest(string text, ref string sender)
        {
            if (String.IsNullOrEmpty(text))
                return false;

            text = GetVerbatim(text);

            //Built-in detection routine for teleport requests
            if (Settings.ChatFormat_Builtins)
            {
                string[] tmp = text.Split(' ');

                //Detect Essentials teleport requests, prossibly with
                //nicknamed names or other modifications such as HeroChat
                if (text.EndsWith("has requested to teleport to you.")
                    || text.EndsWith("has requested that you teleport to them."))
                {
                    //<Rank> Username has requested...
                    //[Rank] Username has requested...
                    if (((tmp[0].StartsWith("<") && tmp[0].EndsWith(">"))
                        || (tmp[0].StartsWith("[") && tmp[0].EndsWith("]")))
                        && tmp.Length > 1)
                        sender = tmp[1];

                    //Username has requested...
                    else sender = tmp[0];

                    //~Username has requested...
                    if (sender.Length > 1 && sender[0] == '~')
                        sender = sender.Substring(1);

                    //Final check on username validity
                    return IsValidName(sender);
                }
            }

            //User-defined regex for teleport requests
            if (Settings.ChatFormat_TeleportRequest != null)
            {
                Match regexMatch = Settings.ChatFormat_TeleportRequest.Match(text);
                if (regexMatch.Success && regexMatch.Groups.Count >= 2)
                {
                    sender = regexMatch.Groups[1].Value;
                    return IsValidName(sender);
                }
            }

            return false;
        }

        /// <summary>
        /// Write some text in the console. Nothing will be sent to the server.
        /// </summary>
        /// <param name="text">Log text to write</param>

        protected void LogToConsole(object text)
        {
            ConsoleIO.WriteLogLine(String.Format("[{0}] {1}", this.GetType().Name, text));
            string logfile = Settings.ExpandVars(Settings.chatbotLogFile);

            if (!String.IsNullOrEmpty(logfile))
            {
                if (!File.Exists(logfile))
                {
                    try { Directory.CreateDirectory(Path.GetDirectoryName(logfile)); }
                    catch { return; /* Invalid path or access denied */ }
                    try { File.WriteAllText(logfile, ""); }
                    catch { return; /* Invalid file name or access denied */ }
                }

                File.AppendAllLines(logfile, new string[] { GetTimestamp() + ' ' + text });
            }
        }

        /// <summary>
        /// Disconnect from the server and restart the program
        /// It will unload and reload all the bots and then reconnect to the server
        /// </summary>
        /// <param name="attempts">If connection fails, the client will make X extra attempts</param>

        protected void ReconnectToTheServer(int ExtraAttempts = 3)
        {
            McTcpClient.ReconnectionAttemptsLeft = ExtraAttempts;
            //Program.Restart();
        }

        /// <summary>
        /// Disconnect from the server and exit the program
        /// </summary>

        protected void DisconnectAndExit()
        {
            //Program.Exit();
        }

        /// <summary>
        /// Unload the chatbot, and release associated memory.
        /// </summary>

        protected void UnloadBot()
        {
            Handler.BotUnLoad(this);
        }

        /// <summary>
        /// Send a private message to a player
        /// </summary>
        /// <param name="player">Player name</param>
        /// <param name="message">Message</param>

        protected void SendPrivateMessage(string player, string message)
        {
            SendText(String.Format("/{0} {1} {2}", Settings.PrivateMsgsCmdName, player, message));
        }

        /// <summary>
        /// Run a script from a file using a Scripting bot
        /// </summary>
        /// <param name="filename">File name</param>
        /// <param name="playername">Player name to send error messages, if applicable</param>

        protected void RunScript(string filename, string playername = "")
        {
            Handler.BotLoad(new ChatBots.Script(filename, playername));
        }

        /// <summary>
        /// Get the current Minecraft World
        /// </summary>
        /// <returns>Minecraft world or null if associated setting is disabled</returns>

        protected Mapping.World GetWorld()
        {
            if (Settings.TerrainAndMovements)
                return Handler.GetWorld();
            return null;
        }

        /// <summary>
        /// Get a Y-M-D h:m:s timestamp representing the current system date and time
        /// </summary>

        protected static string GetTimestamp()
        {
            DateTime time = DateTime.Now;
            return String.Format("{0}-{1}-{2} {3}:{4}:{5}",
                time.Year.ToString("0000"),
                time.Month.ToString("00"),
                time.Day.ToString("00"),
                time.Hour.ToString("00"),
                time.Minute.ToString("00"),
                time.Second.ToString("00"));
        }

        /// <summary>
        /// Load entries from a file as a string array, removing duplicates and empty lines
        /// </summary>
        /// <param name="file">File to load</param>
        /// <returns>The string array or an empty array if failed to load the file</returns>
        
        protected string[] LoadDistinctEntriesFromFile(string file)
        {
            if (File.Exists(file))
            {
                //Read all lines from file, remove lines with no text, convert to lowercase,
                //remove duplicate entries, convert to a string array, and return the result.
                return File.ReadAllLines(file)
                        .Where(line => !String.IsNullOrWhiteSpace(line))
                        .Select(line => line.ToLower())
                        .Distinct().ToArray();
            }
            else
            {
                LogToConsole("File not found: " + Settings.Alerts_MatchesFile);
                return new string[0];
            }
        }

        /// <summary>
        /// Registers the given plugin channel for use by this chatbot.
        /// </summary>
        /// <param name="channel">The name of the channel to register</param>

        protected void RegisterPluginChannel(string channel)
        {
            this.registeredPluginChannels.Add(channel);
            Handler.RegisterPluginChannel(channel, this);
        }

        /// <summary>
        /// Unregisters the given plugin channel, meaning this chatbot can no longer use it.
        /// </summary>
        /// <param name="channel">The name of the channel to unregister</param>

        protected void UnregisterPluginChannel(string channel)
        {
            this.registeredPluginChannels.RemoveAll(chan => chan == channel);
            Handler.UnregisterPluginChannel(channel, this);
        }

        /// <summary>
        /// Sends the given plugin channel message to the server, if the channel has been registered.
        /// See http://wiki.vg/Plugin_channel for more information about plugin channels.
        /// </summary>
        /// <param name="channel">The channel to send the message on.</param>
        /// <param name="data">The data to send.</param>
        /// <param name="sendEvenIfNotRegistered">Should the message be sent even if it hasn't been registered by the server or this bot?  (Some Minecraft channels aren't registered)</param>
        /// <returns>Whether the message was successfully sent.  False if there was a network error or if the channel wasn't registered.</returns>

        protected bool SendPluginChannelMessage(string channel, byte[] data, bool sendEvenIfNotRegistered = false)
        {
            if (!sendEvenIfNotRegistered)
            {
                if (!this.registeredPluginChannels.Contains(channel))
                {
                    return false;
                }
            }
            return Handler.SendPluginChannelMessage(channel, data, sendEvenIfNotRegistered);
        }
    }
}
