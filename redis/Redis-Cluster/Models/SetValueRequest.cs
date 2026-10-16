namespace Models;

public sealed record SetValueRequest(string Value, int? TtlSeconds, bool OnlyIfNotExists = false);