using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FolderScanner.CommandSettings;
using Spectre.Console;
using Spectre.Console.Cli;
using FolderScanner.Models;
namespace FolderScanner.Commands;

public class ScanCommand : Command<ScanSettings>
{
    private const int SafeLimit = 1000;
    public override int Execute(CommandContext context, ScanSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            AnsiConsole.Write(
                new FigletText("Folder Scanner")
                    .Centered()
                    .Color(Color.Cyan1));
            // Lấy đường dẫn thư mục
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
            bool includeFiles = settings.IncludeFiles;
            bool exportToFile = settings.ExportToFile;
            bool deepScan = settings.DeepScan;

            if (!settings.DeepScan)
            {
                deepScan = AnsiConsole.Confirm("Bạn có muốn quét sâu (deepscan tất cả file)?", false);
            }

            if (!settings.IncludeFiles && !deepScan)
            {
                includeFiles = AnsiConsole.Confirm("Bạn có muốn quét cả các tệp tin (không chỉ thư mục)?", false);
            }

            if (!settings.ExportToFile)
            {
                exportToFile = AnsiConsole.Confirm("Bạn có muốn xuất kết quả ra file?", false);
            }

            AnsiConsole.MarkupLine($"\n[green]Đang quét thư mục:[/] [yellow]{folderPath}[/]");
            AnsiConsole.MarkupLine($"[green]Bao gồm tệp tin:[/] [yellow]{(includeFiles ? "Có" : "Không")}[/]\n");

            // Quét thư mục
            var items = new List<FileSystemItemInfo>();

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green bold"))
                .Start("Đang quét thư mục...", ctx =>
                {
                    if (deepScan)
                    {
                        items = PerformDeepScan(folderPath, ctx);
                    }
                    else
                    {
                        items = ScanFolder(folderPath, includeFiles, ctx);
                    }
                });

            if (items.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Không tìm thấy mục nào trong thư mục.[/]");
                return 0;
            }

            // Sắp xếp theo kích thước giảm dần
            items = items.OrderByDescending(x => x.SizeInBytes).ToList();

            if (items.Count > SafeLimit)
            {
                AnsiConsole.MarkupLine(
                    $"[orange1]Cảnh báo: Tìm thấy {items.Count} mục. Con số này quá lớn để hiển thị trên bảng console.[/]");
                string csvPath = Path.Combine(folderPath, $"ScanResult_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                ExportToCsv(items, csvPath);
                AnsiConsole.MarkupLine($"[green]Đã tự động xuất toàn bộ kết quả ra CSV:[/] [yellow]{csvPath}[/]");
            }
            else
            {
                var table = CreateResultTable(items, folderPath);
                AnsiConsole.Write(table);
            }

            // Hiển thị tổng kết
            DisplaySummary(items, folderPath);

            if (deepScan && items.Count <= SafeLimit && exportToFile)
            {
                string csvPath = Path.Combine(folderPath, $"ScanResult_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                ExportToCsv(items, csvPath);
                AnsiConsole.MarkupLine($"[green]Đã tự động xuất toàn bộ kết quả ra CSV:[/] [yellow]{csvPath}[/]");
            }

            if (exportToFile && !deepScan)
            {
                string outputPath = settings.OutputPath;
                if (!Path.IsPathRooted(outputPath))
                {
                    outputPath = Path.Combine(folderPath, $"{outputPath}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                }

                ExportToTextFile(items, folderPath, outputPath, includeFiles);
                AnsiConsole.MarkupLine($"\n[green]Đã xuất kết quả ra file:[/] [yellow]{outputPath}[/]");
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
    
    private List<FileSystemItemInfo> PerformDeepScan(string rootPath, StatusContext ctx, string? ext = null)
    {
        var result = new List<FileSystemItemInfo>();
        var stack = new Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0)
        {
            string currentDir = stack.Pop();
            ctx.Status($"Đang quét: [grey]{Path.GetFileName(currentDir)}[/]");

            try
            {
                // Lấy tất cả tệp tin trong thư mục hiện tại
                foreach (var filePath in Directory.GetFiles(currentDir))
                {
                    var fi = new FileInfo(filePath);
                    result.Add(new FileSystemItemInfo {
                        Name = fi.Name,
                        FullPath = fi.FullName,
                        SizeInBytes = fi.Length,
                        CreatedDate = fi.CreationTime,
                        IsDirectory = false
                    });
                }

                // Đưa các thư mục con vào stack để tiếp tục quét
                foreach (var dirDir in Directory.GetDirectories(currentDir))
                {
                    stack.Push(dirDir);
                    // Lưu thông tin folder (tùy chọn - nếu bạn muốn tính cả size folder tổng)
                    var di = new DirectoryInfo(dirDir);
                    result.Add(new FileSystemItemInfo {
                        Name = di.Name,
                        FullPath = di.FullName,
                        SizeInBytes = 0, // Deep scan thường chỉ liệt kê file để chính xác, hoặc bạn tính size folder sau
                        CreatedDate = di.CreationTime,
                        IsDirectory = true
                    });
                }
            }
            catch (UnauthorizedAccessException) { /* Bỏ qua folder hệ thống bị khóa */ }
            catch (Exception) { }
        }
        return result;
    }
    
    private void ExportToCsv(List<FileSystemItemInfo> items, string outputPath)
    {
        var csv = new StringBuilder();
        // Header
        csv.AppendLine("Type,Name,Size (MB),Size (Bytes),Path,Created Date");

        foreach (var item in items)
        {
            double sizeMb = item.SizeInBytes / (1024.0 * 1024.0);
            string type = item.IsDirectory ? "Folder" : "File";
            
            // Format CSV an toàn (bao quanh bởi dấu ngoặc kép để tránh lỗi dấu phẩy trong tên file)
            csv.AppendLine($"{type},\"{item.Name}\",{sizeMb:F2},{item.SizeInBytes},\"{item.FullPath}\",\"{item.CreatedDate}\"");
        }

        File.WriteAllText(outputPath, csv.ToString(), Encoding.UTF8);
    }
    
    private List<FileSystemItemInfo> ScanFolder(string folderPath, bool includeFiles, StatusContext ctx, string? ext = null)
    {
        var items = new List<FileSystemItemInfo>();

        try
        {
            // Quét các thư mục con
            var directories = Directory.GetDirectories(folderPath);
            foreach (var dir in directories)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    ctx.Status($"Đang quét: {dirInfo.Name}");

                    long size = CalculateDirectorySize(dir);
                    
                    items.Add(new FileSystemItemInfo
                    {
                        Name = dirInfo.Name,
                        FullPath = dirInfo.FullName,
                        SizeInBytes = size,
                        CreatedDate = dirInfo.CreationTime,
                        IsDirectory = true
                    });
                }
                catch (UnauthorizedAccessException)
                {
                    // Bỏ qua các thư mục không có quyền truy cập
                }
                catch (Exception)
                {
                    // Bỏ qua các lỗi khác
                }
            }

            // Quét các tệp tin nếu được yêu cầu
            if (includeFiles)
            {
                var files = Directory.GetFiles(folderPath);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        ctx.Status($"Đang quét: {fileInfo.Name}");

                        items.Add(new FileSystemItemInfo
                        {
                            Name = fileInfo.Name,
                            FullPath = fileInfo.FullName,
                            SizeInBytes = fileInfo.Length,
                            CreatedDate = fileInfo.CreationTime,
                            IsDirectory = false
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Bỏ qua các tệp không có quyền truy cập
                    }
                    catch (Exception)
                    {
                        // Bỏ qua các lỗi khác
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Lỗi khi quét thư mục: {ex.Message}[/]");
        }

        return items;
    }

    private long CalculateDirectorySize(string path)
    {
        long size = 0;

        try
        {
            // Tính kích thước các tệp trong thư mục hiện tại
            var files = Directory.GetFiles(path);
            foreach (var file in files)
            {
                try
                {
                    size += new FileInfo(file).Length;
                }
                catch
                {
                    // Bỏ qua các tệp không thể truy cập
                }
            }

            // Tính kích thước các thư mục con
            var directories = Directory.GetDirectories(path);
            foreach (var dir in directories)
            {
                try
                {
                    size += CalculateDirectorySize(dir);
                }
                catch
                {
                    // Bỏ qua các thư mục không thể truy cập
                }
            }
        }
        catch
        {
            // Bỏ qua lỗi
        }

        return size;
    }

    private Table CreateResultTable(List<FileSystemItemInfo> items, string rootPath)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title($"[bold cyan]Kết quả quét thư mục[/]")
            .Caption($"[grey]Đường dẫn: {rootPath}[/]");

        table.AddColumn(new TableColumn("[bold]STT[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Loại[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Tên[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold]Kích thước[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Ngày tạo[/]").Centered());

        int index = 1;
        foreach (var item in items)
        {
            string typeIcon = item.IsDirectory ? "[yellow]📁[/]" : "[blue]📄[/]";
            string sizeStr = FormatSize(item.SizeInBytes);
            string sizeColor = GetSizeColor(item.SizeInBytes);

            table.AddRow(
                $"[grey]{index}[/]",
                typeIcon,
                item.IsDirectory ? $"[yellow]{EscapeMarkup(item.Name)}[/]" : $"[blue]{EscapeMarkup(item.Name)}[/]",
                $"[{sizeColor}]{sizeStr}[/]",
                $"[grey]{item.CreatedDate:dd/MM/yyyy HH:mm}[/]"
            );
            index++;
        }

        return table;
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

    private void ExportToTextFile(List<FileSystemItemInfo> items, string rootPath, string outputPath, bool includeFiles)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("                           KẾT QUẢ QUÉT THƯ MỤC                                ");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Thư mục gốc: {rootPath}");
        sb.AppendLine($"Thời gian quét: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Bao gồm tệp tin: {(includeFiles ? "Có" : "Không")}");
        sb.AppendLine();
        sb.AppendLine("───────────────────────────────────────────────────────────────────────────────");
        sb.AppendLine($"{"STT",-5} {"Loại",-10} {"Tên",-40} {"Kích thước",-15} {"Ngày tạo",-20}");
        sb.AppendLine("───────────────────────────────────────────────────────────────────────────────");

        int index = 1;
        foreach (var item in items)
        {
            string type = item.IsDirectory ? "[Thư mục]" : "[Tệp]";
            string name = item.Name.Length > 38 ? item.Name.Substring(0, 35) + "..." : item.Name;

            sb.AppendLine($"{index,-5} {type,-10} {name,-40} {FormatSize(item.SizeInBytes),-15} {item.CreatedDate:dd/MM/yyyy HH:mm}");
            index++;
        }

        sb.AppendLine("───────────────────────────────────────────────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("TỔNG KẾT:");
        sb.AppendLine($"  - Tổng số mục: {items.Count}");
        sb.AppendLine($"  - Số thư mục: {items.Count(x => x.IsDirectory)}");
        sb.AppendLine($"  - Số tệp tin: {items.Count(x => !x.IsDirectory)}");
        sb.AppendLine($"  - Tổng dung lượng: {FormatSize(items.Sum(x => x.SizeInBytes))}");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }
}