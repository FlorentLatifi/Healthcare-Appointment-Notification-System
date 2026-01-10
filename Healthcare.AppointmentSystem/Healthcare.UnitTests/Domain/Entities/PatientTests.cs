using FluentAssertions;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Xunit;

namespace Healthcare.UnitTests.Domain.Entities;

/// <summary>
/// Unit tests for Patient entity.
/// </summary>
/// <remarks>
/// Testing Strategy:
/// - Factory method creation
/// - Business rules validation
/// - State management (Active/Inactive)
/// - Contact information updates
/// - Age calculations
/// </remarks>
public class PatientTests
{
    #region Test Data Helpers

    private static Email CreateTestEmail(string email = "patient@test.com")
        => Email.Create(email);

    private static PhoneNumber CreateTestPhone(string phone = "+38349123456")
        => PhoneNumber.Create(phone);

    private static Address CreateTestAddress()
        => Address.Create("123 Main St", "Pristina", "Kosovo", "10000", "Kosovo");

    #endregion

    #region Creation Tests

    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var email = CreateTestEmail();
        var phone = CreateTestPhone();
        var address = CreateTestAddress();
        var dateOfBirth = new DateTime(1990, 5, 15);

        // Act
        var patient = Patient.Create(
            "John",
            "Doe",
            email,
            phone,
            dateOfBirth,
            Gender.Male,
            address);

        // Assert
        patient.Should().NotBeNull();
        patient.FirstName.Should().Be("John");
        patient.LastName.Should().Be("Doe");
        patient.Email.Should().Be(email);
        patient.PhoneNumber.Should().Be(phone);
        patient.DateOfBirth.Should().Be(dateOfBirth.Date);
        patient.Gender.Should().Be(Gender.Male);
        patient.Address.Should().Be(address);
        patient.IsActive.Should().BeTrue();
        patient.FullName.Should().Be("John Doe");
    }

    [Fact]
    public void Create_ShouldTrimNames()
    {
        // Act
        var patient = Patient.Create(
            "  John  ",
            "  Doe  ",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        // Assert
        patient.FirstName.Should().Be("John");
        patient.LastName.Should().Be("Doe");
    }

    [Fact]
    public void Create_ShouldSetCreatedAtToCurrentTime()
    {
        // Act
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        // Assert
        patient.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_ShouldRemoveTimeFromDateOfBirth()
    {
        // Arrange
        var dateWithTime = new DateTime(1990, 5, 15, 14, 30, 45);

        // Act
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            dateWithTime,
            Gender.Male,
            CreateTestAddress());

        // Assert
        patient.DateOfBirth.Should().Be(new DateTime(1990, 5, 15)); // Time removed
        patient.DateOfBirth.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceFirstName_ShouldThrowArgumentException(string firstName)
    {
        // Act
        Action act = () => Patient.Create(
            firstName,
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*firstName*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceLastName_ShouldThrowArgumentException(string lastName)
    {
        // Act
        Action act = () => Patient.Create(
            "John",
            lastName,
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*lastName*");
    }

    [Fact]
    public void Create_WithFutureDateOfBirth_ShouldThrowArgumentException()
    {
        // Arrange
        var futureDate = DateTime.Today.AddDays(1);

        // Act
        Action act = () => Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            futureDate,
            Gender.Male,
            CreateTestAddress());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be in the past*");
    }

    [Fact]
    public void Create_WithDateOfBirthToday_ShouldThrowArgumentException()
    {
        // Arrange
        var today = DateTime.Today;

        // Act
        Action act = () => Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            today,
            Gender.Male,
            CreateTestAddress());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be in the past*");
    }

    [Fact]
    public void Create_WithAgeOver150Years_ShouldThrowArgumentException()
    {
        // Arrange
        var tooOld = DateTime.Today.AddYears(-151);

        // Act
        Action act = () => Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            tooOld,
            Gender.Male,
            CreateTestAddress());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be over 150 years old*");
    }

    #endregion

    #region Age Calculation Tests

    [Fact]
    public void Age_ShouldCalculateCorrectly()
    {
        // Arrange
        var dateOfBirth = DateTime.Today.AddYears(-30);

        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            dateOfBirth,
            Gender.Male,
            CreateTestAddress());

        // Act
        var age = patient.Age;

        // Assert
        age.Should().Be(30);
    }

    [Fact]
    public void Age_BeforeBirthday_ShouldNotIncludeCurrentYear()
    {
        // Arrange - Birthday is tomorrow
        var dateOfBirth = DateTime.Today.AddYears(-30).AddDays(1);

        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            dateOfBirth,
            Gender.Male,
            CreateTestAddress());

        // Act
        var age = patient.Age;

        // Assert
        age.Should().Be(29); // Not yet 30
    }

    [Fact]
    public void IsMinor_WithAge17_ShouldReturnTrue()
    {
        // Arrange
        var dateOfBirth = DateTime.Today.AddYears(-17);

        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            dateOfBirth,
            Gender.Male,
            CreateTestAddress());

        // Act & Assert
        patient.IsMinor().Should().BeTrue();
    }

    [Fact]
    public void IsMinor_WithAge18_ShouldReturnFalse()
    {
        // Arrange
        var dateOfBirth = DateTime.Today.AddYears(-18);

        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            dateOfBirth,
            Gender.Male,
            CreateTestAddress());

        // Act & Assert
        patient.IsMinor().Should().BeFalse();
    }

    [Fact]
    public void IsSenior_WithAge64_ShouldReturnFalse()
    {
        // Arrange
        var dateOfBirth = DateTime.Today.AddYears(-64);

        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            dateOfBirth,
            Gender.Male,
            CreateTestAddress());

        // Act & Assert
        patient.IsSenior().Should().BeFalse();
    }

    [Fact]
    public void IsSenior_WithAge65_ShouldReturnTrue()
    {
        // Arrange
        var dateOfBirth = DateTime.Today.AddYears(-65);

        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            dateOfBirth,
            Gender.Male,
            CreateTestAddress());

        // Act & Assert
        patient.IsSenior().Should().BeTrue();
    }

    #endregion

    #region Update Contact Information Tests

    [Fact]
    public void UpdateContactInformation_WithValidData_ShouldSucceed()
    {
        // Arrange
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        var newEmail = Email.Create("newemail@test.com");
        var newPhone = PhoneNumber.Create("+38349999999");
        var newAddress = Address.Create("456 Oak Ave", "Prizren", "Kosovo", "20000", "Kosovo");

        // Act
        patient.UpdateContactInformation(newEmail, newPhone, newAddress);

        // Assert
        patient.Email.Should().Be(newEmail);
        patient.PhoneNumber.Should().Be(newPhone);
        patient.Address.Should().Be(newAddress);
        patient.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateContactInformation_ShouldSetModifiedAt()
    {
        // Arrange
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        patient.ModifiedAt.Should().BeNull();

        // Act
        patient.UpdateContactInformation(
            Email.Create("new@test.com"),
            CreateTestPhone(),
            CreateTestAddress());

        // Assert
        patient.ModifiedAt.Should().NotBeNull();
        patient.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Update Personal Information Tests

    [Fact]
    public void UpdatePersonalInformation_WithValidData_ShouldSucceed()
    {
        // Arrange
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        // Act
        patient.UpdatePersonalInformation("Jane", "Smith");

        // Assert
        patient.FirstName.Should().Be("Jane");
        patient.LastName.Should().Be("Smith");
        patient.FullName.Should().Be("Jane Smith");
        patient.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdatePersonalInformation_ShouldTrimNames()
    {
        // Arrange
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        // Act
        patient.UpdatePersonalInformation("  Jane  ", "  Smith  ");

        // Assert
        patient.FirstName.Should().Be("Jane");
        patient.LastName.Should().Be("Smith");
    }

    #endregion

    #region Activate/Deactivate Tests

    [Fact]
    public void Deactivate_WhenActive_ShouldSucceed()
    {
        // Arrange
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        patient.IsActive.Should().BeTrue();

        // Act
        patient.Deactivate();

        // Assert
        patient.IsActive.Should().BeFalse();
        patient.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        patient.Deactivate();

        // Act
        Action act = () => patient.Deactivate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already deactivated*");
    }

    [Fact]
    public void Reactivate_WhenInactive_ShouldSucceed()
    {
        // Arrange
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        patient.Deactivate();
        patient.IsActive.Should().BeFalse();

        // Act
        patient.Reactivate();

        // Assert
        patient.IsActive.Should().BeTrue();
        patient.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reactivate_WhenAlreadyActive_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        // Act
        Action act = () => patient.Reactivate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already active*");
    }

    #endregion

    #region FullName Property Tests

    [Fact]
    public void FullName_ShouldCombineFirstAndLastName()
    {
        // Arrange
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        // Act
        var fullName = patient.FullName;

        // Assert
        fullName.Should().Be("John Doe");
    }

    [Fact]
    public void FullName_AfterUpdate_ShouldReflectNewNames()
    {
        // Arrange
        var patient = Patient.Create(
            "John",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            new DateTime(1990, 1, 1),
            Gender.Male,
            CreateTestAddress());

        // Act
        patient.UpdatePersonalInformation("Jane", "Smith");

        // Assert
        patient.FullName.Should().Be("Jane Smith");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithVeryYoungPatient_ShouldSucceed()
    {
        // Arrange - 1 day old
        var dateOfBirth = DateTime.Today.AddDays(-1);

        // Act
        var patient = Patient.Create(
            "Baby",
            "Doe",
            CreateTestEmail(),
            CreateTestPhone(),
            dateOfBirth,
            Gender.Male,
            CreateTestAddress());

        // Assert
        patient.Age.Should().Be(0);
        patient.IsMinor().Should().BeTrue();
    }

    [Fact]
    public void Create_WithVeryOldPatient_ShouldSucceed()
    {
        // Arrange - 149 years old
        var dateOfBirth = DateTime.Today.AddYears(-149);

        // Act
        var patient = Patient.Create(
            "Very",
            "Old",
            CreateTestEmail(),
            CreateTestPhone(),
            dateOfBirth,
            Gender.Male,
            CreateTestAddress());

        // Assert
        patient.Age.Should().Be(149);
        patient.IsSenior().Should().BeTrue();
    }

    #endregion
}