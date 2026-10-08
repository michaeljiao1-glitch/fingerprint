using SampleApplication.UranusCoreAPIServiceReference;
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

namespace SampleApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        UranusCoreClient client = new UranusCoreClient();
        private string AuthenticatedUserID = null;
        private string sessionKey = null;
        private string AppToken = "BayometricSampleAppToken";

        private EventHandler<SearchCompletedEventArgs> searchHandler = (s, ev) => MessageBox.Show(String.Format("Message: {0} PersonFoundID: {1}", ev.Result.ReturnMessage, ev.Result.PersonFoundID));

        public MainWindow()
        {
            InitializeComponent();
        }

      
        private void Register(string personID)
        {
            BFSResponse result = client.RegisterPerson(sessionKey,personID);
            if (result.ResponseCode == BFSClientReturnErrorCode.SUCCESS)
            {
                MessageBox.Show("User enrolled.");
                tbGuid.Text = result.PersonID;
            }
            else
            {
                MessageBox.Show("Could not enroll user. Reason: " + result.ReturnMessage);
            }
        }
        
        private bool ValidateIDToDelete()
        {
            return !((tbGuid.Text == String.Empty || tbGuid.Text.Count(c => !Char.IsWhiteSpace(c)) == 0));
        }

        private void btCreateSession_Click(object sender, RoutedEventArgs e)
        {
            //BFSAuthResponse result = client.CreateSession(null);
            BFSAuthResponse result = client.CreateSession(new AuthRequestInfo { ShowGUI = true, Timeout = 30});
            if (result.ResponseCode == BFSClientReturnErrorCode.SUCCESS)
            {
                AuthenticatedUserID = result.PersonID;
                sessionKey = result.SessionKey;
                MessageBox.Show("User authenticated. SessionKey = " + sessionKey);
            }
            else
            {
                MessageBox.Show("Could not authenticate. Reason: " + result.ReturnMessage);
            }
        }

        private void btEndSession_Click(object sender, RoutedEventArgs e)
        {
            BFSResponseBase result = client.EndSession(sessionKey);
            if (result.ResponseCode == BFSClientReturnErrorCode.SUCCESS)
            {
                MessageBox.Show("Session ended.");
                sessionKey = null;
            }
            else
            {
                MessageBox.Show("Could not end session. Reason: " + result.ReturnMessage);
            }
        }

        private void btRegisterPerson_Click(object sender, RoutedEventArgs e)
        {
            Register(tbGuid.Text != String.Empty ? tbGuid.Text : null);
        }

        private void btUnregisterPerson_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateIDToDelete())
            {
                BFSResponse result = client.UnregisterPerson(sessionKey, tbGuid.Text);
                if (result.ResponseCode == BFSClientReturnErrorCode.SUCCESS)
                {
                    MessageBox.Show("User removed.");
                    tbGuid.Text = result.PersonID;
                }
                else
                {
                    MessageBox.Show("Could not delete user. Reason: " + result.ReturnMessage);
                }
            }
            else
            {
                MessageBox.Show("Biometric ID can't be empty.");
            }
        }

        private void btSearch_Click(object sender, RoutedEventArgs e)
        {
            client.SearchCompleted -= searchHandler;
            client.SearchCompleted += searchHandler;
            client.SearchAsync();

            /*
             * Calling Search() is the same as calling SearchEx the following way
             * client.SearchAsync(null, null, true, 30);
             * */

        }

        private void btCancelSearch_Click(object sender, RoutedEventArgs e)
        {
            BFSResponse result = client.CancelSearch(null);
            if (result.ResponseCode == BFSClientReturnErrorCode.SUCCESS)
            {
                MessageBox.Show("Search Cancelled.");

            }
            else
            {
                MessageBox.Show("Could not cancel search. Reason: " + result.ReturnMessage);
            }
        }

        private void btListUsers_Click(object sender, RoutedEventArgs e)
        {
            //Listing 30 users only
            var response = client.GetRegisteredPersons(0,30);
            
            if(response.ResponseCode != BFSClientReturnErrorCode.SUCCESS && response.ResponseCode != BFSClientReturnErrorCode.END_OF_LIST)
            {
                MessageBox.Show("Could not list users." + response.ReturnMessage);
            }
            else
            {
                MessageBox.Show(response.ReturnMessage);
                List<string> users = response.RegisteredPersons.ToList();
                lbUsers.ItemsSource = null;
                lbUsers.ItemsSource = users;
            }
        }

        
    }
}
