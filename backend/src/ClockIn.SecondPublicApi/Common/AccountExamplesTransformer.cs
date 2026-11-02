using System.Data.Common;
using System.Text.Json;
using ClockIn.Application.Demo.Interfaces;
using ClockIn.Application.Reporting.Interfaces;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ClockIn.SecondPublicApi.Common;

/// <summary>Demo credentials and read-only Office team lookup; never generates activation tokens.</summary>
public sealed partial class AccountExamplesTransformer(
    IDemoData demo,
    IReportingReads reads,
    ILogger<AccountExamplesTransformer> logger) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Description = "Sign in as Ada to create a user, copy the activation token into set-password, "
            + "then sign in as the new user and call /me. Mia can call /me but receives 403 when creating users. "
            + "Calling /me without a bearer token returns 401. Employees require an active teamId; supervisors and admins do not have a team. "
            + "Reload Scalar after demo resets to refresh the Office teamId. Fixed sample emails return 409 if reused.";
        var logins = demo.Accounts.Where(a => a.Name is "Ada" or "Mia")
            .ToDictionary(a => a.Name, a => (IOpenApiExample)Example($"{a.Name} ({a.Role})", new { email = a.Email, password = demo.Password }));
        logins["NewSupervisor"] = Example("New supervisor after activation", new { email = "sample-supervisor@clockin.demo", password = "new-user-password" });
        SetExamples(document, "/api/v1/auth/login", logins);
        SetExamples(document, "/api/v1/auth/set-password", new Dictionary<string, IOpenApiExample>
        {
            ["Activation"] = Example("Paste the activation token from the create response", new
            {
                token = "paste-token-from-create-user-response",
                password = "new-user-password",
            }),
        });
        var creations = new Dictionary<string, IOpenApiExample>
        {
            ["Supervisor"] = Example("Supervisor (no teamId)", new
            {
                email = "sample-supervisor@clockin.demo",
                name = "Sample Supervisor",
                role = "Supervisor",
                teamId = (Guid?)null,
            }),
        };
        try
        {
            TeamRow? office = (await reads.TeamsAsync(cancellationToken)).FirstOrDefault(t => t.Name == "Office" && !t.IsArchived);
            if (office is not null)
            {
                creations["Employee"] = Example("Employee (Office teamId required)", new
                {
                    email = "sample-employee@clockin.demo",
                    name = "Sample Employee",
                    role = "Employee",
                    teamId = office.Id,
                });
            }
            else
            {
                document.Info.Description += " Office is unavailable; prepare demo data or supply another active teamId.";
            }
        }
        catch (DbException)
        {
            LogOfficeUnavailable(logger);
            document.Info.Description += " Prepare the shared demo database to load the Office employee example.";
        }

        SetExamples(document, "/api/v1/users", creations);
    }

    private static OpenApiExample Example(string summary, object value) =>
        new() { Summary = summary, Value = JsonSerializer.SerializeToNode(value) };

    private static void SetExamples(OpenApiDocument document, string route, Dictionary<string, IOpenApiExample> examples)
    {
        OpenApiOperation operation = document.Paths[route].Operations!.Values.Single();
        foreach (OpenApiMediaType media in operation.RequestBody!.Content!.Values)
        {
            media.Examples = examples;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Office team example is unavailable; prepare the shared demo database first.")]
    private static partial void LogOfficeUnavailable(ILogger logger);
}
