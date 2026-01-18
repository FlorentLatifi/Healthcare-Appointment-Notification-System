using FluentValidation;
using Healthcare.Presentation.API.Requests;

namespace Healthcare.Presentation.API.Validators;

/// <summary>
/// Validator for RefundPaymentRequest.
/// </summary>
public sealed class RefundPaymentRequestValidator : AbstractValidator<RefundPaymentRequest>
{
    public RefundPaymentRequestValidator()
    {
        RuleFor(x => x.PaymentId)
            .GreaterThan(0)
            .WithMessage("Payment ID must be greater than 0");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Refund reason is required")
            .MinimumLength(10)
            .WithMessage("Refund reason must be at least 10 characters")
            .MaximumLength(500)
            .WithMessage("Refund reason cannot exceed 500 characters");
    }
}