using System;
using System.IO;
using System.Diagnostics;
using System.ServiceProcess;
using System.Diagnostics.Eventing.Reader;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace PrintLogService
{
    public class PrintEvent
    {
        public DateTime EventTime { get; set; }
        public string UserName { get; set; }
        public string PrinterName { get; set; }
        public int PageCount { get; set; }

        public PrintEvent(DateTime eventTime, string userName, string printerName, int pageCount)
        {
            EventTime = eventTime;
            UserName = userName;
            PrinterName = printerName;
            PageCount = pageCount;
        }

        public override string ToString()
        {
            return $"{EventTime}, {UserName}, {PrinterName}, {PageCount}";
        }
    }

    public partial class Service1 : ServiceBase
    {
        private EventLogWatcher eventLogWatcher;
        private static readonly HttpClient httpClient = new HttpClient();
        private const string LogFilePath = @"C:\\Users\\shein\\Desktop\\Project\\Logs.txt";

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Настраиваем подписку на события
            EventLogQuery eventLogQuery = new EventLogQuery("Microsoft-Windows-PrintService/Operational", PathType.LogName, "*[System/EventID=307]");
            eventLogWatcher = new EventLogWatcher(eventLogQuery);

            eventLogWatcher.EventRecordWritten += new EventHandler<EventRecordWrittenEventArgs>(OnEventRecordWritten);
            eventLogWatcher.Enabled = true;
        }

        private async void OnEventRecordWritten(object sender, EventRecordWrittenEventArgs e)
        {
            try
            {
                if (e.EventRecord != null)
                {
                    // Извлекаем данные из события
                    DateTime eventTime = e.EventRecord.TimeCreated ?? DateTime.MinValue;
                    string userName = e.EventRecord.Properties[2]?.Value?.ToString() ?? "Unknown";
                    string printerName = e.EventRecord.Properties[4]?.Value?.ToString() ?? "Unknown";

                    int pageCount;
                    bool success = int.TryParse(e.EventRecord.Properties[7]?.Value?.ToString(), out pageCount);
                    if (!success)
                    {
                        pageCount = 0; // Устанавливаем значение по умолчанию при ошибке преобразования
                    }

                    // Создаем экземпляр PrintEvent
                    PrintEvent printEvent = new PrintEvent(eventTime, userName, printerName, pageCount);

                    // Записываем экземпляр в текстовый файл
                    LogPrintEventToFile(printEvent);

                    // Отправляем экземпляр в веб-приложение
                    await SendPrintEventAsync(printEvent);
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибок
                File.AppendAllText(@"C:\\Users\\shein\\Desktop\\Project\\ErrorLogs.txt", DateTime.Now + " Ошибка при получении события: " + Environment.NewLine + ex.ToString() + Environment.NewLine + Environment.NewLine);
            }
        }

        private void LogPrintEventToFile(PrintEvent printEvent)
        {
            try
            {
                string logEntry = $"Событие печати: {printEvent.ToString()}";
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Логирование ошибок при записи в файл
                File.AppendAllText(@"C:\\Users\\shein\\Desktop\\Project\\ErrorLogs.txt", DateTime.Now + " Ошибка при записи в файл: " + Environment.NewLine + ex.ToString() + Environment.NewLine + Environment.NewLine);
            }
        }

        private async Task SendPrintEventAsync(PrintEvent printEvent)
        {
            try
            {
                var json = JsonSerializer.Serialize(printEvent);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Укажите URL вашего веб-приложения и API контроллера
                var apiUrl = "https://localhost:7229/";

                // Создаем HttpClientHandler с игно  рированием ошибок сертификата
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                };

                using (var httpClient = new HttpClient(handler))
                {
                    var response = await httpClient.PostAsync(apiUrl, content);
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибок при передаче запросов
                File.AppendAllText(@"C:\\Users\\shein\\Desktop\\Project\\ErrorLog.txt", DateTime.Now + "Ошибка при передачи запроса HTTP: " + Environment.NewLine + ex.ToString() + Environment.NewLine + Environment.NewLine);
            }
        }

        protected override void OnStop()
        {
            // Отключаем подписку на события при остановке службы
            if (eventLogWatcher != null)
            {
                eventLogWatcher.Enabled = false;
                eventLogWatcher.Dispose();
            }
        }
    }
}
