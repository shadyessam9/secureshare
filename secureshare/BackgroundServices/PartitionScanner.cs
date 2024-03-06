using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using secureshare.Models;

namespace secureshare.Services
{
    public class PartitionScanner : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PartitionScanner> _logger;
        private readonly TimeSpan _scanInterval = TimeSpan.FromHours(1); // Hard-coded scan interval

        public PartitionScanner(IServiceProvider serviceProvider, ILogger<PartitionScanner> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<secureshareContext>();

                        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                        {
                            foreach (var directory in drive.RootDirectory.GetDirectories("*", SearchOption.TopDirectoryOnly))
                            {
                                var folderPath = directory.FullName;
                                var fileCount = 0;

                                try
                                {
                                    fileCount = directory.GetFiles().Length; // Count files in the directory
                                }
                                catch (UnauthorizedAccessException ex)
                                {
                                    _logger.LogWarning(ex, $"Access denied when counting files in directory: {folderPath}");
                                }

                                var folder = dbContext.Folders.FirstOrDefault(f => f.FolderPath == folderPath);

                                if (folder == null)
                                {
                                    dbContext.Folders.Add(new Folder
                                    {
                                        FolderPath = folderPath,
                                        Name = directory.Name,
                                        FileCount = fileCount
                                    });
                                }
                                else
                                {
                                    folder.Name = directory.Name;
                                    folder.FileCount = fileCount;
                                    dbContext.Folders.Update(folder);
                                }
                            }
                        }

                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing partition scan.");
                }

                await Task.Delay(_scanInterval, stoppingToken);
            }
        }
    }
}
