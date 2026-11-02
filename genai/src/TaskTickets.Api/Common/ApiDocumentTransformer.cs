using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using TaskTickets.Api.Data;

namespace TaskTickets.Api.Common;

public sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "TaskTickets API";
        document.Info.Version = "v1";
        document.Info.Description = "Disposable local demo. All data and tokens reset on restart. Fictional seed credentials are public.";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" }
        };

        foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations!.Values))
        {
            if (operation.OperationId is not ("Login" or "Register"))
            {
                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            }

            if (operation.RequestBody?.Content is not { } content || !content.TryGetValue("application/json", out var body) || body is null)
            {
                continue;
            }

            switch (operation.OperationId)
            {
                case "Login":
                    body.Examples = new Dictionary<string, IOpenApiExample>
                    {
                        ["Alice"] = new OpenApiExample { Summary = "Alice — public demo credentials", Value = JsonSerializer.SerializeToNode(new { email = "alice@example.test", password = "DemoPassword123!" }) },
                        ["Bob"] = new OpenApiExample { Summary = "Bob — public demo credentials", Value = JsonSerializer.SerializeToNode(new { email = "bob@example.test", password = "DemoPassword123!" }) }
                    };
                    break;
                case "Register":
                    body.Example = JsonSerializer.SerializeToNode(new { displayName = "Charlie", email = "charlie@example.test", password = "DemoPassword123!" });
                    break;
                case "CreateTicket":
                    body.Example = JsonSerializer.SerializeToNode(new { title = "Try the API", description = "Assign this ticket to Bob.", dueDate = "2026-11-02", assignedToUserId = DemoSeed.BobId });
                    break;
                case "UpdateTicket":
                    body.Example = JsonSerializer.SerializeToNode(new { title = "Try the API", description = (string?)null, status = "done", dueDate = (string?)null });
                    break;
                case "AssignTicket":
                    body.Example = JsonSerializer.SerializeToNode(new { assignedToUserId = DemoSeed.BobId });
                    break;
            }
        }

        return Task.CompletedTask;
    }
}
