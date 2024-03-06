using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace secureshare.Controllers
{
    public class FolderInfo
    {
        public string PartitionName { get; set; }
        public List<FolderDetails> Folders { get; set; }
    }

    public class FolderDetails
    {
        public string FolderName { get; set; }
        public string FolderPath { get; set; }
        public int FileCount { get; set; }
    }

    public class FoldersController : Controller
    {
        public IActionResult Index()
        {
            var allPartitionsInfo = DriveInfo.GetDrives()
                .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
                .Select(drive => new FolderInfo
                {
                    PartitionName = drive.Name,
                    Folders = GetFoldersInfo(drive.RootDirectory)
                })
                .ToList();

            return View(allPartitionsInfo);
        }

        private List<FolderDetails> GetFoldersInfo(DirectoryInfo rootDirectory)
        {
            var folders = new List<FolderDetails>();

            if (!rootDirectory.Exists)
            {
                return folders;
            }

            try
            {
                foreach (var folder in rootDirectory.GetDirectories())
                {
                    var folderInfo = new FolderDetails
                    {
                        FolderName = folder.Name,
                        FolderPath = folder.FullName,
                        FileCount = Directory.GetFiles(folder.FullName).Length
                    };

                    folders.Add(folderInfo);
                    Console.WriteLine($"Folder Name: {folderInfo.FolderName}, Path: {folderInfo.FolderPath}, File Count: {folderInfo.FileCount}");

                }
            }
            catch (UnauthorizedAccessException)
            {
                // Handle unauthorized access to folders within the partition if needed
            }
            catch (DirectoryNotFoundException)
            {
                // Handle directories not found within the partition if needed
            }

            return folders;
        }
    }
}
