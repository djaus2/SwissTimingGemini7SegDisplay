using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Diagnostics.Eventing.Reader;
using CommunityToolkit.Mvvm.ComponentModel;
using SwissTimingDisplay.Models;
using SwissTimingDisplay.Services;


namespace SwissTimingDisplay.ViewModels
{
    // These were added to MainViewModel for the Sprint page.
    public enum WindGaugeState
    {
        Ready,
        Acquiring,
        Acquired,
    }


    public enum Sprints
    {
        Distance100m,
        Distance200m,
        Distance300m,
        Distance400m,
        Distance100mHurdles,
        Distance110mHurdles,
        Distance200mHurdles,
        Distance300mHurdles,
        Distance400mHurdles,
        Other,
    }
    public sealed partial class MainViewModel
    {
        private WindGaugeState _siriccoState;
        public WindGaugeState SiriccoState
        {
            get => _siriccoState;
            set
            {
                if (SetProperty(ref _siriccoState, value))
                {
                    OnPropertyChanged(nameof(CanStartRace));
                }
            }
        }

        public bool CanStartRace =>
                !(SiriccoState == WindGaugeState.Acquiring && !IsRaceRunning && RaceHasStartedSinceReset);

        private void UpdateSprintDerivedProperties()
        {
            var metres = GetSprintDistanceInMetres(_sprint);
            ShowWindGaugeButton = _sprint == Sprints.Distance200m || _sprint == Sprints.Distance200mHurdles;
            ShowWindGauge = metres > 0 && metres <= 200;
        }

    }
}
