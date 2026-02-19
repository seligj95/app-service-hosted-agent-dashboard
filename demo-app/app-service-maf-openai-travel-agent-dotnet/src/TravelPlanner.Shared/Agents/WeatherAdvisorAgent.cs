using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TravelPlanner.Shared.ExternalServices;
using TravelPlanner.Shared.Services;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace TravelPlanner.Shared.Agents;

/// <summary>
/// Provides weather forecasts and weather-based recommendations
/// </summary>
public class WeatherAdvisorAgent : BaseAgent
{
    public override string AgentType => "WeatherAdvisor";
    protected override string AgentName => "Weather & Packing Advisor";
    
    protected override string Instructions => "You are a weather and packing specialist. Analyze forecasts, provide packing recommendations, suggest activity modifications based on conditions, warn about severe weather, and recommend best times for outdoor activities. Use the GetWeatherForecast function to get real-time weather data.";

    public WeatherAdvisorAgent(
        ILogger<WeatherAdvisorAgent> logger,
        IOptions<AgentOptions> options,
        IChatClient chatClient,
        IWeatherService weatherService,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider) 
        : base(logger, options, chatClient, CreateChatOptions(weatherService, logger), loggerFactory, serviceProvider)
    {
    }
    
    private static ChatOptions CreateChatOptions(IWeatherService weatherService, ILogger logger)
    {
        var chatOptions = new ChatOptions
        {
            Tools = new List<AITool>
            {
                AIFunctionFactory.Create(GetWeatherForecastFunction(weatherService, logger))
            }
        };
        return chatOptions;
    }
    
    private static Func<double, double, int, Task<string>> GetWeatherForecastFunction(
        IWeatherService weatherService,
        ILogger logger)
    {
        return async (double latitude, double longitude, int days) =>
        {
            logger.LogInformation("Getting weather forecast for {Lat}, {Lon} for {Days} days", 
                latitude, longitude, days);
            
            var forecasts = await weatherService.GetForecastAsync(
                latitude, longitude, DateTime.UtcNow, days);
            
            var summary = string.Join("\n\n", forecasts.Select(f => 
                $"{f.Name}: {f.Temperature}°{f.TemperatureUnit}, {f.ShortForecast}\n" +
                $"Details: {f.DetailedForecast}\n" +
                $"Wind: {f.WindSpeed} {f.WindDirection}"));
            
            return summary;
        };
    }
}
