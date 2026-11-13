using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CyberClub.Core;

namespace CyberClub.App;
public partial class MainWindow : Window
{
    private readonly JsonStore store;
    private ClubService club = new(new ClubState());
    private bool storageReady;
    public MainWindow()
    {
        InitializeComponent();
        var folder = Environment.GetEnvironmentVariable("CYBERCLUB_DATA_DIR") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CyberClub");
        store = new JsonStore(System.IO.Path.Combine(folder, "club.json"));
        try
        {
            club = new ClubService(store.Load());
            storageReady = true;
            Status.Text = "Данные загружены. Можно начинать работу.";
        }
        catch (Exception ex)
        {
            Status.Text = "Не удалось загрузить данные. Изменения заблокированы. " + ex.Message;
        }

        StationChoice.ItemsSource = Enumerable.Range(1, ClubService.StationCount);
        StationChoice.SelectedIndex = 0;
        Refresh();
        if (Environment.GetCommandLineArgs().Contains("--verify-ui"))
            Loaded += async (_, _) => await VerifyUi();
    }

    private void Refresh()
    {
        var active = club.State.Sessions.Where(s => s.Finished == null).ToList();
        FreeCount.Text = (ClubService.StationCount - active.Count).ToString();
        ActiveCount.Text = active.Count.ToString();
        Revenue.Text = club.State.Sessions.Where(s => s.Finished?.LocalDateTime.Date == DateTime.Today).Sum(s => s.Cost).ToString("N2") + " ₽";
        Stations.ItemsSource = Enumerable.Range(1, ClubService.StationCount).Select(n => new { Number = n, Name = $"ПК {n:00}", Status = active.Any(s => s.Station == n) ? "● Занят" : "● Свободен", Color = active.Any(s => s.Station == n) ? "#BF643E" : "#168261" });
        ActiveGrid.ItemsSource = active;
        HistoryGrid.ItemsSource = club.State.Sessions.Where(s => s.Finished != null).OrderByDescending(s => s.Finished).ToList();
        var choice = TariffChoice.SelectedItem as string;
        var rate = RateChoice.SelectedItem as string;
        TariffChoice.ItemsSource = club.State.Tariffs.Select(t => t.Name).ToList();
        TariffChoice.SelectedItem = choice ?? club.State.Tariffs[0].Name;
        RateChoice.ItemsSource = club.State.Tariffs.Select(t => t.Name).ToList();
        RateChoice.SelectedItem = rate ?? club.State.Tariffs[0].Name;
        TariffGrid.ItemsSource = club.State.Tariffs.ToList();
    }

    private void Show(string section)
    {
        Heading.Text = section;
        Home.Visibility = section == "Главная" ? Visibility.Visible : Visibility.Collapsed;
        SessionsPanel.Visibility = section == "Сеансы" ? Visibility.Visible : Visibility.Collapsed;
        TariffsPanel.Visibility = section == "Тарифы" ? Visibility.Visible : Visibility.Collapsed;
        HistoryPanel.Visibility = section == "История" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Navigate(object sender, RoutedEventArgs e) => Show((string)((Button)sender).Tag);
    private void SelectStation(object sender, RoutedEventArgs e)
    {
        StationChoice.SelectedItem = (int)((Button)sender).Tag;
        Show("Сеансы");
    }

    private void Apply(Action action, string success)
    {
        if (!storageReady)
        {
            Status.Text = "Хранилище недоступно. Исправьте файл и перезапустите приложение.";
            return;
        }

        // Копия состояния позволяет откатить изменение при ошибке записи.
        var before = System.Text.Json.JsonSerializer.Serialize(club.State);
        try
        {
            action();
            store.Save(club.State);
            Status.Text = success;
        }
        catch (Exception ex)
        {
            club = new ClubService(System.Text.Json.JsonSerializer.Deserialize<ClubState>(before)!);
            Status.Text = "Операция не выполнена: " + ex.Message;
        }

        Refresh();
    }

    private void StartSession(object sender, RoutedEventArgs e) => Apply(() => club.Start((int)StationChoice.SelectedItem, (string)TariffChoice.SelectedItem, DateTimeOffset.Now), "Сеанс начат и сохранён.");
    private void FinishSession(object sender, RoutedEventArgs e)
    {
        if (ActiveGrid.SelectedItem is not Session selected)
        {
            Status.Text = "Выберите активный сеанс в таблице.";
            return;
        }

        Apply(() => club.Finish(selected.Id, DateTimeOffset.Now), "Сеанс завершён. Стоимость записана в историю.");
    }

    private void SaveRate(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(RateInput.Text, NumberStyles.AllowDecimalPoint, CultureInfo.GetCultureInfo("ru-RU"), out var rate))
        {
            Status.Text = "Введите цену числом, например 120,50.";
            return;
        }

        Apply(() => club.ChangeRate((string)RateChoice.SelectedItem, rate), "Новая цена сохранена.");
    }
}
