using FluentAssertions;
using Healthcare.Application.Commands.CreateDoctor;
using Healthcare.Application.Common;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Healthcare.UnitTests.Application.Commands;

/// <summary>
/// Moq orchestration tests. Identity / User.DoctorId linking against real EF is covered by
/// <see cref="CreateProfileLinkIdentityRegressionTests"/> (see Helpers/README.md).
/// </summary>
public class CreateDoctorHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IDoctorRepository> _doctorRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IDomainEventDispatcher> _eventDispatcherMock;
    private readonly CreateDoctorHandler _handler;

    public CreateDoctorHandlerTests()
    {
        _doctorRepoMock = new Mock<IDoctorRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _unitOfWorkMock.Setup(u => u.Doctors).Returns(_doctorRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _eventDispatcherMock = new Mock<IDomainEventDispatcher>();
        _handler = new CreateDoctorHandler(_unitOfWorkMock.Object, _eventDispatcherMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_ShouldCreateDoctorAndDispatchEvent()
    {
        _doctorRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var command = new CreateDoctorCommand
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "dr.smith@test.com",
            PhoneNumber = "+355672345678",
            LicenseNumber = "LIC-12345",
            Specialty = "Cardiology",
            ConsultationFeeAmount = 100m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 10
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        _doctorRepoMock.Verify(r => r.AddAsync(It.IsAny<Doctor>(), It.IsAny<CancellationToken>()), Times.Once);
        // Identity INSERT (no requesting user → no second SaveChanges for link).
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventDispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<DoctorCacheInvalidationNeededEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateEmail_ShouldReturnFailure()
    {
        var existingDoctor = Doctor.Create(
            "Existing",
            "Doctor",
            Email.Create("dr.smith@test.com"),
            PhoneNumber.Create("+355672345678"),
            "LIC-99999",
            Money.Create(100m, "USD"),
            5,
            Specialty.Cardiology);

        _doctorRepoMock.Setup(r => r.GetByEmailAsync("dr.smith@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDoctor);

        var command = new CreateDoctorCommand
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "dr.smith@test.com",
            PhoneNumber = "+355672345678",
            LicenseNumber = "LIC-12345",
            Specialty = "Cardiology",
            ConsultationFeeAmount = 100m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 10
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
        result.Error.Should().Contain("dr.smith@test.com");

        _doctorRepoMock.Verify(r => r.AddAsync(It.IsAny<Doctor>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventDispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<DoctorCacheInvalidationNeededEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidSpecialty_ShouldReturnFailure()
    {
        _doctorRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var command = new CreateDoctorCommand
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "dr.smith@test.com",
            PhoneNumber = "+355672345678",
            LicenseNumber = "LIC-12345",
            Specialty = "NotARealSpecialty",
            ConsultationFeeAmount = 100m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 10
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid specialty");

        _doctorRepoMock.Verify(r => r.AddAsync(It.IsAny<Doctor>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventDispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<DoctorCacheInvalidationNeededEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidEmail_ShouldReturnFailure()
    {
        _doctorRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var command = new CreateDoctorCommand
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "not-an-email",
            PhoneNumber = "+355672345678",
            LicenseNumber = "LIC-12345",
            Specialty = "Cardiology",
            ConsultationFeeAmount = 100m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 10
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid input");

        _doctorRepoMock.Verify(r => r.AddAsync(It.IsAny<Doctor>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventDispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<DoctorCacheInvalidationNeededEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenRequestingUserAlreadyLinked_ShouldNotPersistNewEntity()
    {
        var userId = 1;
        var email = Email.Create($"user{userId}@test.com");
        var linkedUser = User.Create($"user{userId}", email, "hash", UserRole.Doctor);
        linkedUser.LinkToDoctor(999);

        _doctorRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(linkedUser);

        var command = new CreateDoctorCommand
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "dr.smith@test.com",
            PhoneNumber = "+355672345678",
            LicenseNumber = "LIC-12345",
            Specialty = "Cardiology",
            ConsultationFeeAmount = 100m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 10,
            RequestingUserId = userId
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already linked");

        _doctorRepoMock.Verify(r => r.AddAsync(It.IsAny<Doctor>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _eventDispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<DoctorCacheInvalidationNeededEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenRequestingUserAlreadyLinked_WithNoRequestingUser_ShouldCreateDoctor()
    {
        // When RequestingUserId is null, the user-link check is skipped entirely.
        _doctorRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var command = new CreateDoctorCommand
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "dr.smith@test.com",
            PhoneNumber = "+355672345678",
            LicenseNumber = "LIC-12345",
            Specialty = "Cardiology",
            ConsultationFeeAmount = 100m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 10,
            RequestingUserId = null
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _doctorRepoMock.Verify(r => r.AddAsync(It.IsAny<Doctor>(), It.IsAny<CancellationToken>()), Times.Once);
        // Identity INSERT only when no requesting user is linked.
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventDispatcherMock.Verify(d => d.DispatchAsync(
            It.IsAny<DoctorCacheInvalidationNeededEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithRequestingUser_ShouldSaveTwiceForIdentityThenLink()
    {
        var userId = 1;
        var email = Email.Create($"user{userId}@test.com");
        var unlinkedUser = User.Create($"user{userId}", email, "hash", UserRole.Doctor);

        _doctorRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unlinkedUser);

        var command = new CreateDoctorCommand
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "dr.smith@test.com",
            PhoneNumber = "+355672345678",
            LicenseNumber = "LIC-12345",
            Specialty = "Cardiology",
            ConsultationFeeAmount = 100m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 10,
            RequestingUserId = userId
        };

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _doctorRepoMock.Verify(r => r.AddAsync(It.IsAny<Doctor>(), It.IsAny<CancellationToken>()), Times.Once);
        _userRepoMock.Verify(r => r.UpdateAsync(unlinkedUser, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
