using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Auth;

public sealed record RegisterCommand(string Email, string Password, string FullName, string WorkspaceName) : IRequest<AuthResponseDto>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Password).NotEmpty().MinimumLength(8);
        RuleFor(c => c.FullName).NotEmpty();
        RuleFor(c => c.WorkspaceName).NotEmpty();
    }
}

/// <summary>
/// Registration provisions two records in one transaction-scoped save: a fresh
/// <see cref="Workspace"/> for the new account, and the registering user as that
/// workspace's <see cref="Role.WorkspaceAdmin"/> — see TASK-015 AC3. There is
/// deliberately no separate "create workspace" flow yet; every account gets exactly
/// one workspace, created here.
/// </summary>
public sealed class RegisterCommandHandler(
    IApplicationDbContext context, IPasswordHasher passwordHasher, ITokenService tokenService, IMapper mapper, IAuditLogger auditLogger)
    : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailTaken = await context.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (emailTaken)
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var workspace = new Workspace(request.WorkspaceName, normalizedEmail);
        context.Workspaces.Add(workspace);

        var user = new User(normalizedEmail, passwordHasher.Hash(request.Password), request.FullName, Role.WorkspaceAdmin, workspace.Id);
        context.Users.Add(user);

        await context.SaveChangesAsync(cancellationToken);

        // TASK-017 AC4: tenant registration is a critical platform event.
        await auditLogger.LogAsync(
            AuditEventTypes.WorkspaceRegistered,
            $"Workspace \"{workspace.Name}\" registered by {user.Email}.",
            workspace.Id,
            user.Id,
            cancellationToken);

        var accessToken = tokenService.IssueToken(user);

        return new AuthResponseDto
        {
            Token = accessToken.Value,
            ExpiresAt = accessToken.ExpiresAt,
            User = mapper.Map<UserDto>(user)
        };
    }
}
