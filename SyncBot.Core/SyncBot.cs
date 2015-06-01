using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using P4API;

namespace SyncBot.Core
{
    public class SyncWorker
    {
		public SyncWorker()
		{
			numThreads = Environment.ProcessorCount;
		}

		public void InitializePerforce(string user, string password, string workspace, string server)
		{
			this.user = user;
			this.password = password;
			this.workspace = workspace;
			this.server = server;
		}

		public void SetNumThreads(int numThreads)
		{
			this.numThreads = numThreads;
		}

		public Task Sync(string path)
		{
			return Task.Factory.StartNew(() => 
				{
					GatherFiles(path);
					SyncFiles();
				});
		}

		private void GatherFiles(string path)
		{
			long totalFileSize = 0;
            // IGS_RG: Initialize filesToSync to prevent null exception when failing PerforceConnection or returning 0 records (nothing to sync)
            filesToSync = new ConcurrentQueue<DepotFile>();

			using(var p4 = CreatePerforceConnection())
			{
                try
                {
                    var results = p4.Run("sync", GetSyncArgs(path, true, false));
				    if(results.Records.Length > 0)
				    {
					    totalFileSize = long.Parse(results.Records[0].Fields["totalFileSize"]);
					    long change = long.Parse(results.Records[0].Fields["change"]);

					    filesToSync = new ConcurrentQueue<DepotFile>(results.Records.Select(r => new DepotFile() { Name = r.Fields["depotFile"], Size = long.Parse(r.Fields["fileSize"]), Change = change }).ToList());
					
					    // Look for any files that need to be resolved, as they will not show up in the records, but we still need to sync them
					    foreach(var message in results.Messages)
					    {
						    if(message.Contains("must resolve"))
						    {
							    // Extract the depot file name
							    string fileName = message.Remove(message.IndexOf(" - must resolve"));

							    // For now we'll just ignore the size of files that need to be resolved... in practice there probably won't be that
							    // many, and they're most likely going to be code files, which are small
							    filesToSync.Enqueue(new DepotFile() { Name = fileName, Size = 0, Change = change });
						    }
					    }
				    }
                }
                catch(Exception ex)
                {
                    OnError(ex.Message);
                }
			}

            OnGathered(filesToSync.Count, totalFileSize);   
		}

		private void SyncFiles()
		{
			int numTasks = Math.Min(numThreads, filesToSync.Count);
			Task[] tasks = new Task[numTasks];

			for(int i = 0; i < tasks.Length; i++)
			{
				int current = i;
				tasks[i] = Task.Factory.StartNew(() =>
					{
						using(var p4 = CreatePerforceConnection())
						{
							DepotFile file;
							while(filesToSync.TryDequeue(out file))
							{
								OnSyncing(current, file.Name, file.Size);

								try
								{
									string[] args = GetSyncArgs(file.Name, file.Change, false, false);
									p4.Run("sync", args);
									OnSynced(current, file.Name, file.Size);
								}
								catch(Exception ex)
								{
									OnError(ex.Message);
								}
							}
						}
					});
			}

			Task.WaitAll(tasks);
		}

		private P4Connection CreatePerforceConnection()
		{
			var p4 = new P4Connection() { User = user, Password = password, Client = workspace, Port = server };

			p4.Connect();
            p4.Login(password);

			return p4;
		}

		private string[] GetSyncArgs(string path, bool preview, bool force)
		{
			var args = new List<string>();

			if(preview)
				args.Add("-n");

			if(force)
				args.Add("-f");

			args.Add(string.Format("{0}", path));

			return args.ToArray();
		}

		private string[] GetSyncArgs(string path, long changelist, bool preview, bool force)
		{
			var args = new List<string>();

			if(preview)
				args.Add("-n");

			if(force)
				args.Add("-f");

			args.Add(string.Format("{0}@{1}", path, changelist));

			return args.ToArray();
		}

		private void OnGathered(long totalFileCount, long totalFileSize)
		{
			if(Gathered != null)
			{
				var args = new GatherEventArgs() { TotalFileCount = totalFileCount, TotalFileSize = totalFileSize };
				Gathered(this, args);
			}
		}

		private void OnSyncing(int id, string name, long size)
		{
			if(Syncing != null)
			{
				var args = new SyncEventArgs() { Id = id, Name = name, Size = size };
				Syncing(this, args);
			}
		}

		private void OnSynced(int id, string name, long size)
		{
			if(Synced != null)
			{
				var args = new SyncEventArgs() { Id = id, Name = name, Size = size };
				Synced(this, args);
			}
		}

		private void OnError(string error)
		{
			if(Error != null)
			{
				var args = new ErrorEventArgs() { Error = error };
				Error(this, args);
			}
		}

		// Events
		public delegate void GatherEventHandler(object sender, GatherEventArgs e);
		public event GatherEventHandler Gathered;

		public delegate void SyncEventHandler(object sender, SyncEventArgs e);
		public event SyncEventHandler Syncing;
		public event SyncEventHandler Synced;

		public delegate void ErrorEventHandler(object sender, ErrorEventArgs e);
		public event ErrorEventHandler Error;

		// Members
		private ConcurrentQueue<DepotFile> filesToSync;
		private string user;
		private string password;
		private string workspace;
		private string server;
		private int numThreads;
    }

	public class DepotFile
	{
		public string Name { get; set; }
		public long Size { get; set; }
		public long Change { get; set; }
	}

	public class GatherEventArgs : EventArgs
	{
		public long TotalFileCount { get; set; }
		public long TotalFileSize { get; set; }
	}

	public class SyncEventArgs : EventArgs
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public long Size { get; set; }
	}

	public class ErrorEventArgs : EventArgs
	{
		public string Error { get; set; }
	}
}
