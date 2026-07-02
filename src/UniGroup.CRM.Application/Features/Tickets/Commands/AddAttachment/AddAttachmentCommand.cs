using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Application.Features.Tickets.Commands.AddAttachment;

/// <summary>
/// Command to upload and attach a file to a ticket.
/// </summary>
public record AddAttachmentCommand(
    string TicketId,
    string FileName,
    string ContentType,
    Stream FileContent,
    long FileSizeBytes,
    Guid UploadedById
) : IRequest<Guid>;

/// <summary>
/// Handler for executing the add attachment command.
/// </summary>
public class AddAttachmentCommandHandler : IRequestHandler<AddAttachmentCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddAttachmentCommandHandler"/> class.
    /// </summary>
    public AddAttachmentCommandHandler(IApplicationDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    /// <inheritdoc />
    public async Task<Guid> Handle(AddAttachmentCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Ticket exists
        var ticketExists = await _context.Tickets.AnyAsync(t => t.Id == request.TicketId, cancellationToken);
        if (!ticketExists)
        {
            throw new Exception($"Ticket with ID {request.TicketId} does not exist.");
        }

        // 2. Validate file size (Max 10MB)
        const long maxSizeBytes = 10 * 1024 * 1024; // 10MB
        if (request.FileSizeBytes > maxSizeBytes)
        {
            throw new Exception("File size exceeds the maximum limit of 10MB.");
        }

        // 3. Validate file extension (Images & PDFs only)
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };
        if (!allowedExtensions.Contains(extension))
        {
            throw new Exception("Invalid file type. Only JPG, JPEG, PNG, GIF, and PDF files are allowed.");
        }

        // 4. Save file to storage
        var storageUrl = await _fileStorageService.SaveFileAsync(request.FileContent, request.FileName, cancellationToken);

        // 5. Create Attachment entity
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            TicketId = request.TicketId,
            UploadedById = request.UploadedById,
            FileName = request.FileName,
            StorageUrl = storageUrl,
            FileSizeBytes = request.FileSizeBytes,
            ContentType = request.ContentType,
            CreatedAt = DateTime.UtcNow
        };

        _context.Attachments.Add(attachment);

        // Update Ticket's UpdatedAt timestamp
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket != null)
        {
            ticket.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return attachment.Id;
    }
}
