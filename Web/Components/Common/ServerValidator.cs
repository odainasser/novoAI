using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Web.Services;

namespace Web.Components.Common;

public class ServerValidator : ComponentBase
{
    [Inject] private IJsonStringLocalizer Localizer { get; set; } = default!;
    [CascadingParameter] private EditContext? CurrentEditContext { get; set; }

    private ValidationMessageStore? _messageStore;

    protected override void OnInitialized()
    {
        if (CurrentEditContext == null)
        {
            throw new InvalidOperationException($"{nameof(ServerValidator)} requires a cascading " +
                $"parameter of type {nameof(EditContext)}. For example, you can use {nameof(ServerValidator)} " +
                $"inside an {nameof(EditForm)}.");
        }

        _messageStore = new ValidationMessageStore(CurrentEditContext);
        CurrentEditContext.OnValidationRequested += (s, e) => _messageStore.Clear();
        CurrentEditContext.OnFieldChanged += (s, e) => _messageStore.Clear(e.FieldIdentifier);
    }

    public void DisplayErrors(IDictionary<string, string[]> errors)
    {
        if (_messageStore == null || CurrentEditContext == null) return;

        foreach (var err in errors)
        {
            var fieldIdentifier = new FieldIdentifier(CurrentEditContext.Model, err.Key);
            
            // Translate the error messages (keys) received from the server
            var translatedMessages = err.Value.Select(msg => Localizer[msg]);
            
            _messageStore.Add(fieldIdentifier, translatedMessages);
        }

        CurrentEditContext.NotifyValidationStateChanged();
    }
}
