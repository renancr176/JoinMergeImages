using System.ComponentModel.DataAnnotations;

namespace JoinImages.Models.Request;

public class Base64File
{
    [Required]
    public string FileName { get; set; }
    [Required]
    public string MimeType { get; set; }
    [Required]
    public string Base64Data { get; set; }
}