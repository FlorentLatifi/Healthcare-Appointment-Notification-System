using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Repositories;

namespace Healthcare.Application.Queries.Analytics;

public sealed class GetRevenueReportHandler : IQueryHandler<GetRevenueReportQuery, Result<RevenueReportDto>>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetRevenueReportHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<RevenueReportDto>> HandleAsync(
        GetRevenueReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var totalRevenue = await _paymentRepository.GetTotalRevenueAsync(
            query.DateFrom, query.DateTo, cancellationToken);

        var dto = new RevenueReportDto
        {
            DateFrom = query.DateFrom,
            DateTo = query.DateTo,
            TotalRevenue = totalRevenue,
            Currency = "USD"
        };

        if (!string.IsNullOrWhiteSpace(query.GroupBy))
        {
            if (string.Equals(query.GroupBy, "doctor", StringComparison.OrdinalIgnoreCase))
            {
                var byDoctor = await _paymentRepository.GetRevenueByDoctorAsync(
                    query.DateFrom, query.DateTo, cancellationToken);
                dto.ByDoctor = byDoctor
                    .Select(r => new RevenueByDoctorItemDto
                    {
                        DoctorId = r.DoctorId,
                        DoctorName = $"Dr. {r.FirstName} {r.LastName}",
                        Revenue = r.Revenue
                    })
                    .ToList();
            }
            else if (string.Equals(query.GroupBy, "specialty", StringComparison.OrdinalIgnoreCase))
            {
                var bySpecialty = await _paymentRepository.GetRevenueBySpecialtyAsync(
                    query.DateFrom, query.DateTo, cancellationToken);
                dto.BySpecialty = bySpecialty
                    .Select(r => new RevenueBySpecialtyItemDto
                    {
                        Specialty = r.Specialty,
                        Revenue = r.Revenue
                    })
                    .ToList();
            }
        }

        return Result<RevenueReportDto>.Success(dto);
    }
}
