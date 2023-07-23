using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using Community.PowerToys.Run.Plugin.Everything.Properties;
using Wox.Plugin;
using static Community.PowerToys.Run.Plugin.Everything.Interop.NativeMethods;

namespace Community.PowerToys.Run.Plugin.Everything
{
    internal sealed class Everything
    {
        internal Everything(Settings setting)
        {
            Everything_SetRequestFlags(Request.FULL_PATH_AND_FILE_NAME);
            Everything_SetSort((Sort)setting.Sort);
            Everything_SetMax(setting.Max);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private static dynamic FindExplorerWindow()
        {
            IntPtr explorerWindow = FindWindow("CabinetWClass", null);

            if (explorerWindow != IntPtr.Zero)
            {
                dynamic shellApp = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                dynamic shellWindows = shellApp.Windows();
                foreach (dynamic window in shellWindows)
                {
                    if (window.HWND == (long)explorerWindow)
                    {
                        return window;
                    }
                }
            }

            return null;
        }

        private static string GetExplorerPath(dynamic explorerWindow)
        {
            if (explorerWindow != null)
            {
                return explorerWindow.LocationURL.Replace("file:///", string.Empty).Replace('/', '\\');
            }

            return string.Empty;
        }

        internal IEnumerable<Result> Query(string query, Settings setting)
        {
            string orgqry = query;
            if (orgqry.Contains('\"') && !setting.MatchPath)
            {
                Everything_SetMatchPath(true);
            }

            if (orgqry.Contains(':'))
            {
                string[] nqry = query.Split(':');
                if (setting.Filters.TryGetValue(nqry[0].ToLowerInvariant(), out string value))
                {
                    Everything_SetMax(0xffffffff);
                    query = nqry[1].Trim() + " ext:" + value;
                }
            }

            dynamic explorerWindow = FindExplorerWindow();
            string explorerPath = GetExplorerPath(explorerWindow);
            bool searchInCurrentFolder = Regex.Match(query, @"^\./ ").Success;
            if (searchInCurrentFolder)
            {
                query = Regex.Replace(query, @"^\./ ", $"parent:\"{explorerPath}\" ");
            }
            else
            {
                query = Regex.Replace(query, @"^\. ", $"\"{explorerPath}\" ");
            }

            _ = Everything_SetSearchW(query);
            if (!Everything_QueryW(true))
            {
                throw new Win32Exception("Unable to Query");
            }

            if (orgqry.Contains('\"') && !setting.MatchPath)
            {
                Everything_SetMatchPath(false);
            }

            uint resultCount = Everything_GetNumResults();

            for (uint i = 0; i < resultCount; i++)
            {
                StringBuilder buffer = new StringBuilder(260);
                Everything_GetResultFullPathName(i, buffer, 260);
                string fullPath = buffer.ToString();
                string name = Path.GetFileName(fullPath);
                bool isFolder = Everything_IsFolderResult(i);
                string path = isFolder ? fullPath : Path.GetDirectoryName(fullPath);
                string ext = Path.GetExtension(fullPath.Replace(".lnk", string.Empty));

                var r = new Result()
                {
                    Title = name,
                    ToolTipData = new ToolTipData(name, fullPath),
                    SubTitle = Resources.plugin_name + ": " + fullPath,

                    IcoPath = isFolder ? "Images/folder.png" : (setting.Preview ?
                        fullPath : (SearchHelper.IconLoader.Icon(ext) ?? "Images/file.png")),
                    ContextData = new SearchResult()
                    {
                        Path = fullPath,
                        Title = name,
                        File = !isFolder,
                    },
                    Action = e =>
                    {
                        using var process = new Process();
                        process.StartInfo.FileName = fullPath;
                        process.StartInfo.WorkingDirectory = path;
                        process.StartInfo.UseShellExecute = true;

                        try
                        {
                            if (searchInCurrentFolder && explorerWindow != null)
                            {
                                foreach (var item in explorerWindow.Document.selectedItems())
                                {
                                    explorerWindow.Document.selectItem(item, 0);
                                }

                                var folderItem = explorerWindow.Document.Folder.ParseName(name);
                                explorerWindow.Document.selectItem(folderItem, 8);
                                explorerWindow.Document.selectItem(folderItem, 1);
                            }
                            else
                            {
                                process.Start();
                            }

                            return true;
                        }
                        catch (Win32Exception)
                        {
                            return false;
                        }
                    },

                    QueryTextDisplay = setting.QueryText ? (isFolder ? path : name) : orgqry,
                };
                yield return r;
            }

            Everything_SetMax(setting.Max);
        }
    }
}
