using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EHub.Contracts.Classes;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Classes.Common;

public static class ClassScheduleRules
{
    public const int MaximumScheduleSlots = 12;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyCollection<ClassScheduleSlotDto> Deserialize(string? scheduleJson)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson))
        {
            return Array.Empty<ClassScheduleSlotDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ClassScheduleSlotDto>>(scheduleJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return Array.Empty<ClassScheduleSlotDto>();
        }
    }

    public static string Serialize(IEnumerable<ClassScheduleSlotDto> schedules) =>
        JsonSerializer.Serialize(schedules, JsonOptions);

    public static bool HasSchedule(string? scheduleJson) => Deserialize(scheduleJson).Count > 0;

    public static ClassStatus DetermineOperationalStatus(Guid? primaryLecturerId, string? scheduleJson) =>
        primaryLecturerId.HasValue && HasSchedule(scheduleJson)
            ? ClassStatus.Active
            : ClassStatus.Draft;

    public static string? Validate(IReadOnlyCollection<ClassScheduleSlotDto>? schedules)
    {
        if (schedules == null || schedules.Count > MaximumScheduleSlots)
        {
            return $"Schedules must contain between 0 and {MaximumScheduleSlots} slots.";
        }

        foreach (var slot in schedules)
        {
            if (!Enum.IsDefined(slot.DayOfWeek) || slot.DayOfWeek is DayOfWeek.Sunday)
            {
                return "Day of week must be between Monday and Saturday.";
            }

            if (slot.SlotNumber is < 1 or > 4)
            {
                return "Slot number must be between 1 and 4.";
            }

            if (slot.Room?.Trim().Length > 50)
            {
                return "Room must not exceed 50 characters.";
            }
        }

        var duplicate = schedules
            .GroupBy(slot => new { slot.DayOfWeek, slot.SlotNumber })
            .FirstOrDefault(group => group.Count() > 1);

        return duplicate == null
            ? null
            : $"Duplicate schedule slot on {duplicate.Key.DayOfWeek} (Slot {duplicate.Key.SlotNumber}).";
    }

    public static ClassScheduleSlotDto[] Normalize(IEnumerable<ClassScheduleSlotDto> schedules) =>
        schedules
            .OrderBy(slot => slot.DayOfWeek)
            .ThenBy(slot => slot.SlotNumber)
            .Select(slot => new ClassScheduleSlotDto
            {
                DayOfWeek = slot.DayOfWeek,
                SlotNumber = slot.SlotNumber,
                Room = string.IsNullOrWhiteSpace(slot.Room) ? null : slot.Room.Trim()
            })
            .ToArray();
}
