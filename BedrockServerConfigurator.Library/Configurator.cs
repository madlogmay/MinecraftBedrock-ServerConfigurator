﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text.RegularExpressions;

namespace BedrockServerConfigurator.Library
{
    public class Configurator
    {
        /// <summary>
        /// Folder where all servers reside
        /// </summary>
        public string ServersRootPath { get; }

        /// <summary>
        /// Name for each server
        /// </summary>
        public string ServerName { get; }

        /// <summary>
        /// Returns a path to the template server
        /// </summary>
        public string OriginalServerFolderPath => Path.Combine(ServersRootPath, ServerName);

        /// <summary>
        /// Holds all servers (except template one) that are in ServersRootPath directory
        /// string is the name of the server
        /// </summary>
        public Dictionary<int, Server> AllServers { get; } = new Dictionary<int, Server>();

        /// <summary>
        /// Gets all servers from AllSevers dictionary
        /// </summary>
        public List<Server> AllServersList => AllServers.Values.ToList();

        /// <summary>
        /// Saves the regex for getting a download url for minecraft bedrock server
        /// </summary>
        private Regex urlRegex;

        /// <summary>
        /// Logs all messages from Configurator
        /// </summary>
        public event EventHandler<string> Log;

        /// <summary>
        /// Returns true if folder with template server (downloaded server) has any files
        /// </summary>
        public bool TemplateServerExists => Directory.Exists(OriginalServerFolderPath);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serversRootPath">Path to a folder where all servers will reside. If it doesn't exist it will create it.</param>
        /// <param name="serverName">Name of individual servers.</param>
        public Configurator(string serversRootPath, string serverName)
        {
            AppDomain.CurrentDomain.ProcessExit += (a, b) => StopAllServers();

            ServersRootPath = serversRootPath;
            ServerName = serverName;

            Directory.CreateDirectory(serversRootPath);

            if (serverName.Contains("_"))
            {
                throw new Exception("Dont use _ in serverName");
            }
        }

        /// <summary>
        /// Downloads server that will be used as a template for creating new servers
        /// </summary>
        public void DownloadBedrockServer()
        {
            if (TemplateServerExists)
            {
                throw new Exception($"Template server already exists, delete folder \"{ServerName}\" in \"{ServersRootPath}\".");
            }

            Directory.CreateDirectory(OriginalServerFolderPath);

            string zipFilePath = Path.Combine(OriginalServerFolderPath, ServerName + ".zip");

            using var client = new WebClient();

            CallLog("Download started...");
            (string url, string version) = GetUrlAndVersion(client);
            client.DownloadFile(url, zipFilePath);

            CallLog("Unzipping...");
            ZipFile.ExtractToDirectory(zipFilePath, OriginalServerFolderPath);

            CallLog("Deleting zip file...");
            File.Delete(zipFilePath);

            File.WriteAllText(Path.Combine(OriginalServerFolderPath, "version.txt"), version);

            CallLog("Download finished");
        }

        /// <summary>
        /// Copies server template into a new folder which makes it a new server
        /// </summary>
        public void NewServer()
        {
            CallLog("Creating new server");

            var newServerPath = Path.Combine(ServersRootPath, ServerName + NewServerID());

            CallLog("Original = " + OriginalServerFolderPath);
            CallLog("New = " + newServerPath);

            var copyFolder = Utilities.RunACommand(
                             windows: $"xcopy /E /I \"{OriginalServerFolderPath}\" \"{newServerPath}\"",
                             ubuntu: $"cp -r \"{OriginalServerFolderPath}\" \"{newServerPath}\"");

            copyFolder.Start();

            _ = copyFolder.StandardOutput.ReadToEnd();

            CallLog("Folder copied");
        }

        /// <summary>
        /// Instantiates and adds all unloaded servers to AllServers
        /// </summary>
        public void LoadServers()
        {
            foreach (var name in AllServerDirectories().Except(AllServers.Select(x => x.Value.Name)))
            {
                var serverFolder = Path.Combine(ServersRootPath, name);

                var instance = Utilities.RunACommand(
                               windows: $"cd {serverFolder} && bedrock_server.exe",
                               ubuntu: $"cd {serverFolder} && chmod +x bedrock_server && ./bedrock_server");

                var properties = File.ReadAllText(Path.Combine(serverFolder, "server.properties"));

                var server = new Server(instance, name, serverFolder, new Properties(properties));
                server.Log += (a, b) => CallLog(b);

                AllServers.Add(server.ID, server);

                CallLog($"Loaded {name}");
            }

            FixServerPorts();
        }

        /// <summary>
        /// Runs a command on specified server
        /// </summary>
        /// <param name="serverID"></param>
        /// <param name="command"></param>
        public void RunCommandOnSpecifiedServer(int serverID, string command)
        {
            if (AllServers.TryGetValue(serverID, out Server server))
            {
                server.RunACommand(command);
            }
            else
            {
                CallLog($"Couldn't run command \"{command}\" because server with the ID \"{serverID}\" doesn't exist.");
            }
        }

        /// <summary>
        /// Starts all servers
        /// </summary>
        public void StartAllServers()
        {
            AllServersAction(x => x.StartServer());
        }

        /// <summary>
        /// Stops all servers
        /// </summary>
        public void StopAllServers()
        {
            AllServersAction(x => x.StopServer());
        }

        /// <summary>
        /// Restarts all servers
        /// </summary>
        public void RestartAllServers()
        {
            AllServersAction(x => x.RestartServer());
        }

        /// <summary>
        /// Invokes an action on all loaded servers
        /// </summary>
        /// <param name="action"></param>
        public void AllServersAction(Action<Server> action)
        {
            AllServersList.ForEach(action);
        }

        /// <summary>
        /// Gets name of all created servers (except template server)
        /// </summary>
        /// <returns></returns>
        public string[] AllServerDirectories()
        {
            return Directory
                   .GetDirectories(ServersRootPath)
                   .Select(x => x.Split(Path.DirectorySeparatorChar)[^1])
                   .Where(y => y.Contains("_"))
                   .ToArray();
        }

        /// <summary>
        /// Gets url to download minecraft server
        /// </summary>
        /// <returns></returns>
        private (string url, string version) GetUrlAndVersion(WebClient client)
        {
            if (urlRegex == null)
            {
                var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : "linux";
                var pattern = $"https://minecraft.azureedge.net/bin-{os}/bedrock-server-[0-9.]*.zip";

                urlRegex = new Regex(pattern);
            }

            string text = client.DownloadString("https://www.minecraft.net/en-us/download/server/bedrock/");

            var url = urlRegex.Match(text).Value;
            var version = url.Split("-").OrderByDescending(x => x.Count(y => y == '.')).First()[..^4];

            return (url, version);
        }

        /// <summary>
        /// Returns new highest ID from all created servers
        /// </summary>
        /// <returns></returns>
        private string NewServerID()
        {
            var nums = AllServerDirectories().Select(name => int.Parse(name.Split("_")[^1]));

            return nums.Any() ? $"_{nums.Max() + 1}" : "_1";
        }

        /// <summary>
        /// Updates ports to servers so each server has own port
        /// </summary>
        private void FixServerPorts()
        {
            // gets all servers that have the same ports
            var serversWithSamePorts = AllServers.Values
                .Where(x =>
                AllServers.Values.Any(
                    y => ((x.ServerProperties.ServerPort == y.ServerProperties.ServerPort ||
                         x.ServerProperties.ServerPortv6 == y.ServerProperties.ServerPortv6) &&
                         x.Name != y.Name)))
                .ToList();

            // removes first server (ID 1) from servers with same ports
            serversWithSamePorts.RemoveAll(x => x.ID == 1);

            // gets all servers except those who have same ports
            var alrightServers = AllServers.Values.Except(serversWithSamePorts).ToList();

            // adds to list with alright servers new server that have changed ports
            foreach (var server in serversWithSamePorts)
            {
                server.ServerProperties.ServerPort = alrightServers.Last().ServerProperties.ServerPort + 2;
                server.ServerProperties.ServerPortv6 = alrightServers.Last().ServerProperties.ServerPortv6 + 2;

                alrightServers.Add(server);

                server.UpdateProperties();
            }
        }

        private void CallLog(string message)
        {
            Log?.Invoke(null, message);
        }
    }
}
