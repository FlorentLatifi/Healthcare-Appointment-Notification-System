using System.Text;
using Healthcare.Domain.Entities;

namespace Healthcare.Application.Services;

public static class IcsCalendarService
{
    public static string GenerateAppointmentIcs(Appointment appointment)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Healthcare System//Appointments//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");

        sb.AppendLine("BEGIN:VEVENT");

        var uid = $"appointment-{appointment.ReferenceCode}@healthcare";
        sb.AppendLine(FoldLine($"UID:{uid}"));

        var now = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        sb.AppendLine($"DTSTAMP:{now}");

        var dtStart = appointment.ScheduledTime.Value.ToString("yyyyMMddTHHmmss");
        sb.AppendLine($"DTSTART;VALUE=DATE-TIME:{dtStart}");

        var dtEnd = appointment.ScheduledTime.Value.AddMinutes(30).ToString("yyyyMMddTHHmmss");
        sb.AppendLine($"DTEND;VALUE=DATE-TIME:{dtEnd}");

        var summary = $"Appointment with Dr. {appointment.Doctor?.FullName ?? "Doctor"} - {appointment.ReferenceCode}";
        sb.AppendLine(FoldLine($"SUMMARY:{summary}"));

        var description = appointment.Reason ?? "No reason provided";
        sb.AppendLine(FoldLine($"DESCRIPTION:{EscapeIcsText(description)}"));

        var location = $"Dr. {appointment.Doctor?.FullName ?? "Doctor"}";
        if (appointment.Doctor?.Email is not null)
            location += $" ({appointment.Doctor.Email.Value})";
        sb.AppendLine(FoldLine($"LOCATION:{EscapeIcsText(location)}"));

        var icsStatus = appointment.Status switch
        {
            Domain.Enums.AppointmentStatus.Pending => "TENTATIVE",
            Domain.Enums.AppointmentStatus.Confirmed => "CONFIRMED",
            Domain.Enums.AppointmentStatus.Completed => "CONFIRMED",
            Domain.Enums.AppointmentStatus.Cancelled => "CANCELLED",
            Domain.Enums.AppointmentStatus.NoShow => "CANCELLED",
            _ => "TENTATIVE"
        };
        sb.AppendLine($"STATUS:{icsStatus}");

        sb.AppendLine("END:VEVENT");
        sb.AppendLine("END:VCALENDAR");

        return sb.ToString();
    }

    private static string FoldLine(string line)
    {
        if (line.Length <= 75) return line;

        var sb = new StringBuilder();
        sb.AppendLine(line[..75]);
        for (int i = 75; i < line.Length; i += 74)
        {
            var chunk = i + 74 < line.Length ? line[i..(i + 74)] : line[i..];
            sb.AppendLine($" {chunk}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string EscapeIcsText(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }
}
