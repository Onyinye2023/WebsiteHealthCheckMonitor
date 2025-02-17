using System;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using MailKit.Net.Smtp;
using MimeKit;

namespace HealthWebsiteCheckFunction
{
    public class HealthChecker
    {
        private readonly ILogger _logger;

        // Constructor to initialize logger
        public HealthChecker(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HealthChecker>();
        }

        [Function("HealthChecker")]
        public async Task Run([TimerTrigger("0 */2 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"Timer trigger executed at: {DateTime.Now}");

            // Log the next scheduled execution time
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }

            // Website URL to monitor
            const string siteUrl = "https://group4project-hegsacdnbrc7afad.canadaeast-01.azurewebsites.net";
            
            // Azure Key Vault URL
            const string kvUri = "https://groupkey4.vault.azure.net/";
            
            // Recipient email for alerts
            const string recipientEmail = "ezeugenyionyinye@gmail.com";

            try
            {
                // Authenticate interactively using MSAL.NET
                var token = await AuthenticateWithMSAL();

                // Initialize Key Vault client using DefaultAzureCredential (relies on environment-based authentication)
                var secretClient = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());

                // Retrieve email credentials from Key Vault
                KeyVaultSecret mailSecret = (await secretClient.GetSecretAsync("Email")).Value;
                KeyVaultSecret passSecret = (await secretClient.GetSecretAsync("Password")).Value;

                string senderEmail = mailSecret.Value;
                string senderPassword = passSecret.Value;

                // Check if the website is healthy
                bool isHealthy = await CheckWebsiteHealth(siteUrl);
                if (!isHealthy)
                {
                    // Send an email alert if the website is down
                    await SendEmailAlertAsync(
                        "ALERT: Website is down!",
                        $"Website at {siteUrl} is not responding.",
                        senderEmail,
                        senderPassword,
                        recipientEmail
                    );
                }
            }
            catch (Exception ex)
            {
                // Log any errors encountered during execution
                _logger.LogError(ex, "Error during function execution");
            }
        }

        // Method to check website health
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

        // Method to send an email alert
        public static async Task SendEmailAlertAsync(string subject, string body, string senderEmail, string senderPassword, string recipientEmail)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("My C# App", senderEmail));
            message.To.Add(new MailboxAddress("Recipient", recipientEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };
 
            using var client = new SmtpClient();
            try
            {
                // Connect to Gmail SMTP server
                await client.ConnectAsync("smtp.gmail.com", 465, true);
                await client.AuthenticateAsync(senderEmail, senderPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                Console.WriteLine("Email sent successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
            }
        }
 
        // Method to authenticate using MSAL.NET
        private async Task<string> AuthenticateWithMSAL()
        {
            var clientId = "b56e1a6d-89c7-4811-91ed-12f0b8ab7c20";
            var tenantId = "592f510b-4b4d-4756-8198-5a62b48a0dd9";
            var redirectUri = "http://localhost:5001/";

            var app = PublicClientApplicationBuilder.Create(clientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, tenantId) 
                .WithRedirectUri(redirectUri) 
                .Build();

            var scopes = new[] { "https://vault.azure.net/.default" };

            try
            {
                // Acquire an access token interactively
                var result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
                _logger.LogInformation("Access token acquired successfully.");
                return result.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication failed.");
                throw;
            }
        }
    }
}
