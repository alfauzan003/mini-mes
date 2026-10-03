namespace MiniMes.Api.Modules.Equipment.Domain;

public readonly record struct EquipmentStatusChange(EquipmentStatus From, EquipmentStatus To, string Reason);
