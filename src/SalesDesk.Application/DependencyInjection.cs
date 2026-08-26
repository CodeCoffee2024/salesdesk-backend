using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SalesDesk.Application.Common.Behaviors;
using SalesDesk.Application.Common.Mappings;

namespace SalesDesk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // AutoMapper is pinned to 10.0.0 (see the .csproj comment), which predates
        // the AddAutoMapper(assembly) DI helper that later versions bundle — so the
        // mapper is built and registered by hand instead.
        var mapperConfiguration = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        services.AddSingleton<IMapper>(mapperConfiguration.CreateMapper());

        return services;
    }
}
