using System;
using System.Net;
using System.Net.Mail;
using System.Collections.Generic;
//using System.Linq;
using System.Text;

namespace SMSGATE
{
    public class CEmailConfig
    {

        private string m_OutgoingQueue;
        private string m_SmtpServer;
        private int m_SmtpPort;
        private string m_SenderEmail;
        private string m_SenderPassword;
        private string m_FromEmail;
        private int m_SupportSSL;
        private string m_DisplayName;
        private string m_ReplyTo;
        private MailPriority m_MailPriority;
        private bool m_IsHtml;


        public CEmailConfig()
        {
            m_SupportSSL = 1;
            m_SmtpServer = "";

            m_SenderEmail = "";
            m_SenderPassword = "";
            m_FromEmail = "";
            m_IsHtml = true;
            m_MailPriority = MailPriority.Normal;
            m_ReplyTo = "";
            m_DisplayName = "";
        }
        /// <summary>
        /// The Path to the queue where the email Messages will go
        /// </summary>
        public string OutgoingQueue
        {
            get { return m_OutgoingQueue; }
            set { m_OutgoingQueue = value; }
        }
        /// <summary>
        /// The SMTP Server
        /// </summary>
        public string SmtpServer
        {
            get { return m_SmtpServer; }
            set { m_SmtpServer = value; }
        }
        /// <summary>
        /// Integer port number for the hostname or smtp server
        /// </summary>
        public int SmtpPort
        {
            get { return m_SmtpPort; }
            set { m_SmtpPort = value; }
        }
        /// <summary>
        /// This is the  email address of sender
        /// </summary>
        public string SenderEmail
        {
            get { return m_SenderEmail; }
            set { m_SenderEmail = value; }
        }
        /// <summary>
        /// Authenticating paswword of sender
        /// </summary>
        public string SenderPassword
        {
            get { return m_SenderPassword; }
            set { m_SenderPassword = value; }
        }
        /// <summary>
        /// This where email appears to be comming from
        /// </summary>

        public string FromEmail
        {
            get { return m_FromEmail; }
            set { m_FromEmail = value; }

        }
        /// <summary>
        /// Default display name of email
        /// </summary>
        public string DisplayName
        {
            get { return m_DisplayName; }
            set { m_DisplayName = value; }
        }

        /// <summary>
        /// Flag to show support of SSL, 1 support sll, 0 does not. sefault is 1
        /// </summary>
        public int SupportSSL
        {
            get { return m_SupportSSL; }
            set { m_SupportSSL = value; }
        }
        /// <summary>
        /// Set Email Address To Reply To
        /// </summary>
        public string ReplyTo
        {
            get { return m_ReplyTo; }
            set { m_ReplyTo = value; }
        }

        /// <summary>
        /// Flag to Set Email Body: Text or Html
        /// </summary>
        public bool IsHtml
        {
            get { return m_IsHtml; }
            set { m_IsHtml = value; }
        }

        /// <summary>
        /// Setting Mail Delivery Priority as High, Normal and Low depending on Message Urgency.
        /// </summary>
        public MailPriority MailPriority
        {
            get { return m_MailPriority; }
            set { m_MailPriority = value; }
        }


    }



}



     