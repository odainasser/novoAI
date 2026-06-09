using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;
using Web.Services;

namespace Web.Components.Common;

public class LocalizedDataAnnotationsValidator : ComponentBase, IDisposable
{
    [CascadingParameter] private EditContext? CurrentEditContext { get; set; }
    [Inject] private IJsonStringLocalizer Localizer { get; set; } = default!;

    private IDisposable? _subscriptions;

    protected override void OnInitialized()
    {
        if (CurrentEditContext == null)
        {
            throw new InvalidOperationException($"{nameof(LocalizedDataAnnotationsValidator)} requires a cascading " +
                $"parameter of type {nameof(EditContext)}. For example, you can use {nameof(LocalizedDataAnnotationsValidator)} " +
                $"inside an {nameof(EditForm)}.");
        }

        _subscriptions = new ValidationMessageStoreSubscription(CurrentEditContext, Localizer);
    }

    public void Dispose()
    {
        _subscriptions?.Dispose();
    }

    private class ValidationMessageStoreSubscription : IDisposable
    {
        private readonly EditContext _editContext;
        private readonly ValidationMessageStore _messageStore;
        private readonly IJsonStringLocalizer _localizer;

        public ValidationMessageStoreSubscription(EditContext editContext, IJsonStringLocalizer localizer)
        {
            _editContext = editContext;
            _messageStore = new ValidationMessageStore(_editContext);
            _localizer = localizer;

            _editContext.OnValidationRequested += HandleValidationRequested;
            _editContext.OnFieldChanged += HandleFieldChanged;
        }

        private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
        {
            _messageStore.Clear();
            var validationContext = new ValidationContext(_editContext.Model);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(_editContext.Model, validationContext, validationResults, true);

            foreach (var validationResult in validationResults)
            {
                if (validationResult.MemberNames.Any())
                {
                    foreach (var memberName in validationResult.MemberNames)
                    {
                        var fieldIdentifier = _editContext.Field(memberName);
                        AddMessage(fieldIdentifier, validationResult.ErrorMessage);
                    }
                }
                else
                {
                    // Class-level validation error
                    // We can map this to a field if empty, or just store it. 
                    // However, ValidationMessage usually requires a FieldIdentifier.
                    // Empty/default FieldIdentifier is one way.
                    var fieldIdentifier = new FieldIdentifier(_editContext.Model, string.Empty);
                    AddMessage(fieldIdentifier, validationResult.ErrorMessage);
                }
            }
            _editContext.NotifyValidationStateChanged();
        }

        private void HandleFieldChanged(object? sender, FieldChangedEventArgs e)
        {
            _messageStore.Clear(e.FieldIdentifier);
            
            // We need to validate property directly
            var propertyInfo = e.FieldIdentifier.Model.GetType().GetProperty(e.FieldIdentifier.FieldName);
            if (propertyInfo != null)
            {
                var value = propertyInfo.GetValue(e.FieldIdentifier.Model);
                var validationContext = new ValidationContext(e.FieldIdentifier.Model)
                {
                    MemberName = e.FieldIdentifier.FieldName
                };
                var validationResults = new List<ValidationResult>();
                
                Validator.TryValidateProperty(value, validationContext, validationResults);

                foreach (var validationResult in validationResults)
                {
                    AddMessage(e.FieldIdentifier, validationResult.ErrorMessage);
                }
            }
            _editContext.NotifyValidationStateChanged();
        }

        private void AddMessage(FieldIdentifier fieldIdentifier, string? errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage)) return;
            
            // Localize the message
            // If the ErrorMessage is a localization key, Localizer[key] returns the translated value.
            // If the key is not found, Localizer returns the key itself (usually).
            var translatedMessage = _localizer[errorMessage];
            _messageStore.Add(fieldIdentifier, translatedMessage);
        }

        public void Dispose()
        {
            _messageStore.Clear();
            _editContext.OnValidationRequested -= HandleValidationRequested;
            _editContext.OnFieldChanged -= HandleFieldChanged;
        }
    }
}
