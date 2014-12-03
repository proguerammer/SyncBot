using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyncBot.Core;

namespace SyncBotCLI
{
	class Program
	{
		static string user;
		static string password;
		static string workspace;
		static string server;
		static string path;

		static void Main(string[] args)
		{
			ProcessArguments(args);

			if(user == null || password == null || workspace == null || server == null || path == null)
			{
				Console.WriteLine("Incorrect usage: SyncBotCLI -u <user> -p <password> -w <workspace> -s <server> //path/to/sync");
				return;
			}

			var worker = new SyncWorker();

			worker.Gathered += OnGathered;
			worker.Syncing += OnSyncing;
			worker.Error += OnError;

			worker.InitializePerforce(user, password, workspace, server);

            try
            {
                Task task = worker.Sync(path);
                task.Wait();
            }
            catch (AggregateException ae)
            {
                Console.WriteLine(ae.InnerException.Message);
            }

			Console.WriteLine("Finished.");
		}

		static void ProcessArguments(string[] args)
		{
			for(int i = 0; i < args.Length; ++i)
			{
				if(args[i] == "-u")
				{
					i++;
					user = args[i];
				}
				else if(args[i] == "-p")
				{
					i++;
					password = args[i];
				}
				else if(args[i] == "-w")
				{
					i++;
					workspace = args[i];
				}
				else if(args[i] == "-s")
				{
					i++;
					server = args[i];
				}
				else if(args[i].StartsWith("//"))
				{
					path = args[i];
				}
			}
		}

		static void OnGathered(object sender, GatherEventArgs e)
		{
			Console.WriteLine("Syncing {0} files for a total of {1} bytes", e.TotalFileCount, e.TotalFileSize);
		}

		static void OnSyncing(object sender, SyncEventArgs e)
		{
			Console.WriteLine("{0:D2}: {1}", e.Id, e.Name);
		}

		static void OnError(object sender, ErrorEventArgs e)
		{
			Console.WriteLine("ERROR: {0}", e.Error);
		}
	}
}
