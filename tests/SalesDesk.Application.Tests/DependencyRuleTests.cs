using System.Reflection;
using FluentAssertions;
using SalesDesk.Application.Customers;

namespace SalesDesk.Application.Tests;

/// <summary>
/// Application may depend on Domain, but must not depend on Infrastructure
/// implementations or on the API composition root.
///
/// Note: GetReferencedAssemblies() reports only assemblies the compiled IL actually
/// uses, so these guards bite the moment code touches a forbidden type — but they
/// cannot detect an unused ProjectReference on their own.
/// </summary>
public class DependencyRuleTests
{
    private static readonly Assembly Application = typeof(CustomerDto).Assembly;

    [Theory]
    [InlineData("SalesDesk.Infrastructure")]
    [InlineData("SalesDesk.Api")]
    public void Application_must_not_reference_outer_layers(string forbiddenAssembly)
    {
        Application.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Should().NotContain(forbiddenAssembly);
    }
}
