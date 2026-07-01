using MediatR;
using Microsoft.AspNetCore.Identity;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Auth.Common;
using UniGroup.CRM.Domain.Entities;

namespace UniGroup.CRM.Application.Features.Auth.Commands.Login;

/// <summary>
/// Command to login a user and retrieve authorization tokens.
/// </summary>
public record LoginCommand(
    string Email,
    string Password,
    string IpAddress
) : IRequest<AuthResponse>;

/// <summary>
/// Handler for executing the login command.
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtProvider _jwtProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginCommandHandler"/> class.
    /// </summary>
    public LoginCommandHandler(
        UserManager<ApplicationUser> userManager,
        IJwtProvider jwtProvider)
    {
        _userManager = userManager;
        _jwtProvider = jwtProvider;
    }

    /// <summary>
    /// Validates credentials, generates JWT access tokens, and issues refresh tokens.
    /// </summary>
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
        {
            throw new Exception("Invalid credentials.");
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            throw new Exception("Invalid credentials.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            roles.Add("User");
        }

        var tokenResult = _jwtProvider.GenerateToken(user, roles);
        var refreshToken = _jwtProvider.GenerateRefreshToken(request.IpAddress);

        user.RefreshTokens.Add(refreshToken);
        await _userManager.UpdateAsync(user);

        return new AuthResponse(
            Token: tokenResult.Token,
            TokenExpiration: tokenResult.Expiration,
            RefreshToken: refreshToken.Token,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName,
            LastName: user.LastName
        );
    }
}
