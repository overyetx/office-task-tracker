using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OfficeTaskTracker
{
    public partial class CustomMessageBox : Window
    {
        private MessageBoxResult _result = MessageBoxResult.None;

        public CustomMessageBox(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            InitializeComponent();
            TitleText.Text = caption;
            MessageText.Text = messageBoxText;

            // Set Icon
            switch (icon)
            {
                case MessageBoxImage.Error:
                    IconText.Text = "❌";
                    break;
                case MessageBoxImage.Question:
                    IconText.Text = "❓";
                    break;
                case MessageBoxImage.Warning:
                    IconText.Text = "⚠️";
                    break;
                case MessageBoxImage.Information:
                default:
                    IconText.Text = "ℹ️";
                    break;
            }

            // Set Buttons
            switch (button)
            {
                case MessageBoxButton.OK:
                    AddButton("OK", MessageBoxResult.OK, isDefault: true);
                    break;
                case MessageBoxButton.OKCancel:
                    AddButton("Cancel", MessageBoxResult.Cancel);
                    AddButton("OK", MessageBoxResult.OK, isDefault: true);
                    break;
                case MessageBoxButton.YesNo:
                    AddButton("No", MessageBoxResult.No);
                    AddButton("Yes", MessageBoxResult.Yes, isDefault: true);
                    break;
                case MessageBoxButton.YesNoCancel:
                    AddButton("Cancel", MessageBoxResult.Cancel);
                    AddButton("No", MessageBoxResult.No);
                    AddButton("Yes", MessageBoxResult.Yes, isDefault: true);
                    break;
            }
        }

        private void AddButton(string text, MessageBoxResult result, bool isDefault = false)
        {
            var btn = new Button
            {
                Content = text,
                Width = 80,
                Height = 32,
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = Cursors.Hand,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0)
            };

            // Custom inline style for buttons
            btn.Style = GetButtonStyle(isDefault);
            
            btn.Click += (s, e) =>
            {
                _result = result;
                Close();
            };

            ButtonsPanel.Children.Add(btn);
        }

        private Style GetButtonStyle(bool isPrimary)
        {
            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.Name = "border";
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            
            if (isPrimary)
            {
                factory.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4AA")));
            }
            else
            {
                factory.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a3555")));
            }

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(presenter);
            template.VisualTree = factory;

            Trigger mouseOver = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            mouseOver.Setters.Add(new Setter { TargetName = "border", Property = Border.OpacityProperty, Value = 0.85 });
            template.Triggers.Add(mouseOver);

            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            return style;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Cancel;
            Close();
        }

        public static MessageBoxResult Show(string messageBoxText, string caption = "Message", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            var msgBox = new CustomMessageBox(messageBoxText, caption, button, icon);
            msgBox.ShowDialog();
            return msgBox._result;
        }
    }
}
