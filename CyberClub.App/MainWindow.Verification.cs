using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CyberClub.App;
public partial class MainWindow
{
    // Воспроизводимая проверка настоящего WPF окна в отдельном каталоге данных.
    private async Task VerifyUi()
    {
        var folder = Environment.GetEnvironmentVariable("CYBERCLUB_EVIDENCE_DIR") ?? throw new InvalidOperationException("Не задан каталог результатов проверки.");
        Directory.CreateDirectory(folder);
        var protocol = new List<string>();
        void Check(bool result, string name)
        {
            protocol.Add((result ? "PASS " : "FAIL ") + name);
            if (!result)
                throw new InvalidOperationException(name);
        }

        async Task Capture(string name)
        {
            await Dispatcher.InvokeAsync(() =>
            {
            }, DispatcherPriority.ApplicationIdle);
            UpdateLayout();
            var visual = (FrameworkElement)Content;
            var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(folder, name + ".png"));
            encoder.Save(file);
        }

        try
        {
            Check(FreeCount.Text == "12", "Initial free stations = 12");
            await Capture("01-home");
            Show("Сеансы");
            StartSession(this, new RoutedEventArgs());
            Check(ActiveCount.Text == "1", "Start via window handler creates session");
            await Capture("02-session");
            StartSession(this, new RoutedEventArgs());
            Check(ActiveCount.Text == "1" && Status.Text.Contains("уже занято"), "Duplicate start rejected in UI");
            await Capture("03-duplicate-rejected");
            Show("Тарифы");
            RateInput.Text = "-1";
            SaveRate(this, new RoutedEventArgs());
            Check(club.State.Tariffs[0].HourlyRate == 100m && Status.Text.Contains("Введите цену"), "Negative tariff rejected in UI");
            RateInput.Text = "120,50";
            SaveRate(this, new RoutedEventArgs());
            Check(club.State.Tariffs[0].HourlyRate == 120.50m, "Decimal comma accepted");
            await Capture("04-tariffs");
            Show("Сеансы");
            ActiveGrid.SelectedIndex = 0;
            FinishSession(this, new RoutedEventArgs());
            Check(ActiveCount.Text == "0" && club.State.Sessions[0].Finished != null, "Finish moves session to history");
            Check(club.State.Sessions[0].Rate == 100m, "Active session keeps original rate");
            Show("История");
            await Capture("05-history");
            var restored = store.Load();
            Check(restored.Sessions.Count == 1 && restored.Sessions[0].Finished != null, "Reload saved history");
            Check(restored.Tariffs[0].HourlyRate == 120.50m, "Reload saved tariff");
        }
        catch (Exception ex)
        {
            protocol.Add("ERROR " + ex);
            Environment.ExitCode = 1;
        }
        finally
        {
            File.WriteAllLines(Path.Combine(folder, "ui-checks.log"), protocol);
            Close();
        }
    }
}
