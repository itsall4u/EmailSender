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
using System.Net.Mail;
using System.Net;
using System.IO;
using ExcelDataReader;
using System.Data;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Collections.Concurrent;
using System.Windows.Threading;
using System.Text.RegularExpressions;

namespace EmailSender
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ConcurrentBag<KeyValuePair<string, string>> ListOfErrors;
        private readonly BackgroundWorker bgworker = new BackgroundWorker();
        private string LogFile = @"D:\log.txt";
        private int MailCount = 15;
        private string SendError = string.Empty;
        private Sender CurrentSender;
        private SmtpClient smtp;
        private int Delay = 250;
        private string AttachmentFileName = string.Empty;
        private string SubjectText;
        private string LetterText;
        private string filename;
        private double workerstate = 0;
        public double WorkerState
        {
            get { return workerstate; }
            set
            {
                workerstate = value;
                this.NotifyPropertyChanged("WorkerState");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName]string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }


        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            DelayBox.Text = Delay.ToString();
            MailCountBOX.Text = MailCount.ToString();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MailCount = (!string.IsNullOrEmpty(MailCountBOX.Text)) ? (int.Parse(MailCountBOX.Text) > 30) ? 30 : int.Parse(MailCountBOX.Text) : 10;
            LetterText = Text.Text;
            SubjectText = Subject.Text;
            AttachmentFileName = AttachmentBOX.Text;
            CurrentSender = new Sender(FromBOX.Text, EmailAdressBOX.Text, PSWDBOX.Password);
            if (!string.IsNullOrEmpty(DelayBox.Text)) Delay = int.Parse(DelayBox.Text);
            smtp = new SmtpClient(SMTPBox.Text, int.Parse(PortBox.Text));
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.FileName = "Excel Document"; // Default file name
            dialog.DefaultExt = ".xlsx"; // Default file extension
            dialog.Filter = "Text documents (.xls or .xlsx)|*.xls; *.xlsx"; // Filter files by extension
                                                             // Show open file dialog box
            bool? result = dialog.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                lblStatus.Foreground = Brushes.Black;
                filename = dialog.FileName;
                bgworker.WorkerSupportsCancellation = true;
                bgworker.WorkerReportsProgress = true;
                bgworker.DoWork += Worker_DoWork;
                bgworker.ProgressChanged += Worker_ProgressChanged;
                bgworker.RunWorkerCompleted += Worker_RunWorkerCompleted;
                bgworker.RunWorkerAsync();
            }
        }
        void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblStatus.Text = "Отправлено" + " (" + e.ProgressPercentage + "%)";
            pb1.Value = e.ProgressPercentage;
        }
        void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                lblStatus.Foreground = Brushes.Red;
                lblStatus.Text = "Отменено";
                bgworker.DoWork -= Worker_DoWork;
                bgworker.ProgressChanged -= Worker_ProgressChanged;
                bgworker.RunWorkerCompleted -= Worker_RunWorkerCompleted;
                MessageBox.Show("Во время отправки сообщений произошел сбой", "Ошибка", MessageBoxButton.OK);
            }
            else
            {
                bgworker.DoWork -= Worker_DoWork;
                bgworker.ProgressChanged -= Worker_ProgressChanged;
                bgworker.RunWorkerCompleted -= Worker_RunWorkerCompleted;
                lblStatus.Foreground = Brushes.Green;
                lblStatus.Text = "Выполнено: " + e.Result;
                if (ListOfErrors.Count > 0)
                {
                    try
                    {
                        
                        foreach (KeyValuePair<string, string> error in ListOfErrors)
                        {
                            SendError = error.Key;
                            SendError += '\n';
                        }
                       
                        StreamWriter sw = new StreamWriter(LogFile);
                        //Write a line of text
                        sw.WriteLine(SendError);
                        //Close the file
                        sw.Close();
                        MessageBox.Show("Во время отправки возникли ошибки.\nНекоторые пиьсма не были отправлены.\nСписок адресов, которым сообщение не было доставлено - " + LogFile, "Ошибка", MessageBoxButton.OK);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Во время записи файла произошла ошибка\n" + ex.Message, "Ошибка", MessageBoxButton.OK);
                    }
                    finally
                    {
                        Clipboard.SetText(SendError);
                        MessageBox.Show("Список адресов, которым сообщение не было доставлено\n" + SendError +"\nСписок адресов скопирован в буфер обмена.", "Ошибка", MessageBoxButton.OK);
                    }
                }
                else MessageBox.Show("Успешно отправлено", "Отчет", MessageBoxButton.OK);
            }
        }
        void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            List<string> SendingMailList = new List<string>();
            ListOfErrors = new ConcurrentBag<KeyValuePair<string, string>>();
            object[,] adresses = GetData(filename);
            List<Recipient> ListofRecipients = new List<Recipient>();
            if (adresses != null)
            {
                for (int row = 0; row <= adresses.GetUpperBound(0); row++)
                {
                    Recipient NewRecipient = new Recipient(adresses[row, 0].ToString(), adresses[row, 1].ToString());
                    ListofRecipients.Add(NewRecipient);
                }
                double total = ListofRecipients.Count;
                double current = 0;
                double Value = 0;
                for (int i = 0; i < ListofRecipients.Count; i++)
                {
                    try
                    {
                        MailMessage NewMailMessage = new MailMessage(CurrentSender.eMail, ListofRecipients[i].eMail);
                        NewMailMessage.Subject = SubjectText + " " + i.ToString();
                        NewMailMessage.Body = LetterText;
                        for (int y = i+1; (y < ListofRecipients.Count & y < i+MailCount); y++)
                        {
                            NewMailMessage.To.Add(ListofRecipients[y].eMail);
                            current = y;
                        }
                        i = (int)current;
                        current = i+1;
                        Value = (double)(current / total) * 100;
                        bgworker.ReportProgress((int)Value);
                        if (!string.IsNullOrEmpty(AttachmentFileName)) NewMailMessage.Attachments.Add(new Attachment(AttachmentFileName));
                        // письмо представляет код html
                        NewMailMessage.IsBodyHtml = true;
                        // адрес smtp-сервера и порт, с которого будем отправлять письмо
                        // логин и пароль
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = new NetworkCredential(CurrentSender.eMail.Address.ToString(), CurrentSender.Password);
                        smtp.EnableSsl = true;
                        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                        smtp.Send(NewMailMessage);
                        //foreach (MailAddress adress in NewMailMessage.To)
                        //{
                        //    SendingMailList.Add(adress.Address);
                        //}
                    }
                    catch (Exception ex)
                    {
                        string header = ListofRecipients[i].eMail.ToString();
                        string text = "Sending failed";
                        string text2 = ex.Message;
                        ListOfErrors.Add(new KeyValuePair<string, string>(header, text));
                    }
                    finally
                    {

                        //if ((i % 200 == 0) & (i < 400))
                        //{
                        //    ShowDelay = 40 * 1000 * 60;
                        //    Thread.Sleep(40 * 1000 * 60);
                        //}
                        //else
                        //{
                        //    ShowDelay = Delay;
                        //    
                        //}
                        Thread.Sleep(Delay);
                    }
                }
            }
            else
            {
                bgworker.CancelAsync();
                e.Cancel = true;
            }
        }
        public object[,] GetData(string filename)
        {
            IExcelDataReader excelReader;
            var fileinfo = new FileInfo(filename);
            FileStream stream = null;
            object[,] adresses = null;
            try
            {
                stream = File.Open(filename, FileMode.Open, FileAccess.Read);
                if (fileinfo.Extension.Equals(".xls")) excelReader = ExcelDataReader.ExcelReaderFactory.CreateBinaryReader(stream);
                else if (fileinfo.Extension.Equals(".xlsx")) excelReader = ExcelDataReader.ExcelReaderFactory.CreateOpenXmlReader(stream);
                else throw new Exception("Неверный формат файла");
                using (excelReader)
                {
                    DataSet result = excelReader.AsDataSet();
                    excelReader.Close();
                    excelReader.Dispose();
                    string[] excelSheets = new string[result.Tables.Count];
                    foreach (DataTable data in result.Tables)
                    {
                        object[,] exceldata = new object[data.Rows.Count, data.Columns.Count];
                        for (int k = 0; k < data.Rows.Count; k++)
                        {
                            for (int j = 0; j < data.Columns.Count; j++)
                            {
                                exceldata[k, j] = data.Rows[k].ItemArray[j].ToString();
                                ref object newdata = ref exceldata[k, j];
                                if (newdata != null)
                                {
                                    newdata = newdata.ToString().Trim();
                                    if (string.IsNullOrEmpty(newdata.ToString()))
                                    {
                                        newdata = null;
                                    }
                                }
                            }
                        }
                        adresses = exceldata;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при работе с файлом\n Если файл открыт в Excel, закройте его." + ex.Message, "Ошибка", MessageBoxButton.OK);
            }
            return adresses;
            }

        private void AttachmentBTN_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.FileName = ""; // Default file name
            dialog.DefaultExt = "*.*"; // Default file extension
            dialog.Filter = ""; // Filter files by extension
                                                             // Show open file dialog box
            bool? result = dialog.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                //Высчитываем размер файла. Переводим в килобайты.
                if ((new System.IO.FileInfo(dialog.FileName).Length / 1024 < 24 * 1024)) AttachmentBOX.Text = dialog.FileName;
                else MessageBox.Show("Размер файла превышает 24 МБ", "Ошибка", MessageBoxButton.OK);
            }
        }
    }
    public class Sender
    {
        public string Name { get; set; }
        public MailAddress eMail { get; set; }
        public string Password { get; set; }
        public Sender(string Name, string Email, string Password)
        {
            this.Name = Name;
            this.eMail = new MailAddress(Email, Name);
            this.Password = Password;
        }

    }

    public class PostServer
    {
        public string Adress { get; set; }
        public int Port{ get; set; }
        public string Password { get; set; }
        public PostServer(string Adress, int Port)
        {
            this.Adress = Adress;
            this.Port = Port;
        }
    }


    public class Recipient
    {
        public string Name { get; set; }
        public MailAddress eMail { get; set; }
        public string Gender { get; set; }
        public Recipient(string Name, string Email)
        {
            this.Name = Name;
            this.eMail = new MailAddress(Email);
        }

    }
}
