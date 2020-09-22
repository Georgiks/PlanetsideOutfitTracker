using JsonParser;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace TrackerStatisticsPlugin
{
    /// <summary>
    /// Interaction logic for TrackerWindow.xaml
    /// </summary>
    public partial class TrackerWindow : Window
    {
        public Dictionary<JsonString, PlayerStats> Players { get; set; }
        CollectionView view;

        Action<string> SaveStatsCallback;
        Action<string> SaveDetailsCallback;

        string sessionName;


        public TrackerWindow(EventRecordsHandler handler, Action<string> saveStatsCb, Action<string> saveDetailsCb, string sessionName)
        {
            this.sessionName = sessionName;
            SaveStatsCallback = saveStatsCb;
            SaveDetailsCallback = saveDetailsCb;

            Title = $"PS2 Tracker Session Summary ('{sessionName}')";

            InitializeComponent();

            // only show players that were online during tracking
            Players = handler.Players.Where(pair => pair.Value.OnlineTime.Ticks > 0).ToDictionary(pair => pair.Key, pair => pair.Value);

            DataContext = this;

            // GetDefaultView used for setting up filter returns null when ItemsSource is setup inside xaml until the xaml is loaded (OnLoaded can be used)
            // I want to setup fitler here so I assign itemssource here
            listView.ItemsSource = Players;

            view = (CollectionView)CollectionViewSource.GetDefaultView(listView.ItemsSource);
            view.SortDescriptions.Add(new SortDescription("Value.CharacterName.InnerString", ListSortDirection.Ascending));
        }



        GridViewColumnHeader _lastHeaderClicked = null;
        ListSortDirection _lastDirection = ListSortDirection.Ascending;

        /// <summary>
        /// Add sorting by column functionality to ListView
        /// https://docs.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-sort-a-gridview-column-when-a-header-is-clicked?redirectedfrom=MSDN&view=netframeworkdesktop-4.8
        /// </summary>
        void GridViewColumnHeaderClickedHandler(object sender,
                                                RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    Binding binding;
                    if (headerClicked.Column.CellTemplate == null)
                    {
                        binding = headerClicked.Column.DisplayMemberBinding as Binding;
                    } else
                    {
                        var columnBinding = headerClicked.Column.CellTemplate.LoadContent() as TextBlock;
                        var be = columnBinding.GetBindingExpression(TextBlock.TextProperty);
                        binding = be.ParentBinding;
                    }
                    var sortBy = binding.Path.Path;
                    Sort(sortBy, direction);

                    if (direction == ListSortDirection.Ascending)
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    }
                    else
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowDown"] as DataTemplate;
                    }

                    // Remove arrow from previously sorted header
                    if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
                    {
                        _lastHeaderClicked.Column.HeaderTemplate = null;
                    }

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            view.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            view.SortDescriptions.Add(sd);
            view.Refresh();
        }

        /// <summary>
        /// Open save file dialog and calls respective callback
        /// </summary>
        private void SaveDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.DefaultExt = ".csv";
            saveFileDialog.Filter = "CSV File|*.csv|All Files|*.*";
            saveFileDialog.FileName = sessionName + ".csv";
            if (saveFileDialog.ShowDialog(this) == true)
            {
                SaveDetailsCallback?.Invoke(saveFileDialog.FileName);
            }
        }

        /// <summary>
        /// Opens save file dialog and calls respective callback
        /// </summary>
        private void SaveStatsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.DefaultExt = ".csv";
            saveFileDialog.Filter = "CSV File|*.csv|All Files|*.*";
            saveFileDialog.FileName = sessionName + "_stats.csv";
            if (saveFileDialog.ShowDialog(this) == true)
            {
                SaveStatsCallback?.Invoke(saveFileDialog.FileName);
            }
        }
    }
}
