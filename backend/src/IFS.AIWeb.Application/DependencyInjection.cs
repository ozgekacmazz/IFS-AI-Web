using Microsoft.Extensions.DependencyInjection;

namespace IFS.AIWeb.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<SummarizationService>();
        services.AddSingleton<ISummarizationPromptBuilder, SummarizationPromptBuilder>();
        return services;
    }
}
