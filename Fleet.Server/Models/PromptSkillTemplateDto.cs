namespace Fleet.Server.Models;

public record PromptSkillTemplateDto(
    string Key,
    string Name,
    string Description,
    string WhenToUse,
    string Content
);
