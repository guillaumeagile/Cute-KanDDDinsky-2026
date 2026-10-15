namespace OmniProduct_CoreDomain.Errors;

// Common contract of every validation failure returned by a value object factory
// (Create / Build returning OneOf<TValueObject, TError>). An interface, not a base class.
public interface IValidationError
{
    string Message { get; }
}
