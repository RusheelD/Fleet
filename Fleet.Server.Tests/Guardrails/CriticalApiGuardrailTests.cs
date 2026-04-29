using System.Reflection;
using Fleet.Server.Auth;
using Fleet.Server.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Fleet.Server.Tests.Guardrails;

[TestClass]
public class CriticalApiGuardrailTests
{
    [TestMethod]
    public void AllApiControllersRequireAuthorizationOrExplicitAnonymousAccess()
    {
        var unsecuredControllers = GetControllerTypes()
            .Where(controller =>
                !controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any() &&
                !controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
            .Select(controller => controller.Name)
            .ToArray();

        Assert.AreEqual(
            0,
            unsecuredControllers.Length,
            "Every API controller must require auth or explicitly opt into anonymous access. Missing: " +
            string.Join(", ", unsecuredControllers));
    }

    [TestMethod]
    public void AllApiControllersUseApiControllerAndRouteAttributes()
    {
        var missingAttributes = GetControllerTypes()
            .Where(controller =>
                !controller.GetCustomAttributes<ApiControllerAttribute>(inherit: true).Any() ||
                !controller.GetCustomAttributes<RouteAttribute>(inherit: true).Any())
            .Select(controller => controller.Name)
            .ToArray();

        Assert.AreEqual(
            0,
            missingAttributes.Length,
            "Controllers must be explicit API controllers with route attributes. Missing: " +
            string.Join(", ", missingAttributes));
    }

    [TestMethod]
    public void ProjectScopedControllersRequireOwnershipFilter()
    {
        var missingOwnershipFilter = GetControllerTypes()
            .Where(IsNestedProjectScopedController)
            .Where(controller =>
                !controller.GetCustomAttributes<ServiceFilterAttribute>(inherit: true)
                    .Any(attribute => attribute.ServiceType == typeof(ProjectOwnershipFilter)))
            .Select(controller => controller.Name)
            .ToArray();

        Assert.AreEqual(
            0,
            missingOwnershipFilter.Length,
            "Nested project-scoped controllers must run ProjectOwnershipFilter to prevent cross-project access. Missing: " +
            string.Join(", ", missingOwnershipFilter));
    }

    [TestMethod]
    public void ProjectScopedActionsCarryProjectIdParameter()
    {
        var invalidActions = GetControllerTypes()
            .Where(IsNestedProjectScopedController)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(HasHttpMethodAttribute)
                .Where(method => !method.GetParameters().Any(parameter =>
                    string.Equals(parameter.Name, "projectId", StringComparison.Ordinal)))
                .Select(method => $"{controller.Name}.{method.Name}"))
            .ToArray();

        Assert.AreEqual(
            0,
            invalidActions.Length,
            "Nested project-scoped controller actions must bind projectId explicitly. Missing: " +
            string.Join(", ", invalidActions));
    }

    [TestMethod]
    public void AdminControllerRequiresAdminPolicy()
    {
        var authorize = typeof(AdminController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .SingleOrDefault();

        Assert.IsNotNull(authorize);
        Assert.AreEqual("AdminOnly", authorize.Policy);
    }

    private static Type[] GetControllerTypes()
        => typeof(ProjectsController).Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace == "Fleet.Server.Controllers" &&
                typeof(ControllerBase).IsAssignableFrom(type) &&
                !type.IsAbstract)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

    private static bool IsNestedProjectScopedController(Type controller)
        => controller.GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(attribute => attribute.Template ?? string.Empty)
            .Any(template => template.StartsWith("api/projects/{projectId}/", StringComparison.OrdinalIgnoreCase));

    private static bool HasHttpMethodAttribute(MethodInfo method)
        => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any();
}
