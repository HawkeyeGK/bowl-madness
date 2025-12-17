using System;

namespace BowlPoolManager.Client.Services
{
    public class AppState
    {
        // The event that components subscribe to
        public event Action? OnChange;

        // The method components call to trigger an update
        public void NotifyDataChanged() => OnChange?.Invoke();
    }
}
