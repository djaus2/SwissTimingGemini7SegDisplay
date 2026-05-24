namespace SwissTimingDisplay.Models
{
    public enum TcpCommand
    {
        // GeneralClear,
        // TimeOfDayOrRunningTime,
        // RunningTimeDifference,
        // NumberAndNetTime,
        Roller_Time_Mode_Clear,
        Roller_Time_of_Day_or_Running_Time_,
        //Mistral Wind Gauge TCP Commands:
        WindGauge_Acquisition_Duration,
        WindGauge_Start_of_Measurement,
        WindGauge_Reset_Stop_Clear,
        WindGauge_Resend_Latest,
        WindGauge_Reset_CaptureTime,
        WindGauge_Output,
        WindGauge_ReadFrequency
    }
}
