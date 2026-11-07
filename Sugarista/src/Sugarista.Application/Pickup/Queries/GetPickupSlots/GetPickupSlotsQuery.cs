namespace Sugarista.Application.Pickup.Queries.GetPickupSlots;

using Sugarista.Application.Pickup;

/// <summary>
/// Public pickup availability for the digital menu. Every value comes from
/// live café settings; the result already excludes past slots and slots inside
/// the minimum lead time, and marks full slots as disabled instead of hiding
/// them.
/// </summary>
public record GetPickupSlotsQuery(int? DaysAhead = null) : IRequest<PickupScheduleDto>;

public class GetPickupSlotsQueryHandler : IRequestHandler<GetPickupSlotsQuery, PickupScheduleDto>
{
    private readonly PickupSlotService _pickupSlotService;

    public GetPickupSlotsQueryHandler(PickupSlotService pickupSlotService)
    {
        _pickupSlotService = pickupSlotService;
    }

    public Task<PickupScheduleDto> Handle(GetPickupSlotsQuery request, CancellationToken cancellationToken) =>
        _pickupSlotService.GetScheduleAsync(request.DaysAhead, cancellationToken);
}
