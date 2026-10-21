using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NegativeEncoder.EncodingTask;
using Xunit;

namespace NegativeEncoder.Tests;

public class TaskDetailWindowTests
{
    [Theory]
    [InlineData(WindowState.Normal)]
    [InlineData(WindowState.Maximized)]
    public void RestoringTaskDetail_RestoresOwnerAndClosingDetailKeepsItRestored(WindowState originalState)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? owner = null;
            TaskDetailWindow? detail = null;
            try
            {
                owner = new Window { Width = 300, Height = 200, WindowState = originalState };
                owner.Show();
                detail = new TaskDetailWindow
                {
                    Owner = owner,
                    DataContext = new NegativeEncoder.EncodingTask.EncodingTask()
                };
                detail.Show();
                PumpMessages();

                owner.Activate();
                PumpMessages();
                SystemCommands.MinimizeWindow(owner);
                PumpMessages();
                Assert.Equal(WindowState.Minimized, owner.WindowState);

                // Restore the owned window, as when selecting its taskbar entry.
                SystemCommands.RestoreWindow(detail);
                detail.Activate();
                PumpMessages();
                Assert.Equal(originalState, owner.WindowState);
                Assert.False(detail.ShowInTaskbar);

                detail.Close();
                PumpMessages();
                Assert.Equal(originalState, owner.WindowState);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                detail?.Close();
                owner?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Window lifecycle test timed out.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void PumpMessages()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
