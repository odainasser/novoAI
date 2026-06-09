using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace Web.Services;

public static class HttpResponseMessageExtensions
{
    public static async Task HandleErrorAsync(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    // Use JsonDocument to parse generic JSON content safely
                    using var doc = JsonDocument.Parse(content);
                    var errorObj = doc.RootElement;

                    // Check for "errors" object (ValidationProblemDetails pattern)
                    // Example: "errors": { "Password": ["Error 1", "Error 2"] }
                    if (errorObj.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        var validationErrors = new Dictionary<string, string[]>();
                        
                        foreach (var property in errors.EnumerateObject())
                        {
                            var key = property.Name;
                            // Ensure case consistency or just pass as is. API typically returns proper case or camelCase.
                            
                            if (property.Value.ValueKind == JsonValueKind.Array)
                            {
                                var errorList = new List<string>();
                                foreach (var arrayItem in property.Value.EnumerateArray())
                                {
                                    var errorStr = arrayItem.GetString();
                                    if (!string.IsNullOrEmpty(errorStr))
                                    {
                                        errorList.Add(errorStr);
                                    }
                                }
                                if (errorList.Any())
                                {
                                    validationErrors[key] = errorList.ToArray();
                                }
                            }
                            else if (property.Value.ValueKind == JsonValueKind.String)
                            {
                                var errorStr = property.Value.GetString();
                                if (!string.IsNullOrEmpty(errorStr))
                                {
                                    validationErrors[key] = new[] { errorStr };
                                }
                            }
                        }
                        
                        if (validationErrors.Any())
                        {
                            throw new ServerValidationException(validationErrors);
                        }
                        
                        // Fallback: build string if we couldn't parse as dictionary but errors existed (unlikely if loop ran)
                        // Actually, if EnumerateObject yielded nothing, validationErrors is empty.
                    }

                    // Check for "detail" (ProblemDetails pattern)
                    if (errorObj.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    {
                        var detailStr = detail.GetString();
                        if (!string.IsNullOrWhiteSpace(detailStr))
                        {
                            throw new Exception(detailStr);
                        }
                    }

                    // Check for "message" (Custom API error pattern)
                    if (errorObj.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    {
                         var messageStr = message.GetString();
                         if (!string.IsNullOrWhiteSpace(messageStr))
                        {
                            throw new Exception(messageStr);
                        }
                    }

                    // Check for "error" (Minimal API BadRequest(new { error = "..." }) pattern)
                    if (errorObj.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                    {
                        var errorStr = error.GetString();
                        if (!string.IsNullOrWhiteSpace(errorStr))
                        {
                            throw new Exception(errorStr);
                        }
                    }
                    
                    // Check for "title" if nothing else specific was found
                    if (errorObj.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    {
                        throw new Exception(title.GetString());
                    }
                }
                catch (JsonException)
                {
                    // If JSON parsing fails (e.g. invalid JSON), assume the content itself is the error text
                    // or if it's really complicated/ugly, just throw generic.
                    // But if it looks like JSON but failed parsing, it might be partial. 
                    // To be safe, let's try to just return the content if it's short, or generic error.
                    if (content.Trim().StartsWith("{") || content.Trim().StartsWith("["))
                    {
                         // It tried to be JSON but failed or logic didn't find known properties.
                         // Don't show raw JSON to user.
                         // If we are here, it means we caught JsonException, so it's NOT valid JSON.
                         throw new Exception("An error occurred while processing the request.");
                    }
                    else
                    {
                         // Plain text error (e.g. "User not found")
                         throw new Exception(content);
                    }
                }
                catch (Exception ex) when (ex.Message != content) 
                {
                    // This catches the specific exceptions we threw above (friendly messages)
                    throw;
                }
            }
            
            // Fallback for empty content or unhandled JSON structure
            response.EnsureSuccessStatusCode();
        }
    }
}
