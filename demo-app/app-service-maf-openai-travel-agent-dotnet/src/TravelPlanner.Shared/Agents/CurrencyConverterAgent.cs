using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TravelPlanner.Shared.ExternalServices;
using TravelPlanner.Shared.Services;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace TravelPlanner.Shared.Agents;

/// <summary>
/// Handles currency conversion and budget allocation across different currencies
/// </summary>
public class CurrencyConverterAgent : BaseAgent
{
    public override string AgentType => "CurrencyConverter";
    protected override string AgentName => "Currency Conversion Specialist";
    
    protected override string Instructions => "You are a currency conversion specialist. Convert budgets to local currencies, provide exchange rate information, suggest optimal currency strategies, and explain exchange fees. Help travelers understand their spending power. Use the ConvertCurrency function to get real-time exchange rates.";

    public CurrencyConverterAgent(
        ILogger<CurrencyConverterAgent> logger,
        IOptions<AgentOptions> options,
        IChatClient chatClient,
        ICurrencyService currencyService,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider) 
        : base(logger, options, chatClient, CreateChatOptions(currencyService, logger), loggerFactory, serviceProvider)
    {
    }
    
    private static ChatOptions CreateChatOptions(ICurrencyService currencyService, ILogger logger)
    {
        var chatOptions = new ChatOptions
        {
            Tools = new List<AITool>
            {
                AIFunctionFactory.Create(ConvertCurrencyFunction(currencyService, logger))
            }
        };
        return chatOptions;
    }
    
    private static Func<decimal, string, string, Task<string>> ConvertCurrencyFunction(
        ICurrencyService currencyService, 
        ILogger logger)
    {
        return async (decimal amount, string fromCurrency, string toCurrency) =>
        {
            logger.LogInformation("Converting {Amount} {From} to {To}", amount, fromCurrency, toCurrency);
            var conversion = await currencyService.ConvertAmountAsync(amount, fromCurrency, toCurrency);
            return conversion.GetSummary();
        };
    }
}
