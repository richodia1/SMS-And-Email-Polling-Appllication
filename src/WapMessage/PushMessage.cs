using System;
using System.IO;

namespace WAPPushSMPP
{
	/// <summary>
	/// Encapsulates an SMS WAP Push message
	/// </summary>
	public class PushMessage
	{
		// Ports for the WDP information element, instructing the handset which 
		// application to load on receving the message
		protected static byte[] WDP_DESTINATIONPORT = new byte[] {0x0b, 0x84};
		protected static byte[] WDP_SOURCEPORT = new byte[] {0x23, 0xf0};
		
		ServiceIndication serviceIndication;

		public PushMessage(string href, string text)
		{
			this.serviceIndication = new ServiceIndication(href, text, ServiceIndicationAction.signal_high);
		}

		/// <summary>
		/// Generates the body of the SMS message
		/// </summary>
		/// <returns>byte array</returns>
		public byte[] GetSMSBytes()
		{
			MemoryStream stream = new MemoryStream();
			byte[] wdpHeader = GetWDPHeaderBytes();
			stream.Write(wdpHeader, 0, wdpHeader.Length);

			byte[] pdu = GetPDUBytes();
			stream.Write(pdu, 0, pdu.Length);

			return stream.ToArray();
		}
		
		/// <summary>
		/// Generates the PDU (Protocol Data Unit) comprising the encoded Service Indication
		/// and the WSP (Wireless Session Protocol) headers
		/// </summary>
		/// <returns>byte array comprising the PDU</returns>
		public byte[] GetPDUBytes()
		{
			byte[] body = serviceIndication.GetWBXMLBytes();
			
			byte[] headerBuffer = GetWSPHeaderBytes((byte)body.Length);
			
			MemoryStream stream = new MemoryStream();
			stream.Write(headerBuffer, 0, headerBuffer.Length);
			stream.Write(body, 0, body.Length);

			return stream.ToArray();
		}

		/// <summary>
		/// Generates the WSP (Wireless Session Protocol) headers with the well known
		/// byte values specfic to a Service Indication
		/// </summary>
		/// <param name="contentLength">the length of the encoded Service Indication</param>
		/// <returns>byte array comprising the headers</returns>
		public byte[] GetWSPHeaderBytes(byte contentLength)
		{
			MemoryStream stream = new MemoryStream();

			stream.WriteByte(WSP.TRANSACTIONID_CONNECTIONLESSWSP);
			stream.WriteByte(WSP.PDUTYPE_PUSH);
			
			MemoryStream headersStream = new MemoryStream();
			headersStream.Write(WSP.HEADER_CONTENTTYPE_application_vnd_wap_sic_utf_8, 0, WSP.HEADER_CONTENTTYPE_application_vnd_wap_sic_utf_8.Length);
			
			headersStream.WriteByte(WSP.HEADER_APPLICATIONTYPE);
			headersStream.WriteByte(WSP.HEADER_APPLICATIONTYPE_x_wap_application_id_w2);

//			headersStream.WriteByte(WSP.HEADER_CONTENTLENGTH);
//			headersStream.WriteByte((byte)(contentLength + 128));
//
			headersStream.Write(WSP.HEADER_PUSHFLAG, 0, WSP.HEADER_PUSHFLAG.Length);

			stream.WriteByte((byte)headersStream.Length);

			headersStream.WriteTo(stream);

			return stream.ToArray();
		}

		/// <summary>
		/// Generates the WDP (Wireless Datagram Protocol) or UDH (User Data Header) for the 
		/// SMS message. In the case comprising the Application Port information element
		/// indicating to the handset which application to start on receipt of the message
		/// </summary>
		/// <returns>byte array comprising the header</returns>
		public byte[] GetWDPHeaderBytes()
		{
			MemoryStream stream = new MemoryStream();
			stream.WriteByte(WDP.INFORMATIONELEMENT_IDENTIFIER_APPLICATIONPORT);
			stream.WriteByte((byte)(WDP_DESTINATIONPORT.Length + WDP_SOURCEPORT.Length));
			stream.Write(WDP_DESTINATIONPORT, 0, WDP_DESTINATIONPORT.Length);
			stream.Write(WDP_SOURCEPORT, 0, WDP_SOURCEPORT.Length);

			MemoryStream headerStream = new MemoryStream();

			// write length of header
			headerStream.WriteByte((byte)stream.Length);

			stream.WriteTo(headerStream);
			return headerStream.ToArray();
		}
	}
}
