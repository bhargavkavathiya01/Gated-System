using System.Net;
//using System.Net.Mail;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;


namespace Gated_System.Helpers
{
    public class SendEmailHelper
    {
        private readonly IConfiguration _config;

        public SendEmailHelper(IConfiguration config)
        {
            _config = config;
        }

        public class EmailModel
        {
            public string To { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
        }

        public async Task SendEmailAsync(EmailModel model)
        {
            using var smtpClient = new System.Net.Mail.SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(
                    "nandi.thefutureishtech@gmail.com",
                    "kotjreqvgvumnrpn"
                ),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress("nandi.thefutureishtech@gmail.com"),
                Subject = model.Subject,
                Body = model.Body,
                IsBodyHtml = true,
            };

            mailMessage.To.Add(model.To);

            await smtpClient.SendMailAsync(mailMessage);
        }


        //public async Task SendEmailAsync(EmailModel model)
        //{
        //    var email = new MimeMessage();
        //    // This is the "From" name users will see
        //    email.From.Add(new MailboxAddress("My Web App", _config["EmailSettings:Email"]));
        //    email.To.Add(MailboxAddress.Parse(model.To));
        //    email.Subject = model.Subject;

        //    var builder = new BodyBuilder { HtmlBody = model.Body };
        //    email.Body = builder.ToMessageBody();

        //    using var smtp = new SmtpClient();
        //    try
        //    {
        //        await smtp.ConnectAsync("smtp-relay.brevo.com", 587, SecureSocketOptions.StartTls);
        //        await smtp.AuthenticateAsync(_config["EmailSettings:Email"], _config["EmailSettings:Password"]);
        //        await smtp.SendAsync(email);
        //    }
        //    finally
        //    {
        //        await smtp.DisconnectAsync(true);
        //    }
        //}
    }
}
