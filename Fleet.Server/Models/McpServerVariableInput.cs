using System.ComponentModel.DataAnnotations;

namespace Fleet.Server.Models;

public record McpServerVariableInput(
    [param: Required] string Name,
    string? Value = null,
    bool IsSecret = false,
    bool PreserveExistingValue = false
);
