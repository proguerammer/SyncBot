using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncBot.Core
{
    public static class SyncBot
    {
		public delegate void GatherEventHandler(object sender, GatherEventArgs e);
		public static event GatherEventHandler Gathered;

		public delegate void SyncEventHandler(object sender, SyncEventArgs e);
		public static event SyncEventHandler Syncing;
		public static event SyncEventHandler Synced;

		public static event EventHandler Finished;
    }

	public class GatherEventArgs : EventArgs
	{
		// Number of files
		// Total file size
	}

	public class SyncEventArgs : EventArgs
	{
		// Name of file
		// Revision?
		// Size
	}
}
