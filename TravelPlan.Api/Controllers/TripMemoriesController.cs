using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Api.Services.Reports;
using TravelPlan.Shared.DTOs.TripMemories;
using TravelPlan.Shared.DTOs.TripMemories.Reports;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/trip-memories")]
public class TripMemoriesController : ControllerBase
{
    private readonly ITripMemoryRepository _tripMemories;
    private readonly ITripAccessService _tripAccess;
    private readonly ITripReportService _reportService;
    private readonly IReportHtmlRenderer _htmlRenderer;
    private readonly IValidator<CreateTripMemoryDto> _createValidator;
    private readonly IValidator<UpdateTripMemoryReflectionDto> _reflectionValidator;

    public TripMemoriesController(
        ITripMemoryRepository tripMemories,
        ITripAccessService tripAccess,
        ITripReportService reportService,
        IReportHtmlRenderer htmlRenderer,
        IValidator<CreateTripMemoryDto> createValidator,
        IValidator<UpdateTripMemoryReflectionDto> reflectionValidator)
    {
        _tripMemories = tripMemories;
        _tripAccess = tripAccess;
        _reportService = reportService;
        _htmlRenderer = htmlRenderer;
        _createValidator = createValidator;
        _reflectionValidator = reflectionValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TripMemoryDto>>> List([FromQuery] int tripId, CancellationToken cancellationToken)
    {
        if (!await _tripAccess.HasAccessAsync(tripId, TripRole.Viewer, cancellationToken))
        {
            return NotFound();
        }

        var memories = await _tripMemories.ListByTripIdAsync(tripId, cancellationToken);
        return Ok(memories.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TripMemoryDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var memory = await _tripMemories.GetByIdAsync(id, cancellationToken);
        if (memory is null || !await _tripAccess.HasAccessAsync(memory.TripId, TripRole.Viewer, cancellationToken))
        {
            return NotFound();
        }

        return Ok(ToDto(memory));
    }

    [HttpPost]
    public async Task<ActionResult<TripMemoryDto>> Create(CreateTripMemoryDto dto, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        TripMemory? memory;
        try
        {
            memory = await _reportService.GenerateReportAsync(
                dto.TripId, dto.ReportType, dto.Title, dto.Description, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(title: "Trip not completed", detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }

        if (memory is null)
        {
            ModelState.AddModelError(nameof(dto.TripId), "TripId does not refer to a trip you have edit access to.");
            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(GetById), new { id = memory.Id }, ToDto(memory));
    }

    [HttpPut("{id:int}/reflection")]
    public async Task<ActionResult<TripMemoryDto>> UpdateReflection(int id, UpdateTripMemoryReflectionDto dto, CancellationToken cancellationToken)
    {
        var validation = await _reflectionValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
            return ValidationProblem(ModelState);
        }

        var memory = await _tripMemories.GetByIdAsync(id, cancellationToken);
        if (memory is null || !await _tripAccess.HasAccessAsync(memory.TripId, TripRole.Editor, cancellationToken))
        {
            return NotFound();
        }

        if (memory.ReportType != TripMemoryReportType.MemorySummary)
        {
            ModelState.AddModelError(nameof(dto.Reflection), "Only MemorySummary reports have a Reflection field.");
            return ValidationProblem(ModelState);
        }

        var report = JsonSerializer.Deserialize<TripReportDto>(memory.ReportData, ReportJson.Options)!;
        memory.ReportData = JsonSerializer.Serialize(report with { Reflection = dto.Reflection }, ReportJson.Options);

        await _tripMemories.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(memory));
    }

    [HttpGet("{id:int}/png")]
    public async Task<IActionResult> GetPng(int id, CancellationToken cancellationToken)
    {
        var lookup = await LoadReportAsync(id, cancellationToken);
        if (lookup.NotFound)
        {
            return NotFound();
        }

        var png = TripReportSkiaRenderer.RenderPng(lookup.Report!);
        return File(png, "image/png", $"{Slugify(lookup.Memory!.Title)}.png");
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GetPdf(int id, CancellationToken cancellationToken)
    {
        var lookup = await LoadReportAsync(id, cancellationToken);
        if (lookup.NotFound)
        {
            return NotFound();
        }

        var pdf = TripReportSkiaRenderer.RenderPdf(lookup.Report!);
        return File(pdf, "application/pdf", $"{Slugify(lookup.Memory!.Title)}.pdf");
    }

    [HttpGet("{id:int}/html")]
    public async Task<IActionResult> GetHtml(int id, CancellationToken cancellationToken)
    {
        var lookup = await LoadReportAsync(id, cancellationToken);
        if (lookup.NotFound)
        {
            return NotFound();
        }

        var html = await _htmlRenderer.RenderAsync(lookup.Report!);
        return Content(html, "text/html");
    }

    private readonly record struct ReportLookupResult(TripMemory? Memory, TripReportDto? Report, bool NotFound);

    private async Task<ReportLookupResult> LoadReportAsync(int id, CancellationToken cancellationToken)
    {
        var memory = await _tripMemories.GetByIdAsync(id, cancellationToken);
        if (memory is null || !await _tripAccess.HasAccessAsync(memory.TripId, TripRole.Viewer, cancellationToken))
        {
            return new ReportLookupResult(null, null, true);
        }

        var report = JsonSerializer.Deserialize<TripReportDto>(memory.ReportData, ReportJson.Options);
        return new ReportLookupResult(memory, report, false);
    }

    private static TripMemoryDto ToDto(TripMemory memory)
    {
        var reportData = JsonSerializer.Deserialize<TripReportDto>(memory.ReportData, ReportJson.Options)!;
        return new TripMemoryDto(
            memory.Id,
            memory.TripId,
            memory.UserId,
            memory.ReportType,
            memory.Title,
            memory.Description,
            reportData,
            memory.CreatedAt);
    }

    private static string Slugify(string title)
    {
        var chars = title.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }
        slug = slug.Trim('-');
        return string.IsNullOrEmpty(slug) ? "report" : slug;
    }
}
