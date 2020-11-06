// Version 1.2
// Correct send/receive text/files
// Correct naming
// Contacts collecting and suing
// JSON saving of messages roll and contact list
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using System.Threading;
using Telegram.Bot.Types;

namespace Telegram_Bot
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		TelegramBotEngine engine;

		public MainWindow()
		{
			InitializeComponent();

			// Прикручиваем иконки из папки Images 
			// Папка Images\ должна быть в папке исполняемого файла - bin\Debug\

			// "скрепка" для отправки файлов 
			string sendFileIconFullPath = System.IO.Path.GetFullPath(@"Images\SendFilePaperClip.png");
			SendFileIcon.Source	   = new BitmapImage(new Uri(sendFileIconFullPath, UriKind.Absolute));

			// "галка" для отправки сообщения
			string sendMessageIconFullPath = System.IO.Path.GetFullPath(@"Images\SendMessage.png");
			SendMessageIcon.Source = new BitmapImage(new Uri(sendMessageIconFullPath, UriKind.Absolute));

			// "флоппи дискета" для сохранения ленты сообщений в JSON формате
			string saveFileFloppyIconFullPath = System.IO.Path.GetFullPath(@"Images\SaveFileFloppyIcon.png");
			SaveFileFloppyIcon.Source = new BitmapImage(new Uri(saveFileFloppyIconFullPath, UriKind.Absolute));

			engine = new TelegramBotEngine(this);

			MessagesRoll.ItemsSource = engine.messagesRoll;
			    Contacts.ItemsSource = engine.contactList;
		}


		private void SendMessageButton_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(ContactID.Text)) return;
			engine.SendMessage(Contacts.SelectedItem, InputMessaageField.Text);

			// Очищаем поле ввода
			InputMessaageField.Text = "";
		}

		private void InputMessaageField_KeyDown(object sender, KeyEventArgs e)
		{
			if (String.IsNullOrEmpty(ContactID.Text)) return;
			if (e.Key == Key.Enter)
			{
				if (String.IsNullOrEmpty(InputMessaageField.Text)) return;
				engine.SendMessage(Contacts.SelectedItem, InputMessaageField.Text);

				// Очищаем поле ввода
				InputMessaageField.Text = "";
			}
		}

		private void SendFileButton_Click(object sender, RoutedEventArgs e)
		{
			// Ещё не выбран получатель
			if (String.IsNullOrEmpty(ContactID.Text)) return;

			// Configure open file dialog box
			Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
			dlg.FileName = "*"; // Default file name
			dlg.DefaultExt = ".*"; // Default file extension
			dlg.Filter = "(*.*)|*.*"; // Filter files by extension

			// Show open file dialog box
			bool? result = dlg.ShowDialog();

			// Process open file dialog box results
			if (result != true) return;

			long filesize = new FileInfo(dlg.FileName).Length;
			if (filesize > 20_971_520)
			{
				MessageBox.Show("Бот не может отправлять файлы больше 20 Мб!");
				return;
			}

				// Open document
				engine.SendFile(Contacts.SelectedItem, dlg.FileName);
		}

		private void SaveJSON_Click(object sender, RoutedEventArgs e)
		{
			// Ещё нет сообщений
			if (engine.messagesRoll.Count == 0) return;

			// Configure save file dialog box
			Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
			dlg.FileName = "TelBotMessagesRoll"; // Default file name
			dlg.DefaultExt = ".json"; // Default file extension
			dlg.Filter = "JSON documents (.json)|*.json"; // Filter files by extension

			// Show save file dialog box
			bool? result = dlg.ShowDialog();

			// Process save file dialog box results
			if (result != true) return;

			// Save document
			string Path = dlg.FileName;

			string json = JsonConvert.SerializeObject(engine);
			System.IO.File.WriteAllText(Path, json);

		}

		private void MessagesRoll_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (MessagesRoll.SelectedItem == null) return;
			Message d = MessagesRoll.SelectedItem as Message;
			FocusContact.Text = d.ChatPartnerName;
			ContactID.Text = d.ChatID.ToString();
			// Строчка ниже работает, потому что переопределил Equals in Contact
			Contacts.SelectedItem = 
			engine.contactList[engine.contactList.IndexOf(new Contact() { ChatID = d.ChatID})];
			/* 
			 * но вот так, конечно, гораздо быстрее работает :)
			 * engine.SelectContact(d.ChatID); 
			 */
		}

		private void Contacts_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			Contact c = Contacts.SelectedItem as Contact;
			FocusContact.Text = c.ChatPartnerName;
			ContactID.Text = c.ChatID.ToString();

			Message d = MessagesRoll.SelectedItem as Message;
			if (d != null && c.ChatID != d.ChatID)
				MessagesRoll.SelectedItem = null;
		}
	}
}
