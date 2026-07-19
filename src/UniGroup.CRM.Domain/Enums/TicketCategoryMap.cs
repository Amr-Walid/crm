using System.Collections.Generic;
using System.Linq;

namespace UniGroup.CRM.Domain.Enums;

/// <summary>
/// Static mapping between a high-level <see cref="MainCategory"/> and the
/// set of <see cref="TicketCategory"/> sub-categories that belong to it.
/// Used to build cascading category dropdowns and to validate that a chosen
/// sub-category is consistent with its selected main category.
/// </summary>
public static class TicketCategoryMap
{
    /// <summary>
    /// The canonical main-category → sub-categories mapping.
    /// </summary>
    public static readonly IReadOnlyDictionary<MainCategory, IReadOnlyList<TicketCategory>> Map =
        new Dictionary<MainCategory, IReadOnlyList<TicketCategory>>
        {
            [MainCategory.Maintenance] = new[]
            {
                TicketCategory.ScreenDamage,
                TicketCategory.BatteryIssue,
                TicketCategory.ChargingPort,
                TicketCategory.CameraIssue,
                TicketCategory.SpeakerMicrophone,
                TicketCategory.PhysicalDamage,
                TicketCategory.NetworkConnectivity
            },
            [MainCategory.GeneralSupport] = new[]
            {
                TicketCategory.WarrantyInquiry,
                TicketCategory.GeneralInquiry
            },
            [MainCategory.Complaint] = new[]
            {
                TicketCategory.SoftwareIssue,
                TicketCategory.Other
            }
        };

    /// <summary>
    /// Returns the sub-categories that belong to the specified main category.
    /// </summary>
    /// <param name="main">The main category.</param>
    /// <returns>The list of sub-categories, or an empty list if unknown.</returns>
    public static IReadOnlyList<TicketCategory> GetSubCategories(MainCategory main) =>
        Map.TryGetValue(main, out var subs) ? subs : new List<TicketCategory>();

    /// <summary>
    /// Determines whether the given sub-category belongs to the given main category.
    /// </summary>
    /// <param name="main">The main category.</param>
    /// <param name="sub">The sub-category to validate.</param>
    /// <returns><c>true</c> when the pairing is valid; otherwise <c>false</c>.</returns>
    public static bool IsValidPair(MainCategory main, TicketCategory sub) =>
        Map.TryGetValue(main, out var subs) && subs.Contains(sub);

    /// <summary>
    /// Resolves the main category that owns a given sub-category. Falls back to
    /// <see cref="MainCategory.GeneralSupport"/> if the sub-category is unmapped.
    /// </summary>
    /// <param name="sub">The sub-category.</param>
    /// <returns>The owning main category.</returns>
    public static MainCategory ResolveMain(TicketCategory sub)
    {
        foreach (var kvp in Map)
        {
            if (kvp.Value.Contains(sub))
            {
                return kvp.Key;
            }
        }

        return MainCategory.GeneralSupport;
    }
}
