using System.Net;
using System.Net.Mail;

namespace MotorcyclePartShop.Services
{
    // 1. Tạo Interface
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string message);
    }

    // 2. Implement Class
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            var mailServer = _configuration["EmailSettings:MailServer"];
            var mailPort = int.Parse(_configuration["EmailSettings:MailPort"]);
            var senderName = _configuration["EmailSettings:SenderName"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];

            var client = new SmtpClient(mailServer, mailPort)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = message,
                IsBodyHtml = true // Cho phép gửi nội dung HTML
            };

            mailMessage.To.Add(email);

            await client.SendMailAsync(mailMessage);
        }
    }
}