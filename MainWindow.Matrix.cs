using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.Linq;
using Memo.Models;

namespace Memo
{
    public sealed partial class MainWindow
    {
        private bool _isUpdatingPriorityControls;

        public ObservableCollection<TaskItem> MatrixQ1Tasks { get; } = new ObservableCollection<TaskItem>();
        public ObservableCollection<TaskItem> MatrixQ2Tasks { get; } = new ObservableCollection<TaskItem>();
        public ObservableCollection<TaskItem> MatrixQ3Tasks { get; } = new ObservableCollection<TaskItem>();
        public ObservableCollection<TaskItem> MatrixQ4Tasks { get; } = new ObservableCollection<TaskItem>();

        private void ShowMatrixContent()
        {
            PageHeader.Visibility = Visibility.Visible;
            NotepadContent.Visibility = Visibility.Collapsed;
            TaskListScrollViewer.Visibility = Visibility.Collapsed;
            MatrixContent.Visibility = Visibility.Visible;
            AddTaskBar.Visibility = Visibility.Collapsed;
            LoadMatrixTasks();
        }

        private void LoadMatrixTasks()
        {
            MatrixQ1Tasks.Clear();
            MatrixQ2Tasks.Clear();
            MatrixQ3Tasks.Clear();
            MatrixQ4Tasks.Clear();

            foreach (var task in _dbService.GetActiveTasksForMatrix())
            {
                switch (task.Quadrant)
                {
                    case TaskQuadrant.Q1_ImportantUrgent:
                        MatrixQ1Tasks.Add(task);
                        break;
                    case TaskQuadrant.Q2_ImportantNotUrgent:
                        MatrixQ2Tasks.Add(task);
                        break;
                    case TaskQuadrant.Q3_UrgentNotImportant:
                        MatrixQ3Tasks.Add(task);
                        break;
                    default:
                        MatrixQ4Tasks.Add(task);
                        break;
                }
            }

            SortMatrixQuadrant(MatrixQ1Tasks);
            SortMatrixQuadrant(MatrixQ2Tasks);
            SortMatrixQuadrant(MatrixQ3Tasks);
            SortMatrixQuadrant(MatrixQ4Tasks);

            UpdateMatrixEmptyStates();
        }

        private static void SortMatrixQuadrant(ObservableCollection<TaskItem> tasks)
        {
            var sorted = tasks.ToList();
            sorted.Sort(CompareByDueDateThenCreated);
            if (sorted.Select(t => t.Id).SequenceEqual(tasks.Select(t => t.Id)))
                return;

            tasks.Clear();
            foreach (var task in sorted)
                tasks.Add(task);
        }

        private void UpdateMatrixEmptyStates()
        {
            MatrixQ1Count.Text = $"{MatrixQ1Tasks.Count} 项";
            MatrixQ2Count.Text = $"{MatrixQ2Tasks.Count} 项";
            MatrixQ3Count.Text = $"{MatrixQ3Tasks.Count} 项";
            MatrixQ4Count.Text = $"{MatrixQ4Tasks.Count} 项";

            MatrixQ1Empty.Visibility = MatrixQ1Tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            MatrixQ2Empty.Visibility = MatrixQ2Tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            MatrixQ3Empty.Visibility = MatrixQ3Tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            MatrixQ4Empty.Visibility = MatrixQ4Tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshMatrixIfVisible()
        {
            if (_currentNavTag == "Matrix")
                LoadMatrixTasks();
        }

        private void MatrixTask_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is TaskItem task)
            {
                if (_selectedTask == task)
                {
                    CloseDrawer();
                }
                else
                {
                    _selectedTask = task;
                    ShowDrawer(task);
                    UpdateTaskItemSelection(task);
                }
                e.Handled = true;
            }
        }

        private void RefreshPriorityControls(TaskItem task)
        {
            _isUpdatingPriorityControls = true;
            ImportantSlider.Value = BoolToSliderValue(task.IsImportant);

            if (task.IsUrgencyManual)
            {
                UrgentSlider.Value = BoolToSliderValue(task.IsUrgent);
                UrgencyAutoHint.Visibility = Visibility.Collapsed;
                ResetUrgencyAutoButton.Visibility = Visibility.Visible;
            }
            else
            {
                UrgentSlider.Value = BoolToSliderValue(task.EffectiveIsUrgent);
                UrgencyAutoHint.Text = "本周内截止视为紧急";
                UrgencyAutoHint.Visibility = Visibility.Visible;
                ResetUrgencyAutoButton.Visibility = Visibility.Collapsed;
            }

            UpdatePriorityLevelLabels();
            UpdateQuadrantPickerHighlight(task.Quadrant);
            _isUpdatingPriorityControls = false;
        }

        private void UpdateQuadrantPickerHighlight(TaskQuadrant quadrant)
        {
            ResetQuadrantButtonStyle(QuadrantQ1Button);
            ResetQuadrantButtonStyle(QuadrantQ2Button);
            ResetQuadrantButtonStyle(QuadrantQ3Button);
            ResetQuadrantButtonStyle(QuadrantQ4Button);

            var active = quadrant switch
            {
                TaskQuadrant.Q1_ImportantUrgent => QuadrantQ1Button,
                TaskQuadrant.Q2_ImportantNotUrgent => QuadrantQ2Button,
                TaskQuadrant.Q3_UrgentNotImportant => QuadrantQ3Button,
                _ => QuadrantQ4Button
            };

            active.Background = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            active.Foreground = (SolidColorBrush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
        }

        private static void ResetQuadrantButtonStyle(Button button)
        {
            button.ClearValue(Button.BackgroundProperty);
            button.ClearValue(Button.ForegroundProperty);
        }

        private void ApplyTaskPriority(TaskItem task, bool isImportant, bool isUrgent, bool isUrgencyManual)
        {
            task.IsImportant = isImportant;
            task.IsUrgent = isUrgent;
            task.IsUrgencyManual = isUrgencyManual;
            _dbService.UpdateTaskPriority(task.Id, isImportant, isUrgent, isUrgencyManual);
            RefreshPriorityControls(task);
            RefreshMatrixIfVisible();
        }

        private void QuadrantPicker_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null || sender is not Button button) return;
            var tag = button.Tag?.ToString();
            var (isImportant, isUrgent) = tag switch
            {
                "Q1" => (true, true),
                "Q2" => (true, false),
                "Q3" => (false, true),
                _ => (false, false)
            };
            ApplyTaskPriority(_selectedTask, isImportant, isUrgent, true);
        }

        private void ImportantSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingPriorityControls || _selectedTask == null) return;
            var isImportant = SliderValueToBool(e.NewValue);
            _selectedTask.IsImportant = isImportant;
            _dbService.UpdateTaskImportant(_selectedTask.Id, isImportant);
            UpdatePriorityLevelLabels();
            UpdateQuadrantPickerHighlight(_selectedTask.Quadrant);
            RefreshMatrixIfVisible();
        }

        private void UrgentSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingPriorityControls || _selectedTask == null) return;
            ApplyTaskPriority(_selectedTask, _selectedTask.IsImportant, SliderValueToBool(e.NewValue), true);
        }

        private void UpdatePriorityLevelLabels()
        {
            ImportantLevelText.Text = SliderValueToBool(ImportantSlider.Value) ? "高" : "低";
            UrgentLevelText.Text = SliderValueToBool(UrgentSlider.Value) ? "高" : "低";
        }

        private static bool SliderValueToBool(double value) => value >= 0.5;

        private static double BoolToSliderValue(bool value) => value ? 1 : 0;

        private void ResetUrgencyAuto_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null) return;
            ApplyTaskPriority(_selectedTask, _selectedTask.IsImportant, _selectedTask.IsUrgent, false);
        }

        private void NotifyAutoUrgencyChangedIfNeeded()
        {
            if (_selectedTask != null && !_selectedTask.IsUrgencyManual)
                RefreshPriorityControls(_selectedTask);
            RefreshMatrixIfVisible();
        }
    }
}
