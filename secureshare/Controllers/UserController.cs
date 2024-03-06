using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using secureshare.Models;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using secureshare.ViewModels;
using ImageMagick;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static NuGet.Packaging.PackagingConstants;

namespace secureshare.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;
        private readonly secureshareContext _dbContext;

        public UserController(ILogger<UserController> logger, secureshareContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public IActionResult Index()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier); // Adjust the claim type if necessary

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                // Handle the error, maybe log it and redirect to an error page or login page
                _logger.LogError("UserId claim is missing or invalid.");
                return RedirectToAction("Login", "Auth"); // Redirect to the login page or an error page
            }

            // Query the database to get the user's folder permissions
            var userPermissions = _dbContext.UserFolderPermissions.Where(fp => fp.UserID == userId).ToList();


            // Get all folders from the database
            var allFolders = _dbContext.Folders.ToList();

            // Filter the folders based on the user's permissions
            var folders = allFolders
                .Where(folder => userPermissions.Any(up => up.FolderID == folder.FolderID))
                .Select(folder => new FolderViewModel
                {
                    FolderName = folder.Name ?? "Unnamed Folder",
                    FolderPath = folder.FolderPath ?? "Unknown Path",
                    FileCount = folder.FileCount.HasValue ? folder.FileCount.Value : 0
                })
                .ToList();


            return View(folders);
        }

        public IActionResult FolderContent(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Path.IsPathRooted(folderPath) || !Directory.Exists(folderPath))
            {
                return RedirectToAction("Index");
            }

            int userPermissionType = 0; // Provide an initial value

            var userId = HttpContext.Session.GetInt32("UserId");

            var user = _dbContext.Users.FirstOrDefault(u => u.UserID == userId);

            if (user != null && user.PermissionType.HasValue)
            {
                userPermissionType = (int)user.PermissionType;
            }

            var files = Directory.GetFiles(folderPath);
            var subFolders = Directory.GetDirectories(folderPath);

            var folderContent = new FolderContentViewModel
            {
                FolderPath = folderPath,
                Files = files,
                SubFolders = subFolders,
                UserPermissionType = userPermissionType
            };

            return View(folderContent);
        }



        public IActionResult DownloadFileWithWatermark(string filePath)
        {

            // Get the user ID from the claims
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                // Handle error - User ID claim is missing or invalid
                return RedirectToAction("Error"); // Redirect to an error page or handle it as needed
            }


            LogAction(userId, "Download");

            // Query the database for the user's details
            var user = _dbContext.Users.FirstOrDefault(u => u.UserID == userId);
            if (user == null)
            {
                // Handle error - User not found
                return RedirectToAction("Error"); // Redirect to an error page or handle it as needed
            }

            // Construct watermark text with user details
            string watermarkText = $"{user.Username}";

            if (System.IO.File.Exists(filePath))
            {
                if (IsImageFile(filePath))
                {
                    using (MagickImage image = new MagickImage(filePath))
                    {
                        MagickImage watermark = new MagickImage(new MagickColor("#809F2000"), image.Width, image.Height);

                        int alphaValue = 128; // Adjust this value between 0 (transparent) and 255 (opaque)

                        // Define gray color values
                        ushort grayValue = 0x80; // This is an example gray value, you can adjust it

                        // Create a gray color with the desired opacity
                        MagickColor semiTransparentGray = new MagickColor((ushort)alphaValue, grayValue, grayValue, grayValue);

                        // Use the semi-transparent gray color for the watermark fill color
                        watermark.Settings.FillColor = semiTransparentGray;

                        // Other settings remain the same
                        watermark.Settings.FontPointsize = 120;
                        watermark.Read($"caption:{watermarkText}");

                        // Calculate the angle for diagonal alignment
                        double angleInRadians = Math.Atan2(image.Height, image.Width);
                        double angleInDegrees = angleInRadians * (180 / Math.PI);

                        // Rotate the watermark
                        watermark.Rotate(angleInDegrees);

                        // Calculate the offset for watermark placement (to center it)
                        int offsetX = (image.Width - watermark.Width) / 1;
                        int offsetY = (image.Height - watermark.Height) / 100;

                        // Composite the watermark onto the image at the calculated offset
                        image.Composite(watermark, offsetX, offsetY, CompositeOperator.Over);

                        byte[] fileBytes = image.ToByteArray();
                        return File(fileBytes, "image/jpeg", Path.GetFileName(filePath));
                    }
                }
                else if (IsPdfFile(filePath))
                {
                    try
                    {
                        PdfDocument document = PdfReader.Open(filePath, PdfDocumentOpenMode.Modify);

                        foreach (PdfPage page in document.Pages)
                        {
                            XGraphics gfx = XGraphics.FromPdfPage(page);

                            // Calculate the angle for diagonal alignment
                            double angleInRadians = Math.Atan2(page.Height.Point, page.Width.Point);
                            double angleInDegrees = angleInRadians * (180 / Math.PI);

                            // Create a font size that covers the diagonal length of the page
                            double diagonalLength = Math.Sqrt(Math.Pow(page.Width.Point, 2) + Math.Pow(page.Height.Point, 2));
                            double fontSize = diagonalLength / 15; // Adjust the divisor for font size

                            // Define the font
                            XFont font = new XFont("Arial", fontSize); // Adjust font family and size as needed

                            // Calculate the text width and height
                            double textWidth = gfx.MeasureString(watermarkText, font).Width;
                            double textHeight = gfx.MeasureString(watermarkText, font).Height;

                            // Calculate the offset for centering the text diagonally
                            double offsetX = (page.Width.Point - textWidth) / 2 + 80; // Adjust this value to shift the watermark to the right
                            double offsetY = (page.Height.Point - textHeight) / 8;

                            // Apply rotation transformation
                            gfx.TranslateTransform(offsetX, offsetY);
                            gfx.RotateTransform(angleInDegrees);

                            double opacity = 0.4; // Adjust this value between 0.0 (transparent) and 1.0 (opaque)

                            // Create a color with the desired opacity
                            XColor colorWithOpacity = XColor.FromArgb((int)(opacity * 255), XColors.Gray);

                            // Create a brush with the color having the specified opacity
                            XBrush semiTransparentBrush = new XSolidBrush(colorWithOpacity);

                            // Use the semi-transparent brush for drawing the string
                            gfx.DrawString(watermarkText, font, semiTransparentBrush, 0, 0);

                        }

                        MemoryStream stream = new MemoryStream();
                        document.Save(stream, false);
                        stream.Seek(0, SeekOrigin.Begin);

                        return File(stream.ToArray(), "application/pdf", Path.GetFileName(filePath));
                    }
                    catch (PdfSharpCore.Pdf.IO.PdfReaderException ex)
                    {
                        // Log the error and handle the exception
                        _logger.LogError(ex, "PDF is password protected and cannot be modified.");

                        // Provide a regular download without watermark
                        var fileBytes = System.IO.File.ReadAllBytes(filePath);
                        return File(fileBytes, "application/pdf", Path.GetFileName(filePath));
                    }
                }
                else
                {
                    // For other file types, proceed with regular download (without watermark)
                    var fileBytes = System.IO.File.ReadAllBytes(filePath);
                    return File(fileBytes, "application/octet-stream", Path.GetFileName(filePath));
                }
            }

            
            

            return RedirectToAction("Index");
        }

        private bool IsImageFile(string filePath)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            string extension = Path.GetExtension(filePath);
            return imageExtensions.Contains(extension.ToLower());
        }

        private bool IsPdfFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return extension.ToLower() == ".pdf";
        }


        public IActionResult UploadFile(string folderPath, IFormFile file)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdClaim, out int userId))
            {
                LogAction(userId, "Upload");
            }

            if (string.IsNullOrWhiteSpace(folderPath) || !Path.IsPathRooted(folderPath))
            {
                return RedirectToAction("Index");
            }

            if (file != null && file.Length > 0)
            {
                string filePath = Path.Combine(folderPath, file.FileName);

                // Check if the file exists and rename it if necessary
                int counter = 1;
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);
                string extension = Path.GetExtension(file.FileName);
                while (System.IO.File.Exists(filePath))
                {
                    string tempFileName = $"{fileNameWithoutExtension}{counter}{extension}";
                    filePath = Path.Combine(folderPath, tempFileName);
                    counter++;
                }

                // Save the file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(fileStream);
                }

                return RedirectToAction("FolderContent", new { folderPath = folderPath });
            }

            return RedirectToAction("FolderContent", new { folderPath = folderPath });
        }



        private void LogAction(int userId, string actionPerformed)
        {
            var action = new Models.Action
            {
                UserID = userId,
                ActionPerformed = actionPerformed,
                Timestamp = DateTime.Now
            };

            _dbContext.Actions.Add(action);
            _dbContext.SaveChanges();
        }


    }
}
