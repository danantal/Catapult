﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AlphaLaunch.App
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            Model.MainListModel.Items.CollectionChanged += Items_CollectionChanged;
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SearchItems.Height = 4 + SearchItems.Items.Count * 38;
        }

        private void SearchBarPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
            }
            else if (e.Key == Key.Enter)
            {
                Model.AddIntent(new ExecuteIntent(SearchBar.Text));
                Hide();
            }
        }

        private void SearchBarPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                Model.AddIntent(new MoveSelectionIntent(MoveDirection.Down));
            }
            else if (e.Key == Key.Up)
            {
                Model.AddIntent(new MoveSelectionIntent(MoveDirection.Up));
            }
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;

            SearchItems.Height = 0;

            Activate();
        }

        private void MainWindow_OnActivated(object sender, EventArgs e)
        {
            SearchBar.SelectAll();
            SearchBar.Focus();
        }

        private void SearchBar_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var source = e.OriginalSource as TextBox;
            Model.AddIntent(new SearchIntent(source?.Text ?? string.Empty));
        }
    }
}
