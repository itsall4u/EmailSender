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

namespace EmailSender
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ConcurrentBag<KeyValuePair<string, string>> ListOfErrors = new ConcurrentBag<KeyValuePair<string, string>>();
        private readonly BackgroundWorker bgworker = new BackgroundWorker();
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
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            LetterText = Text.Text;
            SubjectText = Subject.Text;
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.FileName = "Excel Document"; // Default file name
            dialog.DefaultExt = ".xlsx"; // Default file extension
            dialog.Filter = "Text documents (.xlsx)|*.xlsx"; // Filter files by extension
                                                             // Show open file dialog box
            bool? result = dialog.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                lblStatus.Foreground = Brushes.Black;
                filename = dialog.FileName;
            }
            bgworker.WorkerSupportsCancellation = true;
            bgworker.WorkerReportsProgress = true;
            bgworker.DoWork += Worker_DoWork;
            bgworker.ProgressChanged += Worker_ProgressChanged;
            bgworker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            bgworker.RunWorkerAsync();
        }
        void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblStatus.Text = "Отправлено" + " (" + e.ProgressPercentage + "%)";
            pb1.Value = e.ProgressPercentage;
        }
        async void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bgworker.DoWork -= Worker_DoWork;
            bgworker.ProgressChanged -= Worker_ProgressChanged;
            bgworker.RunWorkerCompleted -= Worker_RunWorkerCompleted;
            if (e.Cancelled)
            {
                lblStatus.Foreground = Brushes.Red;
                lblStatus.Text = "Отменено";
            }
            else
            {
                lblStatus.Foreground = Brushes.Green;
                lblStatus.Text = "Выполнено: " + e.Result;
                if (ListOfErrors.Count > 0)
                {
                    StreamWriter sw = new StreamWriter(@"D:\Test\log.txt");
                    //Write a line of text
                    foreach (KeyValuePair<string, string> error in ListOfErrors)
                    {
                        sw.WriteLine( error.Key + " " + error.Value);
                    }
                    //Close the file
                    sw.Close();
                }
                MessageBox.Show("Успешно отправлено", "Отчет", MessageBoxButton.OK);
            }
        }
        void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            object[,] adresses = GetData(filename);
            List<Recipient> ListofRecipients = new List<Recipient>();
            for (int row = 0; row <= adresses.GetUpperBound(0); row++)
            {
                Recipient NewRecipient = new Recipient(adresses[row, 0].ToString(), adresses[row, 1].ToString());
                ListofRecipients.Add(NewRecipient);
            }
            Sender user = new Sender("Адрей", "no_other@mail.ru");
            double total = ListofRecipients.Count;
            double current = 0;
            double Value = 0;
            foreach (Recipient newrecipient in ListofRecipients)
            {
                try
                {
                    MailMessage NewMailMessage = new MailMessage(user.eMail, newrecipient.eMail);
                    NewMailMessage.Subject = SubjectText;
                    NewMailMessage.Body = LetterText;
                    // письмо представляет код html
                    NewMailMessage.IsBodyHtml = true;
                    // адрес smtp-сервера и порт, с которого будем отправлять письмо
                    SmtpClient smtp = new SmtpClient("smtp.mail.ru", 587);
                    // логин и пароль
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential("no_other@mail.ru", "zpmhYxwc4D");
                    smtp.EnableSsl = true;
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.Send(NewMailMessage);
                }
                catch
                {
                    string header = newrecipient.eMail.ToString();
                    string text = "Sending failed";
                    ListOfErrors.Add(new KeyValuePair<string, string>(header, text));
                }
                finally
                {
                    current++;
                Value = (double)(current / total) * 100;
                bgworker.ReportProgress((int)Value);
                Thread.Sleep(200);
                }
            }
        }




            public object[,] GetData(string filename)
        { 
            FileStream stream = null;
            object[,] adresses = null;
                stream = File.Open(filename, FileMode.Open, FileAccess.Read);
                using (IExcelDataReader excelReader = ExcelReaderFactory.CreateOpenXmlReader(stream))
                {
                    DataSet result = excelReader.AsDataSet();
                    excelReader.Close();
                    excelReader.Dispose();
                    string[] excelSheets = new string[result.Tables.Count];
                    foreach (DataTable data in result.Tables)
                    {
                        object[,] exceldata = new object[data.Rows.Count, data.Columns.Count];
                        for (int k = 0; k<data.Rows.Count; k++)
                        {
                            for (int j = 0; j<data.Columns.Count; j++)
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
            return adresses;
            }

    }
    public class Sender
    {
        public string Name { get; set; }
        public MailAddress eMail { get; set; }
        public Sender(string Name, string Email)
        {
            this.Name = Name;
            this.eMail = new MailAddress(Email);
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
