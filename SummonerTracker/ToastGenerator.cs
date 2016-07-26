using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        
        /// <summary>
        /// Exibe uma notificação Toast no Windows
        /// </summary>
        public void ShowToast(ToastTemplateType template, params string[] lines)
        {
            // Get a toast XML template
            XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);

            // Fill in the text elements
            XmlNodeList stringElements = toastXml.GetElementsByTagName("text");
            for (int i = 0; i < lines.Length; i++)
            {
                stringElements[i].AppendChild(toastXml.CreateTextNode(lines[i]));
            }

            // Show the toast. Be sure to specify the AppUserModelId on your application's shortcut!
            ToastNotificationManager.CreateToastNotifier(APP_ID).Show(new ToastNotification(toastXml));
        }
    }
}