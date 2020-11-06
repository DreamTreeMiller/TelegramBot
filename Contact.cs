using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace Telegram_Bot
{
	class Contact 
	{
		public long ChatID {get; set;}
		public string ChatType { get; set; }	// Private or Group
		public string ChatPartnerName { get; set; }		// For group

		// for single user
		public string UserName { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public Contact() { }

		public override bool Equals(object obj)
		{
			Contact c = obj as Contact;
			return this.ChatID.Equals(c.ChatID);
		}

		public override int GetHashCode()
		{
			return (int)ChatID;
		}

	}
}
