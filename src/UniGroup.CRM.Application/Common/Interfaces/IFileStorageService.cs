using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Common.Interfaces;

/// <summary>
/// Service for uploading and storing files.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Saves a file to the storage system and returns its accessible URL or path.
    /// </summary>
    /// <param name="fileStream">The stream of the file content.</param>
    /// <param name="fileName">The original filename.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A string representing the storage URL.</returns>
    Task<string> SaveFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken);
}
