namespace OmniProduct_CoreDomain.Errors;

// Generic validation error, a value object expressing why an input was refused.
// Specific failures (e.g. invalid dates) are their own records implementing IValidationError.
public record ValidationError(string Message) : IValidationError;
