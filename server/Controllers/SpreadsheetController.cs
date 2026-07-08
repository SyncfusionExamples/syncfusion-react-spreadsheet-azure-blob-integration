using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Syncfusion.EJ2.Spreadsheet;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SpreadsheetController : ControllerBase
    {
        //Read Azure Blob Storage settings from configuration
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _storageContainerName;
        private readonly ILogger<SpreadsheetController> _logger;
        // Constructor for spreadsheetController
        public SpreadsheetController(IConfiguration configuration, BlobServiceClient blobServiceClient, ILogger<SpreadsheetController> logger)
        {
            // Store the Blob Service client used to access Azure Blob Storage.
            _blobServiceClient = blobServiceClient;
            // Store the logger for error tracking and diagnostics.
            _logger = logger;
            // Retrieve the target container name from application configuration.
            _storageContainerName = configuration.GetValue<string>("containerName");
            // Validate that a container name has been configured.
            if (string.IsNullOrEmpty(_storageContainerName))
            {
                throw new InvalidOperationException("Configuration 'containerName' is missing or empty.");
            }
        }

        [HttpPost]
        [Route("OpenFromAzure")]
        public async Task<IActionResult> OpenFromAzure([FromBody] FileOptions options)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    string fileName = options.FileName + options.Extension;
                    // Get a reference to the configured Azure Blob Storage container
                    BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(_storageContainerName);
                    // Get a reference to the target Excel file (blob) within the container.
                    BlockBlobClient blockBlobClient = containerClient.GetBlockBlobClient(fileName);
                    // Validate file existence
                    if (!await blockBlobClient.ExistsAsync())
                    {
                        return NotFound("File not found.");
                    }
                    // Download file from Azure Blob Storage
                    await blockBlobClient.DownloadToAsync(stream);
                    stream.Position = 0;

                    // Wrap stream as FormFile
                    OpenRequest open = new OpenRequest
                    {
                        File = new FormFile(stream, 0, stream.Length, options.FileName, fileName)
                    };

                    // Convert Excel file to JSON
                    var result = Workbook.Open(open);

                    return Content(result, "application/json");
                }
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Failed to load spreadsheet from Azure Blob Storage.");
                // Return an error response with the exception message.
                return StatusCode(StatusCodes.Status500InternalServerError, "Error occurred while processing the file.");
            }
        }

        // Receives file details from client
        public class FileOptions
        {
            public string FileName { get; set; } = string.Empty;
            public string Extension { get; set; } = string.Empty;
        }

        [HttpPost]
        [Route("SaveToAzure")]
        public async Task<IActionResult> SaveToAzure([FromForm] SaveSettings saveSettings)
        {
            try
            {
                // Convert spreadsheet JSON to Excel file stream
                Stream fileStream = Workbook.Save<Stream>(saveSettings);
                fileStream.Position = 0; // Reset stream for upload
                // Get the target Blob Storage container.
                BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(_storageContainerName);
                // Create the output file name.
                string blobName = saveSettings.FileName + "." + saveSettings.SaveType.ToString().ToLower();
                // Get a reference to the destination blob.
                BlobClient blobClient = containerClient.GetBlobClient(blobName);
                // Upload the Excel file stream to Azure Blob Storage
                await blobClient.UploadAsync(fileStream, overwrite: true);
                // Return success message
                return Ok("Excel file successfully saved to Azure Blob Storage.");
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Failed to save spreadsheet to Azure Blob Storage.");
                // Return an error response with the exception message.
                return BadRequest("Error saving file to Azure Blob Storage: " + ex.Message);
            }
        }

        [HttpPost]
        [Route("Open")]
        public IActionResult Open([FromForm] IFormCollection openRequest)
        {
            OpenRequest open = new OpenRequest();
            if (openRequest.Files.Count != 0)
            {
                open.File = openRequest.Files[0];
                if (openRequest.ContainsKey("IsManualCalculationEnabled") && bool.TryParse( openRequest["IsManualCalculationEnabled"].ToString(), out bool flag))
                {
                    open.IsManualCalculationEnabled = flag;
                }
            }
            open.Password = openRequest["Password"];
            if (openRequest["SheetIndex"].Count != 0)
            {
                open.SheetIndex = int.Parse(openRequest["SheetIndex"].ToString());
            }
            open.SheetPassword = openRequest["SheetPassword"];
            return Content(Workbook.Open(open));
        }

        [HttpPost]
        [Route("Save")]
        public IActionResult Save([FromForm] SaveSettings saveSettings)
        {
            return Workbook.Save(saveSettings);
        }
    }
}