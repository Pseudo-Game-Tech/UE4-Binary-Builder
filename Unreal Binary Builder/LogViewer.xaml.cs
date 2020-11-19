/************************************************************************/
/* Credits to Federico Berasategui for this implementation.             */
/* https://stackoverflow.com/a/16745054                                 */
/************************************************************************/

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Data.Linq;
using System.Drawing;
using System.Windows.Media;
using System.Reflection;

namespace Unreal_Binary_Builder
{
    class TextEditorWrapper
    {
        private static readonly Type TextEditorType = Type.GetType("System.Windows.Documents.TextEditor, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
        private static readonly PropertyInfo IsReadOnlyProp = TextEditorType.GetProperty("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo TextViewProp = TextEditorType.GetProperty("TextView", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo RegisterMethod = TextEditorType.GetMethod("RegisterCommandHandlers",
            BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Type), typeof(bool), typeof(bool), typeof(bool) }, null);

        private static readonly Type TextContainerType = Type.GetType("System.Windows.Documents.ITextContainer, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
        private static readonly PropertyInfo TextContainerTextViewProp = TextContainerType.GetProperty("TextView");

        private static readonly PropertyInfo TextContainerProp = typeof(TextBlock).GetProperty("TextContainer", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void RegisterCommandHandlers(Type controlType, bool acceptsRichContent, bool readOnly, bool registerEventListeners)
        {
            RegisterMethod.Invoke(null, new object[] { controlType, acceptsRichContent, readOnly, registerEventListeners });
        }

        public static TextEditorWrapper CreateFor(TextBlock tb)
        {
            var textContainer = TextContainerProp.GetValue(tb);

            var editor = new TextEditorWrapper(textContainer, tb, false);
            IsReadOnlyProp.SetValue(editor._editor, true);
            TextViewProp.SetValue(editor._editor, TextContainerTextViewProp.GetValue(textContainer));

            return editor;
        }

        private readonly object _editor;

        public TextEditorWrapper(object textContainer, FrameworkElement uiScope, bool isUndoEnabled)
        {
            _editor = Activator.CreateInstance(TextEditorType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                null, new[] { textContainer, uiScope, isUndoEnabled }, null);
        }
    }

    public class SelectableTextBlock : TextBlock
    {
        static SelectableTextBlock()
        {
            FocusableProperty.OverrideMetadata(typeof(SelectableTextBlock), new FrameworkPropertyMetadata(true));
            TextEditorWrapper.RegisterCommandHandlers(typeof(SelectableTextBlock), true, true, true);

            // remove the focus rectangle around the control
            FocusVisualStyleProperty.OverrideMetadata(typeof(SelectableTextBlock), new FrameworkPropertyMetadata((object)null));
        }

        private readonly TextEditorWrapper _editor;

        public SelectableTextBlock()
        {
            _editor = TextEditorWrapper.CreateFor(this);
        }
    }

    /// <summary>
    /// Interaction logic for LogViewer.xaml
    /// </summary>
    public partial class LogViewer : UserControl
    {
        private ObservableCollection<LogEntry> LogEntries { get; set; }
        private bool AutoScroll = true;

        public enum EMessageType
        {
            Info,
            Debug,
            Warning,
            Error
        }

        public LogViewer()
        {
            InitializeComponent();
            DataContext = LogEntries = new ObservableCollection<LogEntry>();
        }

        public void AddLogEntry(LogEntry InLogEntry, EMessageType InMessageType)
        {
            switch (InMessageType)
            {
                case EMessageType.Info:
                    InLogEntry.MessageColor = Brushes.WhiteSmoke;
                    break;
                case EMessageType.Debug:
                    InLogEntry.MessageColor = Brushes.Aqua;
                    break;
                case EMessageType.Warning:
                    InLogEntry.MessageColor = Brushes.Gold;
                    break;
                case EMessageType.Error:
                    InLogEntry.MessageColor = Brushes.Red;
                    break;
                default:
                    break;

            }
            Dispatcher.BeginInvoke((Action)(() => LogEntries.Add(InLogEntry)));
        }

        public void ClearAllLogs()
        {
            Dispatcher.BeginInvoke((Action)(() => LogEntries.Clear()));
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            try
            {
                // User scroll event : set or unset autoscroll mode
                if (e.ExtentHeightChange == 0)
                {   // Content unchanged : user scroll event
                    if ((e.Source as ScrollViewer).VerticalOffset == (e.Source as ScrollViewer).ScrollableHeight)
                    {   // Scroll bar is in bottom
                        // Set autoscroll mode
                        AutoScroll = true;
                    }
                    else
                    {   // Scroll bar isn't in bottom
                        // Unset autoscroll mode
                        AutoScroll = false;
                    }
                }

                // Content scroll event : autoscroll eventually
                if (AutoScroll && e.ExtentHeightChange != 0)
                {   // Content changed and autoscroll mode set
                    // Autoscroll
                    (e.Source as ScrollViewer).ScrollToVerticalOffset((e.Source as ScrollViewer).ExtentHeight);
                }
            }
            catch (Exception ex)
            {
                LogEntry logEntry = new LogEntry();
                logEntry.Message = string.Format("APPLICATION ERROR: " + ex.Message);
                logEntry.DateTime = DateTime.Now;
                AddLogEntry(logEntry, EMessageType.Error);
            }
        }
    }

    public class PropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
            }));
        }
    }

    public class LogEntry : PropertyChangedBase
    {
        public DateTime DateTime { get; set; }
        public string Message { get; set; }
        public Brush MessageColor { get; set; }
    }
}
