using System.Diagnostics;
using System.Text;

namespace Logistics.Contracts;

public sealed record LogisticsQuantity
{
    public LogisticsQuantity(decimal value, LogisticsUnit unit, string? customUnitName = null)
    {
        if (value <= 0m)
        {
            throw new LogisticsRuleException("invalid_quantity", "Die Menge muss größer als null sein.");
        }

        if (unit == LogisticsUnit.Custom)
        {
            if (string.IsNullOrWhiteSpace(customUnitName))
            {
                throw new LogisticsRuleException(
                    "custom_unit_required",
                    "Für diese Einheit ist eine Bezeichnung erforderlich.");
            }

            CustomUnitName = NormalizeCustomUnit(customUnitName);
        }
        else if (!string.IsNullOrWhiteSpace(customUnitName))
        {
            throw new LogisticsRuleException(
                "custom_unit_not_allowed",
                "Eine Bezeichnung ist nur für benutzerdefinierte Einheiten erlaubt.");
        }

        Value = value;
        Unit = unit;
    }

    public decimal Value { get; }

    public LogisticsUnit Unit { get; }

    public string? CustomUnitName { get; }

    public LogisticsQuantity ConvertTo(LogisticsUnit targetUnit, string? targetCustomUnitName = null)
    {
        var normalizedTargetName = targetUnit == LogisticsUnit.Custom
            ? NormalizeCustomUnit(targetCustomUnitName ?? CustomUnitName ?? string.Empty)
            : null;
        if (!IsCompatible(targetUnit, normalizedTargetName))
        {
            throw new LogisticsRuleException(
                "incompatible_unit",
                "Diese Einheiten können nicht ineinander umgerechnet werden.");
        }

        var baseValue = Value * Factor(Unit);
        return new LogisticsQuantity(baseValue / Factor(targetUnit), targetUnit, normalizedTargetName);
    }

    private bool IsCompatible(LogisticsUnit targetUnit, string? targetCustomUnitName) => Unit switch
    {
        LogisticsUnit.Gram or LogisticsUnit.Kilogram =>
            targetUnit is LogisticsUnit.Gram or LogisticsUnit.Kilogram,
        LogisticsUnit.Milliliter or LogisticsUnit.Liter =>
            targetUnit is LogisticsUnit.Milliliter or LogisticsUnit.Liter,
        LogisticsUnit.Piece => targetUnit == LogisticsUnit.Piece,
        LogisticsUnit.Custom => targetUnit == LogisticsUnit.Custom
            && string.Equals(CustomUnitName, targetCustomUnitName, StringComparison.OrdinalIgnoreCase),
        _ => throw new UnreachableException()
    };

    private static decimal Factor(LogisticsUnit unit) => unit switch
    {
        LogisticsUnit.Gram or LogisticsUnit.Milliliter or LogisticsUnit.Piece or LogisticsUnit.Custom => 1m,
        LogisticsUnit.Kilogram or LogisticsUnit.Liter => 1000m,
        _ => throw new UnreachableException()
    };

    private static string NormalizeCustomUnit(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC).Trim();
        var builder = new StringBuilder(normalized.Length);
        var previousWasWhitespace = false;
        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace) builder.Append(' ');
                previousWasWhitespace = true;
            }
            else
            {
                builder.Append(character);
                previousWasWhitespace = false;
            }
        }
        return builder.ToString();
    }
}

public enum LogisticsUnit
{
    Gram,
    Kilogram,
    Milliliter,
    Liter,
    Piece,
    Custom
}

public sealed class LogisticsRuleException(
    string errorCode,
    string message,
    Exception? innerException = null) : InvalidOperationException(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}
