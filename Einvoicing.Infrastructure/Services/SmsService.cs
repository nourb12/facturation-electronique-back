






using Einvoicing.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Einvoicing.Infrastructure.Services;

public sealed class SmsService(ILogger<SmsService> logger) : ISmsService
{
    public Task EnvoyerAsync(string telephone, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(telephone) || string.IsNullOrWhiteSpace(message))
            return Task.CompletedTask;

        
        logger.LogInformation("[SMS MOCK] -> {Tel} | {Message}", telephone, message);
        return Task.CompletedTask;
    }
}
