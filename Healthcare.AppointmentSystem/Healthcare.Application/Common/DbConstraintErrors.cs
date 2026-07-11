namespace Healthcare.Application.Common;

/// <summary>
/// Detects common database constraint violations from provider exception messages
/// without coupling callers to a specific SQL client type.
/// </summary>
public static class DbConstraintErrors
{
    public static bool IsUniqueViolation(Exception ex, params string[] constraintOrColumnHints)
    {
        var message = FlattenMessage(ex);
        if (string.IsNullOrEmpty(message))
            return false;

        var looksUnique =
            message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("unique index", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("IX_", StringComparison.OrdinalIgnoreCase) &&
                (message.Contains("2601") || message.Contains("2627") ||
                 message.Contains("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase));

        // SQL Server numbers often appear in nested messages
        if (message.Contains("2627") || message.Contains("2601"))
            looksUnique = true;

        if (!looksUnique)
            return false;

        if (constraintOrColumnHints.Length == 0)
            return true;

        return constraintOrColumnHints.Any(hint =>
            message.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsForeignKeyViolation(Exception ex)
    {
        var message = FlattenMessage(ex);
        return message.Contains("REFERENCE constraint", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("547") && message.Contains("constraint", StringComparison.OrdinalIgnoreCase);
    }

    private static string FlattenMessage(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current != null; current = current.InnerException!)
            parts.Add(current.Message);
        return string.Join(" | ", parts);
    }
}
