using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using SMSGATE.Properties;
using Vas.EmailAlertMessage;
using System.IO;
 


namespace SMSGATE
{

   public  class CEmailAlert
    {
       private string SmtpServer = "smtp.gmail.com";
        private int SmtpPort = 587;
        private string RoutingEmail = "";
        private   string Routingpassword = "";
        private string FromEmail = "";
        private string DisplayName = "";
        private string ReplyTo = "";
        private MailPriority MailPriority = MailPriority.Normal;
        private bool IsHtml = true;
        private int SupportSSL = 1;
        private SmtpClient client = null;

        public CEmailAlert()
        {
 

        }
        // constructor
        public CEmailAlert(string smtpServer, int smtpPort, string routingEmail, string routingpassword,string displayName, string fromEmail,int supportSSL, string replyTo, MailPriority mailPriority, bool isHtml)
        {
            SmtpServer = smtpServer;
            SmtpPort = smtpPort;
            RoutingEmail = routingEmail;
            Routingpassword = routingpassword;
            FromEmail = fromEmail;
            DisplayName =displayName ;
            SupportSSL = supportSSL;
            ReplyTo = replyTo;
            MailPriority = mailPriority;
            IsHtml = isHtml;

        }

        ~CEmailAlert()
        {
           // kill the client object here  

        }


        public bool PostMail(CEmailAlertMessage cmail)
        {
            

            bool rtn = false;
            //Builed The MSG
            System.Net.Mail.MailMessage msg = new System.Net.Mail.MailMessage();

            if (cmail.ToProperty.Count > 0)
            {
                foreach (string strTo in cmail.ToProperty)
                {
                    if (strTo.Trim() != "")
                    {
                        msg.To.Add(strTo);
                    }

                }
            }
            else
            {

                return rtn ;
            }
           
            if (cmail.CCProperty.Count > 0)
            {

                foreach (string strTo1 in cmail.CCProperty)
                {
                    if (strTo1.Trim() != "")
                    {
                        msg.CC.Add(strTo1);
                         
                    }

                }
            }

            if (cmail.BccProperty.Count > 0)
            {

                foreach (string strTo2 in cmail.BccProperty)
                {
                    if (strTo2.Trim() != "")
                    {
                        msg.Bcc.Add(strTo2);
                    }

                }
            }

            ReplyTo = string.IsNullOrEmpty(cmail.ReplyToProperty) ? @ReplyTo : cmail.ReplyToProperty;
            if (cmail.DisplayName != "")
            {
                if (cmail.DisplayName.Trim() == String.Empty)
                {

                    msg.ReplyTo = new MailAddress(@ReplyTo, @DisplayName, System.Text.Encoding.UTF8);
                    msg.From = new MailAddress(@FromEmail, @DisplayName, System.Text.Encoding.UTF8);
                }
                else
                {
                    msg.ReplyTo = new MailAddress(@ReplyTo, cmail.DisplayName, System.Text.Encoding.UTF8);
                    msg.From = new MailAddress(@FromEmail, cmail.DisplayName, System.Text.Encoding.UTF8);
                }
            }
            else
            {
                msg.ReplyTo = new MailAddress(@FromEmail, @DisplayName, System.Text.Encoding.UTF8);
                msg.From = new MailAddress(@FromEmail, @DisplayName, System.Text.Encoding.UTF8);

            }


            msg.Subject = cmail.SubjectProperty;
 
            msg.SubjectEncoding = System.Text.Encoding.UTF8;
            msg.Body = cmail.BodyProperty;
            msg.BodyEncoding = System.Text.Encoding.UTF8;
            msg.IsBodyHtml = @IsHtml;
            msg.Priority = @MailPriority;
            
            //Add the Creddentials
            client = new SmtpClient();


            client.UseDefaultCredentials = false;

            client.EnableSsl = true;
            // routing email is the sender
            client.Credentials = new System.Net.NetworkCredential(@RoutingEmail, @Routingpassword);
            client.Port = SmtpPort;            
            client.Host = SmtpServer;
            client.EnableSsl = true;
            
            try
            {
                //you can also call client.Send(msg)
                //client.SendAsync(msg, userState);

                client.Send(msg);
                //Console.WriteLine(cmail. + " has been emailed");
                rtn = true;
                 
            }
            catch (System.Net.Mail.SmtpException ex)
            {

                Console.WriteLine(ex.Message, "Send Mail Error");

            }
            return rtn;
        }

    }
}
