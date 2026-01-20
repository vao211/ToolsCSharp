using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using FolderScanner.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FolderScanner;

class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        AnsiConsole.Write("Chọn chế độ: \n" +
                          "1: Quét kích thước file/thư mục\n" +
                          "2: Quét file theo đuôi\n");

        var line = Console.ReadLine();
        if (!int.TryParse(line, out var choice))
        {
            AnsiConsole.MarkupLine("[red]Lựa chọn không hợp lệ[/]");
            return -1;
        }

        string? commandName = choice switch
        {
            1 => "scan",
            2 => "filter",
            _ => null
        };

        if (string.IsNullOrEmpty(commandName))
        {
            AnsiConsole.MarkupLine("[red]Lựa chọn không hợp lệ[/]");
            return -1;
        }

        // Nếu đang trên Windows và chưa elevated thì khởi động lại với UAC
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !IsRunningAsAdministrator())
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule!.FileName!;
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = commandName, // truyền command đã chọn làm arg
                    UseShellExecute = true,  // BẮT BUỘC để Verb hoạt động
                    Verb = "runas"
                };

                Process.Start(psi);
                return 0; // thoát tiến trình hiện tại, tiến trình elevated sẽ chạy tiếp
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 1223 = user cancelled UAC
                AnsiConsole.MarkupLine($"[red]Không thể nâng quyền: {ex.Message}[/]");
                return -1;
            }
        }

        // Nếu đã elevated hoặc không phải Windows, cấu hình và chạy CommandApp
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("FolderScanner");
            config.SetApplicationVersion("1.0.0");
            config.AddCommand<ScanCommand>("scan");
            config.AddCommand<FilterCommand>("filter");
        });

        // Nếu chương trình được khởi bằng UAC và có arg, dùng arg đó; nếu không, dùng commandName
        string[] runArgs;
        if (args != null && args.Length > 0)
        {
            runArgs = args; // giữ nguyên args khi tiến trình elevated được khởi bởi chính nó
        }
        else
        {
            runArgs = new string[] { commandName };
        }

        return app.Run(runArgs);
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}