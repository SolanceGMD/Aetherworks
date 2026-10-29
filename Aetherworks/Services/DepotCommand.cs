using System.Text.RegularExpressions;

namespace Aetherworks.Services;

public enum DepotAction { Install, Delete }
public sealed record ParsedDepotCommand(int Depot, DepotAction Action);

public static class DepotCommand
{
    private static readonly Regex Pattern = new(@"^sudo\s+fetch\s+depot\s+(?<depot>[1-9][0-9]*)\s*=\s*\[(?<action>install|delete)\]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public static ParsedDepotCommand Parse(string command)
    {
        var match = Pattern.Match(command.Trim());
        if (!match.Success) throw new FormatException("Invalid command. Use: sudo fetch depot 1 = [install] or sudo fetch depot 1 = [delete].");
        if (!int.TryParse(match.Groups["depot"].Value, out var depot)) throw new FormatException("Depot number is invalid.");
        var action = match.Groups["action"].Value.Equals("install", StringComparison.OrdinalIgnoreCase) ? DepotAction.Install : DepotAction.Delete;
        return new ParsedDepotCommand(depot, action);
    }
}