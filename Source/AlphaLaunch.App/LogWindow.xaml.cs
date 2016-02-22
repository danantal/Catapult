﻿using System;
using AlphaLaunch.Core.Debug;

namespace AlphaLaunch.App
{
    public partial class LogWindow
    {
        public LogWindow()
        {
            InitializeComponent();
            LogWindowLogEventSink.Attach(s => Status.Dispatcher.Invoke(() => AddLog(s)));
        }

        private void AddLog(string message)
        {
            Status.Text += message + Environment.NewLine;
            Status.ScrollToEnd();
        }
    }
}
