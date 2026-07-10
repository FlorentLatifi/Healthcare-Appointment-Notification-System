using FluentAssertions;
using Healthcare.Adapters.Caching;
using Healthcare.Adapters.Events.Handlers;
using Healthcare.Application.Commands.CreateDoctor;
using Healthcare.Application.Commands.DeactivateDoctor;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Events;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;
using Healthcare.Presentation.API.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using Healthcare.Presentation.API.Controllers;
using Healthcare.Presentation.API.Requests;
using System.Security.Claims;

namespace Healthcare.UnitTests.Adapters.Caching;

public sealed class DoctorCacheTests
{
    private readonly InMemoryDoctorCacheService _cache;
    private readonly List<Doctor> _testDoctors;

    public DoctorCacheTests()
    {
        var logger = new Mock<ILogger<InMemoryDoctorCacheService>>().Object;
        _cache = new InMemoryDoctorCacheService(logger);

        _testDoctors = new List<Doctor>
        {
            CreateDoctor(1, "Alice", "Smith", isActive: true, acceptingPatients: true),
            CreateDoctor(2, "Bob", "Jones", isActive: true, acceptingPatients: false),
            CreateDoctor(3, "Carol", "White", isActive: false, acceptingPatients: true)
        };
    }

    [Fact]
    public async Task GetAsync_MissThenSetThenHit_ReturnsCachedData()
    {
        var dtos = _testDoctors.Select(MapToDto).ToList();

        var miss = await _cache.GetAsync("all");
        miss.Should().BeNull();

        await _cache.SetAsync("all", dtos);

        var hit = await _cache.GetAsync("all");
        hit.Should().NotBeNull();
        hit!.Count.Should().Be(3);
    }

    [Fact]
    public async Task GetAsync_ExpiredEntry_ReturnsNull()
    {
        var shortTtl = TimeSpan.FromMilliseconds(50);
        var shortTtlCache = new InMemoryDoctorCacheService(
            new Mock<ILogger<InMemoryDoctorCacheService>>().Object,
            shortTtl);
        var dtos = _testDoctors.Select(MapToDto).ToList();

        await shortTtlCache.SetAsync("key", dtos);

        var hit = await shortTtlCache.GetAsync("key");
        hit.Should().NotBeNull("entry was just set and should be valid");

        await Task.Delay(100);

        var result = await shortTtlCache.GetAsync("key");
        result.Should().BeNull("TTL has elapsed and entry should have expired");
    }

    [Fact]
    public async Task InvalidateAllAsync_ClearsAllKeys()
    {
        var dtos = _testDoctors.Select(MapToDto).ToList();
        await _cache.SetAsync("all", dtos);
        await _cache.SetAsync("active", dtos);
        await _cache.SetAsync("accepting-patients", dtos);

        await _cache.InvalidateAllAsync();

        (await _cache.GetAsync("all")).Should().BeNull();
        (await _cache.GetAsync("active")).Should().BeNull();
        (await _cache.GetAsync("accepting-patients")).Should().BeNull();
    }

    [Fact]
    public async Task GetAllDoctors_UsesRepositoryPagination_NotCache()
    {
        var repoMock = new Mock<IDoctorRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.Doctors).Returns(repoMock.Object);

        var allDoctors = _testDoctors;
        repoMock.Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int pn, int ps, CancellationToken _) =>
            {
                var list = allDoctors.OrderBy(d => d.LastName).ThenBy(d => d.FirstName).ToList();
                var totalCount = list.Count;
                var items = list.Skip((pn - 1) * ps).Take(ps).ToList();
                return new PagedResult<Doctor>(items, pn, ps, totalCount);
            });

        var cache = new InMemoryDoctorCacheService(new Mock<ILogger<InMemoryDoctorCacheService>>().Object);
        var loggerMock = new Mock<ILogger<DoctorsController>>();

        var controller = new DoctorsController(
            new Mock<ICommandHandler<CreateDoctorCommand, Result<int>>>().Object,
            new Mock<ICommandHandler<DeactivateDoctorCommand, Result>>().Object,
            unitOfWorkMock.Object,
            cache,
            new Mock<IStringLocalizer<Messages>>().Object,
            loggerMock.Object);

        var firstResponse = await controller.GetAllDoctors(pageNumber: 1, pageSize: 20);
        var secondResponse = await controller.GetAllDoctors(pageNumber: 1, pageSize: 20);

        repoMock.Verify(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateDoctor_CallsHandlerAndReturns201()
    {
        var repoMock = new Mock<IDoctorRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.Doctors).Returns(repoMock.Object);

        repoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Doctor?)null);

        var createHandlerMock = new Mock<ICommandHandler<CreateDoctorCommand, Result<int>>>();
        createHandlerMock.Setup(h => h.HandleAsync(
                It.IsAny<CreateDoctorCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(42));

        var cache = new InMemoryDoctorCacheService(new Mock<ILogger<InMemoryDoctorCacheService>>().Object);
        var loggerMock = new Mock<ILogger<DoctorsController>>();

        var controller = new DoctorsController(
            createHandlerMock.Object,
            new Mock<ICommandHandler<DeactivateDoctorCommand, Result>>().Object,
            unitOfWorkMock.Object,
            cache,
            new Mock<IStringLocalizer<Messages>>().Object,
            loggerMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
                {
                    new(ClaimTypes.Role, "Admin")
                }))
            }
        };

        var request = new CreateDoctorRequest
        {
            FirstName = "New",
            LastName = "Doctor",
            Email = "new@test.com",
            PhoneNumber = "+355672345678",
            LicenseNumber = "LIC12345",
            Specialty = "Cardiology",
            ConsultationFeeAmount = 100m,
            ConsultationFeeCurrency = "USD",
            YearsOfExperience = 5
        };
        var response = await controller.CreateDoctor(request, CancellationToken.None);

        response.Should().BeOfType<CreatedAtActionResult>();
        createHandlerMock.Verify(h => h.HandleAsync(
            It.IsAny<CreateDoctorCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidatDoctorCacheHandler_ClearsCache()
    {
        var dtos = _testDoctors.Select(MapToDto).ToList();
        await _cache.SetAsync("all", dtos);
        await _cache.SetAsync("active", dtos);

        var handler = new InvalidateDoctorCacheHandler(
            _cache,
            new Mock<ILogger<InvalidateDoctorCacheHandler>>().Object);

        await handler.HandleAsync(new DoctorCacheInvalidationNeededEvent());

        (await _cache.GetAsync("all")).Should().BeNull();
        (await _cache.GetAsync("active")).Should().BeNull();
    }

    [Fact]
    public async Task DeleteDoctor_CallsHandlerAndReturns204()
    {
        var repoMock = new Mock<IDoctorRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.Doctors).Returns(repoMock.Object);

        var doctor = CreateDoctor(1, "Delete", "Me", isActive: true, acceptingPatients: true);
        repoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doctor);

        var deactivateHandlerMock = new Mock<ICommandHandler<DeactivateDoctorCommand, Result>>();
        deactivateHandlerMock.Setup(h => h.HandleAsync(
                It.IsAny<DeactivateDoctorCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var cache = new InMemoryDoctorCacheService(new Mock<ILogger<InMemoryDoctorCacheService>>().Object);
        var loggerMock = new Mock<ILogger<DoctorsController>>();

        var controller = new DoctorsController(
            new Mock<ICommandHandler<CreateDoctorCommand, Result<int>>>().Object,
            deactivateHandlerMock.Object,
            unitOfWorkMock.Object,
            cache,
            new Mock<IStringLocalizer<Messages>>().Object,
            loggerMock.Object);

        var response = await controller.DeleteDoctor(1, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
        deactivateHandlerMock.Verify(h => h.HandleAsync(
            It.IsAny<DeactivateDoctorCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Doctor CreateDoctor(int id, string firstName, string lastName,
        bool isActive, bool acceptingPatients)
    {
        var doctor = Doctor.Create(
            firstName,
            lastName,
            Email.Create($"{firstName.ToLower()}.{lastName.ToLower()}@test.com"),
            PhoneNumber.Create("+355672345678"),
            $"LIC{id:D5}",
            Money.Create(100m, "USD"),
            10,
            Specialty.Cardiology);

        var prop = typeof(Healthcare.Domain.Common.Entity).GetProperty("Id",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        prop?.SetValue(doctor, id);

        if (!isActive)
        {
            doctor.Deactivate();
        }

        if (!acceptingPatients)
        {
            doctor.StopAcceptingPatients();
        }

        return doctor;
    }

    private static DoctorDto MapToDto(Doctor doctor)
    {
        return new DoctorDto
        {
            Id = doctor.Id,
            FirstName = doctor.FirstName,
            LastName = doctor.LastName,
            FullName = doctor.FullName,
            Email = doctor.Email.Value,
            PhoneNumber = doctor.PhoneNumber.Value,
            LicenseNumber = doctor.LicenseNumber,
            Specialties = doctor.Specialties.Select(s => s.ToString()).ToList(),
            ConsultationFeeAmount = doctor.ConsultationFee.Amount,
            ConsultationFeeCurrency = doctor.ConsultationFee.Currency,
            IsAcceptingPatients = doctor.IsAcceptingPatients,
            IsActive = doctor.IsActive,
            YearsOfExperience = doctor.YearsOfExperience,
            CreatedAt = doctor.CreatedAt
        };
    }
}
