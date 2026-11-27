namespace Sugarista.Application.Common.Behaviours;

public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<UnhandledExceptionBehaviour<TRequest, TResponse>> _logger;

    public UnhandledExceptionBehaviour(ILogger<UnhandledExceptionBehaviour<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            // Expected for bad credentials or missing permissions. The API middleware
            // records the safe warning; do not log request payloads or a stack trace here.
            throw;
        }
        catch (FluentValidation.ValidationException)
        {
            throw;
        }
        catch (Sugarista.Domain.Exceptions.DomainException ex)
        {
            // Expected business-rule rejection (HTTP 400) — log as concise warning, not an unhandled crash.
            _logger.LogWarning(
                "Domain rule rejected request {RequestName}: {Reason}",
                typeof(TRequest).Name,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;

            _logger.LogError(
                ex,
                "Unhandled exception for request {RequestName}",
                requestName);

            throw;
        }
    }
}
