using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Data.OleDb;
using System.Collections.Specialized;
using System.Collections;
using Vas.Transaction.Protocols;
using log4net;
using log4net.Config;
using System.Text.RegularExpressions;
using System.Net.Mail;

namespace SMSGATE.src.DAL
{
   public class DataAccessLayer
   {
        private string m_Server;
        private string m_UserID;
        private string m_Password;
        private string m_Database;
        private string m_ConnectionString;
        private bool m_IsConnected;
        private SqlConnection m_Cnn;

        /// <summary>
        /// Constructor
        /// </summary>
        public DataAccessLayer()
        {

        }
        /// <summary>
        /// Parameterized Costructor 
        /// </summary>
        /// <param name="Server">Server Name or IP Address of DB Server</param>
        /// <param name="UserID">The SQL Server User Name</param>
        /// <param name="Password">Password</param>
        /// <param name="Database">The Initial Catalog or Database Name</param>
        public DataAccessLayer(string Server, string UserID, string Password, string Database)
        {
            m_Server = Server;
            m_UserID = UserID;
            m_Password = Password;
            m_Database = Database;
            m_ConnectionString = "Server=" + Server + ";User Id=" + UserID + ";Password=" + Password + ";Database=" + Database;
        }
        /// <summary>
        /// Constructor that takes connection String
        /// </summary>
        /// <param name="ConnectionString">Connection String</param>
        public DataAccessLayer(string ConnectionString)
        {

            m_ConnectionString = ConnectionString;
        }
        /// <summary>
        /// The Server Name or IP address
        /// </summary>
        public string Server
        {
            get { return m_Server; }

            set { m_Server = value; }
        }
        /// <summary>
        /// Returns an instance of SqlConnection
        /// </summary>
        public SqlConnection Cnn
        {
            get { return m_Cnn; }

            //set { m_Cnn = value; }
        }

        /// <summary>
        /// The username of the Database
        /// </summary>
        public string UserID
        {
            get { return m_UserID; }
            set { m_UserID = value; }
        }

        public bool IsConnected
        {
            get {  return GetConnectionStatus(); }
            //set { m_IsConnected = value; }
        }

        private bool GetConnectionStatus()
        {
            bool connectionStatus = false;
            
            if (this.m_Cnn.State == ConnectionState.Open)
            {
                connectionStatus = true;
            }
            m_IsConnected = connectionStatus;
            return m_IsConnected;
        }
        /// <summary>
        /// The password to the database
        /// </summary>
        public string Password
        {
            get { return m_Password; }
            set { m_Password = value; }
        }
        /// <summary>
        /// The databasename
        /// </summary>
        public string Database
        {
            get { return m_Database; }
            set { m_Database = value; }
        }
        /// <summary>
        /// Returns 
        /// </summary>
        /// <returns></returns>
        public SqlConnection Connect()
        {

            SqlConnection cnn = null;
            try
            {
                cnn = new SqlConnection(m_ConnectionString);
                cnn.Open();
                m_Server = cnn.DataSource;
                m_Database = cnn.Database;
                 


            }
            catch (SqlException ex)
            {
                string exx = ex.Message;

            }
            m_Cnn = cnn;
            return cnn;
        }

        public  void DisConnect()
        {

             
            try
            {

                this.m_Cnn.Dispose();
                this.m_Cnn.Close();


            }
            catch (SqlException ex)
            {
                string exx = ex.Message;

            }
            
        }
        /// <summary>
        /// Returns a list of protocol objects corresponding to the list of active protocols
        /// </summary>
        /// <returns></returns>
        public ArrayList GetProtocolList()
        {
            ArrayList ar = new ArrayList();
            try
            {
                
      
   SqlCommand cmd = new SqlCommand("Select [NetworkID] ,[OperatorID],[TransportMode] ,[RemoteHost] ,[RemotePort],[SystemID],[Password],[ProtocolVersion],[EnquireLinkEnabled] ,[EnquireLinkInterval],[SourceAddress] ,[AddressPrefix],[AddressRange] ,[Timeout] ,[AddrTON],[AddrNPI],[DestTON],[DestNPI],[SourceTONNumeric],[SourceToNAlpha],[SystemType] FROM [Protocols] where [ActiveStatus] = 1", this.Cnn);
     
      
                TransportProtocols tp = null;
                SqlDataReader dr = cmd.ExecuteReader();
                if (dr.HasRows == true)
                {
                    while (dr.Read() == true)
                    {
                      tp = new TransportProtocols();
                      tp.NetworkID = dr.GetString(0);
                      tp.OperatorID = dr.GetString(1);
                      tp.TransportMode = dr.GetString(2);
                      tp.RemoteHost = dr.GetString(3);
                      tp.RemotePort = int.Parse(dr.GetValue(4).ToString());
                      tp.SystemID = dr.GetString(5);
                      tp.Password = dr.GetString(6);
                      tp.ProtocolVersion = double.Parse(dr.GetValue(7).ToString());                        
                      tp.EnquireLinkEnabled =  (byte ) int.Parse(dr.GetValue(8).ToString())  ;
                      tp.EnquireLinkInterval =  int.Parse(dr.GetValue(9).ToString());
                      tp.SourceAddress = dr.GetString(10);
                      tp.AddressPrefix = dr.GetString(11);  
                      tp.AddressRange = dr.GetString(12);  
                      tp.Timeout =int.Parse(dr.GetValue(13).ToString());
                      tp.AddrTON =  byte.Parse(dr.GetValue(14).ToString());
                      tp.AddrNPI = byte.Parse(dr.GetValue(15).ToString());
                      tp.DestTON = byte.Parse(dr.GetValue(16).ToString());
                      tp.DestNPI = byte.Parse(dr.GetValue(17).ToString());
                      tp.SourceTONNumeric = byte.Parse(dr.GetValue(18).ToString());
                      tp.SourceTonAlpha = byte.Parse(dr.GetValue(19).ToString());
                      tp.SystemType = dr.GetString(20);
                      ar.Add(tp);


                    }
                }
                dr.Dispose();
                cmd.Dispose();
            }
            catch (Exception ex)
            {
                string exx = ex.Message;
            }
             
            return ar;

        }
        /// <summary>
        /// Returns a list of specific Protocols passed as argument
        /// </summary>
        /// <param name="Protocol"></param>
        /// <returns></returns>
        public ArrayList GetProtocolList(string Protocol)
        {
            ArrayList ar = new ArrayList();
            try
            {
  
                // 
                SqlCommand cmd = new SqlCommand("Select [NetworkID] ,[OperatorID],[TransportMode] ,[RemoteHost] ,[RemotePort],[SystemID],[Password],[ProtocolVersion],[EnquireLinkEnabled] ,[EnquireLinkInterval],[SourceAddress] ,[AddressPrefix],[AddressRange] ,[Timeout] ,[AddrTON],[AddrNPI],[DestTON],[DestNPI],[SourceTONNumeric],[SourceToNAlpha] FROM [Protocols] where [ActiveStatus] = 1 and [TransportMode] = '" + Protocol.ToUpper() + "'", this.Cnn);

                TransportProtocols tp = null;
                SqlDataReader dr = cmd.ExecuteReader();
                if (dr.HasRows == true)
                {
                    while (dr.Read() == true)
                    {
                        tp = new TransportProtocols();
                        tp.NetworkID = dr.GetString(0);
                        tp.OperatorID = dr.GetString(1);
                        tp.TransportMode = dr.GetString(2);
                        tp.RemoteHost = dr.GetString(3);
                        tp.RemotePort = int.Parse(dr.GetValue(4).ToString());
                        tp.SystemID = dr.GetString(5);
                        tp.Password = dr.GetString(6);
                        tp.ProtocolVersion = double.Parse(dr.GetValue(7).ToString());
                        tp.EnquireLinkEnabled = byte.Parse(dr.GetValue(8).ToString());
                        tp.EnquireLinkInterval = int.Parse(dr.GetValue(9).ToString());
                        tp.SourceAddress = dr.GetString(10);
                        tp.AddressPrefix = dr.GetString(11); ;
                        tp.AddressRange = dr.GetString(12); ;
                        tp.Timeout = int.Parse(dr.GetValue(13).ToString());
                        tp.AddrTON = byte.Parse(dr.GetValue(14).ToString());
                        tp.AddrNPI = byte.Parse(dr.GetValue(15).ToString());
                        tp.DestTON = byte.Parse(dr.GetValue(16).ToString());
                        tp.DestNPI = byte.Parse(dr.GetValue(17).ToString());
                        tp.SourceTONNumeric = byte.Parse(dr.GetValue(18).ToString());
                        tp.SourceTonAlpha = byte.Parse(dr.GetValue(19).ToString());

                        ar.Add(tp);

                    }
                }
                dr.Dispose();
                cmd.Dispose();
            }
            catch (Exception ex)
            {
                string exx = ex.Message;
            }
             
            return ar;
        }

        public bool InsertNewShortcode(string sCode,string Netwk,string Oprtor, ILog logger)
        {
            bool rtn = false;
            try
            {
                string qry = "INSERT INTO  [ShortCodes] ([ShortCode],[Description],[Owner],[ActiveStatus]) VALUES ('" + sCode + "',' New FROM " + Netwk + "_" + Oprtor + "','INTERNAL',1)";

                SqlCommand cmd = new SqlCommand(qry, this.Cnn);

                cmd.ExecuteNonQuery();
                cmd.Dispose();
                rtn = true;
                logger.Info(Netwk + ":" + Oprtor + ": New ShortCode inserted [" + sCode + "]\n");

            }
            catch (Exception ex)
            {
                logger.Error(Netwk + ":" + Oprtor + ": New ShortCode Insert Error [" + sCode + "] [" + ex.Message  + "]\n");
      
            }


            return rtn;


        }
        public Hashtable GetShortCode(string InternalContentQueue, string ExternalContentQueue)
        {
            Hashtable hs = new Hashtable();

             
            try
            {

                // 
                SqlCommand cmd = new SqlCommand("Select [ShortCode],[Owner] from [ShortCodes] where [ActiveStatus] = 1", this.Cnn);

           
                SqlDataReader dr = cmd.ExecuteReader();
                if (dr.HasRows == true)
                {
                    while (dr.Read() == true)
                    {
                      string owner = dr.GetString(1);

                      switch (owner.ToLower())
                      {
                          case "internal":
                              if (hs.Contains(dr.GetString(0)) == false )
                              {
                              hs.Add(dr.GetString(0), InternalContentQueue);
                              }
                              
                              break;

                          case "external":
                              if (hs.Contains(dr.GetString(0)) == false)
                              {
                                  hs.Add(dr.GetString(0), ExternalContentQueue);
                              }
                              break;

                          default :
                              if (hs.Contains(dr.GetString(0)) == false)
                              {
                                  hs.Add(dr.GetString(0), InternalContentQueue);
                              }
                              break;

                      }

                    }
                }
                dr.Dispose();
                cmd.Dispose();
            }
            catch (Exception ex)
            {
                string exx = ex.Message;
            }

            return hs;

        }

        //internal Hashtable GetShortCode(string InternalContentQueue, string ExternalContentQueue)
        //{
        //    throw new NotImplementedException();
        //}

        public CEmailConfig GetEmailConfiguration()
        {

            CEmailConfig conf = new CEmailConfig();
            SqlDataReader dr = null;
            SqlCommand cmd = null;
            try
            {

                cmd = new SqlCommand("SELECT [OutgoingMailsQueue] ,[SmtpServer] ,[SmtpPort],[SenderEmail],[SenderPassword],[DisplayName] ,[FromEmail],[SupportSSL], [MailPriority], [IsHtml] from [EmailConfig]", this.Cnn);


                dr = cmd.ExecuteReader();
                if (dr.HasRows == true)
                {
                    while (dr.Read() == true)
                    {

                        conf.OutgoingQueue = dr.GetString(0);
                        conf.SmtpServer = dr.GetString(1);
                        conf.SmtpPort = int.Parse(dr.GetValue(2).ToString());
                        ArrayList ar = new ArrayList();
                        ar = GetEmails(dr.GetString(3));
                        if (ar.Count > 0)
                        {
                            conf.SenderEmail = ar[0].ToString();
                        }
                        conf.SenderPassword = dr.GetString(4);
                        conf.DisplayName = dr.GetString(5);
                        ar = new ArrayList();
                        ar = GetEmails(dr.GetString(6));
                        if (ar.Count > 0)
                        {
                            conf.FromEmail = ar[0].ToString();
                        }

                        conf.SupportSSL = int.Parse(dr.GetValue(7).ToString());

                        conf.ReplyTo = dr.GetString(3);

                        switch (dr.GetString(8).ToUpper())
                        {
                            case "HIGH":
                                conf.MailPriority = MailPriority.High;
                                break;
                            case "LOW":
                                conf.MailPriority = MailPriority.Low;
                                break;
                            default:
                                conf.MailPriority = MailPriority.Normal;
                                break;
                        }

                        //conf.MailPriority = (System.Net.Mail.MailPriority)dr.GetValue(9);

                        conf.IsHtml = Convert.ToBoolean(dr.GetValue(9));
                    }
                }

            }
            catch (Exception ex)
            {

                string exx = ex.Message;
            }
            finally
            {
                dr.Dispose();
                cmd.Dispose();

            }
            
            return conf;  
        }

       /// <summary>
       ///  This is regex parttern to validate emails
       /// </summary>
       /// <param name="bodyin"></param>
       /// <returns></returns>
        public  ArrayList GetEmails(string bodyin)
        {

            ArrayList ar = new ArrayList();
            try
            {


                string patternStrict = @"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*";

                //Regex rsg = new Regex(patternStrict, RegexOptions.Multiline);

                MatchCollection mc = Regex.Matches(bodyin, patternStrict);


                for (int i = 0; i < mc.Count; i++)
                {
                    ar.Add(mc[i].ToString());

                }


            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);

            }

            return ar;

        }

        public ArrayList GetEmailList(string emailField)
        {
             ArrayList ar = new ArrayList();
             SqlCommand cmd = null;
             SqlDataReader dr = null;
             try
             {

                 cmd = new SqlCommand("SELECT [EmailAddress] ,[SubscriptionStatus] from [EmailAddresses] where [SubscriptionStatus] =1 and [EmailField]='" + emailField.ToLower() + "'", this.Cnn);


                 dr = cmd.ExecuteReader();
                 if (dr.HasRows == true)
                 {
                     while (dr.Read() == true)
                     {
                         string email = dr.GetString(0);
                         //Validate by Regex
                         ArrayList email1 = GetEmails(email);

                         if (email1.Count > 0)
                         {
                             foreach (string em in email1)
                             {
                                 ar.Add(em);
                             }
                         }
                     }
                 }
                
             }
             catch (Exception ex)
             {


             }
             finally
             {
            dr.Dispose();
                 cmd.Dispose();

             }
             return ar;           
        }
   }
}
