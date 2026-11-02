using ClockIn.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClockIn.PublicApi.Common;

/// <summary>Only the four transactional time commands use this filter; failed login counters must survive errors.</summary>
public sealed class TransactionFilter(IUnitOfWork unitOfWork) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        CancellationToken ct = context.HttpContext.RequestAborted;
        try
        {
            await unitOfWork.BeginAsync(ct);
            ActionExecutedContext result = await next();
            if (result.Exception is null && !result.Canceled)
            {
                await unitOfWork.CommitAsync(ct);
            }
        }
        finally
        {
            await unitOfWork.DisposeAsync();
        }
    }
}
