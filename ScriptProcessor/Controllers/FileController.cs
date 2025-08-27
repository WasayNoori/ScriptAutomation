using Microsoft.AspNetCore.Mvc;
using ScriptProcessor.Services;

namespace ScriptProcessor.Controllers
{
    public class FileController : Controller
    {
        private readonly IFileService _fileService;
        private readonly ILogger<FileController> _logger;

        public FileController(IFileService fileService, ILogger<FileController> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> List()
        {
            try
            {
                var files = await _fileService.ListFilesAsync();
                return View(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files");
                TempData["ErrorMessage"] = $"Error loading files: {ex.Message}";
                return View(new List<ScriptProcessor.Services.BlobFileInfo>());
            }
        }

        public async Task<IActionResult> View(string blobName)
        {
            if (string.IsNullOrEmpty(blobName))
            {
                return NotFound();
            }

            try
            {
                var fileDetails = await _fileService.GetFileDetailsAsync(blobName);
                return View(fileDetails);
            }
            catch (FileNotFoundException)
            {
                return NotFound($"File '{blobName}' not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading file details for {BlobName}", blobName);
                TempData["ErrorMessage"] = $"Error loading file: {ex.Message}";
                return RedirectToAction("List");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, string scriptName, string author, int languageId = 1)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("file", "Please select a file to upload.");
                return (IActionResult)View("Index");
            }

            if (!file.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("file", "Only .txt files are allowed.");
                return (IActionResult)View("Index");
            }

            if (string.IsNullOrWhiteSpace(scriptName))
            {
                ModelState.AddModelError("scriptName", "Script name is required.");
                return (IActionResult)View("Index");
            }

            if (string.IsNullOrWhiteSpace(author))
            {
                ModelState.AddModelError("author", "Author is required.");
                return (IActionResult)View("Index");
            }

            try
            {
                var scriptId = Guid.NewGuid();
                var artifactName = file.FileName;

                using var stream = file.OpenReadStream();
                var result = await _fileService.UploadAsync(
                    scriptId,
                    artifactName,
                    stream,
                    "text/plain",
                    languageId);

                _logger.LogInformation("Successfully uploaded file {FileName} for script {ScriptId}", file.FileName, scriptId);

                ViewBag.SuccessMessage = $"File '{file.FileName}' uploaded successfully. Script ID: {scriptId}";
                ViewBag.UploadResult = result;

                return (IActionResult)View("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {FileName}", file.FileName);
                ModelState.AddModelError("", $"Upload failed: {ex.Message}");
                return (IActionResult)View("Index");
            }
        }
    }
}
