namespace CyberClub.Core;

public sealed record Tariff(string Name, decimal HourlyRate);
public sealed record Session(Guid Id, int Station, string Tariff, decimal Rate, DateTimeOffset Started, DateTimeOffset? Finished = null, decimal Cost = 0);
public sealed class ClubState
{
    public List<Tariff> Tariffs { get; set; } = [new("Стандарт", 100), new("VIP", 150)];
    public List<Session> Sessions { get; set; } = [];
}
public static class Billing
{
    // Оплачивается каждая начатая минута, итог округляется до копеек.
    public static decimal Calculate(decimal rate, TimeSpan duration)
    {
        if (rate <= 0) throw new ArgumentOutOfRangeException(nameof(rate));
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        return decimal.Round(rate * (decimal)Math.Ceiling(duration.TotalMinutes) / 60m, 2, MidpointRounding.AwayFromZero);
    }
}
