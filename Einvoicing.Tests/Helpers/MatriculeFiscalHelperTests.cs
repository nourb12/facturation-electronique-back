using Einvoicing.Application.Helpers;
using FluentAssertions;

namespace Einvoicing.Tests.Helpers;

public class MatriculeFiscalHelperTests
{
    [Fact]
    public void Normalize_RemovesSeparatorsAndUppercases()
    {
        MatriculeFiscalHelper.Normalize(" 1234567a/b/m/000 ")
            .Should().Be("1234567ABM000");
    }

    [Fact]
    public void IsValid_AcceptsNormalizedOrSlashed()
    {
        MatriculeFiscalHelper.IsValid("1495908/S").Should().BeTrue();
        MatriculeFiscalHelper.IsValid("1495908S").Should().BeTrue();
        MatriculeFiscalHelper.IsValid("1234567ABM000").Should().BeTrue();
        MatriculeFiscalHelper.IsValid("1234567A/B/M/000").Should().BeTrue();
    }

    [Fact]
    public void IsValid_RejectsInvalid()
    {
        MatriculeFiscalHelper.IsValid("1234567890123").Should().BeFalse();
    }
}