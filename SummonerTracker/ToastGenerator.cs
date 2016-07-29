using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace SummonerTracker
{
    public class ToastGenerator
    {
        private const string APP_ID = "SummonerTracker";
        private ToastGenerator() { }
        private static ToastGenerator _instance;
        public static ToastGenerator Instance => _instance ?? (_instance = new ToastGenerator());

        public void ShowToast(string imageSource, string imageTooltip, string text)
        {
            // Get a toast XML template
            XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastImageAndText02);

            // Fill in the text element
            XmlNodeList stringElements = toastXml.GetElementsByTagName("text");
            stringElements[0].AppendChild(toastXml.CreateTextNode(text));

            // Fill in the image element
            XmlNodeList toastImage = toastXml.GetElementsByTagName("image");
            ((XmlElement)toastImage[0]).SetAttribute("src", imageSource);
            if (imageTooltip != null) ((XmlElement)toastImage[0]).SetAttribute("alt", imageTooltip);

            // Show the toast. Be sure to specify the AppUserModelId on your application's shortcut!
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(toastXml));
        }

        /// <summary>
        /// Exibe uma notificação Toast no Windows
        /// </summary>
        public void ShowToast(string text)
        {
            // Get a toast XML template
            XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText01);

            // Fill in the text elements
            XmlNodeList stringElements = toastXml.GetElementsByTagName("text");
            stringElements[0].AppendChild(toastXml.CreateTextNode(text));

            // Show the toast. Be sure to specify the AppUserModelId on your application's shortcut!
            ToastNotificationManager.CreateToastNotifier(APP_ID).Show(new ToastNotification(toastXml));
        }
    }
}