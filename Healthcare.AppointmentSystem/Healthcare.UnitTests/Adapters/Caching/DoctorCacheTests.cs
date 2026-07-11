using FluentAssertions;
using Healthcare.Adapters.Caching;
using Healthcare.Adapters.Events.Handlers;
using Healthcare.Application.Commands.CreateDoctor;
using Healthcare.Application.Commands.DeactivateDoctor;
using Healthcare.Application.Common;
using Healthcare.Application.DTOs;
using Healthcare.Application.Ports.Caching;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.Events;
using Healthcare.Domain.ValueObjects;
using Healthcare.Presentation.API.Controllers;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Resources;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace Healthcare.UnitTests.Adapters.Caching;

public sealed class DoctorCacheTests
{
    private readonly CacheSettings _settings = new()
    {
        Enabled = true,
        DefaultTtlSeconds = 300,
        DoctorCatalogTtlSeconds = 300,
        DoctorScheduleTtlSeconds = 1800,
        AvailabilityTtlSeconds = 60
    };

    private readonly ICacheService _cache;
    private readonly IDoctorCacheService _doctorCache;
    private readonly IAvailabilityCacheService _availabilityCache;
    private readonly List<Doctor> _testDoctors;

    public DoctorCacheTests()
    {
        _cache = new InMemoryCacheService(_settings, new Mock<ILogger<InMemoryCacheService>>().Object);
        _doctorCache = new DoctorCacheService(_cache, _settings, new Mock<ILogger<DoctorCacheService>>().Object);
        _availabilityCache = new AvailabilityCacheService(
            _cache, _settings, new Mock<ILogger<AvailabilityCacheService>>().Object);

        _testDoctors = new List<Doctor>
        {
            CreateDoctor(1, "Alice", "Smith", isActive: true, acceptingPatients: true),
            CreateDoctor(2, "Bob", "Jones", isActive: true, acceptingPatients: false),
            CreateDoctor(3, "Carol", "White", isActive: false, acceptingPatients: true)
        };
    }

    [Fact]
    public async Task GetOrCreate_MissThenHit_ReturnsCachedData()
    {
        var loads = 0;
        var dto = MapToDto(_testDoctors[0]);

        var first = await _doctorCache.GetDoctorByIdAsync(1, _ =>
        {
            loads++;
            return Task.FromResult<DoctorDto?>(dto);
        });

        var second = await _doctorCache.GetDoctorByIdAsync(1, _ =>
        {
            loads++;
            return Task.FromResult<DoctorDto?>(dto);
        });

        first.Should().NotBeNull();
        second!.Id.Should().Be(1);
        loads.Should().Be(1, "second call should be a cache hit");
    }

    [Fact]
    public async Task GetOrCreate_AfterManualExpiry_Reloads()
    {
        // CacheSettings floors catalog TTL at 10s — exercise expiry via explicit remove.
        var loads = 0;
        var dto = MapToDto(_testDoctors[0]);

        await _doctorCache.GetDoctorByIdAsync(1, _ =>
        {
            loads++;
            return Task.FromResult<DoctorDto?>(dto);
        });

        await _cache.RemoveAsync(CacheKeys.DoctorById(1));

        await _doctorCache.GetDoctorByIdAsync(1, _ =>
        {
            loads++;
            return Task.FromResult<DoctorDto?>(dto);
        });

        loads.Should().Be(2);
    }

    [Fact]
    public async Task InvalidateDoctor_BumpsListGeneration_AndClearsById()
    {
        var dto = MapToDto(_testDoctors[0]);
        await _doctorCache.GetDoctorByIdAsync(1, _ => Task.FromResult<DoctorDto?>(dto));

        var pageLoads = 0;
        await _doctorCache.GetDoctorPageAsync(
            CacheKeys.DoctorListFilterAll, 1, 20,
            _ =>
            {
                pageLoads++;
                return Task.FromResult(new PagedResult<DoctorDto>(new[] { dto }, 1, 20, 1));
            });

        await _doctorCache.InvalidateDoctorAsync(1);

        var byIdLoads = 0;
        await _doctorCache.GetDoctorByIdAsync(1, _ =>
        {
            byIdLoads++;
            return Task.FromResult<DoctorDto?>(dto);
        });
        byIdLoads.Should().Be(1);

        await _doctorCache.GetDoctorPageAsync(
            CacheKeys.DoctorListFilterAll, 1, 20,
            _ =>
            {
                pageLoads++;
                return Task.FromResult(new PagedResult<DoctorDto>(new[] { dto }, 1, 20, 1));
            });
        pageLoads.Should().Be(2, "generation bump should force a new page load");
    }

    [Fact]
    public async Task Availability_InvalidateDay_ForcesReload()
    {
        var day = new DateOnly(2026, 7, 15);
        var loads = 0;

        DoctorDayAvailabilityDto Factory(CancellationToken _)
        {
            loads++;
            return new DoctorDayAvailabilityDto
            {
                DoctorId = 1,
                Date = day,
                CachedAtUtc = DateTime.UtcNow,
                BookedSlots = new List<BookedSlotDto>()
            };
        }

        await _availabilityCache.GetDayAsync(1, day, ct => Task.FromResult<DoctorDayAvailabilityDto?>(Factory(ct)));
        await _availabilityCache.GetDayAsync(1, day, ct => Task.FromResult<DoctorDayAvailabilityDto?>(Factory(ct)));
        loads.Should().Be(1);

        await _availabilityCache.InvalidateDayAsync(1, day);

        await _availabilityCache.GetDayAsync(1, day, ct => Task.FromResult<DoctorDayAvailabilityDto?>(Factory(ct)));
        loads.Should().Be(2);
    }

    [Fact]
    public async Task Stampede_GetOrCreate_OnlyOneFactoryInvocation()
    {
        var loads = 0;
        var dto = MapToDto(_testDoctors[0]);

        var tasks = Enumerable.Range(0, 20).Select(_ =>
            _doctorCache.GetDoctorByIdAsync(42, async ct =>
            {
                Interlocked.Increment(ref loads);
                await Task.Delay(30, ct);
                return dto;
            }));

        var results = await Task.WhenAll(tasks);
        results.Should().OnlyContain(r => r != null && r.Id == 1);
        loads.Should().Be(1, "stampede single-flight should collapse concurrent misses");
    }

    [Fact]
    public async Task InvalidateDoctorCacheHandler_ClearsCache()
    {
        var dto = MapToDto(_testDoctors[0]);
        await _doctorCache.GetDoctorByIdAsync(1, _ => Task.FromResult<DoctorDto?>(dto));

        var handler = new InvalidateDoctorCacheHandler(
            _doctorCache,
            _availabilityCache,
            new Mock<ILogger<InvalidateDoctorCacheHandler>>().Object);

        await handler.HandleAsync(new DoctorCacheInvalidationNeededEvent(1));

        var loads = 0;
        await _doctorCache.GetDoctorByIdAsync(1, _ =>
        {
            loads++;
            return Task.FromResult<DoctorDto?>(dto);
        });
        loads.Should().Be(1);
    }

    [Fact]
    public async Task GetAllDoctors_UsesCacheOnSecondCall()
    {
        var repoMock = new Mock<IDoctorRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.Doctors).Returns(repoMock.Object);
        unitOfWorkMock.Setup(u => u.Appointments).Returns(new Mock<IAppointmentRepository>().Object);

        repoMock.Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int pn, int ps, CancellationToken _) =>
            {
                var list = _testDoctors.OrderBy(d => d.LastName).ThenBy(d => d.FirstName).ToList();
                return new PagedResult<Doctor>(list.Skip((pn - 1) * ps).Take(ps).ToList(), pn, ps, list.Count);
            });

        var controller = CreateController(unitOfWorkMock.Object);

        await controller.GetAllDoctors(1, 20);
        await controller.GetAllDoctors(1, 20);

        repoMock.Verify(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDoctor_CallsHandlerAndReturns201()
    {
        var createHandlerMock = new Mock<ICommandHandler<CreateDoctorCommand, Result<int>>>();
        createHandlerMock.Setup(h => h.HandleAsync(It.IsAny<CreateDoctorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(42));

        var controller = CreateController(
            new Mock<IUnitOfWork>().Object,
            createHandlerMock.Object);

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
    }

    [Fact]
    public async Task DeleteDoctor_CallsHandlerAndReturns204()
    {
        var deactivateHandlerMock = new Mock<ICommandHandler<DeactivateDoctorCommand, Result>>();
        deactivateHandlerMock.Setup(h => h.HandleAsync(It.IsAny<DeactivateDoctorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var controller = new DoctorsController(
            new Mock<ICommandHandler<CreateDoctorCommand, Result<int>>>().Object,
            deactivateHandlerMock.Object,
            new Mock<IUnitOfWork>().Object,
            _doctorCache,
            _availabilityCache,
            new Mock<IStringLocalizer<Messages>>().Object,
            new Mock<ILogger<DoctorsController>>().Object);

        var response = await controller.DeleteDoctor(1, CancellationToken.None);
        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void CacheKeys_AreStableAndVersioned()
    {
        CacheKeys.DoctorById(5).Should().Contain("cache:v1:doctor:by-id:5");
        CacheKeys.DoctorDayAvailability(3, new DateOnly(2026, 1, 2))
            .Should().Be("cache:v1:availability:doctor:3:date:20260102");
        CacheKeys.AppointmentBookingLock(9, new DateTime(2026, 7, 1, 10, 30, 0, DateTimeKind.Utc))
            .Should().Contain("lock:appointment:doctor:9:time:202607011030");
    }

    private DoctorsController CreateController(
        IUnitOfWork uow,
        ICommandHandler<CreateDoctorCommand, Result<int>>? create = null) =>
        new(
            create ?? new Mock<ICommandHandler<CreateDoctorCommand, Result<int>>>().Object,
            new Mock<ICommandHandler<DeactivateDoctorCommand, Result>>().Object,
            uow,
            _doctorCache,
            _availabilityCache,
            new Mock<IStringLocalizer<Messages>>().Object,
            new Mock<ILogger<DoctorsController>>().Object);

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
            doctor.Deactivate();
        if (!acceptingPatients)
            doctor.StopAcceptingPatients();

        return doctor;
    }

    private static DoctorDto MapToDto(Doctor doctor) => new()
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
