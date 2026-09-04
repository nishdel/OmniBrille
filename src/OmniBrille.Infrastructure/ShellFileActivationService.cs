using System.Diagnostics;
using OmniBrille.Core;

namespace OmniBrille.Infrastructure;

/// <summary>Launches ordinary standalone files after a last-moment authority and type check.</summary>
public sealed class ShellFileActivationService : IFileActivationService
{
    private static readonly HashSet<string> HighRiskExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".application", ".appref-ms", ".bat", ".chm", ".cmd", ".com", ".cpl", ".diagcab",
        ".exe", ".gadget", ".hta", ".inf", ".ins", ".isp", ".jar", ".js", ".jse", ".lnk",
        ".msc", ".msi", ".msix", ".msp", ".mst", ".pif", ".ps1", ".reg", ".scf", ".scr",
        ".sct", ".shb", ".shs", ".url", ".vb", ".vbe", ".vbs", ".vxd", ".website", ".wsf",
        ".wsh", ".xll",
    };

    public Task<FileActivationResult> OpenAsync(
        string accessRoot,
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!Path.IsPathFullyQualified(accessRoot) ||
                !Path.IsPathFullyQualified(path) ||
                !PathBoundary.IsWithin(accessRoot, path))
            {
                return Task.FromResult(FileActivationResult.OutsideAccessRoot);
            }

            var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(accessRoot));
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return Task.FromResult(FileActivationResult.NotFound);
            }

            var info = new FileInfo(fullPath);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return Task.FromResult(FileActivationResult.ReparsePointBlocked);
            }

            var parent = info.Directory;
            while (parent is not null && !PathBoundary.Comparer.Equals(parent.FullName, fullRoot))
            {
                if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return Task.FromResult(FileActivationResult.ReparsePointBlocked);
                }

                parent = parent.Parent;
            }

            if (parent is null)
            {
                return Task.FromResult(FileActivationResult.OutsideAccessRoot);
            }

            if (HighRiskExtensions.Contains(info.Extension))
            {
                return Task.FromResult(FileActivationResult.HighRiskTypeBlocked);
            }

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true,
                WorkingDirectory = info.DirectoryName ?? accessRoot,
            });
            return Task.FromResult(FileActivationResult.Opened);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          ArgumentException or System.ComponentModel.Win32Exception or
                                          InvalidOperationException or NotSupportedException)
        {
            return Task.FromResult(FileActivationResult.Failed);
        }
    }
}
