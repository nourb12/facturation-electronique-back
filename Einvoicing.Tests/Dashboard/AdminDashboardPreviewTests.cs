using Einvoicing.Application.Services;
using Einvoicing.Application.DTOs;
using System.ComponentModel.DataAnnotations;

namespace Einvoicing.Tests.Dashboard;

public class AdminDashboardPreviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("7d", 7)]
    [InlineData("30d", 10)]
    [InlineData("12m", 12)]
    public void PeriodDataIsPopulatedAndCoherent(string period, int points)
    {
        var d = AdminDashboardPreview.Create(period, Now);
        Assert.True(d.IsDemo);
        Assert.Equal(period, d.Period);
        Assert.Equal(points, d.Flow.Length);
        var issued = d.Metrics.Single(x => x.Key == "invoices").Value;
        Assert.Equal(issued, d.Transmission.Sum(x => x.Count));
        Assert.Equal(issued, d.Flow.Sum(x => x.Issued));
        Assert.Equal(d.Transmission.Single(x => x.Key == "accepted").Count, d.Flow.Sum(x => x.Accepted));
        Assert.All(d.Flow, x => Assert.InRange(x.Accepted, 0, x.Issued));
        Assert.Equal(d.Transmission.Single(x => x.Key == "rejected").Count, d.RejectionReasons.Sum(x => x.Count));
        Assert.Equal(d.Companies.Length, d.Companies.Select(x => x.Id).Distinct().Count());
        Assert.Equal(16, d.Plans.Sum(x => x.Count));
        Assert.Equal(2, d.Certificates.Count(x => x.DaysLeft <= 30));
        Assert.Equal(6, d.Services.Length);
        Assert.NotEmpty(d.Queue);
        Assert.NotEmpty(d.Activities);
        Assert.NotEmpty(d.Requests);
        Assert.NotEmpty(d.Anomalies);
        Assert.Equal(Now.AddDays(12), d.Certificates[0].ExpiresAt);
    }

    [Fact]
    public void UnknownPeriodIsRejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => AdminDashboardPreview.Create("invalid", Now));

    [Theory]
    [InlineData("Atlas Conseil", "1934567/E", true)]
    [InlineData("A", "1934567/E", false)]
    [InlineData("Atlas Conseil", "bad", false)]
    public void NewPreviewCompanyIsValidated(string name, string taxId, bool expected)
    {
        var request = new PreviewCompanyRequest { Name = name, TaxId = taxId };
        Assert.Equal(expected, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }
}
