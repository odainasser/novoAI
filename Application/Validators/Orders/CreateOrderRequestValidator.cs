using Application.Features.Orders;
using FluentValidation;

namespace Application.Validators.Orders;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.Channel)
            .IsInEnum().WithMessage("InvalidOrderChannel");

        RuleFor(x => x.PaymentMethod)
            .IsInEnum().WithMessage("InvalidPaymentMethod");

        RuleFor(x => x.CustomerName)
            .MaximumLength(200).WithMessage("MaxLengthError")
            .Matches(@"^[a-zA-Z\u0600-\u06FF\s'-]*$").WithMessage("InvalidNameFormat")
            .When(x => !string.IsNullOrEmpty(x.CustomerName));

        RuleFor(x => x.CustomerEmail)
            .EmailAddress().WithMessage("InvalidEmailFormat")
            .MaximumLength(256).WithMessage("MaxLengthError")
            .When(x => !string.IsNullOrEmpty(x.CustomerEmail));

        RuleFor(x => x.CustomerPhone)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("InvalidPhoneNumber")
            .When(x => !string.IsNullOrEmpty(x.CustomerPhone));

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("DescriptionMaxLength")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("OrderItemsRequired");

        RuleForEach(x => x.Items).SetValidator(new CreateOrderItemRequestValidator());
    }
}

public class CreateOrderItemRequestValidator : AbstractValidator<CreateOrderItemRequest>
{
    public CreateOrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductIdRequired");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("QuantityGreaterThanZero")
            .LessThanOrEqualTo(10000).WithMessage("QuantityMaxValue");
    }
}
