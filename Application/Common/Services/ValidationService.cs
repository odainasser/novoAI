using Application.Common.Interfaces;
using FluentValidation;

namespace Application.Common.Services;

public class ValidationService : IValidationService
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<(bool IsValid, IDictionary<string, string[]> Errors)> ValidateAsync<T>(T instance) where T : class
    {
        var validator = _serviceProvider.GetService(typeof(IValidator<T>)) as IValidator<T>;
        
        if (validator == null)
        {
            return (true, new Dictionary<string, string[]>());
        }

        var validationResult = await validator.ValidateAsync(instance);

        if (validationResult.IsValid)
        {
            return (true, new Dictionary<string, string[]>());
        }

        var errors = validationResult.Errors
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());

        return (false, errors);
    }
}
