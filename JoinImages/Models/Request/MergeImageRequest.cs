using System.ComponentModel.DataAnnotations;

namespace JoinImages.Models.Request;

public class MergeImageRequest
{
    [Required]
    public string FileName { get; set; }
    [Required]
    public IEnumerable<Base64File> Images { get; set; }
    public MergeType MergeType { get; set; } = MergeType.Landscape;
    public ResizeImageType ResizeIType { get; set; } = ResizeImageType.NoResize;
}

public enum MergeType
{
    Landscape,
    Portrait
}

public enum ResizeImageType
{
    NoResize,
    ResizeToSmallest,
    ResizeToLargest,
    ResizeBetweenSmallestAndLargest
}