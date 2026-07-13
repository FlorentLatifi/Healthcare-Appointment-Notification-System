using System.Security.Claims;
using FluentAssertions;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Presentation.API.Authorization;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Presentation.Authorization;

public sealed class PatientRecordAccessTests
{
    private static ClaimsPrincipal Principal(string role, int? patientId = null, int? doctorId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Role, role),
        };
        if (patientId.HasValue)
            claims.Add(new Claim("patient_id", patientId.Value.ToString()));
        if (doctorId.HasValue)
            claims.Add(new Claim("doctor_id", doctorId.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task Patient_OwnRecord_Allowed()
    {
        var appts = new Mock<IAppointmentRepository>();
        var deny = await PatientRecordAccess.GetDenyReasonForPatientDataAsync(
            Principal(AppRoles.Patient, patientId: 5), 5, appts.Object);
        deny.Should().BeNull();
    }

    [Fact]
    public async Task Patient_OtherRecord_Denied()
    {
        var appts = new Mock<IAppointmentRepository>();
        var deny = await PatientRecordAccess.GetDenyReasonForPatientDataAsync(
            Principal(AppRoles.Patient, patientId: 5), 9, appts.Object);
        deny.Should().Be("patient_not_owner");
    }

    [Fact]
    public async Task Doctor_NoLinkedProfile_Denied()
    {
        var appts = new Mock<IAppointmentRepository>();
        var deny = await PatientRecordAccess.GetDenyReasonForPatientDataAsync(
            Principal(AppRoles.Doctor), 9, appts.Object);
        deny.Should().Be("doctor_profile_not_linked");
    }

    [Fact]
    public async Task Doctor_NoCareRelationship_Denied()
    {
        var appts = new Mock<IAppointmentRepository>();
        appts.Setup(a => a.HasDoctorPatientCareRelationshipAsync(3, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var deny = await PatientRecordAccess.GetDenyReasonForPatientDataAsync(
            Principal(AppRoles.Doctor, doctorId: 3), 9, appts.Object);
        deny.Should().Be("doctor_no_care_relationship");
    }

    [Fact]
    public async Task Doctor_WithCareRelationship_Allowed()
    {
        var appts = new Mock<IAppointmentRepository>();
        appts.Setup(a => a.HasDoctorPatientCareRelationshipAsync(3, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var deny = await PatientRecordAccess.GetDenyReasonForPatientDataAsync(
            Principal(AppRoles.Doctor, doctorId: 3), 9, appts.Object);
        deny.Should().BeNull();
    }

    [Fact]
    public async Task Admin_AlwaysAllowed()
    {
        var appts = new Mock<IAppointmentRepository>();
        var deny = await PatientRecordAccess.GetDenyReasonForPatientDataAsync(
            Principal(AppRoles.Admin), 999, appts.Object);
        deny.Should().BeNull();
        appts.Verify(
            a => a.HasDoctorPatientCareRelationshipAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
