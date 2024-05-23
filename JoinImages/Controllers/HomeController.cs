using JoinImages.Extensions;
using JoinImages.Models.Request;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace JoinImages.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;

        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("MergeImages")]
        public async Task<IActionResult> MergeImagesAsync([FromBody] MergeImageRequest request)
        {
            var validImageFormats = new List<string>() { "jpg", "jpeg", "png", "bmp", "gif", "pbm", "tiff", "tga", "webp" }; // Formats suported by SixLabors.ImageSharp
            try
            {
                var uploadFolderPath = _configuration.GetSection("UploadFolderPath").Value;

                #region Validations

                if (string.IsNullOrEmpty(uploadFolderPath))
                    throw new Exception("UploadFolderPath not configured.");

                if (uploadFolderPath.ToUpper() != "%TEMP%")
                {
                    try
                    {
                        Path.GetFullPath(uploadFolderPath);
                    }
                    catch (Exception e)
                    {
                        throw new Exception("UploadFolderPath is invalid.");
                    }
                }
                else
                {
                    uploadFolderPath = Path.GetTempPath();
                }

                if (request.Images.Count() <= 1)
                {
                    throw new Exception("More the one image shound be provided.");
                }

                foreach (var image in request.Images)
                {
                    if (string.IsNullOrEmpty(image.Base64Data) || !image.Base64Data.IsBase64Encoded())
                        throw new Exception("Some image is not a valid");
                    var extension = image.FileName.Substring(image.FileName.LastIndexOf("."),
                        image.FileName.Length - image.FileName.LastIndexOf(".")).Replace(".", "");
                    if (!validImageFormats.Contains(extension))
                        throw new Exception("Some image is invalid format.");
                }

                #endregion

                if (!uploadFolderPath.EndsWith(Path.DirectorySeparatorChar))
                    uploadFolderPath = $"{uploadFolderPath}{Path.DirectorySeparatorChar}";

                if (!Directory.Exists(uploadFolderPath))
                    Directory.CreateDirectory(uploadFolderPath);

#if DEBUG
                var newFileName = $"{request.FileName.Substring(0, request.FileName.LastIndexOf("."))} {request.MergeType} {request.ResizeIType}.png".NormalizeFileName();
#else
                var newFileName = $"{request.FileName.Substring(0, request.FileName.LastIndexOf("."))} {DateTime.Now.ToString("O")}.png".NormalizeFileName();
#endif

                var filePath = $"{uploadFolderPath}{newFileName}";

                var pngImages = new Dictionary<string, Image<Rgba32>>();

                foreach (var imageData in request.Images)
                {
                    if (!imageData.FileName.EndsWith(".png"))
                    {
                        #region Create temp image files normalized imagens to PNG
                        var convertedFilePath = $"{Path.GetTempPath()}{Guid.NewGuid()}.png";
                        var originalFilePath = $"{Path.GetTempPath()}{imageData.FileName}";
                        imageData.Base64Data.SaveBase64AsFile(originalFilePath);
                        using (Image image = Image.Load(originalFilePath))
                        {
                            image.SaveAsPng(convertedFilePath);
                            pngImages.Add(convertedFilePath, Image.Load<Rgba32>(convertedFilePath));
                        }
                        System.IO.File.Delete(originalFilePath);
                        #endregion
                    }
                    else
                    {
                        var savedFilePath = $"{Path.GetTempPath()}{Guid.NewGuid()}.png";
                        imageData.Base64Data.SaveBase64AsFile(savedFilePath);
                        pngImages.Add(savedFilePath, Image.Load<Rgba32>(savedFilePath));
                    }
                }

                #region Define the Merged Image Width and Height

                var mergedImageWidth = 0;
                var mergedImageHeight = 0;

                switch (request.ResizeIType)
                {
                    case ResizeImageType.ResizeToSmallest:
                        switch (request.MergeType)
                        {
                            case MergeType.Portrait:
                                mergedImageWidth = pngImages.Min(x => x.Value.Width);
                                break;
                            default:
                                mergedImageHeight = pngImages.Min(x => x.Value.Height);
                                break;
                        }
                        break;
                    case ResizeImageType.ResizeBetweenSmallestAndLargest:
                        var mediumWidth = Math.Round((pngImages.Min(x => x.Value.Width) + pngImages.Max(x => x.Value.Width)) / 2M, 0) ;
                        var mediumHeight = Math.Round((pngImages.Min(y => y.Value.Height) + pngImages.Max(y => y.Value.Height)) / 2M, 0);
                        switch (request.MergeType)
                        {
                            case MergeType.Portrait:
                                mergedImageWidth = (int) mediumWidth;
                                break;
                            default:
                                mergedImageHeight = (int) mediumHeight;
                                break;
                        }
                        break;
                    default:
                        switch (request.MergeType)
                        {
                            case MergeType.Portrait:
                                mergedImageWidth = pngImages.Max(x => x.Value.Width);
                                mergedImageHeight = pngImages.Sum(x => x.Value.Height);
                                break;
                            default:
                                mergedImageWidth = pngImages.Sum(x => x.Value.Width);
                                mergedImageHeight = pngImages.Max(x => x.Value.Height);
                                break;
                        }
                        break;
                }

                #endregion

                #region Resize images if requested

                if (request.ResizeIType != ResizeImageType.NoResize)
                {
                    switch (request.MergeType)
                    {
                        case MergeType.Portrait:
                            foreach (var pngImage in pngImages.Where(x => x.Value.Width != mergedImageWidth))
                            {
                                var newProportionalHeight = (mergedImageWidth * pngImage.Value.Height) / pngImage.Value.Width;

                                pngImage.Value.Mutate(o => o.Resize(new Size(mergedImageWidth, newProportionalHeight)));
                            }

                            mergedImageHeight = pngImages.Sum(x => x.Value.Height);
                            break;
                        default:
                            foreach (var pngImage in pngImages.Where(x => x.Value.Height != mergedImageHeight))
                            {
                                var newProportionalWidth = (mergedImageHeight * pngImage.Value.Width) / pngImage.Value.Height;
                                pngImage.Value.Mutate(o => o.Resize(new Size(newProportionalWidth, mergedImageHeight)));
                            }

                            mergedImageWidth = pngImages.Sum(x => x.Value.Width);
                            break;
                    }
                }

                #endregion


                #region Merge the images and save to the desired path

                using (Image<Rgba32> outputImage = new Image<Rgba32>(mergedImageWidth, mergedImageHeight)) // create output image of the correct dimensions
                {
                    var positionX = 0;
                    var positionY = 0;
                    // take source images and draw them onto the new image
                    foreach (var pngImage in pngImages)
                    {
                        outputImage.Mutate(o => o.DrawImage(pngImage.Value, new Point(positionX, positionY), 1f));

                        switch (request.MergeType)
                        {
                            case MergeType.Portrait:
                                positionY += pngImage.Value.Height;
                                break;
                            default:
                                positionX += pngImage.Value.Width;
                                break;
                        }
                    }

                    outputImage.SaveAsPng(filePath);
                }

                foreach (var pngImage in pngImages)
                {
                    pngImage.Value.Dispose();
                    System.IO.File.Delete(pngImage.Key);
                }

                #endregion


                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest("Could not merge images");
            }
        }
    }
}