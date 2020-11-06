using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Telegram_Bot
{
	/// <summary>
	/// One message sent to or received by bot
	/// </summary>
	class Message
	{
		public long ID { get; set; }                // Message ID
		public long ChatID { get; set; }            // Chat ID 
													// Если сообщение пришло от отдельного user(не группы),
													// тогда ID user'a и chat'a - одинаковые.
													// Если сообщение пришло из группы, 
													// тогда ID user'a и chat'a разные,
													// и надо использовать ChatId, потому что для отсылки сообщения
													// нужен именно ChatId
		public bool Incoming { get; set; }			// True - входящее для бота сообщение
													// False - исходящее от бота сообщение
		public string ChatType { get; set; }        // Private - сообщение от/для single User
													// Group   - сообщение из/в групповой чат
		
		// Собеседник бота на другом конце чата
		public string UserName { get; set; }        // ChatType: Private -	Chat.Username 
													// ChatType: Group:	 -	Chat.Title

		public string FirstName { get; set; }       // ChatType: Private - Chat.FirstName
													// ChatType: Group:	 - ""
		public string LastName { get; set; }        // ChatType: Private - Chat.LastName
													// ChatType: Group:  - ""

		public string MsgTitleInRoll { get; set; }  // Поле для отображения в ленте сообщений
													// Либо отправителя - от которого бот получил сообщю/файл
													// -->	ChatPartnerName
													// Либо получателя - кому бот отправил сообщ-е/файл
													// -->	"Бот для" + ChatPartnerName
		public string ChatPartnerName { get; set; } // ChatType: Private:	поле строится из FN, LN или UserName
													// ChatType: Group:		Chat.Title

		public string Type { get; set; }			// Тип сообщения - текст, файл, аудио, видео, стикер ...
		public string Text { get; set; }			// Текст сообщения
		public DateTime MessageDT { get; set; }		// Дата и время отправки сообщения
		public Message () { }
	}
}
