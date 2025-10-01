using Microsoft.AspNetCore.Mvc;
using ScriptProcessor.Services;

namespace ScriptProcessor.Controllers
{
    public class FileController : Controller
    {
        private readonly IFileService _fileService;
        private readonly IApiService _apiService;
        private readonly GlossaryDBService _glossaryService;
        private readonly ILogger<FileController> _logger;

        public FileController(IFileService fileService, IApiService apiService, GlossaryDBService glossaryService, ILogger<FileController> logger)
        {
            _fileService = fileService;
            _apiService = apiService;
            _glossaryService = glossaryService;
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

        public async Task<IActionResult> ViewFile(string blobName)
        {
            if (string.IsNullOrEmpty(blobName))
            {
                return NotFound();
            }

            try
            {
                var fileDetails = await _fileService.GetFileDetailsAsync(blobName);

                // Check if there's translated content from a recent translation
                var translatedContent = TempData["TranslatedContent"]?.ToString();

                // Create a new instance with the translated content if available
                if (!string.IsNullOrEmpty(translatedContent))
                {
                    fileDetails = fileDetails with { TranslatedContent = translatedContent };
                }

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
            // Validation checks - these can return immediately
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("file", "Please select a file to upload.");
                return View("Index");
            }

            if (!file.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("file", "Only .txt files are allowed.");
                return View("Index");
            }

            if (string.IsNullOrWhiteSpace(scriptName))
            {
                ModelState.AddModelError("scriptName", "Script name is required.");
                return View("Index");
            }

            if (string.IsNullOrWhiteSpace(author))
            {
                ModelState.AddModelError("author", "Author is required.");
                return View("Index");
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

                return View("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {FileName}", file.FileName);
                ModelState.AddModelError("", $"Upload failed: {ex.Message}");
                return View("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessFile(string selectedFile, string targetLanguage, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(selectedFile) || string.IsNullOrEmpty(targetLanguage))
            {
                TempData["ErrorMessage"] = "Please select both a file and target language.";
                return RedirectToAction("List");
            }

            try
            {
                var glossary = new Dictionary<string, string>();

                // Get the full blob URL instead of just the blob name
                var fullBlobUrl = await _fileService.GetBlobUrlAsync(selectedFile);

                var request = new TranslateRequest
                {
                    BlobPath = fullBlobUrl,
                    Glossary = glossary
                };

                _logger.LogInformation("Processing file {FileName} for translation to {Language}", selectedFile, targetLanguage);

                var response = await _apiService.TranslateAsync(request);

                if (response.Success)
                {
                    // Store the translated content in TempData to display in the Translation tab
                    TempData["TranslatedContent"] = response.TranslatedContent;
                    TempData["SuccessMessage"] = $"File successfully processed for {targetLanguage} translation.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Processing failed: {response.Message}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file {FileName} for {Language} translation", selectedFile, targetLanguage);
                TempData["ErrorMessage"] = $"Error processing file: {ex.Message}";
            }

            // If called from ViewFile, redirect back to ViewFile, otherwise go to List
            if (Request.Headers.Referer.ToString().Contains("ViewFile"))
            {
                return RedirectToAction("ViewFile", new { blobName = selectedFile });
            }

            return RedirectToAction("List");
        }

        [HttpPost]
        public async Task<IActionResult> GetGlossary([FromBody] GlossaryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.ScriptText))
            {
                return BadRequest(new { success = false, message = "Script text is required." });
            }

            try
            {
                var foundTerms = await _glossaryService.SelectedWordsWithCounts(request.ScriptText, request.TargetLanguage ?? "french");

                _logger.LogInformation("Found {TermCount} glossary terms in script text", foundTerms.Count);

                return Json(new {
                    success = true,
                    terms = foundTerms.Select(kvp => new {
                        englishTerm = kvp.Key,
                        translation = kvp.Value.translation,
                        count = kvp.Value.count
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving glossary terms");
                return Json(new { success = false, message = $"Error retrieving glossary terms: {ex.Message}" });
            }
        }
    }

    public class GlossaryRequest
    {
        public string ScriptText { get; set; } = string.Empty;
        public string TargetLanguage { get; set; } = "french";
    }
}
