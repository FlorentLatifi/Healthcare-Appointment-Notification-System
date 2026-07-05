namespace Healthcare.Application.Queries.Analytics;

public sealed record DoctorRevenueResult(int DoctorId, string FirstName, string LastName, decimal Revenue);

public sealed record SpecialtyRevenueResult(string Specialty, decimal Revenue);

public sealed record StatusCountsResult(int Created, int Confirmed, int Completed, int Cancelled, int NoShow);

public sealed record DailyVolumeResult(DateTime Date, int Created, int Confirmed, int Cancelled);

public sealed record WeeklyVolumeResult(int Year, int Week, int Created, int Confirmed, int Cancelled);
