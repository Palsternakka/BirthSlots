using System.Net;
using System.Text;
using System.Text.RegularExpressions;

internal class Program
{
    private static async Task Main(string[] args)
    {        
        while(true)
        {
            await DoScrape();
            Thread.Sleep(TimeSpan.FromMinutes(5));
        }
    }

    private static async Task DoScrape()
    {
        Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm} - Scrape Starting...");

        try
        {
            // Get Session ID
            using var client = new HttpClient();
            var response = await client.GetAsync("https://telford.zipporah.co.uk/Registrars.Live/BirthBookingProcess/");
            
            var setCookieHeader = response.Headers.GetValues("Set-Cookie");
            var aspNetSessionId = string.Empty;

            foreach (var headerValue in setCookieHeader)
            {
                var cookieParts = headerValue.Split(';').Select(part => part.Trim()).ToList();

                foreach (var part in cookieParts)
                {
                    if (part.StartsWith("ASP.NET_SessionId="))
                    {
                        aspNetSessionId = part.Substring("ASP.NET_SessionId=".Length);
                        break;
                    }
                }

                if (aspNetSessionId != null)
                    break;
            }

            if (aspNetSessionId == string.Empty)
            {
                throw new Exception("Could not get session ID");
            }

            // Do Intro Questions (without this no appointments are returned)
            var introUri = new Uri("https://telford.zipporah.co.uk/Registrars.Live/BirthBookingProcess/IntroductionAndQuestions");

            using var introClient = new HttpClient(GetHandler(introUri, aspNetSessionId));
            await introClient.PostAsync(introUri, null);

            var appointments = new List<string>();

            var dates = new List<DateTime>();
            var today = DateTime.Today;

            for (int i = 0; i <= 30; i++)
            {
                var futureDate = today.AddDays(i);

                // Set the day of current appointment below (will search for appointments prior)
                if (futureDate == new DateTime(today.Year, 7, 1))
                {
                    break;
                }

                dates.Add(futureDate);
            }

            foreach (var date in dates)
            {
                var timeUri = new Uri($"https://telford.zipporah.co.uk/Registrars.Live/BirthBookingProcess/GetSlots?date={date.ToString("ddd MMM dd yyyy").Replace(" ", "+")}&resourceCategoryId=1318811250&processType=Calendar");

                using var timeClient = new HttpClient(GetHandler(timeUri, aspNetSessionId));

                var timeResponse = await timeClient.GetStringAsync(timeUri);

                var times = new List<string>();

                foreach (Match match in Regex.Matches(timeResponse, @"\d{2}:\d{2}"))
                {
                    // Skip these
                    if (match.Value == "00:00" || match.Value == "00:20")
                    {
                        continue;
                    }

                    times.Add(match.Value);
                }

                foreach (var time in times)
                {
                    appointments.Add(date.ToString("ddd dd MMM ") + time);
                }
            }

            if (appointments.Any())
            {
                var sb = new StringBuilder();

                foreach (var appointment in appointments.Distinct())
                {
                    sb.AppendLine(appointment);
                }

                var textClient = new TextmagicRest.Client("oliverdalton", "XtJPTKKfo3nSr53dkTLIXin6VQQYYi");
                textClient.SendMessage(sb.ToString(), "447948512994");

                Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm} - {appointments.Distinct().Count()} appointments sent");
            }
            else
            {
                Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm} - No appointments");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm} - {ex}");
        }
    }

    private static HttpClientHandler GetHandler(Uri uri, string sessionId)
    {
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer
        };

        cookieContainer.Add(uri, new Cookie("ASP.NET_SessionId", sessionId));
        cookieContainer.Add(uri, new Cookie("js", "1"));

        return handler;
    }
}