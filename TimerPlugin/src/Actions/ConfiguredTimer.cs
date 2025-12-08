namespace Loupedeck.TimerPlugin.Actions
{
    using System;
    using System.Timers;
    using Loupedeck;
    using Loupedeck.TimerPlugin.Services;
    using Loupedeck.TimerPlugin.Models;

    public class ConfiguredTimer : PluginDynamicCommand
    {
        private System.Timers.Timer _countdownTimer;
        private DateTime _startTime;
        private int _remainingMilliseconds;
        private TimerPreset _currentTimer;
        private bool _isRunning;

        public ConfiguredTimer()
            : base()
        {
            this.DisplayName = "Configured Timers";
            this.Description = "Run pre-configured timers";
            this.GroupName = "Timers";

            this._countdownTimer = new System.Timers.Timer(1000);
            this._countdownTimer.Elapsed += this.OnCountdownTick;
            this._countdownTimer.AutoReset = true;

            // Try to load parameters - service may not be initialized yet
            this.TryUpdateParameters();

            // Subscribe to configuration changes (will work once service is initialized)
            try
            {
                TimerConfigurationService.Instance.ConfigurationChanged += this.OnConfigurationChanged;
            }
            catch (InvalidOperationException)
            {
                // Service not initialized yet, that's OK - it will be subscribed later
                PluginLog.Info("TimerConfigurationService not ready for event subscription, will retry later");
            }
        }

        private void OnConfigurationChanged(object sender, EventArgs e)
        {
            // Make sure we're subscribed to future changes
            try
            {
                TimerConfigurationService.Instance.ConfigurationChanged -= this.OnConfigurationChanged;
                TimerConfigurationService.Instance.ConfigurationChanged += this.OnConfigurationChanged;
            }
            catch (InvalidOperationException)
            {
                // Service still not ready
            }

            this.TryUpdateParameters();
            this.ActionImageChanged();
        }

        public void RefreshParameters()
        {
            this.TryUpdateParameters();
        }

        private void TryUpdateParameters()
        {
            try
            {
                // Check if service is initialized
                if (!TimerConfigurationService.IsInitialized)
                {
                    PluginLog.Info("TimerConfigurationService not initialized yet, skipping parameter update");
                    return;
                }

                this.RemoveAllParameters();

                // Add each configured timer as a separate parameter
                var config = TimerConfigurationService.Instance.GetConfiguration();
                PluginLog.Info($"Loading {config.Timers.Count} timers from configuration");

                foreach (var timer in config.Timers)
                {
                    PluginLog.Info($"Timer: {timer.Name}, Active: {timer.IsActive}, ID: {timer.Id}");
                    if (timer.IsActive)
                    {
                        PluginLog.Info($"Adding timer parameter: {timer.Id} - {timer.Name}");
                        this.AddParameter(timer.Id, timer.Name, "Configured Timers");
                    }
                }

                PluginLog.Info("Timer parameters updated successfully");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to update timer parameters");
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            // Parse the selected timer
            var config = TimerConfigurationService.Instance.GetConfiguration();
            _currentTimer = config.Timers.Find(t => t.Id == actionParameter);

            if (_currentTimer == null)
            {
                PluginLog.Error($"Timer not found: {actionParameter}");
                return;
            }

            PluginLog.Info($"Starting timer: {_currentTimer.Name}");

            // If already running, stop it (toggle behavior)
            if (_isRunning)
            {
                this._countdownTimer.Stop();
                _isRunning = false;
                _remainingMilliseconds = 0;
                this.ActionImageChanged(actionParameter);
                PluginLog.Info($"Timer stopped: {actionParameter}");
                return;
            }

            var totalMilliseconds = _currentTimer.GetTotalMilliseconds();
            if (totalMilliseconds <= 0)
            {
                PluginLog.Warning($"Timer has no duration: {_currentTimer.Name}");
                return;
            }

            this._startTime = DateTime.Now;
            this._remainingMilliseconds = totalMilliseconds;
            this._isRunning = true;

            // Set up delay timer for haptic feedback
            var delayTimer = new System.Timers.Timer(totalMilliseconds);
            delayTimer.Elapsed += (s, e) => OnDelayElapsed(_currentTimer.Haptic);
            delayTimer.AutoReset = false;
            delayTimer.Start();

            this._countdownTimer.Start();
            this.ActionImageChanged(actionParameter);
        }

        private void OnCountdownTickHandler(object sender, ElapsedEventArgs e)
        {
            // Placeholder for cleanup
        }

        protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            if (string.IsNullOrEmpty(actionParameter))
            {
                return "Timer\r\nNot Set";
            }

            var timer = TimerConfigurationService.Instance.GetTimer(actionParameter);
            if (timer == null)
            {
                return "Timer\r\nNot Found";
            }

            if (_currentTimer != null && _currentTimer.Id == actionParameter && _isRunning && _remainingMilliseconds > 0)
            {
                var remainingTime = TimeSpan.FromMilliseconds(_remainingMilliseconds);

                // Format based on image size
                if (imageSize == PluginImageSize.Width60)
                {
                    // Compact format for small buttons
                    if (remainingTime.TotalHours >= 1)
                        return $"{timer.Name}\r\n{remainingTime:h\\:mm\\:ss}";
                    else
                        return $"{timer.Name}\r\n{remainingTime:mm\\:ss}";
                }
                else
                {
                    // Full format for larger buttons
                    return $"{timer.Name}\r\n{remainingTime:hh\\:mm\\:ss}";
                }
            }
            else
            {
                return $"{timer.Name}\r\n{timer.GetDisplayTime()}";
            }
        }

        private void OnDelayElapsed(string haptic)
        {
            this._countdownTimer.Stop();
            this._remainingMilliseconds = 0;
            this._isRunning = false;

            if (!string.IsNullOrEmpty(haptic))
            {
                this.Plugin.PluginEvents.RaiseEvent(haptic);
            }

            this.ActionImageChanged();
        }

        private void OnCountdownTick(object sender, ElapsedEventArgs e)
        {
            if (!this._isRunning) return;

            var elapsed = (int)(DateTime.Now - this._startTime).TotalMilliseconds;
            this._remainingMilliseconds = Math.Max(0, _currentTimer.GetTotalMilliseconds() - elapsed);

            if (this._remainingMilliseconds <= 0)
            {
                this._countdownTimer.Stop();
                this._isRunning = false;
            }

            this.ActionImageChanged();
        }
    }
}
