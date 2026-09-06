using FluentAssertions;
using NetArchTest.Rules;
using Stockma.Domain.Common;
using Stockma.Domain.Entities;

namespace Stockma.Architecture.Tests;

public class TenantArchitectureTests
{
    private static readonly Type[] IsolationRoots = [typeof(Tenant)];

    [Fact]
    public void EveryDomainEntity_MustImplementITenantEntity()
    {
        var result = Types.InAssembly(typeof(ITenantEntity).Assembly)
            .That()
            .ResideInNamespace("Stockma.Domain.Entities")
            .And()
            .AreClasses()
            .And()
            .DoNotHaveName(IsolationRoots.Select(type => type.Name).ToArray())
            .Should()
            .ImplementInterface(typeof(ITenantEntity))
            .GetResult();

        result.FailingTypeNames
            .Should()
            .BeNullOrEmpty("toda entidad tenant debe implementar ITenantEntity para que el filtro global de EF la alcance (FR-002)");
    }

    [Fact]
    public void Tenant_MustNotImplementITenantEntity()
    {
        typeof(Tenant)
            .Should()
            .NotBeAssignableTo<ITenantEntity>("Tenant es la unidad de aislamiento, no un sujeto de aislamiento");
    }
}
