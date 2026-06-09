using FluentValidation;

namespace Application.Common.Interfaces;

public interface IValidationService
{
    Task<(bool IsValid, IDictionary<string, string[]> Errors)> ValidateAsync<T>(T instance) where T : class;
}
