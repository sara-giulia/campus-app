using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakePhotoProcessor : IPhotoProcessor
{
    public Result<ProcessedPhoto> Result { get; set; } = new ProcessedPhoto([1, 2, 3, 4], [9, 9]);

    public int CallCount { get; private set; }

    public byte[]? LastContent { get; private set; }

    public Result<ProcessedPhoto> Process(byte[] content)
    {
        CallCount++;
        LastContent = content;
        return Result;
    }
}
