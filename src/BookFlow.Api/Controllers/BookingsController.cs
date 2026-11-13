using BookFlow.Api.Contracts;
using BookFlow.Api.Domain;
using BookFlow.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BookingsController(IBookingService bookingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var bookings = await bookingService.GetAllAsync(cancellationToken);
        return Ok(bookings.Select(ToDto).ToArray());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingDto>> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var booking = await bookingService.GetByIdAsync(id, cancellationToken);
        return booking is null ? NotFound() : Ok(ToDto(booking));
    }

    [HttpPost]
    public async Task<ActionResult<BookingDto>> CreateAsync(
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await bookingService.CreateAsync(
            request.ResourceId,
            request.CustomerName,
            request.Start!.Value,
            request.End!.Value,
            cancellationToken);

        if (result.Booking is null)
        {
            return ToProblem(result.Error);
        }

        return CreatedAtAction(nameof(GetByIdAsync), new { id = result.Booking.Id }, ToDto(result.Booking));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BookingDto>> UpdateAsync(
        int id,
        UpdateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await bookingService.UpdateAsync(
            id,
            request.ResourceId,
            request.CustomerName,
            request.Start!.Value,
            request.End!.Value,
            cancellationToken);

        return result.Booking is null ? ToProblem(result.Error) : Ok(ToDto(result.Booking));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var deleted = await bookingService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private ObjectResult ToProblem(BookingError? error)
    {
        return error switch
        {
            BookingError.BookingNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Booking not found."),
            BookingError.ResourceNotFound => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Resource not found.",
                detail: "The specified resource does not exist."),
            BookingError.EndNotAfterStart => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid booking period.",
                detail: "End must be after Start."),
            BookingError.StartInPast => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid booking period.",
                detail: "Bookings cannot start in the past."),
            BookingError.Overlap => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Booking conflict.",
                detail: "The resource already has a confirmed booking that overlaps this period."),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static BookingDto ToDto(Booking booking)
    {
        return new BookingDto(
            booking.Id,
            booking.ResourceId,
            booking.CustomerName,
            booking.Start,
            booking.End,
            booking.Status.ToString());
    }
}
