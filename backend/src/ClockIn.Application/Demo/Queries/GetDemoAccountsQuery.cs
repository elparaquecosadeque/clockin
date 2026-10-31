using ClockIn.Application.Demo.Interfaces;
using ClockIn.Application.Demo.Results;

namespace ClockIn.Application.Demo.Queries;

/// <summary>Anonymous; the API only routes it when demo data is on.</summary>
public sealed record GetDemoAccountsQuery;

public sealed class GetDemoAccountsHandler(IDemoData demo)
{
    public DemoAccountsDto Handle(GetDemoAccountsQuery query) => new(demo.Password, demo.FeaturedAccounts);
}
