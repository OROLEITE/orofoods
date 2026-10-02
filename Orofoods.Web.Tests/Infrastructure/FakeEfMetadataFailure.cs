namespace Microsoft.EntityFrameworkCore.Metadata;

internal static class FakeEfMetadataFailure
{
    public static void CreateModel() => throw new BadImageFormatException("Bad IL range.");
}
