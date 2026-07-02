using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;

namespace UniGroup.CRM.Infrastructure.Services;

/// <summary>
/// Service to store files locally in the wwwroot/uploads directory.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _uploadFolder;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileStorageService"/> class.
    /// </summary>
    public LocalFileStorageService()
    {
        _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
    }

    /// <inheritdoc />
    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_uploadFolder))
        {
            Directory.CreateDirectory(_uploadFolder);
        }

        // Create a unique filename to avoid overwrites
        var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var filePath = Path.Combine(_uploadFolder, uniqueFileName);

        using (var destinationStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await fileStream.CopyToAsync(destinationStream, cancellationToken);
        }

        // Return the relative path to be stored in the database
        return $"/uploads/{uniqueFileName}";
    }
}
