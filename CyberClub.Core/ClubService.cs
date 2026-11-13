namespace CyberClub.Core;

public sealed class ClubService
{
    public const int StationCount = 12;
    public ClubState State { get; }
    public ClubService(ClubState state) => State = state;
    public Session Start(int station, string tariff, DateTimeOffset now)
    {
        if (station < 1 || station > StationCount) throw new ArgumentOutOfRangeException(nameof(station), "Выберите место от 1 до 12.");
        if (State.Sessions.Any(s => s.Station == station && s.Finished == null)) throw new InvalidOperationException("Это место уже занято.");
        var selected = State.Tariffs.SingleOrDefault(t => t.Name == tariff) ?? throw new ArgumentException("Тариф не найден.");
        var session = new Session(Guid.NewGuid(), station, selected.Name, selected.HourlyRate, now);
        State.Sessions.Add(session);
        return session;
    }
    public Session Finish(Guid id, DateTimeOffset now)
    {
        var index = State.Sessions.FindIndex(s => s.Id == id);
        if (index < 0) throw new ArgumentException("Сеанс не найден.");
        var session = State.Sessions[index];
        if (session.Finished != null) throw new InvalidOperationException("Сеанс уже завершён.");
        var result = session with { Finished = now, Cost = Billing.Calculate(session.Rate, now - session.Started) };
        State.Sessions[index] = result;
        return result;
    }
    public void ChangeRate(string name, decimal rate)
    {
        if (rate <= 0 || rate > 10000) throw new ArgumentOutOfRangeException(nameof(rate), "Цена должна быть больше 0 и не больше 10 000.");
        var index = State.Tariffs.FindIndex(t => t.Name == name);
        if (index < 0) throw new ArgumentException("Тариф не найден.");
        State.Tariffs[index] = new Tariff(name, rate);
    }
}
