using MediatR;
using Microsoft.AspNetCore.Identity;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Auth.Common;
using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Application.Features.Auth.Commands.Register;

/// <summary>
/// Command to register a new user in the CRM system.
/// </summary>
public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string IpAddress
) : IRequest<AuthResponse>;

/// <summary>
/// Handler for executing the registration command.
/// </summary>
public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtProvider _jwtProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterCommandHandler"/> class.
    /// </summary>
    public RegisterCommandHandler(
        UserManager<ApplicationUser> userManager,
        IJwtProvider jwtProvider)
    {
        _userManager = userManager;
        _jwtProvider = jwtProvider;
    }

    /// <summary>
    /// Handles user registration, JWT generation, and initial refresh token creation.
    /// </summary>
    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new Exception("Email is already in use.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception($"User registration failed: {errors}");
        }

        var roles = new List<string> { "User" };

        var tokenResult = _jwtProvider.GenerateToken(user, roles);
        var refreshToken = _jwtProvider.GenerateRefreshToken(request.IpAddress);

        user.RefreshTokens.Add(refreshToken);
        await _userManager.UpdateAsync(user);

        return new AuthResponse(
            Token: tokenResult.Token,
            TokenExpiration: tokenResult.Expiration,
            RefreshToken: refreshToken.Token,
            Email: user.Email,
            FirstName: user.FirstName,
            LastName: user.LastName
        );
    }
}
