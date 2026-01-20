using System.ComponentModel;
using Spectre.Console.Cli;

namespace FolderScanner.CommandSettings;

public class ScanSettings : Spectre.Console.Cli.CommandSettings
{
    [CommandOption("-f|--include-files")]
    [Description("Bao gồm cả các tệp tin (không chỉ thư mục)")]
    [DefaultValue(false)]
    public bool IncludeFiles { get; set; }

    [CommandOption("-e|--export")]
    [Description("Xuất kết quả ra file txt")]
    [DefaultValue(false)]
    public bool ExportToFile { get; set; }

    [CommandOption("-o|--output <PATH>")]
    [Description("Đường dẫn file xuất (mặc định: scan_result.txt)")]
    [DefaultValue("scan_result.txt")]
    public string OutputPath { get; set; } = $"scan_result";

    [CommandOption("-p|--path <PATH>")]
    [Description("Đường dẫn thư mục cần quét (nếu không chỉ định sẽ mở hộp thoại chọn)")]
    public string? FolderPath { get; set; }
    
    [CommandOption("-d|--deep")]
    [Description("Quét sâu toàn bộ file, xuất ra file csv")]
    [DefaultValue(false)]
    public bool DeepScan { get; set; }
    
    [CommandOption("-x|--extension <EXT>")]
    [Description("Lọc theo đuôi tệp tin (ví dụ: .zip, .mp4)")]
    public string? Extension { get; set; }
}