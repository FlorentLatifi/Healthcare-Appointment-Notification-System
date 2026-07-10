using FluentAssertions;
using Healthcare.Application.Commands.CreatePatient;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

public class CreatePatientHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPatientRepository> _patientRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly CreatePatientHandler _handler;

    public CreatePatientHandlerTests()
    {
        _patientRepoMock = new Mock<IPatientRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _unitOfWorkMock.Setup(u => u.Patients).Returns(_patientRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _handler = new CreatePatientHandler(_unitOfWorkMock.Object);
    }

    private static User MakeUnlinkedUser(int userId)
    {
        var email = Email.Create($"user{userId}@test.com");
        return User.Create($"user{userId}", email, "hash", UserRole.Patient);
    }

    private static User MakeLinkedUser(int userId)
    {
        var user = MakeUnlinkedUser(userId);
        user.LinkToPatient(999);
        return user;
    }

    private static CreatePatientCommand ValidCommand(int requestingUserId) => new()
    {
        FirstName = "John",
        LastName = "Doe",
        Email = "john.doe@test.com",
        PhoneNumber = "+355671234567",
        DateOfBirth = new DateTime(1990, 1, 1),
        Gender = "Male",
        Street = "1 Main St",
        City = "Tirana",
        State = "Tirana",
        PostalCode = "1001",
        Country = "Albania",
        RequestingUserId = requestingUserId,
    };

    [Fact]
    public async Task HandleAsync_WithValidCommand_ShouldCreatePatient()
    {
        var userId = 1;
        _patientRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUnlinkedUser(userId));

        var command = ValidCommand(userId);
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _patientRepoMock.Verify(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateEmail_ShouldReturnFailure()
    {
        var userId = 1;
        var existingPatient = Patient.Create(
            "Existing", "Patient",
            Email.Create("john.doe@test.com"),
            PhoneNumber.Create("+355671234567"),
            new DateTime(1990, 1, 1),
            Gender.Male,
            Address.Create("1 St", "City", "State", "1001", "Country"));

        _patientRepoMock.Setup(r => r.GetByEmailAsync("john.doe@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPatient);

        var command = ValidCommand(userId);
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
        _patientRepoMock.Verify(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenRequestingUserAlreadyLinked_ShouldNotPersistNewEntity()
    {
        var userId = 1;
        var linkedUser = MakeLinkedUser(userId);

        _patientRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(linkedUser);

        var command = ValidCommand(userId);
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already linked");
        _patientRepoMock.Verify(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithRequestingUserNotFound_ShouldReturnFailure()
    {
        var userId = 999;
        _patientRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = ValidCommand(userId);
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _patientRepoMock.Verify(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidInput_ShouldReturnFailure()
    {
        var userId = 1;
        _patientRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeUnlinkedUser(userId));

        var command = ValidCommand(userId);
        command.Email = "not-an-email";

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _patientRepoMock.Verify(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
