using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

/// <summary>
/// Seeds a fresh Inspection with a sensible default list of rooms and
/// items so an admin doesn't have to type every "Kitchen / Refrigerator"
/// line by hand. The catalog is intentionally in-memory + code — small
/// enough that DB-backed customization isn't worth the complexity in v1.
/// </summary>
public static class InspectionChecklistTemplate
{
    // (Room, Items[]) — order preserved for display.
    private static readonly (string Room, string[] Items)[] Rooms = new[]
    {
        ("Exterior / Entrance",
            new[] { "Front door", "Doorknob & lock", "Doorbell", "Porch / stoop", "Exterior lights", "Mailbox" }),
        ("Living Room",
            new[] { "Walls", "Ceiling", "Flooring", "Windows", "Window coverings", "Doors", "Light fixtures", "Electrical outlets", "HVAC vents" }),
        ("Dining Room",
            new[] { "Walls", "Ceiling", "Flooring", "Windows", "Window coverings", "Light fixtures" }),
        ("Kitchen",
            new[] { "Walls", "Ceiling", "Flooring", "Cabinets", "Countertops", "Sink & faucet", "Refrigerator", "Stove / Oven", "Microwave", "Dishwasher", "Garbage disposal", "Windows" }),
        ("Master Bedroom",
            new[] { "Walls", "Ceiling", "Flooring", "Closet", "Windows", "Window coverings", "Doors", "Light fixtures", "Electrical outlets" }),
        ("Master Bathroom",
            new[] { "Walls", "Ceiling", "Flooring", "Sink & faucet", "Toilet", "Tub / Shower", "Mirror", "Cabinets", "Exhaust fan" }),
        ("Bathroom",
            new[] { "Walls", "Ceiling", "Flooring", "Sink & faucet", "Toilet", "Tub / Shower", "Mirror", "Cabinets", "Exhaust fan" }),
        ("Laundry",
            new[] { "Washer connection", "Dryer connection", "Flooring", "Sink" }),
        ("Garage / Parking",
            new[] { "Garage door", "Opener", "Flooring", "Electrical outlets" }),
        ("Utility / Systems",
            new[] { "Water heater", "HVAC unit", "Smoke detectors", "Carbon monoxide detectors", "Circuit breaker panel" }),
    };

    /// <summary>
    /// Returns fresh InspectionItem objects (Id = 0, InspectionId = 0)
    /// that the caller assigns to a saved Inspection.
    /// </summary>
    public static IEnumerable<InspectionItem> Seed()
    {
        var order = 100;
        foreach (var (room, items) in Rooms)
        {
            foreach (var item in items)
            {
                yield return new InspectionItem
                {
                    Room = room,
                    Item = item,
                    Condition = InspectionCondition.NotAssessed,
                    Order = order
                };
                order += 10;
            }
        }
    }
}
