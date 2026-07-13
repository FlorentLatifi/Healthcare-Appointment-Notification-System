using Healthcare.Application.Common;
using Healthcare.Application.Ports.Payments;

namespace Healthcare.Application.Services;

/// <summary>
/// Ensures a Stripe PaymentIntent cannot be applied to a different appointment (rebinding attack).
/// </summary>
public static class PaymentIntentBinding
{
    public const string AppointmentIdMetadataKey = "appointment_id";

    /// <summary>
    /// Validates that the PaymentIntent is permanently bound to <paramref name="expectedAppointmentId"/>.
    /// Optionally checks amount/currency against the appointment consultation fee.
    /// </summary>
    public static Result Validate(
        PaymentConfirmationResult confirmation,
        int expectedAppointmentId,
        decimal? expectedAmount = null,
        string? expectedCurrency = null)
    {
        if (!confirmation.Metadata.TryGetValue(AppointmentIdMetadataKey, out var boundRaw)
            || string.IsNullOrWhiteSpace(boundRaw))
        {
            return Result.Failure(
                "PaymentIntent is missing appointment_id metadata and cannot be applied safely.");
        }

        if (!int.TryParse(boundRaw.Trim(), out var boundAppointmentId))
        {
            return Result.Failure(
                "PaymentIntent appointment_id metadata is invalid.");
        }

        if (boundAppointmentId != expectedAppointmentId)
        {
            return Result.Failure(
                $"PaymentIntent is bound to appointment {boundAppointmentId} " +
                $"and cannot be applied to appointment {expectedAppointmentId}.");
        }

        if (expectedAmount.HasValue && !string.IsNullOrWhiteSpace(expectedCurrency))
        {
            var expectedCents = ToSmallestUnit(expectedAmount.Value, expectedCurrency);
            var currencyMatches = string.Equals(
                confirmation.Currency?.Trim(),
                expectedCurrency.Trim(),
                StringComparison.OrdinalIgnoreCase);

            if (confirmation.AmountInCents > 0 && confirmation.AmountInCents != expectedCents)
            {
                return Result.Failure(
                    "PaymentIntent amount does not match the appointment consultation fee.");
            }

            if (!string.IsNullOrWhiteSpace(confirmation.Currency) && !currencyMatches)
            {
                return Result.Failure(
                    "PaymentIntent currency does not match the appointment consultation fee.");
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Converts major units to Stripe-style smallest unit (matches gateway adapter rules).
    /// </summary>
    public static long ToSmallestUnit(decimal amount, string currency)
    {
        var zeroDecimal = new[] { "JPY", "KRW", "VND", "CLP" };
        if (zeroDecimal.Contains(currency.ToUpperInvariant()))
            return (long)amount;
        return (long)(amount * 100);
    }
}
