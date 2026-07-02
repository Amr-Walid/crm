using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;

namespace UniGroup.CRM.Infrastructure.Services;

/// <summary>
/// Service to generate consecutive ticket numbers in the format T-YYYY-00001.
/// </summary>
public class TicketNumberGenerator : ITicketNumberGenerator
{
    private readonly IApplicationDbContext _context;
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketNumberGenerator"/> class.
    /// </summary>
    /// <param name="context">The application database context.</param>
    public TicketNumberGenerator(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"T-{year}-";

            // Find the ticket with the highest sequence number for the current year
            var maxTicketId = await _context.Tickets
                .Where(t => t.Id.StartsWith(prefix))
                .Select(t => t.Id)
                .OrderByDescending(id => id)
                .FirstOrDefaultAsync(cancellationToken);

            int nextSeq = 1;
            if (!string.IsNullOrEmpty(maxTicketId) && maxTicketId.Length >= prefix.Length)
            {
                var seqStr = maxTicketId.Substring(prefix.Length);
                if (int.TryParse(seqStr, out int currentMax))
                {
                    nextSeq = currentMax + 1;
                }
            }

            return $"{prefix}{nextSeq:D5}";
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
