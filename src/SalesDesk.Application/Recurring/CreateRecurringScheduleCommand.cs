using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Application.Documents;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Recurring;

public sealed record CreateRecurringScheduleCommand(
    Guid CustomerId,
    Guid TemplateId,
    DocumentType Type,
    DateOnly StartDate,
    RecurrenceInterval Interval,
    int DueDateOffsetDays,
    bool AutoDispatch,
    List<CreateDocumentLineItemRequest> LineItems,
    string? Currency = null,
    string? ClientCountry = null) : IRequest<RecurringScheduleDto>;

public sealed class CreateRecurringScheduleCommandValidator : AbstractValidator<CreateRecurringScheduleCommand>
{
    public CreateRecurringScheduleCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.TemplateId).NotEmpty();
        RuleFor(c => c.DueDateOffsetDays).GreaterThanOrEqualTo(0);
        RuleFor(c => c.LineItems).NotEmpty().WithMessage("At least one line item is required.");
        RuleForEach(c => c.LineItems).SetValidator(new CreateDocumentLineItemRequestValidator());
        RuleFor(c => c.Currency).Matches("^[A-Za-z]{3}$").WithMessage("Currency must be a 3-letter ISO 4217 code.").When(c => c.Currency is not null);
        RuleFor(c => c.ClientCountry).Matches("^[A-Za-z]{2}$").WithMessage("Client country must be a 2-letter ISO 3166-1 alpha-2 code.").When(c => c.ClientCountry is not null);
    }
}

public sealed class CreateRecurringScheduleCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<CreateRecurringScheduleCommand, RecurringScheduleDto>
{
    public async Task<RecurringScheduleDto> Handle(CreateRecurringScheduleCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();

        var customer = await context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        var template = await context.Templates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Template), request.TemplateId);

        var workspace = await context.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
        var currency = request.Currency ?? workspace?.DefaultCurrency ?? "USD";
        var clientCountry = request.ClientCountry ?? customer.Country ?? workspace?.Country;

        var schedule = new RecurringSchedule(
            workspaceId, customer.Id, template.Id, request.Type, request.StartDate,
            request.Interval, request.DueDateOffsetDays, request.AutoDispatch, currency, clientCountry);

        foreach (var item in request.LineItems)
        {
            schedule.AddLineItem(item.Description, item.Quantity, item.UnitPrice, item.ProductId);
        }

        context.RecurringSchedules.Add(schedule);
        await context.SaveChangesAsync(cancellationToken);

        var created = await context.RecurringSchedules
            .Include(s => s.Customer)
            .Include(s => s.Template)
            .Include(s => s.LineItems)
            .FirstAsync(s => s.Id == schedule.Id, cancellationToken);

        return created.ToDto();
    }
}
