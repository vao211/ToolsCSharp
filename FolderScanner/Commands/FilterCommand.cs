using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FolderScanner.CommandSettings;
using FolderScanner.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FolderScanner.Commands;

public class FilterCommand : Command<FilterExtSettings>
{
    private const int SafeLimit = 1000;

    public override int Execute(CommandContext context, FilterExtSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            AnsiConsole.Write(
                new FigletText("Extension Filter")
                    .Centered()
                    .Color(Color.Green1)
            );
            string? folderPath = settings.FolderPath;
            if (string.IsNullOrEmpty(folderPath))
            {
                folderPath = SelectFolder();
                if (string.IsNullOrEmpty(folderPath))
                {
                    AnsiConsole.MarkupLine("[red]Không có thư mục nào được chọn. Thoát chương trình.[/]");
                    return 0;
                }
            }

            if (!Directory.Exists(folderPath))
            {
                AnsiConsole.MarkupLine($"[red]Thư mục không tồn tại: {folderPath}[/]");
                return 0;
            }

            // Hỏi người dùng về các tùy chọn nếu không có trong command line
            bool exportToFile = settings.ExportToFile;
            string? extension = settings.Extension;
            if (!settings.ExportToFile)
            {
                exportToFile = AnsiConsole.Confirm("Bạn có muốn xuất kết quả ra file?", false);
            }

            if (string.IsNullOrEmpty(extension))
            {
                extension = AnsiConsole.Ask<string>("Nhập đuôi tệp (ví dụ: .mp4 hoặc png):");
            }

            if (!string.IsNullOrEmpty(extension) && !extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            AnsiConsole.MarkupLine($"\n[green]Đang quét thư mục:[/] [yellow]{folderPath}[/]");

            var items = new List<FileSystemItemInfo>();

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Arc)
                .Start("Scanning and filtering...", ctx => { items = PerformScan(folderPath, extension, ctx); });
            if (items.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Không tìm thấy mục nào trong thư mục.[/]");
                return 0;
            }

            items = items.OrderByDescending(x => x.SizeInBytes).ToList();
            if (items.Count > SafeLimit)
            {
                AnsiConsole.MarkupLine(
                    $"[orange1]Cảnh báo: Tìm thấy {items.Count} mục. Con số này quá lớn để hiển thị trên bảng console.[/]");
                string csvPath = Path.Combine(folderPath,
                    $"Filter_file_{extension}_Result_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                ExportToCsv(items, csvPath);
            }
            else
            {
                var table = CreateResultTable(items, folderPath);
                AnsiConsole.Write(table);
            }

            DisplaySummary(items, folderPath);
            if (exportToFile && items.Count <= SafeLimit)
            {
                string csvPath = Path.Combine(folderPath,
                    $"Filter_file_{extension}_Result_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                ExportToCsv(items, csvPath);
            }

            AnsiConsole.MarkupLine("\n[grey]Nhấn phím bất kỳ để thoát...[/]");
            Console.ReadKey();

            return 1;
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: " + e.Message);
            return 0;
        }
    }
    
    private Table CreateResultTable(List<FileSystemItemInfo> items, string rootPath)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title($"[bold cyan]Kết quả quét thư mục[/]")
            .Caption($"[grey]Đường dẫn: {rootPath}[/]");

        table.AddColumn(new TableColumn("[bold]STT[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Tên[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold]Kích thước[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Ngày tạo[/]").LeftAligned());

        int index = 1;
        foreach (var item in items)
        {
            string sizeStr = FormatSize(item.SizeInBytes);
            string sizeColor = GetSizeColor(item.SizeInBytes);

            table.AddRow(
                $"[grey]{index}[/]",
                $"[yellow]{EscapeMarkup(item.Name)}[/]",
                $"[{sizeColor}]{sizeStr}[/]",
                $"[grey]{item.CreatedDate:dd/MM/yyyy HH:mm}[/]"
            );
            index++;
        }

        return table;
    }
    private List<FileSystemItemInfo> PerformScan(string folderPath, string extension, StatusContext context)
    {
        var result = new List<FileSystemItemInfo>();
        var stack = new Stack<string>();
        stack.Push(folderPath);

        while (stack.Count > 0)
        {
            string currentDir = stack.Pop();
            try
            {
                var files = Directory.GetFiles(currentDir);
                foreach (var filePath in files)
                {
                    var fi = new FileInfo(filePath);
                    if (!string.IsNullOrEmpty(extension) &&
                        !fi.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase))
                        continue;
                    result.Add(new FileSystemItemInfo()
                    {
                        Name = fi.Name,
                        FullPath = fi.FullName,
                        SizeInBytes = fi.Length,
                        CreatedDate = fi.CreationTime,
                        IsDirectory = false
                    });
                }

                foreach (var dir in Directory.GetDirectories(currentDir))
                {
                    stack.Push(dir);
                    context.Status($"Đang quét: [grey]{Path.GetFileName(dir)}[/]");
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception) { }
        }
        return result;
    }

    private void ExportToCsv(List<FileSystemItemInfo> items, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Type,Name,Size_MB,Size_Bytes,CreatedDate,Path");

        foreach (var item in items)
        {
            double mb = item.SizeInBytes / (1024.0 * 1024.0);
            // Escape dấu ngoặc kép để tránh lỗi CSV nếu tên file có dấu phẩy
            string safeName = item.Name.Replace("\"", "\"\"");
            string safePath = item.FullPath.Replace("\"", "\"\"");

            sb.AppendLine($"{(item.IsDirectory ? "Folder" : "File")},\"{safeName}\",{mb:F2},{item.SizeInBytes},\"{item.CreatedDate:yyyy-MM-dd HH:mm:ss}\",\"{safePath}\"");
        }
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        AnsiConsole.MarkupLine($"[green]Đã xuất kết quả thành công ra:[/] [yellow]{outputPath}[/]");
    }
    private string? SelectFolder()
    {
        AnsiConsole.MarkupLine("[cyan]Đang mở hộp thoại chọn thư mục...[/]\n");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return SelectFolderWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return SelectFolderLinux();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return SelectFolderMacOS();
        }

        // Fallback: nhập đường dẫn thủ công
        return AnsiConsole.Ask<string>("Nhập đường dẫn thư mục cần quét:");
    }

    private string? SelectFolderWindows()
    {
        try
        {
            // Sử dụng PowerShell để mở dialog chọn folder
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = @"-Command ""Add-Type -AssemblyName System.Windows.Forms; $folderBrowser = New-Object System.Windows.Forms.FolderBrowserDialog; $folderBrowser.Description = 'Chọn thư mục cần quét'; $folderBrowser.ShowNewFolderButton = $false; if ($folderBrowser.ShowDialog() -eq 'OK') { Write-Output $folderBrowser.SelectedPath }""",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch
        {
            return AnsiConsole.Ask<string>("Nhập đường dẫn thư mục cần quét:");
        }
    }

    private string? SelectFolderLinux()
    {
        try
        {
            // Thử sử dụng zenity
            var psi = new ProcessStartInfo
            {
                FileName = "zenity",
                Arguments = "--file-selection --directory --title=\"Chọn thư mục cần quét\" --modal",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch
        {
            return AnsiConsole.Ask<string>("Nhập đường dẫn thư mục cần quét:");
        }
    }

    private string? SelectFolderMacOS()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = "-e 'tell application \"Finder\" to set folderPath to POSIX path of (choose folder with prompt \"Chọn thư mục cần quét\")'",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch
        {
            return AnsiConsole.Ask<string>("Nhập đường dẫn thư mục cần quét:");
        }
    }
    
    private string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }

    private string FormatSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:N2} {suffixes[suffixIndex]}";
    }

    private string GetSizeColor(long bytes)
    {
        double mb = bytes / (1024.0 * 1024.0);

        if (mb >= 1000) return "red";
        if (mb >= 100) return "orange1";
        if (mb >= 10) return "yellow";
        return "green";
    }

    private void DisplaySummary(List<FileSystemItemInfo> items, string rootPath)
    {
        var panel = new Panel(
                new Markup(
                    $"[bold]Tổng số mục:[/] [cyan]{items.Count}[/]\n" +
                    $"[bold]Thư mục:[/] [yellow]{items.Count(x => x.IsDirectory)}[/]\n" +
                    $"[bold]Tệp tin:[/] [blue]{items.Count(x => !x.IsDirectory)}[/]\n" +
                    $"[bold]Tổng dung lượng:[/] [green]{FormatSize(items.Sum(x => x.SizeInBytes))}[/]"
                ))
            .Header("[bold cyan]Tổng kết[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);
    }
}