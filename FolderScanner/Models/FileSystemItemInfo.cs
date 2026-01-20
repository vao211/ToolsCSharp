namespace FolderScanner.Models;

public class FileSystemItemInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsDirectory { get; set; }    
}