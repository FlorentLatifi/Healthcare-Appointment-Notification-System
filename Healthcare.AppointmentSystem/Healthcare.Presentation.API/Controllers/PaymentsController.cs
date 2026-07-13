using Asp.Versioning;
using Healthcare.Application.Commands.ProcessPayment;
using Healthcare.Application.Commands.RefundPayment;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Payments;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Authorization;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Healthcare.Presentation.API.Controllers;

/// <summary>
/// Controller for managing payments.
/// </summary>
/// <remarks>
/// 
/// Payment Flow (2-step process):
/// 
/// STEP 1 - Create Payment Intent (before showing payment form):
/// POST /api/payments/create-intent
/// ↓
/// Returns: { clientSecret: "pi_xxx_secret_yyy" }
/// ↓
/// Frontend uses this with Stripe.js to show payment form
/// 
/// STEP 2 - Process Payment (after Stripe.js confirms the PaymentIntent client-side):
/// POST /api/payments/process
/// ↓
/// Retrieves PaymentIntent status + metadata from Stripe (does not re-charge)
/// ↓
/// Rejects if metadata.appointment_id != request.appointmentId (anti-rebinding)
/// ↓
/// Creates/updates Payment entity + Auto-confirms Appointment
/// 
/// Refund Flow:
/// POST /api/payments/refund
/// ↓
/// Processes refund with Stripe
/// ↓
/// Updates Payment entity status
/// 
/// REST Endpoints:
/// - POST   /api/payments/create-intent  - Create payment intent (step 1)
/// - POST   /api/payments/process        - Process payment (step 2)
/// - POST   /api/payments/refund         - Refund a payment
/// - GET    /api/payments/{id}           - Get payment by ID
/// - GET    /api/payments/appointment/{appointmentId} - Get payment by appointment
/// - GET    /api/payments                - Get all payments (admin only)
/// </remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class PaymentsController : ControllerBase
{
    private readonly ICommandHandler<ProcessPaymentCommand, Result<int>> _processPaymentHandler;
    private readonly ICommandHandler<RefundPaymentCommand, Result> _refundPaymentHandler;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        ICommandHandler<ProcessPaymentCommand, Result<int>> processPaymentHandler,
        ICommandHandler<RefundPaymentCommand, Result> refundPaymentHandler,
        IPaymentGateway paymentGateway,
        IUnitOfWork unitOfWork,
        ILogger<PaymentsController> logger)
    {
        _processPaymentHandler = processPaymentHandler;
        _refundPaymentHandler = refundPaymentHandler;
        _paymentGateway = paymentGateway;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Creates a payment intent (Step 1 - before collecting payment details).
    /// </summary>
    /// <param name="request">Payment intent creation details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Client secret for Stripe.js integration.</returns>
    /// <response code="200">Payment intent created successfully.</response>
    /// <response code="400">Invalid request or appointment not found.</response>
    /// <response code="404">Appointment not found.</response>
    [HttpPost("create-intent")]
    [Authorize(Roles = AppRoles.Patient)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePaymentIntent(
        [FromBody] CreatePaymentIntentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating payment intent for appointment {AppointmentId}",
            request.AppointmentId);

        // 1. Fetch appointment
        var appointment = await _unitOfWork.Appointments
                .GetByIdAsync(request.AppointmentId, cancellationToken);

        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", request.AppointmentId);
            return NotFound(ApiResponse<object>.ErrorResponse(
                $"Appointment with ID {request.AppointmentId} not found",
                "Appointment not found"));
        }

        if (User.GetRole() == AppRoles.Patient && User.GetPatientId() != appointment.PatientId)
            return Forbid();

        // 2. Check if payment already exists
        var existingPayment = await _unitOfWork.Payments
            .GetByAppointmentIdAsync(request.AppointmentId, cancellationToken);

        if (existingPayment != null &&
            existingPayment.Status == Domain.Enums.PaymentStatus.Succeeded)
        {
            _logger.LogWarning("Payment already processed for appointment {AppointmentId}",
                request.AppointmentId);
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Payment has already been processed for this appointment",
                "Payment already exists"));
        }

        // 3. Create payment intent with gateway
        var description = request.Description
            ?? $"Consultation fee - {appointment.Doctor.FullName} - {appointment.ScheduledTime.ToDisplayString()}";

        var metadata = new Dictionary<string, string>
            {
                { "appointment_id", appointment.Id.ToString() },
                { "patient_id", appointment.PatientId.ToString() },
                { "doctor_id", appointment.DoctorId.ToString() }
            };

        var result = await _paymentGateway.CreatePaymentIntentAsync(
            appointment.ConsultationFee.Amount,
            appointment.ConsultationFee.Currency,
            description,
            metadata,
            cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError("Failed to create payment intent: {Error}", result.Error);
            return BadRequest(ApiResponse<object>.ErrorResponse(
                result.Error,
                "Failed to create payment intent"));
        }

        var paymentIntent = result.Value;

        _logger.LogInformation(
            "Payment intent created successfully: {PaymentIntentId} for appointment {AppointmentId}",
            paymentIntent.PaymentIntentId, request.AppointmentId);

        // 4. Return client secret (frontend needs this for Stripe.js)
        var response = new
        {
            paymentIntentId = paymentIntent.PaymentIntentId,
            clientSecret = paymentIntent.ClientSecret,
            amount = appointment.ConsultationFee.Amount,
            currency = appointment.ConsultationFee.Currency,
            appointmentId = appointment.Id
        };

        return Ok(ApiResponse<object>.SuccessResponse(
            response,
            "Payment intent created. Use client secret to complete payment on frontend."));
    }

    /// <summary>
    /// Reconciles a client-confirmed PaymentIntent against an appointment (Step 2).
    /// The card is charged by Stripe.js; this endpoint verifies PI metadata is bound to
    /// the appointment and then updates local payment/appointment state.
    /// </summary>
    /// <param name="request">Payment processing details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Payment ID if successful.</returns>
    /// <response code="200">Payment processed successfully.</response>
    /// <response code="400">Invalid request, rebinding rejected, or payment failed.</response>
    [HttpPost("process")]
    [Authorize(Roles = AppRoles.Patient)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessPayment(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Reconciling payment for appointment {AppointmentId} with intent {PaymentIntentId}",
            request.AppointmentId, request.PaymentIntentId);

        // 1. Fetch appointment and verify ownership
        var appointment = await _unitOfWork.Appointments
            .GetByIdAsync(request.AppointmentId, cancellationToken);

        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", request.AppointmentId);
            return NotFound(ApiResponse<object>.ErrorResponse(
                $"Appointment with ID {request.AppointmentId} not found",
                "Appointment not found"));
        }

        if (User.GetRole() == AppRoles.Patient && User.GetPatientId() != appointment.PatientId)
            return Forbid();

        var command = new ProcessPaymentCommand
        {
            AppointmentId = request.AppointmentId,
            PaymentIntentId = request.PaymentIntentId
        };

        var result = await _processPaymentHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Payment processing failed: {Error}", result.Error);
            return BadRequest(ApiResponse<int>.ErrorResponse(
                result.Error,
                "Payment processing failed"));
        }

        _logger.LogInformation("Payment {PaymentId} processed successfully", result.Value);

        return Ok(ApiResponse<int>.SuccessResponse(
            result.Value,
            "Payment processed successfully. Appointment has been confirmed."));
    }

    /// <summary>
    /// Refunds a payment.
    /// </summary>
    /// <param name="request">Refund details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    /// <response code="200">Refund processed successfully.</response>
    /// <response code="400">Invalid request or refund failed.</response>
    /// <response code="404">Payment not found.</response>
    [HttpPost("refund")]
    [Authorize(Roles = AppRoles.AdminOrDoctor)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefundPayment(
        [FromBody] RefundPaymentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing refund for payment {PaymentId}", request.PaymentId);

        // 1. Fetch payment and its appointment to verify doctor ownership
        var payment = await _unitOfWork.Payments.GetByIdAsync(request.PaymentId, cancellationToken);
        if (payment == null)
        {
            _logger.LogWarning("Payment {PaymentId} not found", request.PaymentId);
            return NotFound(ApiResponse.ErrorResponse(
                $"Payment with ID {request.PaymentId} not found",
                "Payment not found"));
        }

        var appointment = await _unitOfWork.Appointments
            .GetByIdAsync(payment.AppointmentId, cancellationToken);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} for payment {PaymentId} not found",
                payment.AppointmentId, request.PaymentId);
            return NotFound(ApiResponse.ErrorResponse(
                "Associated appointment not found",
                "Payment not found"));
        }

        if (User.GetRole() == AppRoles.Doctor && User.GetDoctorId() != appointment.DoctorId)
            return Forbid();

        var command = new RefundPaymentCommand
        {
            PaymentId = request.PaymentId,
            Reason = request.Reason
        };

        var result = await _refundPaymentHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Refund failed for payment {PaymentId}: {Error}",
                request.PaymentId, result.Error);

            if (result.Error.Contains("not found"))
            {
                return NotFound(ApiResponse.ErrorResponse(result.Error, "Payment not found"));
            }

            return BadRequest(ApiResponse.ErrorResponse(result.Error, "Refund failed"));
        }

        _logger.LogInformation("Refund processed successfully for payment {PaymentId}",
            request.PaymentId);

        return Ok(ApiResponse.SuccessResponse("Refund processed successfully"));
    }

    /// <summary>
    /// Gets a payment by ID.
    /// </summary>
    /// <param name="id">The payment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Payment details.</returns>
    /// <response code="200">Payment found.</response>
    /// <response code="404">Payment not found.</response>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPaymentById(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving payment {PaymentId}", id);

        var payment = await _unitOfWork.Payments.GetByIdAsync(id, cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning("Payment {PaymentId} not found", id);
            return NotFound(ApiResponse<PaymentDto>.ErrorResponse(
                $"Payment with ID {id} not found",
                "Payment not found"));
        }

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(payment.AppointmentId, cancellationToken);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} for payment {PaymentId} not found", payment.AppointmentId, id);
            return NotFound(ApiResponse<PaymentDto>.ErrorResponse(
                $"Associated appointment not found", "Payment not found"));
        }

        var role = User.GetRole();
        if (role == AppRoles.Patient && User.GetPatientId() != appointment.PatientId)
            return Forbid();
        if (role == AppRoles.Doctor && User.GetDoctorId() != appointment.DoctorId)
            return Forbid();

        var dto = MapToDto(payment);
        return Ok(ApiResponse<PaymentDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// Gets payment by appointment ID.
    /// </summary>
    /// <param name="appointmentId">The appointment ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Payment details.</returns>
    /// <response code="200">Payment found.</response>
    /// <response code="404">Payment not found.</response>
    [HttpGet("appointment/{appointmentId}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPaymentByAppointment(
        int appointmentId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving payment for appointment {AppointmentId}", appointmentId);

        var appointment = await _unitOfWork.Appointments.GetByIdAsync(appointmentId, cancellationToken);
        if (appointment == null)
        {
            _logger.LogWarning("Appointment {AppointmentId} not found", appointmentId);
            return NotFound(ApiResponse<PaymentDto>.ErrorResponse(
                $"Appointment with ID {appointmentId} not found",
                "Payment not found"));
        }

        var role = User.GetRole();
        if (role == AppRoles.Patient && User.GetPatientId() != appointment.PatientId)
            return Forbid();
        if (role == AppRoles.Doctor && User.GetDoctorId() != appointment.DoctorId)
            return Forbid();

        var payment = await _unitOfWork.Payments
            .GetByAppointmentIdAsync(appointmentId, cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning("No payment found for appointment {AppointmentId}", appointmentId);
            return NotFound(ApiResponse<PaymentDto>.ErrorResponse(
                $"No payment found for appointment {appointmentId}",
                "Payment not found"));
        }

        var dto = MapToDto(payment);
        return Ok(ApiResponse<PaymentDto>.SuccessResponse(dto));
    }

    /// <summary>
    /// Gets all payments (admin only).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all payments.</returns>
    /// <response code="200">Payments retrieved successfully.</response>
    /// <summary>
    /// Gets paginated list of all payments (admin only).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPayments(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all payments - Page: {Page}, Size: {Size}", pageNumber, pageSize);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var pagedEntities = await _unitOfWork.Payments
            .GetPagedAsync(pageNumber, pageSize, cancellationToken);
        var pagedResult = new PagedResult<PaymentDto>(
            pagedEntities.Items.Select(MapToDto),
            pagedEntities.PageNumber,
            pagedEntities.PageSize,
            pagedEntities.TotalCount);

        return Ok(ApiResponse<PagedResult<PaymentDto>>.SuccessResponse(
            pagedResult,
            $"Retrieved page {pageNumber} of {pagedResult.TotalPages} ({pagedResult.Items.Count()} payment(s))"));
    }

    /// <summary>
    /// Maps Payment entity to PaymentDto.
    /// </summary>
    private static PaymentDto MapToDto(Domain.Entities.Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            AppointmentId = payment.AppointmentId,
            Amount = payment.Amount.Amount,
            Currency = payment.Amount.Currency,
            Status = payment.Status.ToString(),
            TransactionId = payment.TransactionId?.Value,
            PaymentMethod = payment.PaymentMethod,
            PaymentProcessor = payment.PaymentProcessor,
            PaidAt = payment.PaidAt,
            RefundedAt = payment.RefundedAt,
            RefundTransactionId = payment.RefundTransactionId?.Value,
            FailureReason = payment.FailureReason,
            CreatedAt = payment.CreatedAt
        };
    }
}
