using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using secureshare.Models;
using secureshare.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static System.Formats.Asn1.AsnWriter;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.IO;
using System;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using System.Text;
using System.Management;


namespace secureshare.Controllers
{
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly secureshareContext _dbcontext;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;

        public AdminController(ILogger<AdminController> logger, secureshareContext context, ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider)
        {
            _logger = logger;
            _dbcontext = context;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;


        }

        public IActionResult Index()
        {

            var today = DateTime.Today;

            var foldersCount = _dbcontext.Folders.Count(); // Get the count of folders
            var usersCount = _dbcontext.Users.Count(); // Get the count of users
            var uploadsCount = _dbcontext.Actions.Where(a => a.ActionPerformed == "Upload").Count(); // Get the count of uploads
            var downloadsCount = _dbcontext.Actions.Where(a => a.ActionPerformed == "Download").Count(); // Get the count of download
            var actions = _dbcontext.Actions.Where(a => a.Timestamp.HasValue && a.Timestamp.Value.Date == today).ToList(); // Get today actions
            var users = _dbcontext.Users.ToList();
            var folders = _dbcontext.Folders.ToList();



            var viewModel = new DashboardViewModel
            {
                Folders = foldersCount,
                Users = usersCount,
                Uploads = uploadsCount,
                Downloads = downloadsCount,
                Actions = actions,
                users = users,
                folders = folders
            };

            return View(viewModel);
        }


        public async Task<IActionResult> FoldersManagement()
        {
            var viewModel = new PartitionAndFoldersViewModel
            {
                Partitions = await _dbcontext.Partitions.ToListAsync(),
                Folders = await _dbcontext.Folders.ToListAsync()
            };


            return View(viewModel);
        }

        [HttpGet]
        public IActionResult GetAvailablePartitions()
        {
            var partitions = DriveInfo.GetDrives()
                                      .Where(d => d.IsReady)
                                      .Select(d => d.Name)
                                      .ToList();

            return Json(partitions);
        }

        [HttpGet]
        public IActionResult GetAvailableFolders()
        {
            var folderList = new List<FolderViewModel>();
            var localPartitions = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.Name).ToList();

            foreach (var partitionName in localPartitions)
            {
                try
                {
                    var driveInfo = new DriveInfo(partitionName);
                    if (!driveInfo.IsReady)
                        continue;

                    foreach (var directory in driveInfo.RootDirectory.GetDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        ProcessDirectory(directory, partitionName, folderList);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing partition: {partitionName}");
                }
            }

            return Json(folderList);
        }

        private void ProcessDirectory(DirectoryInfo directory, string partitionName, List<FolderViewModel> folderList)
        {
            var folderPath = directory.FullName;
            var fileCount = 0;
            try
            {
                fileCount = directory.GetFiles().Length;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, $"Access denied for directory: {folderPath}");
                return;
            }


            folderList.Add(new FolderViewModel
            {
                FolderPath = folderPath,
                FolderName = partitionName,
                FileCount = fileCount
            });
        }



        [HttpPost]
        public async Task<IActionResult> AddPartition(string partitionPath)
        {
            if (string.IsNullOrWhiteSpace(partitionPath))
            {
                return BadRequest("Partition path is required.");
            }

            try
            {
                var partition = _dbcontext.Partitions.FirstOrDefault(p => p.Name == partitionPath);
                if (partition == null)
                {
                    partition = new Partition { Name = partitionPath };
                    _dbcontext.Partitions.Add(partition);
                    await _dbcontext.SaveChangesAsync();
                }

                var drive = new DriveInfo(partitionPath);
                if (drive.IsReady)
                {
                    foreach (var directory in drive.RootDirectory.GetDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        ProcessAndSaveDirectory(directory, partition.Id);
                    }
                    await _dbcontext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding partition.");
                return StatusCode(500, "Internal Server Error");
            }

            return RedirectToAction("FoldersManagement");
        }

        [HttpPost]
        public async Task<IActionResult> RemovePartition(int partitionId)
        {
            var partition = await _dbcontext.Partitions
                                            .Include(p => p.Folders)
                                            .FirstOrDefaultAsync(p => p.Id == partitionId);

            if (partition != null)
            {
                _dbcontext.Folders.RemoveRange(partition.Folders);
                _dbcontext.Partitions.Remove(partition);
                await _dbcontext.SaveChangesAsync();

                return RedirectToAction("FoldersManagement");
            }
            else
            {
                return NotFound($"Partition with ID {partitionId} not found.");
            }
        }


        [HttpPost]
        public async Task<IActionResult> AddNetworkLocation(string networkPath)
        {
            if (string.IsNullOrWhiteSpace(networkPath))
            {
                return BadRequest("Network path is required.");
            }

            // Verify if the network path is a valid and accessible directory
            if (!Directory.Exists(networkPath))
            {
                _logger.LogError($"Invalid or inaccessible network path: {networkPath}");
                return BadRequest("Invalid or inaccessible network path.");
            }

            try
            {
                var partition = _dbcontext.Partitions.FirstOrDefault(p => p.Name == networkPath);
                if (partition == null)
                {
                    partition = new Partition { Name = networkPath };
                    _dbcontext.Partitions.Add(partition);
                    await _dbcontext.SaveChangesAsync();
                }

                // Manually iterate through the directories at the network path
                foreach (var directoryPath in Directory.GetDirectories(networkPath))
                {
                    var directoryInfo = new DirectoryInfo(directoryPath);
                    ProcessAndSaveDirectory(directoryInfo, partition.Id);
                }

                await _dbcontext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding network location.");
                return StatusCode(500, "Internal Server Error");
            }

            return RedirectToAction("FoldersManagement");
        }


        private void ProcessAndSaveDirectory(DirectoryInfo directory, int partitionId)
        {
            var folderPath = directory.FullName;
            var fileCount = 0;
            try
            {
                fileCount = directory.GetFiles().Length;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, $"Access denied for directory: {folderPath}");
                return;
            }

            var folder = _dbcontext.Folders.FirstOrDefault(f => f.FolderPath == folderPath);
            if (folder == null)
            {
                _dbcontext.Folders.Add(new Folder
                {
                    FolderPath = folderPath,
                    Name = directory.Name,
                    FileCount = fileCount,
                    PartitionID = partitionId
                });
            }
            else
            {
                folder.Name = directory.Name;
                folder.FileCount = fileCount;
                _dbcontext.Folders.Update(folder);
            }
        }



        public async Task<IActionResult> UsersMangement()
        {

            var viewModel = new UsersViewModel
            {
                Users = await _dbcontext.Users.ToListAsync(),
                Departments = await _dbcontext.Departments.ToListAsync(),
                Branchs = await _dbcontext.Branches.ToListAsync()
            };

            return View(viewModel);
        }

        public async Task<IActionResult> UserProfile(int userId)
        {
            var viewModel = new UserProfileViewModel
            {
                User = await _dbcontext.Users.FirstOrDefaultAsync(u => u.UserID == userId),

                // Assuming UserFolderPermissions includes a navigation property to Folder
                Folderspermissions = await _dbcontext.UserFolderPermissions
                    .Include(ufp => ufp.Folder)
                    .Where(ufp => ufp.UserID == userId)
                    .ToListAsync(),

                // Get the list of all folders, if needed separately
                Folders = await _dbcontext.Folders.ToListAsync(),

                // Get the list of all departments, if needed for a dropdown, etc.
                Departments = await _dbcontext.Departments.ToListAsync(),


                Branchs = await _dbcontext.Branches.ToListAsync(),

                UserFolders = await _dbcontext.UserFolderPermissions.Where(ufp => ufp.UserID == userId).Select(ufp => ufp.Folder).ToListAsync()


            };

            return View(viewModel);
        }



        [HttpPost]
        public async Task<IActionResult> UpdateUserProfile(UserProfileViewModel model)
        {

            // Find the user in the database
            var userInDb = await _dbcontext.Users.FindAsync(model.User.UserID);


            // Update the user's properties with the values from the form
            userInDb.Username = model.User.Username;
            userInDb.Password = model.User.Password; // Make sure to hash the password if needed
            userInDb.DepartmentID = model.User.DepartmentID;
            userInDb.BranchID = model.User.BranchID;
            userInDb.PermissionType = model.User.PermissionType;

            await _dbcontext.SaveChangesAsync();


            // Redirect to the UserProfile page or any other appropriate page
            return RedirectToAction("UserProfile", new { userId = model.User.UserID });
        }



        [HttpPost]
        public async Task<IActionResult> AddFolderForUser(int userId, List<int> selectedFolders)
        {

            // Add selected folders to UserFolderPermission for the new user
            foreach (var folderId in selectedFolders)
            {
                _dbcontext.UserFolderPermissions.Add(new UserFolderPermission
                {
                    UserID = userId,
                    FolderID = folderId
                });
            }
            await _dbcontext.SaveChangesAsync(); // Save the folder permissions

            return RedirectToAction("UserProfile", new { userId = userId });
        }


        [HttpPost]
        public async Task<IActionResult> RemoveFolderFromUser(int userId, int folderId)
        {
            var permission = await _dbcontext.UserFolderPermissions
                                             .FirstOrDefaultAsync(ufp => ufp.UserID == userId && ufp.FolderID == folderId);

            if (permission != null)
            {
                _dbcontext.UserFolderPermissions.Remove(permission);
                await _dbcontext.SaveChangesAsync();
            }

            return RedirectToAction("UserProfile", new { userId = userId });
        }






        public async Task<IActionResult> AddUser()
        {
            var viewModel = new UsersViewModel
            {
                Departments = await _dbcontext.Departments.ToListAsync(),
                Branchs = await _dbcontext.Branches.ToListAsync(),
                Folders = await _dbcontext.Folders.ToListAsync(),
            };
            return View(viewModel);
        }


        [HttpPost]
        public async Task<IActionResult> AddUser(User user, List<int> selectedFolders)
        {
            if (ModelState.IsValid)
            {

                var User = await _dbcontext.Users.FirstOrDefaultAsync(ufp => ufp.Username == user.Username);

                if (User != null)
                {
                    return View(user);
                }


                // Add the new user
                _dbcontext.Users.Add(user);
                await _dbcontext.SaveChangesAsync(); // Save changes to get the new user ID

                int newUserId = user.UserID; // Retrieve the newly created User ID

                // Add selected folders to UserFolderPermission for the new user
                foreach (var folderId in selectedFolders)
                {
                    _dbcontext.UserFolderPermissions.Add(new UserFolderPermission
                    {
                        UserID = newUserId,
                        FolderID = folderId
                    });
                }
                await _dbcontext.SaveChangesAsync(); // Save the folder permissions

                return RedirectToAction("UsersMangement"); // Redirect after successful operation
            }

            // If the model state is not valid, return to the form with the entered data
            // Repopulate any necessary lists for dropdowns or other data required for the form
            ViewData["Departments"] = new SelectList(_dbcontext.Departments, "DepartmentID", "Name", user.DepartmentID);
            ViewData["FolderAccess"] = new SelectList(_dbcontext.Folders, "FolderID", "Name"); // Assuming FolderID and Name are the properties
            return View(user);
        }



        [HttpPost]
        public async Task<IActionResult> RemoveUser(int userId)
        {
            var User = await _dbcontext.Users.FirstOrDefaultAsync(ufp => ufp.UserID == userId);

            if (User != null)
            {

                foreach (var permission in _dbcontext.UserFolderPermissions.Where(ufp => ufp.UserID == userId))
                {
                    _dbcontext.UserFolderPermissions.Remove(permission);
                }

                foreach (var action in _dbcontext.Actions.Where(ufp => ufp.UserID == userId))
                {
                    _dbcontext.Actions.Remove(action);
                }

                _dbcontext.Users.Remove(User);
                await _dbcontext.SaveChangesAsync();
            }

            return RedirectToAction("UsersMangement");
        }




        public async Task<IActionResult> UfReport(int? DepartmentID, int? BranchID)
        {
            IQueryable<User> userQuery = _dbcontext.Users;
            IQueryable<Folder> folderQuery = _dbcontext.Folders;
            IQueryable<Models.Action> actionQuery = _dbcontext.Actions;
            IQueryable<UserFolderPermission> ufpQuery = _dbcontext.UserFolderPermissions;

            // Apply DepartmentID filter if provided
            if (DepartmentID.HasValue && DepartmentID.Value != 0)
            {
                userQuery = userQuery.Where(u => u.DepartmentID == DepartmentID.Value);
            }

            // Apply BranchID filter if provided
            if (BranchID.HasValue && BranchID.Value != 0)
            {
                userQuery = userQuery.Where(u => u.BranchID == BranchID.Value);
            }

            var viewModel = new ReportViewModel
            {
                folders = await folderQuery.CountAsync(),
                users = await userQuery.CountAsync(),
                Uploads = await actionQuery.Where(a => a.ActionPerformed == "Upload").CountAsync(),
                Downloads = await actionQuery.Where(a => a.ActionPerformed == "Download").CountAsync(),
                Users = await userQuery.ToListAsync(),
                Folders = await folderQuery.ToListAsync(),
                UFPS = await ufpQuery.ToListAsync(),
                Actions = await actionQuery.ToListAsync(),
            };

            string htmlContent = await RenderViewAsync("UfReport", viewModel);
            var byteArray = Encoding.UTF8.GetBytes(htmlContent);
            var contentType = "text/html"; // Or "application/pdf" if you're converting to PDF
            var fileName = "report.html"; // Or "report.pdf" for PDF files

            return new FileContentResult(byteArray, contentType)
            {
                FileDownloadName = fileName
            };
        }



        [HttpPost]
        public async Task<IActionResult> AReport(DateTime? specificDate, int? DepartmentID, int? BranchID)
        {
            // If specificDate is null, default to today's date
            var date = specificDate ?? DateTime.Today;

            // Define the start and end of the specific date
            var startDate = date.Date;
            var endDate = date.Date.AddDays(1).AddTicks(-1);

            // Start with all users
            var userQuery = _dbcontext.Users.AsQueryable();

            // Apply DepartmentID filter if provided
            if (DepartmentID.HasValue && DepartmentID.Value != 0)
            {
                userQuery = userQuery.Where(u => u.DepartmentID == DepartmentID.Value);
            }

            // Apply BranchID filter if provided
            if (BranchID.HasValue && BranchID.Value != 0)
            {
                userQuery = userQuery.Where(u => u.BranchID == BranchID.Value);
            }

            // Execute the user query
            var users = await userQuery.ToListAsync();

            // Extract user IDs from the filtered users
            var userIds = users.Select(u => u.UserID).ToList();

            // Query the Actions table for actions performed by these users on the specific date
            var actionsQuery = _dbcontext.Actions
                                           .Where(a => userIds.Contains((int)a.UserID) &&
                                                       a.Timestamp.HasValue &&
                                                       a.Timestamp.Value >= startDate &&
                                                       a.Timestamp.Value <= endDate);

            // Execute the actions query
            var actions = await actionsQuery.ToListAsync();

            var viewModel = new ReportViewModel
            {
                Uploads = await actionsQuery.CountAsync(a => a.ActionPerformed == "Upload"),
                Downloads = await actionsQuery.CountAsync(a => a.ActionPerformed == "Download"),
                Users = await _dbcontext.Users.ToListAsync(),
                Folders = await _dbcontext.Folders.ToListAsync(),
                UFPS = await _dbcontext.UserFolderPermissions.ToListAsync(),
                Actions = actions
            };

            string htmlContent = await RenderViewAsync("AReport", viewModel);
            var byteArray = Encoding.UTF8.GetBytes(htmlContent);
            var contentType = "text/html"; // Or "application/pdf" if you're converting to PDF
            var fileName = "report.html"; // Or "report.pdf" for PDF files

            return new FileContentResult(byteArray, contentType)
            {
                FileDownloadName = fileName
            };
        }


        private async Task<string> RenderViewAsync(string viewName, object model)
        {
            var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };

            using var sw = new StringWriter();
            var viewResult = _viewEngine.FindView(ControllerContext, viewName, false);

            if (viewResult.View == null)
            {
                viewResult = _viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: false);
            }

            if (!viewResult.Success)
            {
                throw new InvalidOperationException($"A view with the name {viewName} could not be found");
            }

            var viewContext = new ViewContext(
                ControllerContext,
                viewResult.View,
                viewData,
                new TempDataDictionary(HttpContext, _tempDataProvider),
                sw,
                new HtmlHelperOptions()
            );

            await viewResult.View.RenderAsync(viewContext);
            return sw.ToString();
        }

    }
}