using System;
using System.Net.Http;
using System.Net.Mail;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace HealthWebsiteCheckFunction
{
    public class TimerTrigger1
    {
        public readonly ILogger _logger;

        public TimerTrigger1(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TimerTrigger1>();
        }

        [Function("TimerTrigger1")]
        public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"Timer trigger executed at: {DateTime.Now}");

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }

            // Define constants for your site and Key Vault.
            const string siteUrl = "https://group4project-hegsacdnbrc7afad.canadaeast-01.azurewebsites.net";
            const string kvUri = "https://groupkey4.vault.azure.net/";
            const string recipientEmail = "ezeugenyionyinye@gmail.com";

            try
            {
                // Initialize Key Vault client using DefaultAzureCredential (ensure your managed identity or local credentials are configured)
                var secretClient = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());

                // Retrieve secrets (ensure these names match those you created in Key Vault)
                KeyVaultSecret mailSecret = (await secretClient.GetSecretAsync("Email")).Value;
                KeyVaultSecret passSecret = (await secretClient.GetSecretAsync("Password")).Value;

                string senderEmail = mailSecret.Value;
                string senderPassword = passSecret.Value;

                // Check website health
                bool isHealthy = await CheckWebsiteHealth(siteUrl);
                if (!isHealthy)
                {
                    await SendEmailAlertAsync(
                        subject: "ALERT: Website is down!",
                        body: $"Website at {siteUrl} is not responding at the moment.",
                        senderEmail: senderEmail,
                        senderPassword: senderPassword,
                        recipientEmail: recipientEmail
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during function execution");
            }
        }

        public async Task<bool> CheckWebsiteHealth(string url)
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            try
            {
                var response = await httpClient.GetAsync(url);
                _logger.LogInformation($"Website response: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Website health check failed");
                return false;
            }
        }

        public async Task SendEmailAlertAsync(
            string subject,
            string body,
            string senderEmail,
            string senderPassword,
            string recipientEmail)
        {
            using var smtpClient = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new System.Net.NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true,
                UseDefaultCredentials = false
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail),
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
            };

            mailMessage.To.Add(recipientEmail);

            try
            {
                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("Email alert sent successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email alert.");
            }
        }
    }
}
