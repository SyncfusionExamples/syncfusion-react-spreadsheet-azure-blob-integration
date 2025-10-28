using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Syncfusion.EJ2.Spreadsheet;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SpreadsheetController : ControllerBase
    {
        private readonly string _storageConnectionString;
        private readonly string _storageContainerName;
        public SpreadsheetController(IConfiguration configuration)
        {
            _storageConnectionString = configuration.GetValue<string>("connectionString");
            _storageContainerName = configuration.GetValue<string>("containerName");
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

                    // Connect to Azure Blob Storage
                    BlobServiceClient blobServiceClient = new BlobServiceClient(_storageConnectionString);
                    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_storageContainerName);
                    BlockBlobClient blockBlobClient = containerClient.GetBlockBlobClient(fileName);

                    // Download file into memory
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
                Console.WriteLine($"Error: {ex.Message}");
                return Content("Error occurred while processing the file.");
            }
        }

        // To receive file details from the client.
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

                // Define Azure Blob Storage client
                BlobServiceClient blobServiceClient = new BlobServiceClient(_storageConnectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_storageContainerName);

                // Define blob name using file name and save type
                string blobName = saveSettings.FileName + "." + saveSettings.SaveType.ToString().ToLower();
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                // Upload the Excel file stream to Azure Blob Storage
                await blobClient.UploadAsync(fileStream, overwrite: true);

                // Return success message
                return Ok("Excel file successfully saved to Azure Blob Storage.");
            }
            catch (Exception ex)
            {
                // Handle errors and return message
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
                if (openRequest.ContainsKey("IsManualCalculationEnabled") && bool.TryParse(openRequest["IsManualCalculationEnabled"].ToString(), out bool flag))
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

