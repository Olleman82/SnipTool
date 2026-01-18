namespace SnipTool.Models;

public sealed record BurstStatus(bool IsActive, DateTime StartedAt, string? Folder, int Count);
