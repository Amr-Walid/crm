using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Common.Interfaces;

/// <summary>
/// Service to generate consecutive ticket numbers in the format T-YYYY-00001.
/// </summary>
public interface ITicketNumberGenerator
{
    /// <summary>
    /// Generates a unique, consecutive ticket identifier.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A string representing the generated ticket number.</returns>
    Task<string> GenerateAsync(CancellationToken cancellationToken);
}
