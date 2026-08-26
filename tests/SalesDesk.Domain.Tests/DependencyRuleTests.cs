using System.Reflection;
using FluentAssertions;
using SalesDesk.Domain.Customers;

namespace SalesDesk.Domain.Tests;

/// <summary>
/// Guards the Clean Architecture dependency direction: Domain sits at the centre
/// and must not depend on any outer layer.
///
/// Note: GetReferencedAssemblies() reports only assemblies the compiled IL actually
/// uses, so these guards bite the moment code touches a forbidden type — but they
/// cannot detect an unused ProjectReference on their own.
/// </summary>
public class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(Customer).Assembly;

    [Theory]
    [InlineData("SalesDesk.Application")]
    [InlineData("SalesDesk.Infrastructure")]
    [InlineData("SalesDesk.Api")]
    public void Domain_must_not_reference_outer_layers(string forbiddenAssembly)
    {
        Domain.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Should().NotContain(forbiddenAssembly);
    }
}
