
 
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Messaging;
using System.Collections;
using Devshock.Net;
using System.Net;
using System.IO;
using Devshock.Protocol.Smpp;
using Devshock.Protocol.SmppPdu;
using Devshock.Common;
using SMSGATE.Properties;
using SMSGATE.src.DAL;
using Vas.Transaction.Protocols;
using Vas.Transaction.Messaging;
using SMSGATE.src.SMPP;
using SMSGATE.src.Common;
using System.Reflection;
using System.Globalization;       
using log4net;
using log4net.Config;
using Vas.EmailAlertMessage;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Xml;
 
 


namespace SMSGATE
{
    class Program
    {

        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //private static readonly ILog logger = LogManager.GetLogger(typeof(Program ));
        // Data Base connection Tools
        #region DataBase Connection String from ConfigFile
        private static string conn = ".\\Private$\\" + Settings.Default.ConnectionString;
        private static string InternalContentQueue = ".\\Private$\\" + Settings.Default.InternalMessageArrival;
        private static string ExternalContentQueue = ".\\Private$\\" + Settings.Default.ExternalMessageArrival;
        private static string ErrorMessageQueue = ".\\Private$\\" + Settings.Default.ErrorMessageQueue; // bad messages goes here
        private static string GeneralMessageDeparture = ".\\Private$\\" + Settings.Default.MessageDeparture;
        private static string DeliveryResponseMessageQueue = ".\\Private$\\" + Settings.Default.DeliveryResponseMessageQueue;
        private static string ShortCodePrefixToTrim = Settings.Default.ShortCodePrefixToTrim.Trim();
        // This is the queue where transactionMessage Object loggs data to the transactionLog Database
        private static string DatabaseLogger = ".\\Private$\\" + Settings.Default.DatabaseLoggerQueue;
        #endregion

        //Threading management tools
        #region Thread Control Parameters
        private static bool m_isApplicationTerminating = false;
        //private static ArrayList ActiveBinds = null;
        // Stores SMPP Binds
        private static Hashtable SmppBinds = null;

        private static Hashtable HttpBinds = null;
        //Stores Shortcodes and contentprovider queue parths that determines which vas processes message
        private static Hashtable  ShortCodeTable = null;
        private static int m_threads = 0;
        private static TimeSpan ThraedSleepTime = Settings.Default.ThraedSleepTime;
        private static TimeSpan FileAchiveIntervalIndays = Settings.Default.FileArchiveIntervalIndays;
        private static Object m_thisLock = new Object();
        private static bool isFirstInstance = true;

        private static ArrayList adminToList = null;
        private static    ArrayList adminCCList =null;
        private static  ArrayList adminBCCList = null;

         private static  CEmailConfig  emailConfig = null;
         private static CEmailAlert cem =  null; //  new CEmailAlert(emailConfig.SmtpServer, emailConfig.SmtpPort, emailConfig.SenderEmail, emailConfig.SenderPassword, emailConfig.DisplayName, emailConfig.FromEmail, emailConfig.SupportSSL);



        #endregion
        
        static void Main(string[] args)
        {
            
        
      //log4net.Appender.TelnetAppender

            // log4net here
            string appId = String.Format(@"{0}\{1}.exe", Environment.CurrentDirectory,
                               Assembly.GetExecutingAssembly().GetName().Name);


            // Connect to log4net file
            XmlConfigurator.Configure(new System.IO.FileInfo(Settings.Default.InstrumentationFileName));
            log4net.GlobalContext.Properties["identity"] = appId;

            
            //DOMConfigurator.Configure(fl);
         
           
            // Smpp Bind storage
            SmppBinds = new Hashtable();

            // List of shortcodes and corresponding content providers
            ShortCodeTable = new Hashtable();
            
             // Http Bind Storage
            HttpBinds = new Hashtable();

            Console.Title = Settings.Default.ConsoleTitle + " : Running Since: " + DateTime.Now.ToString ("yyyy-MM-dd HH:mm:ss.fff");
              DataAccessLayer dal = new DataAccessLayer(Settings.Default.ConnectionString);
            try
            {
                
                dal.Connect();

                if (dal.IsConnected == true)
                {

                    ShortCodeTable = dal.GetShortCode(InternalContentQueue, ExternalContentQueue);
                    ArrayList ar = dal.GetProtocolList();

                    Thread SmppMessageThread = null; // MessageHandlers        
                    Thread SmppArrivalThread = null; // Arriving Message
                    Thread SmppDepartureThread = null; // Departing Message
                    Thread SmppGeneralDepartureThread = null;
                    Thread HttpReceiverThread = null;
                    Thread EventSchedulerThread = null; // Message Scheduling
                    Thread BackupRoutineThread = null; // Achives activity logs as compressed files
                    foreach (TransportProtocols tp in ar)
                    {
                        // Queues for messaging are created and formated here
                        InitializeMessageQueue(tp);

                          //DoSomeThing(tp);
                        try
                        {
                           
                            switch (tp.TransportMode.ToLower())
                            {
                                case "smpp":
                            //Messaging
                            SmppMessageThread = new Thread(new ParameterizedThreadStart(SmppMessageHandler));
                            SmppMessageThread.Start(tp);
                            Interlocked.Increment(ref  m_threads);

                            //Arrival Management
                            SmppArrivalThread = new Thread(new ParameterizedThreadStart(SmppArrivalHandler));
                            SmppArrivalThread.Start(tp);
                            Interlocked.Increment(ref  m_threads);

                            //Individual Departure Management
                            // This is exit queue for each transmitter bind extablished in smpp 3.3 or 3.4
                            SmppDepartureThread = new Thread(new ParameterizedThreadStart(SmppDepartureHandler));
                            SmppDepartureThread.Start(tp);
                            Interlocked.Increment(ref  m_threads);
                            break;
                                case "http":
                                    //
                            HttpReceiverThread   = new Thread(new ParameterizedThreadStart(HttpReciverHandler));
                            HttpReceiverThread.Start(tp);
                            Interlocked.Increment(ref  m_threads);
                            break;

                                default :
                            break;

                          }
                        }
                        catch (Exception ex)
                        {
                            logger.Error("Thread Start Error: " + ex.Message.ToNewLine());
                        }



                    } // end foreach loop


                    // General Departure Thread
                    try
                    {
                        SmppGeneralDepartureThread = new Thread(new ParameterizedThreadStart(SmppGeneralDepartureHandler));
                        SmppGeneralDepartureThread.Start();
                        Interlocked.Increment(ref  m_threads);
                    }
                    catch (Exception ex1)
                    {
                        logger.Error("Thread Start Error:[DepartureHandler] " + ex1.Message.ToNewLine());
                    }

                    // Backup And Achiving of logs

                    BackupRoutineThread = new Thread(new ParameterizedThreadStart(BackupRoutineHandler));
                    BackupRoutineThread.Start();
                    Interlocked.Increment(ref  m_threads);


                    // Alert Notification Thread
                    // This will send alert to System Administrators configured in the EmailAddresses table
                    try
                    {

                         
                         // The To Email Field- can be many as configured in the EmailAddresses table
                         adminToList = new ArrayList();
                         adminToList = dal.GetEmailList("to");

                        // Get the CC mails
                        adminCCList = new ArrayList();
                        adminCCList = dal.GetEmailList("cc");


                        // Get the bcc field
                        adminBCCList = new ArrayList();
                        adminBCCList = dal.GetEmailList("bcc");

                        // Get the email routing configuration data
                        emailConfig = new CEmailConfig();
                        emailConfig = dal.GetEmailConfiguration();

                              InitializeMessageQueue(emailConfig);

                              cem = new CEmailAlert(emailConfig.SmtpServer, emailConfig.SmtpPort, emailConfig.SenderEmail, emailConfig.SenderPassword, emailConfig.DisplayName, emailConfig.FromEmail, emailConfig.SupportSSL, emailConfig.ReplyTo, emailConfig.MailPriority, emailConfig.IsHtml);


                       Thread SmppEmailThread = new Thread(new ParameterizedThreadStart(SmppEmailThreadHandler));
                        SmppEmailThread.Start();
                        Interlocked.Increment(ref  m_threads);
                    }
                    catch (Exception ex1)
                    {
                        logger.Error("Thread Start Error:[DepartureHandler] " + ex1.Message.ToNewLine());
                    }





                } // end if
                else
                {
                    logger.Fatal("Data base Connection Error".ToNewLine());
                    dal.DisConnect();
                }

            }
            catch (Exception ex2)
            {
                logger.Error("Commencement Error " + ex2.Message.ToNewLine());
            }
 
 


            Mutex mutex = null;

            while ((m_threads > 0) || (!m_isApplicationTerminating))
            {
                if (!m_isApplicationTerminating)
                {
                    //mutex = new Mutex(false, Environment.CurrentDirectory, out isFirstInstance);
                    string mutexname = String.Format(@"{0}\{1}.exe", Environment.CurrentDirectory, Assembly.GetExecutingAssembly().GetName().Name);
                    mutex = new Mutex(false, mutexname.Replace(@"\", String.Empty), out isFirstInstance);
                    Thread.Sleep(1000);
                }
                if (isFirstInstance)
                {
                    m_isApplicationTerminating = true;

                    Thread.Sleep(1000);
                }
                else
                {
                    if (mutex != null)
                    {
                        mutex.Close();
                    }
                    Thread.Sleep(1000);
                }

                Thread.Sleep(ThraedSleepTime);
            }





        }

        private static void BackupRoutineHandler(object data)
        {
            Mutex mutex = null;

            while ((m_threads > 0) || (!m_isApplicationTerminating))
            {
                if (!m_isApplicationTerminating)
                {
                    //mutex = new Mutex(false, Environment.CurrentDirectory, out isFirstInstance);
                    string mutexname = String.Format(@"{0}\{1}.exe", Environment.CurrentDirectory, Assembly.GetExecutingAssembly().GetName().Name);
                    mutex = new Mutex(false, mutexname.Replace(@"\", String.Empty), out isFirstInstance);
                    Thread.Sleep(1000);
                }
                if (isFirstInstance)
                {
                    m_isApplicationTerminating = true;

                    Thread.Sleep(1000);
                }
                else
                {
                    if (mutex != null)
                    {
                        mutex.Close();
                    }
                    Thread.Sleep(1000);
                }
                // Create Achive

                string sptern = "*" + DateTime.Now.Year + "*";

                string[] filenames = Directory.GetFiles(System.IO.Directory.GetCurrentDirectory() + "\\logs", sptern);


                if (filenames.Length >= Settings.Default.NumberofFiletoArchive)
                {
                    CFileCompressionUtility fcu = new CFileCompressionUtility();
                    fcu.PKCompress(System.IO.Directory.GetCurrentDirectory() + "\\logs", System.IO.Directory.GetCurrentDirectory() + "\\Archive\\vaslog" + DateTime.Now.ToString("yyyyMMdd") + ".rar", logger);
                   // fcu.PKCompress(System.IO.Directory.GetCurrentDirectory() + "\\logs", System.IO.Directory.GetCurrentDirectory() + "\\Archive\\vaslog" + DateTime.Now.ToString("yyyyMMdd") + ".rar", logger);
                
                }
                Thread.Sleep(FileAchiveIntervalIndays);
            }
        }


        private static void HttpReciverHandler(object data)
        {

            TransportProtocols tp = (TransportProtocols)data ;

            

            if (IsValidIP(tp.RemoteHost) == true)
            {
                //
            }
            else
            {
                try
                {
                    IPAddress[] address = Dns.GetHostAddresses(tp.RemoteHost);
                     
                    foreach (IPAddress theaddress in address)
                    {
                        if (IsValidIP(theaddress.ToString()) == true  )
                        {
                             tp.RemoteHost = theaddress.ToString();
                             logger.Warn("Http IP Host Address: " + theaddress.ToString().ToNewLine());
                            break;
                        }
                        
                       
                        
                    }

                }
                catch (Exception ex)
                {

                    //  ex.Message;
                }

            }
            
            CreateHttpListener(tp);
            
            


            Mutex mutex = null;

            while ((m_threads > 0) || (!m_isApplicationTerminating))
            {
                if (!m_isApplicationTerminating)
                {
                    //mutex = new Mutex(false, Environment.CurrentDirectory, out isFirstInstance);
                    string mutexname = String.Format(@"{0}\{1}.exe", Environment.CurrentDirectory, Assembly.GetExecutingAssembly().GetName().Name);
                    mutex = new Mutex(false, mutexname.Replace(@"\", String.Empty), out isFirstInstance);
                    Thread.Sleep(1000);
                }
                if (isFirstInstance)
                {
                    m_isApplicationTerminating = true;
                    Thread.Sleep(1000);
                }
                else
                {
                    if (mutex != null)
                    {
                        mutex.Close();
                    }

                }

                Thread.Sleep(ThraedSleepTime);


            }
        }

        private static void CreateHttpListener(TransportProtocols tp)
        {


              HttpListener httpListener = null;

        
            try
            {
                if (!HttpListener.IsSupported)
                {
                    logger.Info("Windows XP SP2 or Server 2003 is required to use the HttpListener class.".ToNewLine());
                    return;
                }

                // Ensure the the required security rights are granted
                WebPermission webPermission = new WebPermission();

                webPermission.AddPermission(NetworkAccess.Accept, "http://" + tp.RemoteHost + ":" + tp.RemotePort.ToString() + "/");

                webPermission.AddPermission(NetworkAccess.Connect, "http://" + tp.RemoteHost + ":" + tp.RemotePort.ToString() + "/");
                webPermission.Demand();

                SocketPermission socketPermission = new SocketPermission(PermissionState.Unrestricted);

                socketPermission.AddPermission(NetworkAccess.Accept,
                                               TransportType.All,
                                                tp.RemoteHost,
                                               Convert.ToInt32(tp.RemotePort.ToString()));

                socketPermission.AddPermission(NetworkAccess.Connect,
                                               TransportType.All,
                                                 tp.RemoteHost,
                                               Convert.ToInt32(tp.RemotePort.ToString()));
                socketPermission.Demand();
                logger.Info("url=http://" + tp.RemoteHost + ":" + tp.RemotePort.ToString().ToNewLine());
                // Create an HTTP listener
                httpListener = new HttpListener();
                httpListener.Prefixes.Add("http://" + tp.RemoteHost + ":" +
                                                       tp.RemotePort.ToString() + "/");


                //always add local host
                httpListener.Prefixes.Add("http://localhost:" +
                                                       tp.RemotePort.ToString() + "/");
               
                httpListener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                httpListener.Start();



                HttpBinds.Add(tp.RemotePort, tp);




                 
                while ((m_threads > 0) || (!m_isApplicationTerminating))
                {

                    logger.Info("Waiting for HTTP request to be processed asyncronously.".ToNewLine());

                    IAsyncResult iAsyncResult = httpListener.BeginGetContext(new AsyncCallback(processHttpRequest),
                                                                             httpListener);
                    iAsyncResult.AsyncWaitHandle.WaitOne();
                    System.Threading.Thread.Sleep(100);

                }

                System.Threading.Thread.Sleep(1000);
                httpListener.Stop();
            }


            catch (HttpListenerException exception)
            {
                logger.Error(exception.Message.ToNewLine());
                logger.Error(exception.StackTrace.ToNewLine());
            }

            finally
            {
                lock (m_thisLock)
                {
                    m_threads--;
                }
                //CiscoCrmAdapter_GetValueService_Service1
                try
                {
                    httpListener.Stop();
                    httpListener.Close();
                }

                catch (Exception)
                {
                }
            }

            return;
        }

   private static bool IsValidIP(string addr )
        {
            
          //create our match pattern
            string pattern = @"\b(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b$";
          //create our Regular Expression object
          Regex check = new Regex(pattern);
          //boolean variable to hold the status
          bool valid = false;
          //check to make sure an ip address was provided
          if (addr == "")
          {
              //no address provided so return false
              valid = false;
          }
          else
          {
              //address provided so use the IsMatch Method
              //of the Regular Expression object
              valid = check.IsMatch(addr, 0);
          }
          //return the results
          return valid;
      
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="iAsyncResult"></param>
        public static void processHttpRequest(IAsyncResult iAsyncResult)
        {
            HttpListener httpListener = null;
            HttpListenerContext httpListenerContext = null;
            HttpListenerRequest httpListenerRequest = null;
            HttpListenerResponse httpListenerResponse = null;

            System.IO.Stream outputStream = null;
            //String responseString = null;
           
            try
            {
                //http://localhost:5020/mydoc.aspx?user=godwin&password=agbon&sms_src_addr=cybsoft&sms_dest_addr=2347025686099&sms_text=Hello Wourld&sms_id=5353534343
               
                httpListener = (HttpListener)iAsyncResult.AsyncState;

                // Call EndGetContext to complete the asynchronous operation.
                httpListenerContext = httpListener.EndGetContext(iAsyncResult);
                httpListenerRequest = httpListenerContext.Request;
                NameValueCollection queryString = httpListenerRequest.QueryString;
                string ResponseMessage ="Wrong message format";
                if (queryString.AllKeys.Length >=  6 )
                {
                string user = getQueryStringValue("user", queryString);
                string password = getQueryStringValue("password", queryString);
                string sms_src_addr = getQueryStringValue("sms_src_addr", queryString);
                string sms_dest_addr = getQueryStringValue("sms_dest_addr", queryString);
                string sms_text = getQueryStringValue("sms_text", queryString);
                string sms_id = getQueryStringValue("sms_id", queryString);

                logger.Warn(httpListenerContext.Request.LocalEndPoint.Port.ToString().ToNewLine());

                TransactionMessage msg = new TransactionMessage();
                msg.ArrivalProtocol = "http";
                msg.DateIn = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                msg.MessageType = SmsMessageType.SMSText;
                msg.ModifiedState = ModificationState.ResponseOut;
                msg.ModifiedStatus = 1;
                msg.ShortCode = sms_src_addr;
                msg.Msisdn = sms_dest_addr;
                msg.ResponseMessage = sms_text;

                ResponseMessage = "User ID or password may be wrong";

                if (HttpBinds.ContainsKey(httpListenerContext.Request.LocalEndPoint.Port) == true)
                {

                    TransportProtocols tp = (TransportProtocols)HttpBinds[httpListenerContext.Request.LocalEndPoint.Port];
                    if ((tp.SystemID.ToLower().Trim() == user.ToLower().Trim()) && (tp.Password.Trim() == password.Trim()))
                    {
                        msg.NetworkID = tp.NetworkID;
                        msg.OperatorID = tp.OperatorID;
                        ResponseMessage = msg.GuID;
                        sendToFinalDeparture(msg);
                    }

                }

               
                }

                logger.Info(httpListenerRequest.RawUrl.ToNewLine());
                logger.Info(queryString.ToString().ToNewLine());

               
                // Obtain a response object
                httpListenerResponse = httpListenerContext.Response;

                byte[] buffer = Encoding.UTF8.GetBytes(ResponseMessage);

                // Get a response stream and write the response to it
                httpListenerResponse.ContentLength64 = buffer.Length;
                outputStream = httpListenerResponse.OutputStream;
                outputStream.Write(buffer, 0, buffer.Length);

            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                Console.WriteLine(exception.StackTrace);
            }

            finally
            {
                try
                {
                    Console.WriteLine("Processed: " + httpListenerRequest.RawUrl + "\n");


                }

                catch (Exception)
                {
                }
            }

            return;
        }

        /// <summary>
        /// Handles Email notifications
        /// </summary>
        /// <param name="data"></param>
        private static void SmppEmailThreadHandler(object data)
        {

           // Wait for thread email Notifications
            CEmailAlertMessage alertMessage = new CEmailAlertMessage();
             
            
            WaitforEmail();
            
            
            
            
            Mutex mutex = null;

            while ((m_threads > 0) || (!m_isApplicationTerminating))
            {
                if (!m_isApplicationTerminating)
                {
                    //mutex = new Mutex(false, Environment.CurrentDirectory, out isFirstInstance);
                    string mutexname = String.Format(@"{0}\{1}.exe", Environment.CurrentDirectory, Assembly.GetExecutingAssembly().GetName().Name);
                    mutex = new Mutex(false, mutexname.Replace(@"\", String.Empty), out isFirstInstance);
                    Thread.Sleep(1000);
                }
                if (isFirstInstance)
                {
                    m_isApplicationTerminating = true;
                    Thread.Sleep(1000);
                }
                else
                {
                    if (mutex != null)
                    {
                        mutex.Close();
                    }

                }

                Thread.Sleep(ThraedSleepTime);
                
            }
        }
        // Email Sender Queue
        private static void WaitforEmail( )
        {
            try
            {
                //Create an instance of Me ssageQueue. Set its formatter.
                MessageQueue pQueue = new MessageQueue(".\\Private$\\" + emailConfig.OutgoingQueue);
                pQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(CEmailAlertMessage) });
                pQueue.PeekCompleted += new PeekCompletedEventHandler(PeekEmailMessageCompleted);
                logger.Info("Waiting for message assync... @ " + pQueue + "".ToNewLine());
                // Begin the asynchronous peek operation.
                pQueue.BeginPeek();
            }
            catch (Exception ex)
            {

                logger.Error(ex.Message.ToNewLine());
            }
        }

        private static void PeekEmailMessageCompleted(Object source, PeekCompletedEventArgs asyncResult)
        {
 
            
            // Connect to the queue.
            MessageQueue mq = (MessageQueue)source;

            try
            {
                // End the asynchronous peek operation.
                Message m = mq.EndPeek(asyncResult.AsyncResult);

               
                // Cast message to Season Business Entity
                CEmailAlertMessage msg = (CEmailAlertMessage)m.Body;
              
                bool retn = cem.PostMail(msg);
                              

                // Distribute to the appropriate queue

                if (retn == true)
                {
                    mq.ReceiveById(m.Id);
                }
                else
                {
                    // Log to Event Sink
                    Thread.Sleep(3000);
                }


            }
            catch (Exception exception)
            {
                logger.Error(exception.Message + "|" + exception.StackTrace.ToNewLine());
            }
            // Restart the asynchronous peek operation.

            mq.BeginPeek();
            ////mq.BeginReceive();
            return;
        }

        // Dommy Method
        private static void DoSomeThing(TransportProtocols tp)
        {
            if (tp.NetworkID.ToLower() == "visafone")
            {
                MessageQueue mq = new MessageQueue(".\\Private$\\" + tp.NetworkID + "_" + tp.OperatorID + "_IN");
                mq.DefaultPropertiesToSend.Recoverable = true;
                mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                mq.DefaultPropertiesToSend.Label = tp.NetworkID + ":" + tp.OperatorID;
                mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;

                TransactionMessage transactionMessage = new TransactionMessage();

                transactionMessage.ShortCode = "20101";
                transactionMessage.Msisdn = "2347025686099";
                transactionMessage.RequestMessage = "Yes";
                transactionMessage.ArrivalProtocol = "SMPP";
                transactionMessage.MessageType = 0; // SmsMessageType.SMSText.ToString();
                transactionMessage.NetworkID = tp.NetworkID;
                transactionMessage.OperatorID = tp.OperatorID;
                transactionMessage.DateIn = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"); ;
                transactionMessage.LastModified = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff"); ;
                transactionMessage.Direction = 1;
                transactionMessage.ModifiedStatus++;
                transactionMessage.ModifiedState = 0; // ModificationState.Arrival.ToString();
                transactionMessage.ResponseMessage = "";


                mq.Send(transactionMessage);
            }
        }


        private static void WaitforArrival(TransportProtocols transportprotocol)
        {
            //Create an instance of Me ssageQueue. Set its formatter.
            try
            {
                MessageQueue pQueue = new MessageQueue(".\\Private$\\" + transportprotocol.NetworkID + "_" + transportprotocol.OperatorID + "_IN");
                pQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                pQueue.PeekCompleted += new PeekCompletedEventHandler(PeekArrivalMessageCompleted);
                logger.Info("Waiting for message assync... @ " + ".\\Private$\\" + transportprotocol.NetworkID + "_" + transportprotocol.OperatorID + "_IN\n".ToNewLine());
                // Begin the asynchronous peek operation.
                pQueue.BeginPeek();
            }
            catch (Exception ex)
            {

                logger.Error(ex.Message.ToNewLine());
            }
             
        }
        private static void PeekArrivalMessageCompleted(Object source, PeekCompletedEventArgs asyncResult)
        {

            // Connect to the queue.
            MessageQueue mq = (MessageQueue)source;

            try
            {
                // End the asynchronous peek operation.
                Message m = mq.EndPeek(asyncResult.AsyncResult);


                // Cast message to Season Business Entity
                TransactionMessage msg = (TransactionMessage)m.Body;


                bool retn = false;

                SendToDbLogger(msg);
                retn = SendToContentProvider(msg); 
                 
                // Distribute to the appropriate queue

                if (retn == true)
                {
                    mq.ReceiveById(m.Id);
                }
                else
                {
                    // Log to Event Sink
                    Thread.Sleep(3000);
                }


            }
            catch (Exception exception)
            {
                logger.Error(exception.Message + "|" + exception.StackTrace.ToNewLine());
            }
            // Restart the asynchronous peek operation.

            mq.BeginPeek();
            ////mq.BeginReceive();
            return;
        }

        private static bool SendToContentProvider(TransactionMessage transactionMessage)
        {
            bool rtn = false;
            if (transactionMessage.ShortCode.Trim().Length == 0)
            {
       // SendToErrorQueue
                SendMessageToErrorQueue(transactionMessage,"No Origin");
                logger.Warn("Message Without Origin".ToNewLine());
                rtn = true;
                return rtn;
            }
            // Handle case where the shortcode is not yet configured

            if (ShortCodeTable.Contains(transactionMessage.ShortCode) == false)
            {
                // This must be a new short code not yet in the database
                // Send alert to owner
                ShortCodeTable.Add(transactionMessage.ShortCode, InternalContentQueue);
                // Insert this into the database and notify the System Administrator
               
                ConfigureShortCode(transactionMessage);

            }


            string queuePath = ShortCodeTable[transactionMessage.ShortCode].ToString();

            try
            {
                MessageQueue mq = new MessageQueue(queuePath);
                mq.DefaultPropertiesToSend.Recoverable = true;
                mq.Formatter = new System.Messaging.XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                //mq.DefaultPropertiesToSend.Label = transactionMessage.Network + ":" + transactionMessage.OperatorID + ": " + transactionMessage.Msisdn;
                mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                mq.Send(transactionMessage);
                rtn = true;
            }
            catch (Exception ex)
            {

                logger.Error(ex.Message.ToNewLine());

            }
            


            return rtn;
        }

        private static void ConfigureShortCode(TransactionMessage transactionMessage)
        {
            
            DataAccessLayer dasl = new DataAccessLayer(Settings.Default.ConnectionString);
            try
            {
                
                dasl.Connect();
                if (dasl.IsConnected == true)
                {
                  bool opt=  dasl.InsertNewShortcode(transactionMessage.ShortCode, transactionMessage.NetworkID, transactionMessage.OperatorID, logger);
                   // Send email to the admins
                  raiseFriendlyAlert(transactionMessage);
                }
            }
            catch (Exception ex)
            {

                logger.Error("New ShortCode Insert Failure: " + ex.Message.ToNewLine());

                SendMessageToErrorQueue(transactionMessage, ex.Message);
            }
            finally
            {
                dasl.DisConnect();
            }
            
        }

        private static void raiseFriendlyAlert(TransactionMessage transactionMessage)
        {
            try
            {
                CEmailAlertMessage emailmessage = new CEmailAlertMessage();
                emailmessage.SenderProperty = emailConfig.SenderEmail;
                emailmessage.SubjectProperty = "New ShortCode Confguration";
                emailmessage.DisplayName = emailConfig.DisplayName;
                emailmessage.SourceApplication = Settings.Default.ConsoleTitle;
                emailmessage.FromProperty = emailConfig.FromEmail;
                // To Email
                foreach (string emTo in adminToList)
                {
                    emailmessage.ToProperty.Add(emTo);
                }
                // CC Email
                foreach (string emCC in adminCCList)
                {
                    emailmessage.CCProperty.Add(emCC);

                }

                // BCC
                foreach (string emBCC in adminBCCList)
                {
                    emailmessage.BccProperty.Add(emBCC);
                }
                // 
                StringBuilder sb = new StringBuilder("Hello,\n");
                sb.AppendLine("A message arrived from a new short code: " + transactionMessage.ShortCode);
                sb.AppendLine("The short code has been inserted into the ShordCode table though");
                sb.AppendLine("the service it represents is not yet known. Please verify and");
                sb.AppendLine("make sure it is properly configured.");
                sb.AppendLine("At the time of sending this email message, the state of the server is as follows:");
                sb.AppendLine("============================================================================");
                sb.AppendLine("Total Number of active Shortcodes: " + ShortCodeTable.Count.ToString());
                int i = 0;
                foreach (string scode in ShortCodeTable.Keys)
                {
                    i++;
                    sb.AppendLine(i.ToString() + ". " + scode);
                }

                sb.AppendLine();
                sb.AppendLine("Total Number of active SMPP Binds: " + SmppBinds.Count.ToString());
                int j = 0;
                foreach (string scode1 in SmppBinds.Keys)
                {
                    j++;
                    sb.AppendLine(j.ToString() + ". " + scode1);
                }


                emailmessage.BodyProperty = sb.ToString();

                SendMessageToEmailQueue(emailmessage);
            }
            catch (Exception ex)
            {

                logger.Error(ex.Message.ToNewLine());
            }
            

        }

        private static void SendMessageToEmailQueue(CEmailAlertMessage emailmessage )
        {
            try
            {
                MessageQueue mq = new MessageQueue(".\\Private$\\" + emailConfig.OutgoingQueue);
                mq.DefaultPropertiesToSend.Recoverable = true;
                mq.DefaultPropertiesToSend.Label = emailmessage.SubjectProperty;
                mq.Formatter = new System.Messaging.XmlMessageFormatter(new Type[] { typeof(CEmailAlertMessage) });
                //mq.DefaultPropertiesToSend.Label = transactionMessage.Network + ":" + transactionMessage.OperatorID + ": " + transactionMessage.Msisdn;
                mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                mq.Send(emailmessage);
            }
            catch (Exception ex)
            {

                logger.Error(ex.Message.ToNewLine());
            }
        }



        private static void SendMessageToErrorQueue(TransactionMessage transactionMessage,string errorMsg)
        {
            try
            {
                MessageQueue mq = new MessageQueue(ErrorMessageQueue);
                mq.DefaultPropertiesToSend.Recoverable = true;
                mq.DefaultPropertiesToSend.Label = errorMsg;
                mq.Formatter = new System.Messaging.XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                //mq.DefaultPropertiesToSend.Label = transactionMessage.Network + ":" + transactionMessage.OperatorID + ": " + transactionMessage.Msisdn;
                mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                mq.Send(transactionMessage);
            }
            catch (Exception ex)
            {

                logger.Error(ex.Message.ToNewLine());
            }
        }

        private static void WaitforDeparture(TransportProtocols transportprotocol)
        {
            try
            {

                //Create an instance of Me ssageQueue. Set its formatter.
                MessageQueue pQueue = new MessageQueue(".\\Private$\\" + transportprotocol.NetworkID + "_" + transportprotocol.OperatorID + "_OUT");
                pQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                pQueue.PeekCompleted += new PeekCompletedEventHandler(PeekDepartureMessageCompleted);
                logger.Info("Waiting for message assync... @ " + ".\\Private$\\" + transportprotocol.NetworkID + "_" + transportprotocol.OperatorID + "_OUT".ToNewLine());
                // Begin the asynchronous peek operation.
                pQueue.BeginPeek();
            }
            catch (Exception ex)
            {

                logger.Error(ex.Message.ToNewLine());
            }
             
        }
        private static void PeekDepartureMessageCompleted(Object source, PeekCompletedEventArgs asyncResult)
        {

            // Connect to the queue.
            MessageQueue mq = (MessageQueue)source;

            try
            {
                // End the asynchronous peek operation.
                Message m = mq.EndPeek(asyncResult.AsyncResult);


                // Cast message to Season Business Entity
                TransactionMessage msg = (TransactionMessage)m.Body;

                
                
                bool retn = false;
                 retn = SendToSubscriber(msg);
                //  Send Message back to subscriber

                if (retn == true)
                {
                    mq.ReceiveById(m.Id);
                }
                else
                {
                    // Log to Event Sink
                    Thread.Sleep(3000);
                }


            }
            catch (Exception exception)
            {
                logger.Error(exception.Message + "|" + exception.StackTrace.ToNewLine());
            }
            // Restart the asynchronous peek operation.

            mq.BeginPeek();
            ////mq.BeginReceive();
            return;
        }

        private static bool SendToSubscriber(TransactionMessage transactionMessage)
        {
            bool rtn = false;
            if (SmppBinds.Count != 0)
            {
                //string mykey = transactionMessage.OperatorID;

                try
                {
                    SmppMessenger sm = (SmppMessenger)SmppBinds[transactionMessage.OperatorID.ToUpper()];
                    if (sm.IsConnected == SmppMessenger.BindConnectionState.TranceiverConnected || sm.IsConnected == SmppMessenger.BindConnectionState.BothTRConnected)
                    {

                        rtn = sm.SendMessage(transactionMessage);

                    }
                }
                catch (Exception exp )
                {

                    logger.Error(transactionMessage.NetworkID + ":" + transactionMessage.OperatorID + ": [Operator : " + transactionMessage.OperatorID + " is offline: " + exp.Message.ToNewLine());
                }

                

            }
            else
            {
                Thread.Sleep(1000);
            }
                return rtn;


        }





        private static void InitializeMessageQueue(TransportProtocols tp)
        {
            try
            {
                DefaultPropertiesToSend defaultPropertiesToSend;
                defaultPropertiesToSend = new DefaultPropertiesToSend();
                defaultPropertiesToSend.AttachSenderId = true;
                defaultPropertiesToSend.Recoverable = true;
                
                MessageQueue mq;
                if (!MessageQueue.Exists(".\\Private$\\" + tp.NetworkID + "_" + tp.OperatorID + "_IN"))
                {
                    mq = MessageQueue.Create(".\\Private$\\" + tp.NetworkID + "_" + tp.OperatorID + "_IN");
                    mq.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
                   mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;

                }
                else
                {
                    mq = new MessageQueue(".\\Private$\\" + tp.NetworkID + "_" + tp.OperatorID + "_IN");
                    mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                    mq.DefaultPropertiesToSend = defaultPropertiesToSend;
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }
                // Out going queue

                if (!MessageQueue.Exists(".\\Private$\\" + tp.NetworkID + "_" + tp.OperatorID + "_OUT"))
                {
                    mq = MessageQueue.Create(".\\Private$\\" + tp.NetworkID + "_" + tp.OperatorID + "_OUT");
                    mq.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
                     mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }
                else
                {
                    mq = new MessageQueue(".\\Private$\\" + tp.NetworkID + "_" + tp.OperatorID + "_IN");
                    mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                    mq.DefaultPropertiesToSend = defaultPropertiesToSend;
                     mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }

                // MessageDeparture

                if (!MessageQueue.Exists(GeneralMessageDeparture))
                {
                    mq = MessageQueue.Create(GeneralMessageDeparture);
                    mq.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }
                else
                {
                    mq = new MessageQueue(GeneralMessageDeparture);
                    mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                    mq.DefaultPropertiesToSend = defaultPropertiesToSend;
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }





                // MessageDeparture

                if (!MessageQueue.Exists(InternalContentQueue))
                {
                    mq = MessageQueue.Create(InternalContentQueue);
                    mq.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }
                else
                {
                    mq = new MessageQueue(InternalContentQueue);
                    mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                    mq.DefaultPropertiesToSend = defaultPropertiesToSend;
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }

                // MessageDeparture

                if (!MessageQueue.Exists(ExternalContentQueue))
                {
                    mq = MessageQueue.Create(ExternalContentQueue);
                    mq.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }
                else
                {
                    mq = new MessageQueue(ExternalContentQueue);
                    mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                    mq.DefaultPropertiesToSend = defaultPropertiesToSend;
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }


                // MessageDeparture

                if (!MessageQueue.Exists(DatabaseLogger))
                {
                    mq = MessageQueue.Create(DatabaseLogger);
                    mq.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }
                else
                {
                    mq = new MessageQueue(DatabaseLogger);
                    mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                    mq.DefaultPropertiesToSend = defaultPropertiesToSend;
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }


                // Error Message

                if (!MessageQueue.Exists(ErrorMessageQueue))
                {
                    mq = MessageQueue.Create(ErrorMessageQueue);
                    mq.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }
                else
                {
                    mq = new MessageQueue(ErrorMessageQueue);
                    mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                    mq.DefaultPropertiesToSend = defaultPropertiesToSend;
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }

                // Delivery Response 

                if (!MessageQueue.Exists(DeliveryResponseMessageQueue))
                {
                    mq = MessageQueue.Create(DeliveryResponseMessageQueue);
                    mq.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }
                else
                {
                    mq = new MessageQueue(DeliveryResponseMessageQueue);
                    mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                    mq.DefaultPropertiesToSend = defaultPropertiesToSend;
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }
            }
            catch (Exception exception)
            {
                logger.Fatal(exception.Message + "|" + exception.StackTrace.ToNewLine());
            }
  
        }


        private static void InitializeMessageQueue(CEmailConfig co)
        {
            try
            {
                DefaultPropertiesToSend defaultPropertiesToSend;
                defaultPropertiesToSend = new DefaultPropertiesToSend();
                defaultPropertiesToSend.AttachSenderId = true;
                defaultPropertiesToSend.Recoverable = true;

                MessageQueue mq;
                if (!MessageQueue.Exists(".\\Private$\\" + co.OutgoingQueue))
                {
                    mq = MessageQueue.Create(".\\Private$\\" + co.OutgoingQueue);
                    mq.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;

                }
                else
                {
                    mq = new MessageQueue(".\\Private$\\" + co.OutgoingQueue);
                    mq.Formatter = new XmlMessageFormatter(new Type[] { typeof(Vas.EmailAlertMessage.CEmailAlertMessage) });
                   
                    
                    mq.DefaultPropertiesToSend = defaultPropertiesToSend;
                    mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                }
                // Out going queue
  
               
            }
            catch (Exception exception)
            {
                logger.Fatal(exception.Message + "|" + exception.StackTrace.ToNewLine());
            }

        }




        // Departure message handler


    static void SmppGeneralDepartureHandler(object data)
          {
            WaitForOutGoingMessage();

            

            Mutex mutex = null;

            while ((m_threads > 0) || (!m_isApplicationTerminating))
            {
                if (!m_isApplicationTerminating)
                {
                    //mutex = new Mutex(false, Environment.CurrentDirectory, out isFirstInstance);
                    string mutexname = String.Format(@"{0}\{1}.exe", Environment.CurrentDirectory, Assembly.GetExecutingAssembly().GetName().Name);
                    mutex = new Mutex(false, mutexname.Replace(@"\", String.Empty), out isFirstInstance);
                    Thread.Sleep(1000);
                }
                if (isFirstInstance)
                {
                    m_isApplicationTerminating = true;
                    Thread.Sleep(1000);
                }
                else
                {
                    if (mutex != null)
                    {
                        mutex.Close();
                    }
                    Thread.Sleep(ThraedSleepTime);
                }
            }



        }

        private static void WaitForOutGoingMessage()
         
        {   //Create an instance of Me ssageQueue. Set its formatter.
            try
            {
                MessageQueue pQueue = new MessageQueue(GeneralMessageDeparture);


                pQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                pQueue.PeekCompleted += new PeekCompletedEventHandler(PeekGeneralDepartureMessageCompleted);
                logger.Info("Waiting for message assync... " + GeneralMessageDeparture.ToNewLine());
                // Begin the asynchronous peek operation.
                pQueue.BeginPeek();
            }
            catch (Exception ex)
            {

                logger.Error(ex.Message.ToNewLine());
            }

        }
        private static void PeekGeneralDepartureMessageCompleted(Object source, PeekCompletedEventArgs asyncResult)
        {

            // Connect to the queue.
            MessageQueue mq = (MessageQueue)source;

            try
            {
                // End the asynchronous peek operation.
                Message m = mq.EndPeek(asyncResult.AsyncResult);


                // Cast message to Season Business Entity
                TransactionMessage msg = (TransactionMessage)m.Body;

                bool retn = false;

                msg.LastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                msg.ModifiedState = ModificationState.ResponseOut;
                msg.ModifiedStatus++;

                SendToDbLogger(msg);
                sendToFinalDeparture(msg);
                             
                mq.ReceiveById(m.Id);              
            
            }
            catch (Exception exception)
            {
                logger.Error(exception.Message + "|" + exception.StackTrace.ToNewLine());
                Thread.Sleep(3000);
            }
            // Restart the asynchronous peek operation.

            mq.BeginPeek();
            ////mq.BeginReceive();
            return;
        }
        // Sends message to final departure queue
        private static void sendToFinalDeparture(TransactionMessage transactionMessage)
        {
             try
            {
                MessageQueue mq = new MessageQueue(".\\Private$\\" + transactionMessage.NetworkID + "_" + transactionMessage.OperatorID + "_OUT");
                mq.DefaultPropertiesToSend.Recoverable = true;
                mq.Formatter = new System.Messaging.XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                //mq.DefaultPropertiesToSend.Label = transactionMessage.Network + ":" + transactionMessage.OperatorID + ": " + transactionMessage.Msisdn;
                mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                mq.Send(transactionMessage);
            }
            catch (Exception ex)
            {

                logger.Error(ex.Message.ToNewLine());

            }
            return;
        }

        //Sends message to Queue for logging into Transaction log
        private static void SendToDbLogger(TransactionMessage transactionMessage)
        {
             try
            {
                MessageQueue mq = new MessageQueue(DatabaseLogger);
                mq.DefaultPropertiesToSend.Recoverable = true;
                mq.Formatter = new System.Messaging.XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
                mq.DefaultPropertiesToSend.Label = transactionMessage.NetworkID + ":" + transactionMessage.OperatorID + ": " + transactionMessage.Msisdn;
                mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
                mq.Send(transactionMessage);
            }
            catch (Exception ex)
            {

                logger.Error(ex.Message.ToNewLine());

            }
             return;
        }

 



        /// <summary>
        /// Communication Handler
        /// </summary>
        /// <param name="data"></param>
        static void SmppMessageHandler(object data)
        {
             TransportProtocols transportprotocol = (TransportProtocols)data;

             SmppMessenger smpp = new SmppMessenger(transportprotocol, DeliveryResponseMessageQueue, ShortCodePrefixToTrim);

             smpp.Connect();
             SmppBinds.Add(transportprotocol.OperatorID.ToUpper(), smpp);

             logger.Info("Bind Count as at Stated Period : " + SmppBinds.Count.ToString().ToNewLine());

             Mutex mutex = null;

             while ((m_threads > 0) || (!m_isApplicationTerminating))
             {
                 if (!m_isApplicationTerminating)
                 {
                     //mutex = new Mutex(false, Environment.CurrentDirectory, out isFirstInstance);
                     string mutexname = String.Format(@"{0}\{1}.exe", Environment.CurrentDirectory, Assembly.GetExecutingAssembly().GetName().Name);
                     mutex = new Mutex(false, mutexname.Replace(@"\", String.Empty), out isFirstInstance);
                     Thread.Sleep(1000);
                 }
                 if (isFirstInstance)
                 {
                     m_isApplicationTerminating = true;
                     Thread.Sleep(1000);
                 }
                 else
                 {
                     if (mutex != null)
                     {
                         mutex.Close();
                     }
                     
                 }

                 Thread.Sleep(ThraedSleepTime);

                 try
                 {
                     switch (smpp.IsConnected)
                     {

                         //smpp = new SmppMessenger(transportprotocol);
                         case SmppMessenger.BindConnectionState.TranceiverConnected:
                             //--        logger.Info(transportprotocol.NetworkID + ":" + transportprotocol.OperatorID + "->Tranceiver ConnectionStatus: Connected\n");
                             //SendMessageToServer(smpp, transportprotocol);
                             break;

                         case SmppMessenger.BindConnectionState.TranceiverDisconnected:
                             logger.Error(transportprotocol.NetworkID + ":" + transportprotocol.OperatorID + "->Tranceiver ConnectionStatus: Disconnected".ToNewLine());
                             smpp.Connect();
                             break;

                         case SmppMessenger.BindConnectionState.BothTRConnected:
                             //--     logger.Info(transportprotocol.NetworkID + ":" + transportprotocol.OperatorID + "->Both Transmitter and Receiver ConnectionStatus: Connected\n");
                             //SendMessageToServer(smpp, transportprotocol);
                             break;

                         case SmppMessenger.BindConnectionState.BothTRDisconnected:
                             logger.Error(transportprotocol.NetworkID + ":" + transportprotocol.OperatorID + "->Both Transmitter and Receiver ConnectionStatus: Disconnected".ToNewLine());
                             smpp.Connect();
                             break;

                         case SmppMessenger.BindConnectionState.ReceiverDisconnected:
                             logger.Error(transportprotocol.NetworkID + ":" + transportprotocol.OperatorID + "->Receiver ConnectionStatus: Disconnected".ToNewLine());
                             smpp.Connect();
                             //SendMessageToServer(smpp, transportprotocol);
                             break;
                         case SmppMessenger.BindConnectionState.TransmitterDisconnected:
                             logger.Error(transportprotocol.NetworkID + ":" + transportprotocol.OperatorID + "->TransmitterConnectionStatus: Disconnected".ToNewLine());

                             smpp.Connect();
                             break;
                     }
                 }
                 catch (Exception errGen)
                 {

                     logger.Error(transportprotocol.NetworkID + ":" + transportprotocol.OperatorID + errGen.Message.ToNewLine());

                      
                 }


             }

 

        }

        private static void SendMessageToServer(SmppMessenger smpp, TransportProtocols transportprotocol)
        {
             

                //string DateIN = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss fff");
                //TransactionMessage transactionMessage = new TransactionMessage();

                //transactionMessage.ShortCode = "2008";
                //transactionMessage.Msisdn = "23480303939";
                //transactionMessage.RequestMessage = "";
                //transactionMessage.ArrivalProtocol = "SMPP";
                //transactionMessage.MessageType = 0; // SmsMessageType.SMSText;
                //transactionMessage.NetworkID = transportprotocol.NetworkID;
                //transactionMessage.OperatorID = transportprotocol.OperatorID;
                //transactionMessage.DateIn = DateIN;
                //transactionMessage.LastModified = DateIN;
                //transactionMessage.Direction = 1;
                //transactionMessage.ModifiedStatus++;
                //transactionMessage.ModifiedState = 0; // ModificationState.ResponseOut;
                //transactionMessage.ResponseMessage = "Hello: " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss fff");
                //smpp.SendMessage(transactionMessage);
             
        }
        /// <summary>
        /// Extracts value from a query key
        /// </summary>
        /// <param name="keyName">name of key</param>
        /// <param name="queryString">the entire querystring</param>
        /// <returns>returns value if key exists</returns>
        private static String getQueryStringValue(String keyName,
                                                              NameValueCollection queryString)
        {
            String keyValue = null;

            // Get each header and display each value
            foreach (String key in queryString.AllKeys)
            {
                if (keyName.ToLower().CompareTo(key.ToLower()) == 0)
                {
                    keyValue = queryString[key];
                }
            }

            return keyValue;
        }

        /// <summary>
        /// Manages arriving Messages, uses shortcode to identify them behind to content fetcher
        /// </summary>
        /// <param name="data"></param>
        static void SmppArrivalHandler(object data)
        {
            TransportProtocols transportprotocol = (TransportProtocols)data;

            WaitforArrival(transportprotocol);





            Mutex mutex = null;

            while ((m_threads > 0) || (!m_isApplicationTerminating))
            {
                if (!m_isApplicationTerminating)
                {
                    //mutex = new Mutex(false, Environment.CurrentDirectory, out isFirstInstance);
                    string mutexname = String.Format(@"{0}\{1}.exe", Environment.CurrentDirectory, Assembly.GetExecutingAssembly().GetName().Name);
                    mutex = new Mutex(false, mutexname.Replace(@"\", String.Empty), out isFirstInstance);
                    Thread.Sleep(1000);
                }
                if (isFirstInstance)
                {
                    m_isApplicationTerminating = true;
                    Thread.Sleep(1000);
                }
                else
                {
                    if (mutex != null)
                    {
                        mutex.Close();
                    }

                }

                Thread.Sleep(ThraedSleepTime);
               


            }

        }


        /// <summary>
        /// Departing Messages: Threads are expected to lookinto activeBinds collection for the appropriate bind to use in sending out
        /// </summary>
        /// <param name="data"></param>
        static void SmppDepartureHandler(object data)
        {
            TransportProtocols transportprotocol = (TransportProtocols)data;
            WaitforDeparture(transportprotocol);
            Mutex mutex = null;

            while ((m_threads > 0) || (!m_isApplicationTerminating))
            {
                if (!m_isApplicationTerminating)
                {
                    //mutex = new Mutex(false, Environment.CurrentDirectory, out isFirstInstance);
                    string mutexname = String.Format(@"{0}\{1}.exe", Environment.CurrentDirectory, Assembly.GetExecutingAssembly().GetName().Name);
                    mutex = new Mutex(false, mutexname.Replace(@"\", String.Empty), out isFirstInstance);
                    Thread.Sleep(1000);
                }
                if (isFirstInstance)
                {
                    m_isApplicationTerminating = true;
                    Thread.Sleep(1000);
                }
                else
                {
                    if (mutex != null)
                    {
                        mutex.Close();
                    }

                }

                Thread.Sleep(ThraedSleepTime);



            }


        }


    }
}
