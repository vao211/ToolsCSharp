using System.ComponentModel;
using Spectre.Console.Cli;

namespace FolderScanner.CommandSettings;

public class FilterExtSettings : Spectre.Console.Cli.CommandSettings
{
    [CommandOption("-x|--extension <EXTENSION>")]
    [Description("Nhập loại file (extension) cần quét. VD: .txt")]
    public string Extension { get; set; }

    [CommandOption("-p|--path <PATH>")]
    [Description("Đường dẫn thư mục cần quét (nếu không chỉ định sẽ mở hộp thoại chọn)")]
    public string? FolderPath { get; set; }
    
    [CommandOption("-e|--export")]
    [Description("Xuất kết quả ra file csv")]
    [DefaultValue(false)]
    public bool ExportToFile { get; set; }
    
    [CommandOption("-o|--output <PATH>")]
    [Description("Đường dẫn file xuất")]
    public string OutputPath { get; set; }
}