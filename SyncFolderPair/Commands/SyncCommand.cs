using System;
using System.Collections.Generic;
using System.Text;

namespace SyncFolderPair.Commands
{
    /// <summary>
    /// フォルダペアのフォルダ内容を同期させる
    /// </summary>
    public static class SyncCommand
    {
        public static void Run(string pairName)
        {
            // TODO: var pair = PairLoader.Load(pairName);
            // TODO: SyncEngine.Run(pair);
            Console.WriteLine($"[sync] pair={pairName}");
        }
    }
}
