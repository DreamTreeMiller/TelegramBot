using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Collections.ObjectModel;
using System.Net;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using System.Threading;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Args;

namespace Telegram_Bot
{
	class TelegramBotEngine
	{
		private MainWindow w;
		static TelegramBotClient bot;
		public ObservableCollection<Message> messagesRoll { get; set; }
		public ObservableCollection<Contact> contactList { get; set; }
		private string token;

		public TelegramBotEngine(MainWindow W)
		{
			this.messagesRoll = new ObservableCollection<Message>();
			this.w = W;
			this.contactList = new ObservableCollection<Contact>();
			token = System.IO.File.ReadAllText(@"c:\Users\Tiller\OneDrive\DM\source\repos\TeleBotToken\token");
			bot = new TelegramBotClient(token);

			bot.OnMessage += MessageListener;

			bot.StartReceiving();
		}

		public Contact SelectContact(long chatid)
		{
			foreach (Contact c in contactList)
				if (c.ChatID == chatid) return c;
			return null;
		}
		private void MessageListener(object sender, Telegram.Bot.Args.MessageEventArgs e)
		{
			string baseUrl  = $@"https://api.telegram.org/bot{token}/getFile?file_id=";
			string fileName = "";
			string filetoobig = $"К сожалению, сейчас Телеграм бот не может принимать и отсылать файлы " +
								"размером больше 20 Мб.";

			// собираем адресную книгу. Это для 10 домашнего задания
			// Создаём запись "контакт"
			w.Dispatcher.Invoke(() =>
			{
				// Проверяем, он уже есть в списке контактов. Если нет, то добавляем
				// Ключ для проверки - ChatID
				// Работает, потому что переопределил Equals in Contact
				// Но отдельная функция NewContact, которая просто пробегает по списку и сравнивает ChatID,
				// думаю, будет работать быстрее. 
				// Здесь ведь надо ещё экземпляр класса создать... Но так красивее, конечно
				if (!contactList.Contains(new Contact() { ChatID = e.Message.Chat.Id } )) 
					contactList.Add(new Contact()
					{
						ChatID = e.Message.Chat.Id,
						ChatPartnerName = ComposeChatPartnerName(e),
						ChatType = e.Message.Chat.Type.ToString(),
						UserName = e.Message.Chat.Username,
						FirstName = e.Message.Chat.FirstName,
						LastName = e.Message.Chat.LastName
					});
			});

			if (e.Message.Type == MessageType.Text)
			{
				// Отобразили полученное сообщение
				IncomingTextMessageProcessor(e);
				return;
			}
			else
			{
				switch (e.Message.Type)
				{
					// Всяческие файлы, включая отдельные фото(!), которые пересылают, как файлы
					case MessageType.Document:
						DocumentReceived(e, filetoobig, out fileName);
						break;

					case MessageType.Photo:
						PhotoReceived(e, filetoobig, out fileName, baseUrl);
						break;
					
					case MessageType.Audio:
						AudioReceived(e, filetoobig, out fileName, baseUrl);
						break;

					case MessageType.Voice:
						VoiceReceived(e, filetoobig, out fileName, baseUrl);
						break;

					case MessageType.Video:
						VideoReceived(e, filetoobig, out fileName, baseUrl);
						break;

					case MessageType.VideoNote:
						VideoNoteReceived(e, filetoobig, out fileName, baseUrl);
						break;

					case MessageType.Sticker:
						StickerReceived(e, filetoobig, out fileName, baseUrl);
						break;

						// Cообщения других типов игнорируем
					default:
						return;
				}
			}
			// Файл уже отправили, и у получателя появилась в ленте запись.
			// Сделаем запись в ленте бота, но отправлять саму запись не будем
			OutgoingTextMessageProcessor(e, "Файл" + fileName + "\n" + "отправлен.");
		}

		#region Message Types processor

		private void DocumentReceived(MessageEventArgs e, string filetoobig, out string fileName)
		{
			fileName = "";
			if (e.Message.Document.FileSize > 20_971_520)
			{
				// Отсылаем отправителю сообщение, что файл слишком большой
				// У себя это сообщение не храним, т.к. мы ничего не получили
				bot.SendTextMessageAsync(e.Message.Chat.Id, filetoobig);
				return;
			}
			else
			{
				DownloadFile(e.Message.Document.FileId, e.Message.Document.FileName);
				e.Message.Text = $"{e.Message.Document.FileName}\n" +
					   $"Размер: {e.Message.Document.FileSize:##,# bytes}";

				// Пишем в ленту, что получили файл
				IncomingTextMessageProcessor(e);

				fileName = e.Message.Document.FileName;
				// Отсылаем файл назад
				var iof = new InputOnlineFile(e.Message.Document.FileId);
				bot.SendDocumentAsync(e.Message.Chat.Id, iof);
			}
		}

		private void PhotoReceived(MessageEventArgs e, string filetoobig, out string fileName, string baseUrl)
		{
			WebClient wc = new WebClient();

			// Изначально количество вариантов различного качества не известно!
			int numOfSizes = e.Message.Photo.Length;
			fileName = "";
			// Мы выбирем максимальный
			PhotoSize photo = e.Message.Photo[numOfSizes - 1];

			// На всякий случай проверим размер фото.
			// Если больше 20 Мб :) то выберем поменьше, благо, есть из чего выбирать
			while (numOfSizes > 1)
			{
				if (photo.FileSize <= 20_971_520) break;
				numOfSizes--;
				photo = e.Message.Photo[numOfSizes - 1];
			};

			// Но если даже самое маленькое фото больше 20 метров, тогда извините
			// Это, конечно, из ряда фантастики, но мало ли ...
			if (photo.FileSize > 20_971_520)
			{
				// Отсылаем отправителю сообщение, что файл слишком большой
				// У себя это сообщение не храним, т.к. мы ничего не получили
				bot.SendTextMessageAsync(e.Message.Chat.Id, filetoobig);
				return;
			}
			else
			{
				// Получаем имя и расширение файла
				// Для этого делаем запрос, т.к. в e.Message этой инфы нет,
				// только для файлов типа Document.
				JObject fileInfo = JObject.Parse(wc.DownloadString(baseUrl + photo.FileId));

				// Создаём уникальное имя файла
				string fileExt = (string)System.IO.Path.GetExtension((string)fileInfo["result"]["file_path"]);
				fileName = photo.FileUniqueId + fileExt;

				// скачиваем файл на диск
				DownloadFile(photo.FileId, fileName);

				// Пишем в ленту, что получили файл
				e.Message.Text = $"Фото сохранено в файле {fileName}\n" +
								 $"Размер: {photo.FileSize:##,# байт}";
				IncomingTextMessageProcessor(e);

				// Отсылаем файл назад
				var iof = new InputOnlineFile(photo.FileId);
				bot.SendPhotoAsync(e.Message.Chat.Id, iof);
			}
		}

		private void AudioReceived(MessageEventArgs e, string filetoobig, out string fileName, string baseUrl)
		{
			WebClient wc = new WebClient();
			fileName = "";

			if (e.Message.Audio.FileSize > 20_971_520)
			{
				// Отсылаем отправителю сообщение, что файл слишком большой
				// У себя это сообщение не храним, т.к. мы ничего не получили
				bot.SendTextMessageAsync(e.Message.Chat.Id, filetoobig);
				return;
			}
			else
			{
				// Получаем имя и расширение файла
				// Для этого делаем запрос, т.к. в e.Message этой инфы нет,
				// только для файлов типа Document.
				JObject fileInfo = JObject.Parse(wc.DownloadString(baseUrl + e.Message.Audio.FileId));

				// Пытаемся создать нормальное и в то же время уникальное имя файла
				string fileExt = (string)System.IO.Path.GetExtension((string)fileInfo["result"]["file_path"]);
				fileName = e.Message.Audio.Title + "_" +
						   e.Message.Audio.Performer + "_" +
						   e.Message.Audio.FileUniqueId +
						   fileExt;

				// скачиваем файл на диск
				DownloadFile(e.Message.Audio.FileId, fileName);

				// Пишем в ленту, что получили файл
				e.Message.Text = $"Аудио файл {fileName}\n" +
					   $"Размер: {e.Message.Audio.FileSize:##,# байт}";
				IncomingTextMessageProcessor(e);

				// Отсылаем файл назад
				InputOnlineFile iof = new InputOnlineFile(e.Message.Audio.FileId);
				bot.SendAudioAsync(e.Message.Chat.Id, iof);
			}

		}

		private void VoiceReceived(MessageEventArgs e, string filetoobig, out string fileName, string baseUrl)
		{
			WebClient wc = new WebClient();
			fileName = "";

			if (e.Message.Voice.FileSize > 20_971_520)
			{
				// Отсылаем отправителю сообщение, что файл слишком большой
				// У себя это сообщение не храним, т.к. мы ничего не получили
				bot.SendTextMessageAsync(e.Message.Chat.Id, filetoobig);
				return;
			}
			else
			{
				// Получаем имя и расширение файла
				// Для этого делаем запрос, т.к. в e.Message этой инфы нет,
				// только для файлов типа Document.
				JObject fileInfo = JObject.Parse(wc.DownloadString(baseUrl + e.Message.Voice.FileId));

				// Создаём уникальное имя файла
				string fileExt = (string)System.IO.Path.GetExtension((string)fileInfo["result"]["file_path"]);
				fileName = e.Message.Voice.FileUniqueId + fileExt;

				// скачиваем файл на диск
				DownloadFile(e.Message.Voice.FileId, fileName);

				// Пишем в ленту, что получили файл
				e.Message.Text = $"Голосовое сообщение сохранено в файле {fileName}\n" +
					   $"Размер: {e.Message.Voice.FileSize:##,# байт}";
				IncomingTextMessageProcessor(e);

				// Отсылаем файл назад
				InputOnlineFile iof = new InputOnlineFile(e.Message.Voice.FileId);
				bot.SendVoiceAsync(e.Message.Chat.Id, iof);
			}
		}

		private void VideoReceived(MessageEventArgs e, string filetoobig, out string fileName, string baseUrl)
		{
			WebClient wc = new WebClient();
			fileName = "";

			if (e.Message.Video.FileSize > 20_971_520)
			{
				// Отсылаем отправителю сообщение, что файл слишком большой
				// У себя это сообщение не храним, т.к. мы ничего не получили
				bot.SendTextMessageAsync(e.Message.Chat.Id, filetoobig);
				return;
			}
			else
			{
				// Получаем имя и расширение файла
				// Для этого делаем запрос, т.к. в e.Message этой инфы нет,
				// только для файлов типа Document.
				JObject fileInfo = JObject.Parse(wc.DownloadString(baseUrl + e.Message.Video.FileId));

				// Создаём уникальное имя файла
				string fileExt = (string)System.IO.Path.GetExtension((string)fileInfo["result"]["file_path"]);
				fileName = e.Message.Video.FileUniqueId + fileExt;

				// скачиваем файл на диск
				DownloadFile(e.Message.Video.FileId, fileName);

				// Пишем в ленту, что получили файл
				e.Message.Text = $"Видео файл {fileName}\n" +
					   $"Размер: {e.Message.Video.FileSize:##,# байт}";
				IncomingTextMessageProcessor(e);

				// Отсылаем файл назад
				InputOnlineFile iof = new InputOnlineFile(e.Message.Video.FileId);
				bot.SendVideoAsync(e.Message.Chat.Id, iof);
			}
		}

		private void VideoNoteReceived(MessageEventArgs e, string filetoobig, out string fileName, string baseUrl)
		{
			WebClient wc = new WebClient();
			fileName = "";

			if (e.Message.VideoNote.FileSize > 20_971_520)
			{
				// Отсылаем отправителю сообщение, что файл слишком большой
				// У себя это сообщение не храним, т.к. мы ничего не получили
				bot.SendTextMessageAsync(e.Message.Chat.Id, filetoobig);
				return;
			}
			else
			{
				// Получаем имя и расширение файла
				// Для этого делаем запрос, т.к. в e.Message этой инфы нет,
				// только для файлов типа Document.
				JObject fileInfo = JObject.Parse(wc.DownloadString(baseUrl + e.Message.VideoNote.FileId));

				// Создаём уникальное имя файла
				string fileExt = (string)System.IO.Path.GetExtension((string)fileInfo["result"]["file_path"]);
				fileName = e.Message.VideoNote.FileUniqueId + fileExt;

				// скачиваем файл на диск
				DownloadFile(e.Message.VideoNote.FileId, fileName);

				// Пишем в ленту, что получили файл
				e.Message.Text = $"Видео сообщение сохранено в файл {fileName}\n" +
					   $"Размер: {e.Message.VideoNote.FileSize:##,# байт}";
				IncomingTextMessageProcessor(e);

				// Отсылаем файл назад
				InputTelegramFile itf = new InputTelegramFile(e.Message.VideoNote.FileId);
				bot.SendVideoNoteAsync(e.Message.Chat.Id, itf);
			}
		}

		private void StickerReceived(MessageEventArgs e, string filetoobig, out string fileName, string baseUrl)
		{
			WebClient wc = new WebClient();
			fileName = "";

			if (e.Message.Sticker.FileSize > 20_971_520)
			{
				// Отсылаем отправителю сообщение, что файл слишком большой
				// У себя это сообщение не храним, т.к. мы ничего не получили
				bot.SendTextMessageAsync(e.Message.Chat.Id, filetoobig);
				return;
			}
			else
			{
				// Получаем имя и расширение файла
				// Для этого делаем запрос, т.к. в e.Message этой инфы нет,
				// только для файлов типа Document.
				JObject fileInfo = JObject.Parse(wc.DownloadString(baseUrl + e.Message.Sticker.FileId));

				// Пытаемся создать нормальное и в то же время уникальное имя файла
				string fileExt = (string)System.IO.Path.GetExtension((string)fileInfo["result"]["file_path"]);
				fileName = e.Message.Sticker.Emoji + "_" +
						   e.Message.Sticker.SetName + "_" +
						   e.Message.Sticker.FileUniqueId +
						   fileExt;

				// скачиваем файл на диск
				DownloadFile(e.Message.Sticker.FileId, fileName);

				// Пишем в ленту, что получили файл
				e.Message.Text = $"Стикер сохранён в файл {fileName}\n" +
					   $"Размер: {e.Message.Sticker.FileSize:##,# байт}";
				IncomingTextMessageProcessor(e);

				// Отсылаем файл назад
				InputOnlineFile iof = new InputOnlineFile(e.Message.Sticker.FileId);
				bot.SendStickerAsync(e.Message.Chat.Id, iof);
			}
		}

		#endregion

		#region Messages Feed methods

		/// <summary>
		/// Формирует имя собеседника, отображаемое в ленте чата.
		/// </summary>
		/// <param name="e"></param>
		/// <returns>
		/// Для чата типа личное сообщение (private)
		/// Если у пользователя прописаны поля имя и фамилия, составляем из них
		/// Если нет, то берём просто username
		/// Для группового чата (group) берёт заголовок чата
		/// </returns>
		private string ComposeChatPartnerName(Telegram.Bot.Args.MessageEventArgs e)
		{
			if (e.Message.Chat.Type == ChatType.Private)
			{
				bool emptyFN = String.IsNullOrEmpty(e.Message.From.FirstName);
				bool emptyLN = String.IsNullOrEmpty(e.Message.From.LastName);
				if (emptyFN && emptyLN)
				{
					return e.Message.From.Username;
				}
				else if (emptyFN)
				{
					return e.Message.From.LastName;
				}
				else if (emptyLN)
				{
					return e.Message.From.FirstName;
				}
				else
				{
					return e.Message.From.FirstName + " " + e.Message.From.LastName;
				}
			}
			else // Chat.Type == ChatType.Group. 
				 // Для групповых чатов отображаемое имя - заголовок чата.
				 // Не проверял, но предполагаю, что строка кода ниже 
				 // подойдёт и для сообщения из supergroup и каналов новостей 
			{
				return e.Message.Chat.Title;
			}
		}

		/// <summary>
		/// Обработчик полученного сообщения
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void IncomingTextMessageProcessor (Telegram.Bot.Args.MessageEventArgs e)
		{
			// Формируем отображаемое имя
			string ChatPartnerName = ComposeChatPartnerName(e);

			w.Dispatcher.Invoke(() =>
			{
				messagesRoll.Add(new Message()
				{
					ID				= e.Message.MessageId,
					ChatID			= e.Message.Chat.Id,
					Incoming		= true,
					ChatType		= e.Message.Chat.Type.ToString(),
					UserName		= e.Message.Chat.Username,
					FirstName		= e.Message.Chat.FirstName,
					LastName		= e.Message.Chat.LastName,
					MsgTitleInRoll	= ChatPartnerName,
					ChatPartnerName	= ChatPartnerName,
					Type			= e.Message.Type.ToString(),
					Text			= e.Message.Text,
					MessageDT		= DateTime.Now
				}) ;
				// Прокручиваем список, чтобы был виден его последний элемент
				// Пример нашёл в инете 
				var border = (Border)VisualTreeHelper.GetChild(w.MessagesRoll, 0);
				var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
				scrollViewer.ScrollToBottom();
			});
		}

		/// <summary>
		/// Записывает сообщение в ленту сообщений. 
		/// Только в ленту! Само сообщение не отправляет.
		/// </summary>
		/// <param name="e">Тут данные получателя</param>
		/// <param name="textout">Текст сообщения</param>
		private void OutgoingTextMessageProcessor (Telegram.Bot.Args.MessageEventArgs e, string textout)
		{
			// Формируем отображаемое имя
			string ChatPartnerName = ComposeChatPartnerName(e);

			// Проверим, не пуста ли лента сообщений
			// count - номер (не порядковый!) сообщения в ленте
			// если лента пуста, то просто начинаем с 1
			long count = messagesRoll.Count == 0 ? 1 : messagesRoll.Last().ID + 1;

			// Написали, что отправили файл обратно
			w.Dispatcher.Invoke(() =>
			{
				messagesRoll.Add(new Message()
				{
					ID				= count,
					ChatID			= e.Message.Chat.Id,
					Incoming		= false,
					ChatType		= e.Message.Chat.Type.ToString(),
					UserName		= e.Message.Chat.Username,
					FirstName		= e.Message.Chat.FirstName,
					LastName		= e.Message.Chat.LastName,
					MsgTitleInRoll	= "Сообщение для " + ChatPartnerName,
					ChatPartnerName = ChatPartnerName,
					Type			= e.Message.Type.ToString(),
					Text			= textout,
					MessageDT		= DateTime.Now
				});

				// Прокручиваем список, чтобы был виден его последний элемент
				// Пример нашёл в инете 
				var border = (Border)VisualTreeHelper.GetChild(w.MessagesRoll, 0);
				var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
				scrollViewer.ScrollToBottom();
			});
		}

		#endregion

		#region Send Message & File, Download File  

		/// <summary>
		/// Получает и сохраняет на диске файл документ
		/// </summary>
		/// <param name="fileId"></param>
		/// <param name="path"></param>
		static async void DownloadFile(string fileId, string path)
		{
			var file = await bot.GetFileAsync(fileId);
			FileStream fs = new FileStream(path, FileMode.OpenOrCreate);
			await bot.DownloadFileAsync(file.FilePath, fs);
			fs.Close();

			fs.Dispose();
		}

		/// <summary>
		/// Отправляет текстовое сообщение
		/// </summary>
		/// <param name="destination">
		/// Элемент ленты, содержащий данные собеседника, 
		/// кому отправляется сообщение
		/// </param>
		/// <param name="Text">Текст сообщения</param>
		public void SendMessage(object destination, string Text)
		{
			Contact d = destination as Contact;

			bot.SendTextMessageAsync(d.ChatID, Text);

			// Проверим, не пуста ли лента сообщений
			// count - номер (не порядковый!) сообщения в ленте
			// если лента пуста, то просто начинаем с 1
			long count = messagesRoll.Count == 0 ? 1 : messagesRoll.Last().ID + 1;

			// Формируем запись в ленте сообщений
			w.Dispatcher.Invoke(() =>
			{
				messagesRoll.Add(new Message()
				{
					ID				= count,
					ChatID			= d.ChatID,
					Incoming		= false,
					ChatType		= d.ChatType,
					UserName		= d.UserName,
					FirstName		= d.FirstName,
					LastName		= d.LastName,
					MsgTitleInRoll	= "Сообщение для " + d.ChatPartnerName,
					ChatPartnerName = d.ChatPartnerName,
					Type			= "Text",
					Text			= Text,
					MessageDT		= DateTime.Now
				});

				// Прокручиваем список, чтобы был виден его последний элемент
				// Пример нашёл в инете 
				var border = (Border)VisualTreeHelper.GetChild(w.MessagesRoll, 0);
				var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
				scrollViewer.ScrollToBottom();
			});
		}

		public async void SendFile(object destination, string fullFileName)
		{
			Contact d = destination as Contact; 
			string shortfn = System.IO.Path.GetFileName(fullFileName);
			using (Stream fs = new FileStream(fullFileName, FileMode.Open, FileAccess.Read))
			{
				await bot.SendDocumentAsync(d.ChatID, new InputOnlineFile(fs, shortfn));
			}

			// Проверим, не пуста ли лента сообщений
			// count - номер (не порядковый!) сообщения в ленте
			// если лента пуста, то просто начинаем с 1
			long count = messagesRoll.Count == 0 ? 1 : messagesRoll.Last().ID + 1;

			// Записывает сообщение об отправленном файле в ленту
			w.Dispatcher.Invoke(() =>
			{
				messagesRoll.Add(new Message()
				{
					ID				= count,
					ChatID			= d.ChatID,
					Incoming		= false,
					ChatType		= d.ChatType,
					UserName		= d.UserName,
					FirstName		= d.FirstName,
					LastName		= d.LastName,
					MsgTitleInRoll	= "Сообщение для " + d.ChatPartnerName,
					ChatPartnerName	= d.ChatPartnerName,
					Type			= "SendingFile",
					Text			= "Файл " + shortfn + " послан.",
					MessageDT		= DateTime.Now
				});
				// Прокручиваем список, чтобы был виден его последний элемент
				// Пример нашёл в инете 
				var border = (Border)VisualTreeHelper.GetChild(w.MessagesRoll, 0);
				var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
				scrollViewer.ScrollToBottom();
			});
		}

		#endregion
	}
}
