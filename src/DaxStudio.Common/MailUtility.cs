using System;
using System.IO;
using System.Net.Mail;
using System.Reflection;

namespace DaxStudio.Common
{

    /*  // Example usage
     *  
        var mailMessage = new MailMessage();
        mailMessage.From = new MailAddress("someone@yourdomain.com");
        mailMessage.Subject = "Your subject here";
        mailMessage.IsBodyHtml = true;
        mailMessage.Body = "<span style='font-size: 12pt; color: red;'>My HTML formatted body</span>";

        mailMessage.Attachments.Add(new Attachment("C://Myfile.pdf"));

        var filename = "C://Temp/mymessage.eml";

        //save the MailMessage to the filesystem
        mailMessage.Save(filename);

        //Open the file with the default associated application registered on the local machine
        Process.Start(filename);
     */
    public static class MailUtility
    {
        //Extension method for MailMessage to save to a file on disk
        public static void Save(this MailMessage message, string filename, bool addUnsentHeader = true)
        {
            using (var filestream = File.Open(filename, FileMode.Create))
            {
                if (addUnsentHeader)
                {
                    using (var binaryWriter = new BinaryWriter(filestream))
                    {
                        //Write the Unsent header to the file so the mail client knows this mail must be presented in "New message" mode
                        binaryWriter.Write(System.Text.Encoding.UTF8.GetBytes("X-Unsent: 1" + Environment.NewLine));
                    }
                }

                var assembly = typeof(SmtpClient).Assembly;
                var mailWriterType = assembly.GetType("System.Net.Mail.MailWriter");

                // Get reflection info for MailWriter contructor
                var mailWriterContructor = mailWriterType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Stream) }, null);

                // Construct MailWriter object with our FileStream
                var mailWriter = mailWriterContructor.Invoke(new object[] { filestream });

                // Get reflection info for Send() method on MailMessage
                var sendMethod = typeof(MailMessage).GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic);

                sendMethod.Invoke(message, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { mailWriter, true, true }, null);

                // Finally get reflection info for Close() method on our MailWriter
                var closeMethod = mailWriter.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic);

                // Call close method
                closeMethod.Invoke(mailWriter, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { }, null);
            }
        }
    }
}