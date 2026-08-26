using FluentValidation;
using MediatR;

namespace SalesDesk.Application.Auth;

public sealed record ForgotPasswordCommand(string Email) : IRequest;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}

/// <summary>
/// No outbound-email infrastructure exists in this codebase yet, so this is a
/// deliberate no-op: it always succeeds, whether or not the address belongs to an
/// account, so the API never reveals which emails are registered. A real reset-link
/// flow (token issuance + delivery) is future work, not part of TASK-015.
/// </summary>
public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    public Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken) => Task.CompletedTask;
}
