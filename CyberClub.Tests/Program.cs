using CyberClub.Core;

var tests = new List<(string Name, Action Test)>();
void Test(string name, Action test) => tests.Add((name, test));
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}; actual {actual}");
}

void Throws<T>(Action action)
    where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }

    throw new Exception($"Expected {typeof(T).Name}");
}

var now = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
Test("Billing zero", () => Equal(0m, Billing.Calculate(100, TimeSpan.Zero)));
Test("Billing hour", () => Equal(100m, Billing.Calculate(100, TimeSpan.FromHours(1))));
Test("Billing partial minute", () => Equal(1.67m, Billing.Calculate(100, TimeSpan.FromSeconds(1))));
Test("Billing exact minute", () => Equal(1.67m, Billing.Calculate(100, TimeSpan.FromMinutes(1))));
Test("Billing next minute", () => Equal(3.33m, Billing.Calculate(100, TimeSpan.FromSeconds(61))));
Test("Billing VIP", () => Equal(225m, Billing.Calculate(150, TimeSpan.FromMinutes(90))));
Test("Billing negative time", () => Throws<ArgumentOutOfRangeException>(() => Billing.Calculate(100, TimeSpan.FromSeconds(-1))));
Test("Billing zero rate", () => Throws<ArgumentOutOfRangeException>(() => Billing.Calculate(0, TimeSpan.Zero)));
Test("Billing negative rate", () => Throws<ArgumentOutOfRangeException>(() => Billing.Calculate(-1, TimeSpan.Zero)));
Test("Start valid", () =>
{
    var c = new ClubService(new());
    Equal(12, c.Start(12, "VIP", now).Station);
});
Test("Start duplicate", () =>
{
    var c = new ClubService(new());
    c.Start(1, "VIP", now);
    Throws<InvalidOperationException>(() => c.Start(1, "VIP", now));
    Equal(1, c.State.Sessions.Count);
});
Test("Station below limit", () => Throws<ArgumentOutOfRangeException>(() => new ClubService(new()).Start(0, "VIP", now)));
Test("Station above limit", () => Throws<ArgumentOutOfRangeException>(() => new ClubService(new()).Start(13, "VIP", now)));
Test("Unknown tariff", () => Throws<ArgumentException>(() => new ClubService(new()).Start(1, "missing", now)));
Test("Finish cost", () =>
{
    var c = new ClubService(new());
    var s = c.Start(1, "VIP", now);
    Equal(150m, c.Finish(s.Id, now.AddHours(1)).Cost);
});
Test("Finish duplicate", () =>
{
    var c = new ClubService(new());
    var s = c.Start(1, "VIP", now);
    c.Finish(s.Id, now);
    Throws<InvalidOperationException>(() => c.Finish(s.Id, now));
});
Test("Finish unknown", () => Throws<ArgumentException>(() => new ClubService(new()).Finish(Guid.NewGuid(), now)));
Test("Clock rollback", () =>
{
    var c = new ClubService(new());
    var s = c.Start(1, "VIP", now);
    Throws<ArgumentOutOfRangeException>(() => c.Finish(s.Id, now.AddMinutes(-1)));
    Equal<DateTimeOffset?>(null, c.State.Sessions[0].Finished);
});
Test("Reuse station", () =>
{
    var c = new ClubService(new());
    var s = c.Start(1, "VIP", now);
    c.Finish(s.Id, now);
    c.Start(1, "VIP", now);
    Equal(2, c.State.Sessions.Count);
});
Test("Rate snapshot", () =>
{
    var c = new ClubService(new());
    var s = c.Start(1, "VIP", now);
    c.ChangeRate("VIP", 200);
    Equal(150m, c.Finish(s.Id, now.AddHours(1)).Cost);
    Equal(200m, c.Start(1, "VIP", now).Rate);
});
foreach (var value in new[]
{
    0m,
    -1m,
    10001m
}

)
{
    var rate = value;
    Test($"Invalid rate {rate}", () => Throws<ArgumentOutOfRangeException>(() => new ClubService(new()).ChangeRate("VIP", rate)));
}

Test("Rate upper boundary", () =>
{
    var c = new ClubService(new());
    c.ChangeRate("VIP", 10000);
    Equal(10000m, c.State.Tariffs[1].HourlyRate);
});
Test("Unknown rate name", () => Throws<ArgumentException>(() => new ClubService(new()).ChangeRate("missing", 100)));
var temp = Path.Combine(Path.GetTempPath(), "CyberClubTests-" + Guid.NewGuid());
Directory.CreateDirectory(temp);
Test("Missing file defaults", () => Equal(2, new JsonStore(Path.Combine(temp, "missing.json")).Load().Tariffs.Count));
Test("JSON roundtrip and backup", () =>
{
    var store = new JsonStore(Path.Combine(temp, "state.json"));
    var c = new ClubService(new());
    c.Start(1, "VIP", now);
    store.Save(c.State);
    c.ChangeRate("VIP", 300);
    store.Save(c.State);
    Equal(300m, store.Load().Tariffs[1].HourlyRate);
    Equal(1, store.Load().Sessions.Count);
    Equal(true, File.Exists(Path.Combine(temp, "state.json.bak")));
});
Test("Corrupt JSON rejected", () =>
{
    var p = Path.Combine(temp, "bad.json");
    File.WriteAllText(p, "broken");
    Throws<System.Text.Json.JsonException>(() => new JsonStore(p).Load());
});
Test("Invalid state rejected", () =>
{
    var p = Path.Combine(temp, "invalid.json");
    File.WriteAllText(p, "{\"Tariffs\":[],\"Sessions\":[]}");
    Throws<InvalidDataException>(() => new JsonStore(p).Load());
});
int failed = 0;
var results = new List<object>();
foreach (var(name, test)in tests)
{
    try
    {
        test();
        Console.WriteLine("PASS " + name);
        results.Add(new { name, passed = true });
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine("FAIL " + name + ": " + ex.Message);
        results.Add(new { name, passed = false, error = ex.Message });
    }
}

Directory.Delete(temp, true);
Console.WriteLine($"TOTAL {tests.Count}; PASSED {tests.Count - failed}; FAILED {failed}");
if (args.Length > 0)
    File.WriteAllText(args[0], System.Text.Json.JsonSerializer.Serialize(new { total = tests.Count, passed = tests.Count - failed, failed, results }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
return failed == 0 ? 0 : 1;
