namespace DiskSpaceTracker.Services;

/// <summary>Plays the "Bip" notification sound (C:\Windows\Media\notify.wav) on demand.</summary>
public interface INotificationSoundService
{
    /// <summary>Play the notification sound, blocking until it has fully played.</summary>
    void PlayNotification();
}
