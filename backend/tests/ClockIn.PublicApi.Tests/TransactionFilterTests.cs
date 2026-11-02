using AwesomeAssertions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.PublicApi.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace ClockIn.PublicApi.Tests;

public sealed class TransactionFilterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Commits_only_successful_actions_and_always_disposes(bool failed)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        var executed = new ActionExecutedContext(action, [], new object())
        {
            Exception = failed ? new InvalidOperationException("failed action") : null,
        };

        await new TransactionFilter(unitOfWork).OnActionExecutionAsync(executing, () => Task.FromResult(executed));

        await unitOfWork.Received(1).BeginAsync(CancellationToken.None);
        await unitOfWork.Received(failed ? 0 : 1).CommitAsync(CancellationToken.None);
        await unitOfWork.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Disposes_without_commit_when_the_action_throws()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        Func<Task> execute = () => new TransactionFilter(unitOfWork).OnActionExecutionAsync(executing,
            () => throw new InvalidOperationException("failed action"));

        await execute.Should().ThrowAsync<InvalidOperationException>();
        await unitOfWork.DidNotReceive().CommitAsync(CancellationToken.None);
        await unitOfWork.Received(1).DisposeAsync();
    }
}
