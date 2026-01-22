using System;
using System.Collections.Generic;
using System.Text;

namespace SyncFolderPair.Models
{
    public static class App
    {
        public static string DataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SyncFolderPair");

    }
}
