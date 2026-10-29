namespace SmartQueueSystemGUI.Services;
public sealed class CampusReminderWorker(QueueStore store,ILogger<CampusReminderWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));
        try {
            while(await timer.WaitForNextTickAsync(stoppingToken)) {
                try {store.ProcessReminders();}
                catch(Exception error) when(error is IOException or UnauthorizedAccessException) {logger.LogError("Unable to save campus reminders. Check service storage availability.");}
            }
        }catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){}
    }
}
