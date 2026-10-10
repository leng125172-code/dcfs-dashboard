using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Api.Controllers;

namespace WhaleDeck.IntegrationTests.Security;

public sealed class ManagementRouteTests
{
    [Fact]
    public void OperationRoutesDoNotUseMvcReservedActionKey()
    {
        var templates = typeof(ManagementController)
            .GetMethods()
            .SelectMany(method => method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true))
            .Cast<HttpPostAttribute>()
            .Select(attribute => attribute.Template ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(templates, template => template.Contains("{action}", StringComparison.Ordinal));
        Assert.Contains(templates, template => template.EndsWith("/{operation}/plan", StringComparison.Ordinal));
        Assert.Contains(templates, template => template.EndsWith("/{operation}", StringComparison.Ordinal));
    }
}
