using SyncFolderPair.Models;
using SyncFolderPair.Services;

namespace SyncFolderPair.Commands
{
    /// <summary>
    /// 二つのフォルダの同一にする。<br/>
    /// 
    /// オプションが指定されていない場合、以下の処理を行う。<br/>
    /// (1) 片方のフォルダにしかないファイルの場合、もう片方のフォルダににコピーし、その旨をコンソールに出力する。<br/>
    /// (2) 両方のフォルダに存在し、タイムスタンプもサイズも同じ場合、ファイル操作はせず、コンソールへの出力も行わない。<br/>
    /// (3) 両方のフォルダに存在し、タイムスタンプが異なる場合、ファイル操作はせず、その旨をコンソールに出力する。<br/>
    /// (4) 両方のフォルダに存在し、タイムスタンプが同じなのにサイズが異なる場合、ファイル操作はせず、異常な状態である旨をコンソールに出力する。<br/>
    /// 
    /// "force"オプションが指定されている場合、上記の(3)のケースの処理が異なる。<br/>
    /// 古いファイルはゴミ箱に移動させ、新しいファイルをコピーし、その旨をコンソールに出力する。<br/>
    /// 
    /// "check"オプションが指定されている場合、実際のファイル操作は行わず、差異状況をコンソールに出力するのみ。<br/>
    /// </summary>
    public static class AlignCommand
    {
        public static void Run(Span<string> args)
        {
            if (args.Length == 0)
                throw new ArgumentException("Specify pair name.");

            var pair = DirectoryPairs.Get(args[0]);

            switch (args.Length)
            {
                case 1:
                    // 片方のディレクトリにだけあるファイルをもう片方にコピーするだけ
                    DirectoryAligner.Align(pair.LeftDirectory, pair.RightDirectory);
                    break;
                case 2:
                    switch (args[1])
                    {
                        case "force":
                            // 両方のディレクトリにあるファイルも、更新日時が新しい方で上書きコピーする
                            DirectoryAligner.ForceAlign(pair.LeftDirectory, pair.RightDirectory);
                            break;
                        case "check":
                            DirectoryDifferencePrinter.Check(pair.LeftDirectory, pair.RightDirectory);
                            break;
                        default:
                            throw new ArgumentException($"Wrong option [{args[1]}].");
                    }
                    break;
                default:
                    throw new ArgumentException("Parameter count error.");
            }
        }
    }
}
