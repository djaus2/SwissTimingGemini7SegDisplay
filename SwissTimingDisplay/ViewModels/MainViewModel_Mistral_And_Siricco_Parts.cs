using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;

using SwissTimingDisplay.Models;
using SwissTimingDisplay.ViewModels;
using SwissTimingDisplay.Services;

namespace SwissTimingDisplay.ViewModels
{
    public sealed partial class MainViewModel
    {
        // Backing fields for shared properties
        private string _timeInput = "";
        private bool _useWallClockTimeOfDay = false;
        private bool _anchorDisplay = false;
        private string _status = "";
        private bool _isRaceRunning = false;
        private bool _raceHasStartedSinceReset = false;
        private bool _sentClearCommand = false;

        // Shared properties
        public string TimeInput
        {
            get => _timeInput;
            set
            {
                if (SetProperty(ref _timeInput, value))
                {
                    OnPropertyChanged(nameof(DisplayTime));
                }
            }
        }

        public bool IsConnected => _serialPortService.IsConnected;

        public string? ConnectedPortName => _serialPortService.ConnectedPortName;

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool AnchorDisplay
        {
            get => _anchorDisplay;
            set
            {
                if (SetProperty(ref _anchorDisplay, value))
                {
                }
            }
        }

        public bool UseWallClockTimeOfDay
        {
            get => _useWallClockTimeOfDay;
            set
            {
                if (SetProperty(ref _useWallClockTimeOfDay, value))
                {
                    if (value)
                    {
                        DisplayMode = DisplayMode.HHMMSS;
                        // Set ShowSimulatorPunctuation to true when wallclock is enabled
                        if (!ShowSimulatorPunctuation)
                        {
                            ShowSimulatorPunctuation = true;
                        }
                    }
                    else
                    {
                        // If wallclock is disabled and ShowSimulatorPunctuation is true, set to MMSSDD
                        if (ShowSimulatorPunctuation)
                        {
                            DisplayMode = DisplayMode.MMSSDD;
                        }
                    }

                    OnPropertyChanged(nameof(DisplayTime));
                    OnPropertyChanged(nameof(DisplayModeLabel));
                    OnPropertyChanged(nameof(IsDisplayModeHHMMSS));
                }
            }
        }

        public bool IsRaceRunning
        {
            get => _isRaceRunning;
            set
            {
                if (SetProperty(ref _isRaceRunning, value))
                {
                    OnPropertyChanged(nameof(DisplayTime));
                }
            }
        }

        public bool RaceHasStartedSinceReset
        {
            get => _raceHasStartedSinceReset;
            set => SetProperty(ref _raceHasStartedSinceReset, value);
        }

        public Task SendRawAsync(byte[] payload)
        {
            // Check if this is a clear command (STX + B + EOT/ETX)
            // The payload contains the full frame including STX and EOT/ETX
            if (payload.Length >= 3 && payload[0] == 0x02 && payload[1] == 0x42)
            {
                _sentClearCommand = true;
            }

            return _serialPortService.SendAsync(payload);
        }
    }
}
