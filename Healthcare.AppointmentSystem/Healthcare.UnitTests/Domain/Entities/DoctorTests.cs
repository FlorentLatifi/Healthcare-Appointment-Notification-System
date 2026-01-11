using FluentAssertions;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Xunit;

namespace Healthcare.UnitTests.Domain.Entities;

/// <summary>
/// Unit tests for Doctor entity.
/// </summary>
public class DoctorTests
{
    #region Test Data Helpers

    private static Email CreateTestEmail(string email = "doctor@test.com")
        => Email.Create(email);

    private static PhoneNumber CreateTestPhone(string phone = "+38349987654")
        => PhoneNumber.Create(phone);

    private static Money CreateTestFee(decimal amount = 50, string currency = "USD")
        => Money.Create(amount, currency);

    #endregion

    #region Creation Tests

    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var email = CreateTestEmail();
        var phone = CreateTestPhone();
        var fee = CreateTestFee();

        // Act
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            email,
            phone,
            "LIC-12345",
            fee,
            10,
            Specialty.GeneralPractice);

        // Assert
        doctor.Should().NotBeNull();
        doctor.FirstName.Should().Be("Jane");
        doctor.LastName.Should().Be("Smith");
        doctor.Email.Should().Be(email);
        doctor.PhoneNumber.Should().Be(phone);
        doctor.LicenseNumber.Should().Be("LIC-12345");
        doctor.ConsultationFee.Should().Be(fee);
        doctor.YearsOfExperience.Should().Be(10);
        doctor.Specialties.Should().ContainSingle();
        doctor.Specialties.Should().Contain(Specialty.GeneralPractice);
        doctor.IsActive.Should().BeTrue();
        doctor.IsAcceptingPatients.Should().BeTrue();
        doctor.FullName.Should().Be("Dr. Jane Smith");
    }

    [Fact]
    public void Create_ShouldTrimNames()
    {
        // Act
        var doctor = Doctor.Create(
            "  Jane  ",
            "  Smith  ",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Assert
        doctor.FirstName.Should().Be("Jane");
        doctor.LastName.Should().Be("Smith");
    }

    [Fact]
    public void Create_ShouldNormalizeLicenseNumberToUpperCase()
    {
        // Act
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "lic-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Assert
        doctor.LicenseNumber.Should().Be("LIC-12345");
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceFirstName_ShouldThrowArgumentException(string firstName)
    {
        // Act
        Action act = () => Doctor.Create(
            firstName,
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*firstName*");
    }

    [Fact]
    public void Create_WithNegativeExperience_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            -1,
            Specialty.Cardiology);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void Create_WithExperienceOver70Years_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            71,
            Specialty.Cardiology);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot exceed 70*");
    }

    [Theory]
    [InlineData("LIC")]
    [InlineData("1234")]
    public void Create_WithTooShortLicenseNumber_ShouldThrowArgumentException(string license)
    {
        // Act
        Action act = () => Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            license,
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 5 characters*");
    }

    // ✅ SHTO këtë test të ri për null/whitespace:
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceLicenseNumber_ShouldThrowArgumentException(string license)
    {
        // Act
        Action act = () => Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            license,
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or whitespace*");
    }

    #endregion

    #region Specialty Management Tests

    [Fact]
    public void AddSpecialty_WithValidSpecialty_ShouldSucceed()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.GeneralPractice);

        // Act
        doctor.AddSpecialty(Specialty.Cardiology);

        // Assert
        doctor.Specialties.Should().HaveCount(2);
        doctor.Specialties.Should().Contain(Specialty.GeneralPractice);
        doctor.Specialties.Should().Contain(Specialty.Cardiology);
        doctor.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddSpecialty_WhenAlreadyHasSpecialty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Act
        Action act = () => doctor.AddSpecialty(Specialty.Cardiology);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already has*");
    }

    [Fact]
    public void AddSpecialty_WhenAlreadyHas3Specialties_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.GeneralPractice);

        doctor.AddSpecialty(Specialty.Cardiology);
        doctor.AddSpecialty(Specialty.Dermatology);

        // Act
        Action act = () => doctor.AddSpecialty(Specialty.Pediatrics);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot have more than 3*");
    }

    [Fact]
    public void RemoveSpecialty_WithValidSpecialty_ShouldSucceed()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.GeneralPractice);

        doctor.AddSpecialty(Specialty.Cardiology);

        // Act
        doctor.RemoveSpecialty(Specialty.Cardiology);

        // Assert
        doctor.Specialties.Should().ContainSingle();
        doctor.Specialties.Should().Contain(Specialty.GeneralPractice);
        doctor.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void RemoveSpecialty_WhenOnlyOneSpecialty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.GeneralPractice);

        // Act
        Action act = () => doctor.RemoveSpecialty(Specialty.GeneralPractice);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one specialty*");
    }

    [Fact]
    public void HasSpecialty_WhenDoctorHasSpecialty_ShouldReturnTrue()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Act & Assert
        doctor.HasSpecialty(Specialty.Cardiology).Should().BeTrue();
    }

    [Fact]
    public void HasSpecialty_WhenDoctorDoesNotHaveSpecialty_ShouldReturnFalse()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Act & Assert
        doctor.HasSpecialty(Specialty.Pediatrics).Should().BeFalse();
    }

    #endregion

    #region Consultation Fee Management Tests

    [Fact]
    public void UpdateConsultationFee_WithValidFee_ShouldSucceed()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            Money.Create(50, "USD"),
            10,
            Specialty.Cardiology);

        var newFee = Money.Create(75, "USD");

        // Act
        doctor.UpdateConsultationFee(newFee);

        // Assert
        doctor.ConsultationFee.Should().Be(newFee);
        doctor.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateConsultationFee_WithMoreThan50PercentReduction_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            Money.Create(100, "USD"),
            10,
            Specialty.Cardiology);

        var tooLowFee = Money.Create(40, "USD"); // 60% reduction

        // Act
        Action act = () => doctor.UpdateConsultationFee(tooLowFee);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*more than 50%*");
    }

    #endregion

    #region Contact Information Tests

    [Fact]
    public void UpdateContactInformation_WithValidData_ShouldSucceed()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        var newEmail = Email.Create("newemail@test.com");
        var newPhone = PhoneNumber.Create("+38349111111");

        // Act
        doctor.UpdateContactInformation(newEmail, newPhone);

        // Assert
        doctor.Email.Should().Be(newEmail);
        doctor.PhoneNumber.Should().Be(newPhone);
        doctor.ModifiedAt.Should().NotBeNull();
    }

    #endregion

    #region Accepting Patients Tests

    [Fact]
    public void StopAcceptingPatients_WhenAccepting_ShouldSucceed()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Act
        doctor.StopAcceptingPatients();

        // Assert
        doctor.IsAcceptingPatients.Should().BeFalse();
        doctor.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void StopAcceptingPatients_WhenAlreadyNotAccepting_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        doctor.StopAcceptingPatients();

        // Act
        Action act = () => doctor.StopAcceptingPatients();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already not accepting*");
    }

    [Fact]
    public void StartAcceptingPatients_WhenNotAccepting_ShouldSucceed()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        doctor.StopAcceptingPatients();

        // Act
        doctor.StartAcceptingPatients();

        // Assert
        doctor.IsAcceptingPatients.Should().BeTrue();
        doctor.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void StartAcceptingPatients_WhenInactive_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        doctor.Deactivate();

        // Act
        Action act = () => doctor.StartAcceptingPatients();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*inactive*");
    }

    #endregion

    #region Activate/Deactivate Tests

    [Fact]
    public void Deactivate_WhenActive_ShouldSucceed()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Act
        doctor.Deactivate();

        // Assert
        doctor.IsActive.Should().BeFalse();
        doctor.IsAcceptingPatients.Should().BeFalse(); // Also stops accepting
        doctor.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reactivate_WhenInactive_ShouldSucceed()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        doctor.Deactivate();

        // Act
        doctor.Reactivate();

        // Assert
        doctor.IsActive.Should().BeTrue();
        doctor.ModifiedAt.Should().NotBeNull();
    }

    #endregion

    #region Experience Tests

    [Fact]
    public void IsExperienced_WithLessThan10Years_ShouldReturnFalse()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            9,
            Specialty.Cardiology);

        // Act & Assert
        doctor.IsExperienced().Should().BeFalse();
    }

    [Fact]
    public void IsExperienced_With10OrMoreYears_ShouldReturnTrue()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Act & Assert
        doctor.IsExperienced().Should().BeTrue();
    }

    #endregion

    #region FullName Tests

    [Fact]
    public void FullName_ShouldIncludeDrTitle()
    {
        // Arrange
        var doctor = Doctor.Create(
            "Jane",
            "Smith",
            CreateTestEmail(),
            CreateTestPhone(),
            "LIC-12345",
            CreateTestFee(),
            10,
            Specialty.Cardiology);

        // Act & Assert
        doctor.FullName.Should().Be("Dr. Jane Smith");
    }

    #endregion
}