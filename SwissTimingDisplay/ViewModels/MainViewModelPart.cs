using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using SwissTimingDisplay.Models;
using SwissTimingDisplay.ViewModels;

namespace SwissTimingDisplay.ViewModels
{
    public sealed partial class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private static class WindGauge
        {
            public const int AcquisitionDurationMin = 0;
            public const int AcquisitionDurationMax = 30;
            public const int AcquisitionDurationDefault = 10;
            public static  int duration { get; set; } = AcquisitionDurationDefault;

            public static int WindSpeed { get; set; } = 0;

        }
        private void ProcessReceivedWindGaugeFrame(TcpCommand tcpCommand, List<char> chars)
        {
            switch (tcpCommand)
            {
                case TcpCommand.WindGauge_Acquisition_Duration:
                    // CWI Handle acquisition duration update if needed
                    int duration = 10;
                    if (chars?.Count == 2)
                    {
                        int durationTens = (byte)chars[0] - (byte)'0';
                        int durationUnits = (byte)chars[1] - (byte)'0';
                        if ((durationTens >= 0) &&
                            (durationTens < 3) &&
                            (durationUnits >= 0) &&
                            (durationTens < 10))
                        {
                            duration = durationTens * 10 + durationUnits;
                        }
                    }
                    WindGaugeSetAquisitionDuration(duration);
                    break;
                case TcpCommand.WindGauge_Start_of_Measurement:
                    // CWS Handle start of measurement if needed
                    WindGaugeStartMeasurement();
                    break;
                case TcpCommand.WindGauge_Reset_Stop_Clear:
                    // CWR Handle Reset(stop acquisition, clear the scoreboard)
                    WindGaugeResetStopClear();
                    break;
                case TcpCommand.WindGauge_Resend_Latest:
                    // CWO Handle resend latest if needed
                    WindGaugeResendLatest();
                    break;

           
            }
        }

        private void WindGaugeSetAquisitionDuration(int duration)
        {
            WindGauge.duration = duration;
        }

        private void WindGaugeResendLatest()
        {          
            SendResult();
        }

        private void WindGaugeResetStopClear()
        {
            WindGauge.WindSpeed = 0;
            throw new NotImplementedException();
        }

        private void WindGaugeStartMeasurement()
        {
            throw new NotImplementedException();
        }

        private void SendResult()
        {
            float speed = WindGauge.WindSpeed;
            // Got to send that back to the device, but we don't have the code for that yet, so let's just throw for now
            throw new NotImplementedException();    
        }
    }
}
