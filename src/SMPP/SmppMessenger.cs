using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Devshock.Net;
using System.Net;
using System.IO;
using Devshock.Protocol.Smpp;
using Devshock.Protocol.SmppPdu;
using Devshock.Common;
using Vas.Transaction.Protocols;
using Vas.Transaction.Messaging;
using System.Messaging;
using System.Timers;
using System.Globalization;
using System.Reflection;
using log4net;
using log4net.Config;
using SMSGATE.Properties;
using WAPPushSMPP;
 


//




//
 
namespace SMSGATE.src.SMPP
{
   public class SmppMessenger
    {
       private static   ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
      // Vas.EmailAlertMessage.CEmailAlertMessage cem = new Vas.EmailAlertMessage.CEmailAlertMessage();
       
       
       private  string m_DeliveryResponseMessageQueue = ""; //".\\Private$\\" + Settings.Default.DeliveryResponseMessageQueue;
       private string ShortCodePrefixToTrim = "";
       private TransportProtocols tProtocol;
       private SmppConnection smppconn;
       private SmppConnection smppconn2; // for smpp 3.3
       SmppBindRes ResBind;
       SmppBindRes ResBind2; // for smpp 3.3
       
        
       private System.Timers.Timer enquireTimer;

      /// <summary>
      ///   TranceiverConnected,
      ///BothTRConnected,
      ///TranceiverDisconnected,
      ///TransmitterDisconnected,          
       ///ReceiverDisconnected,
       ///BothTRDisconnected
       /// </summary>
       public enum BindConnectionState
       {
        TranceiverConnected,
        BothTRConnected,
        TranceiverDisconnected,
        TransmitterDisconnected,
        ReceiverDisconnected,
        BothTRDisconnected
       }
        //private   SmppConnection mySmppClient = new SmppConnection();

       /// <summary>
       /// ------------>
       /// </summary>
       /// <param name="transportprotocol"></param>
       public SmppMessenger(TransportProtocols transportprotocol, string DeliveryResponseMessageQueue, string shortCodePrefixToTrim)
       {
           
           m_DeliveryResponseMessageQueue = DeliveryResponseMessageQueue;
           ShortCodePrefixToTrim = shortCodePrefixToTrim; // 234 prefix to trim from shortcodes
           // log4net here
           string appId = String.Format(@"{0}\{1}.exe", Environment.CurrentDirectory,
                              Assembly.GetExecutingAssembly().GetName().FullName);

           XmlConfigurator.Configure(new System.IO.FileInfo(Settings.Default.InstrumentationFileName));
           log4net.LogicalThreadContext.Properties["identity"] = appId;

            //id:483D47F1 sub:001 dlvrd:001 submit date:0905151857 done date:0905151857 stat:DELIVRD err:000 Text:Dear Godwin Akpabio
           
           tProtocol= transportprotocol;

           // Treat the mode in which client will bind based on protocol version
           if (tProtocol.ProtocolVersion < 3.4)
           {
              
               // Transmitter
               smppconn = new SmppConnection();
               InitializeParameters(smppconn,SmppConnectionMode.Transmitter ,tProtocol);

               // Client disconnected

               smppconn.OnEnquireLinkReq += new SmppEnquireLinkHandler(smppconn_OnEnquireLinkReq);
               smppconn.OnUnBindReq += new SmppUnBindHandler(smppconn_OnUnBindReq);
         //No neet towait for message      //smppconn.OnDeliverSmReq += new SmppDeliverSmHandler(smppconn_OnDeliverSmReq);

               // Receiver


               smppconn2 = new SmppConnection();
               InitializeParameters( smppconn2,SmppConnectionMode.Receiver, tProtocol);
               
               // Client disconnected

               smppconn2.OnEnquireLinkReq += new SmppEnquireLinkHandler(smppconn_OnEnquireLinkReq);
               smppconn2.OnUnBindReq += new SmppUnBindHandler(smppconn_OnUnBindReq);
               smppconn2.OnDeliverSmReq += new SmppDeliverSmHandler(smppconn_OnDeliverSmReq);
            
           
           }
           else
           {
               // Tranceiver
           smppconn = new SmppConnection();
           InitializeParameters(tProtocol);

            // Client disconnected
          
           smppconn.OnEnquireLinkReq += new SmppEnquireLinkHandler(smppconn_OnEnquireLinkReq);
           smppconn.OnUnBindReq += new SmppUnBindHandler(smppconn_OnUnBindReq);
           smppconn.OnDeliverSmReq += new SmppDeliverSmHandler(smppconn_OnDeliverSmReq);

           }

          
           enquireTimer = new System.Timers.Timer(double.Parse(tProtocol.EnquireLinkInterval.ToString()));
          enquireTimer.Elapsed += new ElapsedEventHandler(timerElapsedEvent);
          enquireTimer.Enabled = false;

           
       }

       private void InitializeParameters(SmppConnection smppX, SmppConnectionMode smppConnectionMode, TransportProtocols tProtocol)
       {
           if (IsValidIP(tProtocol.RemoteHost) == true)
           {
               smppX.Settings.RemoteHost = tProtocol.RemoteHost;
           }
           else
           {
               try
               {
                   IPAddress[] address = Dns.GetHostAddresses(tProtocol.RemoteHost);

                   foreach (IPAddress theaddress in address)
                   {
                       smppX.Settings.RemoteHost = theaddress.ToString();
                       break;
                   }
               }
               catch (Exception ex)
               {

                 //  ex.Message;
               }

           }
            
           smppX.Settings.Timeout = tProtocol.Timeout;
           smppX.Settings.ConnectionMode = smppConnectionMode;           
           smppX.Settings.RemotePort = tProtocol.RemotePort;
           smppX.Settings.BindParams.AddressRange = tProtocol.AddressRange;
           //mySmppClient.Settings.BindParams.AddressNpi = Convert.ToByte(1);
           //mySmppClient.Settings.BindParams.AddressTon = Convert.ToByte(1); 
           
           smppX.Settings.BindParams.Password = tProtocol.Password;
           smppX.Settings.BindParams.SystemId = tProtocol.SystemID;
           smppX.Settings.BindParams.SystemType = tProtocol.SystemType;
           smppX.Settings.BindParams.InterfaceVersion = Convert.ToByte(52);
           logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ": Initialized Successfully\n");
       }

       

       void smppconn_OnDeliverSmReq(object sender, SmppDeliverSmEventArgs e)
       {

           
                                          
     
           string shortcode = "";
           try
           {

               //String RHost = smppconn.Settings.RemoteHost;
               //string Rport = (smppconn.Settings.RemotePort).ToString();
               string DateIN = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");


               // Strip 234 away from begining of short code
               shortcode = e.Pdu.Body.DestinationAddress;
               string shortcodeprefix = ShortCodePrefixToTrim;

               if (shortcodeprefix.Trim().Length != 0)
               {
                   if (shortcode.Trim().StartsWith(shortcodeprefix.Trim()))
                   {
                       shortcode = shortcode.Substring(shortcodeprefix.Trim().Length, shortcode.Length - shortcodeprefix.Trim().Length);
                   }
               }

               TransactionMessage transactionMessage = new TransactionMessage();
               //transactionMessage.ShortCode =   e.Pdu.Body.DestinationAddress;
               transactionMessage.ShortCode = shortcode.Trim();
               transactionMessage.Msisdn = e.Pdu.Body.SourceAddress;
               transactionMessage.RequestMessage = e.Pdu.Body.ShortMessage.ToString();
               transactionMessage.ArrivalProtocol = "SMPP";
               transactionMessage.MessageType = SmsMessageType.SMSText;
               transactionMessage.NetworkID = tProtocol.NetworkID;
               transactionMessage.OperatorID = tProtocol.OperatorID;
               transactionMessage.DateIn = DateIN;
               transactionMessage.LastModified = DateIN;
               transactionMessage.Direction = 0;
               transactionMessage.ModifiedStatus = 0;
               transactionMessage.ModifiedState = ModificationState.Arrival;
               
               //Use ESMCLASS to Determine if message is MO,Delivery Response, Error Message
               // ESMCLASS=0 --> Mobile Originated
               // ESMCLASS=4 --> Delivery Response
               //ESMCLASS=4 ---> Error Message
               switch (e.Pdu.Body.EsmClass.Value.ToString())
               {
                   case "0":  //MO
                       
                                   
                       SendMessageToArrivalQueue(transactionMessage);

 logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + " :New MO Message: Destination [" + shortcode + "] Source [" + e.Pdu.Body.SourceAddress
             + "] Sequence Number [" + e.Pdu.Header.SequenceNumber + "] EsmClass [" + e.Pdu.Body.EsmClass.Value.ToString() + "] Body [" + e.Pdu.Body.ShortMessage.ToString() + "]\n");

// This is just an echo response
 //transactionMessage.ResponseMessage = "Message Received at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.sss") + ", thank you. Email: evans.okosodo@cyberspace.net.ng";
 //transactionMessage.Direction = 1;
 //transactionMessage.ModifiedStatus = 1;
 //transactionMessage.ModifiedState = ModificationState.ResponseOut;
 
 //SendMessageToDepartureQueue(transactionMessage);
                       // Clean Here later
             
             break;

                   case "4": // Delivery Response//Error Response
                       SendMessageToDeliveryResponseQueue(transactionMessage);

 logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + " :Delivery Message: Destination [" + shortcode + "] Source [" + e.Pdu.Body.SourceAddress
                                    + "] Sequence Number [" + e.Pdu.Header.SequenceNumber + "] EsmClass [" + e.Pdu.Body.EsmClass.Value.ToString() + "] Body [" + e.Pdu.Body.ShortMessage.ToString() + "]\n");


                       break;

                  
               default: // Any Other type which is rare

                       logger.Warn(tProtocol.NetworkID + ":" + tProtocol.OperatorID + " :Strange Message: Destination [" + shortcode + "] Source [" + e.Pdu.Body.SourceAddress
                                    + "] Sequence Number [" + e.Pdu.Header.SequenceNumber + "] EsmClass [" + e.Pdu.Body.EsmClass.Value.ToString() + "] Body [" + e.Pdu.Body.ShortMessage.ToString() + "]\n");

                   
                   
                   break;
              
               }
                

               

               // No copy to logger
           }
           catch (Exception ex)
           {
               logger.Error(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ":New Message Error:  " + ex.Message + "]\n");

           }


           // Send Delivery Response
           try
           {

               smppconn.DeliverSmRes(new SmppDeliverSmRes(e.Pdu.Header.SequenceNumber, e.Pdu.Body.SmDefaultMessageId.ToString()));

           }
           catch (Exception delex)
           {
               
              logger.Error(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ":New Message Error:  " + delex.Message + "]\n");

           }
           //id:483D47F1 sub:001 dlvrd:001 submit date:0905151857 done date:0905151857 stat:DELIVRD err:000 Text:Dear Godwin Akpabio
           return;
       }

       private void SendMessageToDepartureQueue(TransactionMessage transactionMessage)
       {
           try
           {
               MessageQueue mq = new MessageQueue(".\\Private$\\" + transactionMessage.NetworkID + "_" + transactionMessage.OperatorID + "_OUT");
               mq.DefaultPropertiesToSend.Recoverable = true;
               mq.Formatter = new System.Messaging.XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
               mq.DefaultPropertiesToSend.Label = transactionMessage.NetworkID + ":" + transactionMessage.OperatorID + ": " + transactionMessage.Msisdn;
               mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
               mq.Send(transactionMessage);
           }
           catch (Exception ex)
           {


               logger.Error(transactionMessage.NetworkID + ":" + transactionMessage.OperatorID + ":Send New Message to Queue Error:  " + ex.Message + "]\n");

           }
           return;
       }

       private void SendMessageToDeliveryResponseQueue(TransactionMessage transactionMessage)
       {
           try
           {
               MessageQueue mq = new MessageQueue(m_DeliveryResponseMessageQueue);
               mq.DefaultPropertiesToSend.Recoverable = true;
               mq.Formatter = new System.Messaging.XmlMessageFormatter(new Type[] { typeof(TransactionMessage) });
               mq.DefaultPropertiesToSend.Label = transactionMessage.NetworkID + ":" + transactionMessage.OperatorID + ": " + transactionMessage.Msisdn;
               mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
               mq.Send(transactionMessage);

               
           }
           catch (Exception ex)
           {


               logger.Error(transactionMessage.NetworkID + ":" + transactionMessage.OperatorID + ":Send New Message to Queue Error:  " + ex.Message + "]\n");

           }
           return;
       }

       private void SendMessageToArrivalQueue(TransactionMessage transactionMessage)
       {
           try
           {
               MessageQueue mq = new MessageQueue(".\\Private$\\" + transactionMessage.NetworkID + "_" + transactionMessage.OperatorID + "_IN");
               mq.DefaultPropertiesToSend.Recoverable = true;
               mq.Formatter = new System.Messaging.XmlMessageFormatter(new Type[] { typeof(TransactionMessage) }); 
               mq.DefaultPropertiesToSend.Label = transactionMessage.NetworkID + ":" + transactionMessage.OperatorID + ": " + transactionMessage.Msisdn;
               mq.MaximumQueueSize = MessageQueue.InfiniteQueueSize;
               mq.Send(transactionMessage);
           }
           catch (Exception ex)
           {


               logger.Error(transactionMessage.NetworkID + ":" + transactionMessage.OperatorID + ":Send New Message to Queue Error:  " + ex.Message + "]\n");

           }
           return;
       }

       void smppconn_OnEnquireLinkReq(object sender, SmppEnquireLinkEventArgs e)
       {
          logger.Info( "Enquire Link Received from " + tProtocol.NetworkID + ":" + tProtocol.OperatorID + ": Sequence Number " +  e.Pdu.Header.SequenceNumber.ToString() + ": connGuid " + e.ConnGuid.ToString() + "\n");
           
           return;
       }

       void smppconn_OnUnBindReq(object sender, SmppUnBindEventArgs e)
       {
           
           logger.Warn(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ": " + smppconn.Settings.ConnectionMode.Value + " Client disconnected : SequenceNumber [" + e.Pdu.Header.SequenceNumber.ToString() + "] ConnGuid [" + e.ConnGuid.ToString() + "] \n");

            
       }

       public BindConnectionState IsConnected
       {
           get { return GetConnectionStatus(); }
        //TranceiverConnected,
        //BothTRConnected,
        //TranceiverDisconnected,
        //TransmitterDisconnected,
        //ReceiverDisconnected,
        //BothTRDisconnected
            
       }

       private BindConnectionState GetConnectionStatus()
       {

           if (tProtocol.ProtocolVersion < 3.4)
           {
               //smppconn.Connected;

               if (smppconn.Connected && smppconn2.Connected)
               {
                   return BindConnectionState.BothTRConnected;

               }
               else
               {//->

                   if (!smppconn.Connected && !smppconn2.Connected)
                   {
                       return BindConnectionState.BothTRDisconnected;

                   }
                   else
                   {
                       if (smppconn.Connected == false)
                       {
                           return BindConnectionState.TransmitterDisconnected;

                       }
                       else
                       {
                           return BindConnectionState.ReceiverDisconnected;

                       }


                   }
               }//->
           }
           else
           {
               // Tranceiver
               if (smppconn.Connected == true)
               {
                   return BindConnectionState.TranceiverConnected;
               }
               else
               {
                   return BindConnectionState.TranceiverDisconnected;

               }
           }


       }


       

       /// <summary>
       /// ------------->
       /// </summary>
       public void Connect() 
       {

           try
           {
               if (tProtocol.ProtocolVersion < 3.4)
               {
                   
                   // Transmitter
                   if (!smppconn.Connected)
                   {
                      
                       try 
                       {
                           try
                           {
                               ResBind = smppconn.Bind();
                           }
                           catch (SmppTimeOutException errtime)
                           {
                               
                                 logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID +  ":" + errtime.Message + "\n");

                           }

                       }
                       catch (SmppGenericNackException egn2)
                       {
                           logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ":" + egn2.Message + "\n");


                       }

                       if (smppconn.Connected == true)
                       {
                           logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + " Transmitter Client Connected Successfully\n");

                       }
                   }

                   if (!smppconn2.Connected)
                   {
                       try
                       {
                           try
                           {
                               ResBind2 = smppconn2.Bind(); // Receiver
                           }
                           catch (SmppTimeOutException errtime)
                           {

                               logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ":" + errtime.Message + "\n");

                           }
                       }
                       catch (SmppGenericNackException egn1)
                       {
                           logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ":" + egn1.Message + "\n");


                       }
                       if (smppconn2.Connected == true)
                       {
                           logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + " Receiver Client Connected Successfully\n");
                       }
                   }
               }
               else
               {
                   if (!smppconn.Connected)
                   {
                       try
                       {
                           try
                           {
                               //cyberspace
                               ResBind = smppconn.Bind(); // Tranceiver
                           }
                           catch (SmppTimeOutException errtime)
                           {

                               logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ":" + errtime.Message + "\n");

                           }
                       }
                       catch (SmppGenericNackException egn)
                       {
                           logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ":" + egn.Message + "\n");
                           

                       }


                       if (smppconn.Connected == true)
                       {

                           logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + " Tranceiver Client Connected Successfully\n");
                       }
                   }
               }


               enquireTimer.Enabled = true;
           }
           catch (SmppException ex)
           {
                logger.Error(tProtocol.NetworkID + ":" + tProtocol.OperatorID  + ": " + ex.Message + "\n");

              
           }

           

       }
       /// <summary>
       /// -------------------->
       /// </summary>
       /// <param name="transportprotocol"></param>
      public void InitializeParameters(TransportProtocols transportprotocol)
       {
           smppconn.Settings.RemoteHost = transportprotocol.RemoteHost; // in case

           if (IsValidIP(transportprotocol.RemoteHost) == true)
           {
               smppconn.Settings.RemoteHost = transportprotocol.RemoteHost;
           }
           else
           {
               try
               {
                   IPAddress[] address = Dns.GetHostAddresses(transportprotocol.RemoteHost);

                   foreach (IPAddress theaddress in address)
                   {
                       smppconn.Settings.RemoteHost = theaddress.ToString();
                       break;
                   }
               }
               catch (Exception ex)
               {

                   //  ex.Message;
               }

           }
          
          
          
          smppconn.Settings.Timeout = transportprotocol.Timeout;
           smppconn.Settings.ConnectionMode = SmppConnectionMode.Transceiver;
          // smppconn.Settings.RemoteHost = transportprotocol.RemoteHost;
           smppconn.Settings.RemotePort = transportprotocol.RemotePort;
           smppconn.Settings.BindParams.AddressRange = transportprotocol.AddressRange;
           //smppconn.Settings.BindParams.AddressNpi = Convert.ToByte(1);
           //smppconn.Settings.BindParams.AddressTon = Convert.ToByte(1);             
           smppconn.Settings.BindParams.Password = transportprotocol.Password;
           smppconn.Settings.BindParams.SystemId = transportprotocol.SystemID;
           smppconn.Settings.BindParams.SystemType = transportprotocol.SystemType;
           smppconn.Settings.BindParams.InterfaceVersion = Convert.ToByte(52);
          
           //logger.Error(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ": " + ex.Message + "\n");

           logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + " Client Initialized Successfully\n");

            
       }


      private void timerElapsedEvent(object sender, ElapsedEventArgs elapsedEventArgs)
      {
          //if (isSending)
          //{
          //    //System.Diagnostics.Trace.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", DateTimeFormatInfo.InvariantInfo) + "] " + m_smppConfigInfo.m_name + " " + GetConnectionModeAsString(m_smppClient.Settings.ConnectionMode) + " EnquireLink deferred");
          //    return;
          //}

          //System.Diagnostics.Trace.WriteLine("[" + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss.fff", DateTimeFormatInfo.InvariantInfo) + "] " +m_smppConfigInfo.m_name + " " + m_smppClient.Connection_Mode + " EnquireLink executing");

          SmppEnquireLinkRes enquireLinkResponse;

          try
          {
              enquireTimer.Enabled = false;

              if (smppconn != null && smppconn.Connected)
              {
                  enquireLinkResponse = smppconn.EnquireLink(new SmppEnquireLinkReq());

                  if (enquireLinkResponse.Header.CommandStatus != 0)
                  {
                      //log.Warn(m_smppConfigInfo.m_name + " " + GetConnectionModeAsString(m_smppClient.Settings.ConnectionMode) + " EnquireLink failed");

                     // Console.ForegroundColor = ConsoleColor.Red;
                      //System.Diagnostics.Trace.WriteLine("[" + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss.fff",
                      //                    DateTimeFormatInfo.InvariantInfo) + "] " + m_smppConfigInfo.m_name + " " + GetConnectionModeAsString(m_smppClient.Settings.ConnectionMode) + " EnquireLink failed");

                      //Console.WriteLine("EnquireLink request threw an exception");
                     // Console.ResetColor();

                      if (smppconn.Connected)
                      {
                          smppconn.UnBind();
                          //log.Warn(m_smppConfigInfo.m_name + " " + GetConnectionModeAsString(m_smppClient.Settings.ConnectionMode) + " Unbind");
                      }
                      //attemptReconnect(m_smppClient.LastException);
                  }
                  else
                  {
                      //log.Debug(m_smppConfigInfo.m_name + " " + GetConnectionModeAsString(m_smppClient.Settings.ConnectionMode) + " Sequence_Number: " + enquireLinkResponse.Header.SequenceNumber.ToString());

                      logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ": EnquireLink SequenceNumber: " + enquireLinkResponse.Header.SequenceNumber.ToString() + "\n");

                       
                     // Console.WriteLine("EnquireLink_Res: {0}", enquireLinkResponse.Header.SequenceNumber.ToString());
                              //System.Diagnostics.Trace.WriteLine("[" + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss.fff",
                              //                    DateTimeFormatInfo.InvariantInfo) + "] " + m_smppConfigInfo.m_name + " " + GetConnectionModeAsString(m_smppClient.Settings.ConnectionMode) + " Sequence_Number: " + enquireLinkResponse.Header.SequenceNumber.ToString());
                              ////Console.WriteLine(m_smppConfigInfo.m_name + " " + m_smppClient.Connection_Mode + " Command_Id: {0}", enquireLinkResponse.Header.Command_Id);
                              //Console.WriteLine(m_smppConfigInfo.m_name + " " + m_smppClient.Connection_Mode + " Command_Length: {0}", enquireLinkResponse.Header.Command_Length);
                              //Console.WriteLine(m_smppConfigInfo.m_name + " " + m_smppClient.Connection_Mode + " Command_Status: {0}", enquireLinkResponse.Header.Command_Status);
                  }
                  //
              }
              else if (!smppconn.Connected)
              {
                 // attemptReconnect();
              }
          }

          catch (Exception exception)
          {
              //Console.ForegroundColor = ConsoleColor.Red;
              //System.Diagnostics.Trace.WriteLine("[" + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss.fff",
              //DateTimeFormatInfo.InvariantInfo) + "] " + m_smppConfigInfo.m_name + " " + GetConnectionModeAsString(m_smppClient.Settings.ConnectionMode)
              //+ exception.Message);
              //Console.ResetColor();
              logger.Error(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ":  " + exception.Message  + "\n");

              //logger.Info(tProtocol.NetworkID + ":" + tProtocol.OperatorID + " Transmitter Client Connected Successfully\n");

                      

          }

          finally
          {
              enquireTimer.Enabled = (smppconn != null);
          }

          return;
      }

      public bool SendMessage(TransactionMessage transactionmessage)
      {
          bool sendSuccess = false;
          switch (transactionmessage.MessageType)
          {

              case SmsMessageType.SMSText: // Plain SMS
               sendSuccess=   SendPlainSMS(transactionmessage);
                  break;

              case SmsMessageType.WAPServiceIndication: // WAP Service Indication
              sendSuccess=    SendWapServiceIndication(transactionmessage);
                  break;

              case SmsMessageType.SMSBinary: // Binary SMS
           sendSuccess=       SendSMSBinary(transactionmessage);
                  break;

              default:
                  break;
                  

          }
          
         return   sendSuccess ;
      }

      private bool SendSMSBinary(TransactionMessage transactionmessage)
      {
         
          // Yet to be implemented
          return true;
      }

      private bool SendWapServiceIndication(TransactionMessage transactionmessage)
      {
          bool sendSuccess = false;
          SmppSubmitSmReq.BodyPdu ReqSubmit_Sm = new SmppSubmitSmReq.BodyPdu();
          SmppSubmitSmReq.BodyPdu submitRequest = new Devshock.Protocol.SmppPdu.SmppSubmitSmReq.BodyPdu();
          //submitRequest.RegisteredDelivery = new Devshock.Common.BitBuilder();
          //submitRequest.RegisteredDelivery.Value = 1;

          PushMessage message = createPushMessage(transactionmessage.ResponseMessage);
          ReqSubmit_Sm.EsmClass = new Devshock.Common.BitBuilder();
          ReqSubmit_Sm.EsmClass.Value = 0x40;
          ReqSubmit_Sm.ShortMessage.SetValue(message.GetSMSBytes(), SmppDataCoding.FromValue(0xF5));

          //// setup the sending params
          ////submitRequest.Data_Coding = 0;
          //submitRequest.DestinationAddressNpi = m_smppConfigInfo.m_destNPI;
          //submitRequest.DestinationAddressTon = m_smppConfigInfo.m_destTON;
          //submitRequest.SourceAddressNpi = m_smppConfigInfo.m_addrNPI;
          //submitRequest.SourceAddressTon = m_smppConfigInfo.m_addrTON;

          //submitRequest.DestinationAddress = smsMessageInfo.m_destAddress;
          //submitRequest.SourceAddress = smsMessageInfo.m_srcAddress;

          //return sendSuccess;


          //bool sendSuccess = false;

         // SmppSubmitSmReq.BodyPdu ReqSubmit_Sm = new SmppSubmitSmReq.BodyPdu();

          SmppSubmitSmRes ResSubmit_Sm = null;

          if (smppconn.Connected == false)
          {
              this.Connect();


              if (ResBind.Header.CommandStatus == 0)
              {
                  try
                  {
                      ReqSubmit_Sm = new SmppSubmitSmReq.BodyPdu();
                      //SmppSubmitSmRes ResSubmit_Sm;
                      // ReqSubmit_Sm.EsmClass = new BitBuilder((byte)0x40); // UHDI this should always be 0x40
                      // ReqSubmit_Sm.ShortMessage.DataCoding = SmppDataCoding.FromValue((byte)0x00); //if it is unicode use 0x08 like for arabic or other unicode lang.
                      ReqSubmit_Sm.SourceAddressTon = new BitBuilder(tProtocol.AddrTON).Value;
                      ReqSubmit_Sm.SourceAddressNpi = new BitBuilder(tProtocol.AddrNPI).Value;
                      ReqSubmit_Sm.DestinationAddressTon = new BitBuilder(tProtocol.DestTON).Value;
                      ReqSubmit_Sm.DestinationAddressNpi = new BitBuilder(tProtocol.DestNPI).Value;
                      ReqSubmit_Sm.RegisteredDelivery = new BitBuilder(1);

                      ReqSubmit_Sm.DestinationAddress = transactionmessage.Msisdn;
                      ReqSubmit_Sm.SourceAddress = transactionmessage.ShortCode;  // 
                      //ReqSubmit_Sm.ShortMessage.SetValue(transactionmessage.ResponseMessage);
                      ReqSubmit_Sm.ShortMessage.SetValue(message.GetSMSBytes(), SmppDataCoding.FromValue(0xF5));

                      if (smppconn.Connected == true)
                      {
                          try
                          {
                              ResSubmit_Sm = smppconn.SubmitSm(new SmppSubmitSmReq(ReqSubmit_Sm));
                              // No copy to logger

                              //logger.Error(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ": EnquireLink SequenceNumber: " + enquireLinkResponse.Header.SequenceNumber.ToString() + "\n");

                              //logger.Info( transactionmessage.NetworkID + ":" + transactionmessage.OperatorID  +  " Send Message to: " + Transmitter Client Connected Successfully\n");

                              logger.Info(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message: Destination [" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                                + "]Body [" + transactionmessage.ResponseMessage + "]\n");



                              //Console.WriteLine("|" +  + ":" +  + ":" +  DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ": Sent Message to: "  + transactionmessage.Msisdn + " : Message: " + transactionmessage.ResponseMessage);
                          }
                          catch (Exception ex01)
                          {

                              logger.Error(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message Destination[" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                              + "]Error [" + ex01.Message + "]\n");
                          }

                          //ResSubmit_Sm = smppconn.SubmitSm(new SmppSubmitSmReq(ReqSubmit_Sm));
                      }
                      else
                      {
                          return sendSuccess;
                      }
                      // if (ResSubmit_Sm.Header.CommandStatus == 0)

                      try
                      {
                          if (ResSubmit_Sm.Header.CommandStatus == 0)
                          {
                              sendSuccess = true;
                          }

                      }
                      catch (Exception ex02)
                      {


                          logger.Error(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message Destination[" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                         + "]Error [" + ex02.Message + "]\n");
                      }
                  }
                  catch (Exception exception)
                  {


                      logger.Error(exception.Message + "\n");

                  }

              }
          }
          else
          {
              try
              {

                  ReqSubmit_Sm.DestinationAddressTon = Convert.ToByte(tProtocol.DestTON);
                  ReqSubmit_Sm.DestinationAddressNpi = Convert.ToByte(tProtocol.DestNPI);
                  ReqSubmit_Sm.SourceAddressNpi = Convert.ToByte(tProtocol.AddrNPI);
                  ReqSubmit_Sm.SourceAddressTon = Convert.ToByte(tProtocol.AddrTON);
                  ReqSubmit_Sm.RegisteredDelivery = new BitBuilder(1);
                  ReqSubmit_Sm.DestinationAddress = transactionmessage.Msisdn;
                  //ReqSubmit_Sm.ShortMessage.SetValue(transactionmessage.ResponseMessage);   // = mySmppClient.SetShortMessage(ReqSubmit_Sm.Data_Coding, m_Message);
                  ReqSubmit_Sm.ShortMessage.SetValue(message.GetSMSBytes(), SmppDataCoding.FromValue(0xF5));

                  
                  ReqSubmit_Sm.SourceAddress = transactionmessage.ShortCode;

                  if (smppconn.Connected == true)
                  {

                      try
                      {
                          ResSubmit_Sm = smppconn.SubmitSm(new SmppSubmitSmReq(ReqSubmit_Sm));
                          // No copy to logger

                          logger.Info(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message: Destination [" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                               + "]Body [" + transactionmessage.ResponseMessage + "]\n");

                      }
                      catch (Exception ex03)
                      {


                          logger.Error(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message Destination[" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                               + "]Error [" + ex03.Message + "]\n");

                      }


                  }
                  else
                  {
                      return sendSuccess;
                  }
                  //ResSubmit_Sm = smppconn.SubmitSm(new SmppSubmitSmReq(ReqSubmit_Sm));

                  try
                  {
                      if (ResSubmit_Sm.Header.CommandStatus == 0)
                      {
                          sendSuccess = true;
                      }
                  }
                  catch (Exception ex04)
                  {



                      logger.Error(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message Destination[" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                          + "]Error [" + ex04.Message + "]\n");
                  }


              }
              catch (SmppException exception)
              {

                  logger.Error(exception.Message + "\n");
              }

          }

          return sendSuccess;










      }

      private bool SendPlainSMS(TransactionMessage transactionmessage)
      {
          bool sendSuccess = false;
          
          SmppSubmitSmReq.BodyPdu ReqSubmit_Sm = new SmppSubmitSmReq.BodyPdu();

          SmppSubmitSmRes ResSubmit_Sm = null;

          if (smppconn.Connected == false)
          {
              this.Connect();


              if (ResBind.Header.CommandStatus == 0)
              {
                  try
                  {
                      ReqSubmit_Sm = new SmppSubmitSmReq.BodyPdu();
                      //SmppSubmitSmRes ResSubmit_Sm;
                      // ReqSubmit_Sm.EsmClass = new BitBuilder((byte)0x40); // UHDI this should always be 0x40
                      // ReqSubmit_Sm.ShortMessage.DataCoding = SmppDataCoding.FromValue((byte)0x00); //if it is unicode use 0x08 like for arabic or other unicode lang.
                      ReqSubmit_Sm.SourceAddressTon = new BitBuilder(5).Value;
                      ReqSubmit_Sm.SourceAddressNpi = new BitBuilder(0).Value;
                      ReqSubmit_Sm.DestinationAddressTon = new BitBuilder(1).Value;
                      ReqSubmit_Sm.DestinationAddressNpi = new BitBuilder(1).Value;
                      ReqSubmit_Sm.RegisteredDelivery = new BitBuilder(1);

                      ReqSubmit_Sm.DestinationAddress = transactionmessage.Msisdn;
                      ReqSubmit_Sm.SourceAddress = transactionmessage.ShortCode;  // 
                      ReqSubmit_Sm.ShortMessage.SetValue(transactionmessage.ResponseMessage);
                      if (smppconn.Connected == true)
                      {
                          try
                          {
                              ResSubmit_Sm = smppconn.SubmitSm(new SmppSubmitSmReq(ReqSubmit_Sm));
                              // No copy to logger

                              //logger.Error(tProtocol.NetworkID + ":" + tProtocol.OperatorID + ": EnquireLink SequenceNumber: " + enquireLinkResponse.Header.SequenceNumber.ToString() + "\n");

                              //logger.Info( transactionmessage.NetworkID + ":" + transactionmessage.OperatorID  +  " Send Message to: " + Transmitter Client Connected Successfully\n");

                              logger.Info(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message: Destination [" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                                + "]Body [" + transactionmessage.ResponseMessage + "]\n");



                              //Console.WriteLine("|" +  + ":" +  + ":" +  DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ": Sent Message to: "  + transactionmessage.Msisdn + " : Message: " + transactionmessage.ResponseMessage);
                          }
                          catch (Exception ex01)
                          {

                              logger.Error(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message Destination[" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                              + "]Error [" + ex01.Message + "]\n");
                          }

                          //ResSubmit_Sm = smppconn.SubmitSm(new SmppSubmitSmReq(ReqSubmit_Sm));
                      }
                      else
                      {
                          return sendSuccess;
                      }
                      // if (ResSubmit_Sm.Header.CommandStatus == 0)

                      try
                      {
                          if (ResSubmit_Sm.Header.CommandStatus == 0)
                          {
                              sendSuccess = true;
                          }

                      }
                      catch (Exception ex02)
                      {


                          logger.Error(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message Destination[" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                         + "]Error [" + ex02.Message + "]\n");
                      }
                  }
                  catch (Exception exception)
                  {


                      logger.Error(exception.Message + "\n");

                  }

              }
          }
          else
          {
              try
              {

                  ReqSubmit_Sm.DestinationAddressTon = Convert.ToByte(1);
                  ReqSubmit_Sm.DestinationAddressNpi = Convert.ToByte(1);
                  ReqSubmit_Sm.SourceAddressNpi = Convert.ToByte(0);
                  ReqSubmit_Sm.SourceAddressTon = Convert.ToByte(5);
                  ReqSubmit_Sm.RegisteredDelivery = new BitBuilder(1);
                  ReqSubmit_Sm.DestinationAddress = transactionmessage.Msisdn;
                  ReqSubmit_Sm.ShortMessage.SetValue(transactionmessage.ResponseMessage);   // = mySmppClient.SetShortMessage(ReqSubmit_Sm.Data_Coding, m_Message);
                  ReqSubmit_Sm.SourceAddress = transactionmessage.ShortCode;

                  if (smppconn.Connected == true)
                  {

                      try
                      {
                          ResSubmit_Sm = smppconn.SubmitSm(new SmppSubmitSmReq(ReqSubmit_Sm));
                          // No copy to logger

                          logger.Info(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message: Destination [" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                               + "]Body [" + transactionmessage.ResponseMessage + "]\n");

                      }
                      catch (Exception ex03)
                      {


                          logger.Error(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message Destination[" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                               + "]Error [" + ex03.Message + "]\n");

                      }


                  }
                  else
                  {
                      return sendSuccess;
                  }
                  //ResSubmit_Sm = smppconn.SubmitSm(new SmppSubmitSmReq(ReqSubmit_Sm));

                  try
                  {
                      if (ResSubmit_Sm.Header.CommandStatus == 0)
                      {
                          sendSuccess = true;
                      }
                  }
                  catch (Exception ex04)
                  {



                      logger.Error(transactionmessage.NetworkID + ":" + transactionmessage.OperatorID + ":Out Message Destination[" + transactionmessage.Msisdn + "] Source [" + transactionmessage.ShortCode
                          + "]Error [" + ex04.Message + "]\n");
                  }


              }
              catch (SmppException exception)
              {

                  logger.Error(exception.Message + "\n");
              }

          }

          return sendSuccess;

      }

       /// <summary>
       /// ---------------------->
       /// </summary>
      public void Disconnect()
      {

          try
          {
              smppconn.UnBind();
          }
          catch (SmppException ex)
          {

              
              logger.Error(ex.Message + "\n");
          }
      }
       //
      //private SmppSubmitSmReq.BodyPdu SendSMSBinary(TransactionMessage smsMessageInfo)
      //{
      //    SmppSubmitSmReq.BodyPdu submitRequest = new Devshock.Protocol.SmppPdu.SmppSubmitSmReq.BodyPdu();
      //    submitRequest.RegisteredDelivery = new Devshock.Common.BitBuilder();
      //    submitRequest.EsmClass = new Devshock.Common.BitBuilder();
      //    submitRequest.EsmClass.Value = 0x40;
      //    //byte[] messageBytes = Encoding.Default.GetBytes(smsMessageInfo.m_messageText);
      //    //submitRequest.ShortMessage.SetValue(smsMessageInfo.m_messageText, SmppDataCoding.FromValue(0xF5));

      //    //// setup the sending params
      //    ////submitRequest.Data_Coding = 0;
      //    //submitRequest.DestinationAddressNpi = m_smppConfigInfo.m_destNPI;
      //    //submitRequest.DestinationAddressTon = m_smppConfigInfo.m_destTON;
      //    //submitRequest.SourceAddressNpi = m_smppConfigInfo.m_addrNPI;
      //    //submitRequest.SourceAddressTon = m_smppConfigInfo.m_addrTON;

      //    //submitRequest.DestinationAddress = smsMessageInfo.m_destAddress;
      //    //submitRequest.SourceAddress = smsMessageInfo.m_srcAddress;

      //    return submitRequest;
      //}

      

      private PushMessage createPushMessage(string text)
      {
          //extract url from text
          string matchPattern = @"(?<Url>(?<Protocol>\w+):\/\/(?<Domain>[\w.]+\/?)\S*)";
          Match urlMatch = Regex.Match(text, matchPattern);

          string urlString = urlMatch.Value;
          string textMessage = Regex.Replace(text, matchPattern, "");

          return new PushMessage(urlString, textMessage);
      }


       //




      public bool IsValidIP(string addr)
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
 
    }
}


 