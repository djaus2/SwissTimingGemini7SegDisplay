# Swiss Timing 6 Digit 7 Segment Display

> Nb: Under development  
> Latest: Null modem cable across 2 INT31 enables sent time to be displayed in 7 seg display, without : and .  
> Next: Test with actual display.

## About
This is a WPF app that can generate display timing messages for a Gemini 6 Digit 7 segment display, functionimg for example, as a running clock or elapsed race time for Athletics events. Messages are sent via the selected Serial-Send port.

Times (wallclcok or race running time) are also displayed in a simulated 6 x 7 Segement display in-app. When the Serial-Receive port is not connected, that display displays the transmitted time.

The capability for receiving messages as sent and displaying the received times has also been added but not yet tested. To enable serial reception,the Serial-Receive port is connected and as such the received data is displayed in the 7 segments rather than the transmitted data. In this mode,the app can simulate the physical display.

> Note that it is intended that the app can simultaneously send and receive timing messages


## Related Documents
- [INT31 Manual](SwissTimingDisplay/docs/INT131.pdf)
- [Gemini 6 Digit  7 Segment Display Manual](SwissTimingDisplay/docs/SwissTimingGeminiDisplay.pdf)
- [MVAC Track Setup](SwissTimingDisplay/docs/MVACDisplayandWindGaugeCabling.pdf)

## The App


<img width="1632" height="1210" alt="image" src="https://github.com/user-attachments/assets/e6c3addc-868c-45ad-afe9-b43f98196cb7" />  

**_The app displaying sent wallclock time_** 

## RollerMode Commands
<img width="1584" height="918" alt="image" src="https://github.com/user-attachments/assets/283229b8-4c18-467b-b261-169f04f8c451" />


Also see [Models/CharCommand.cs](SwissTimingDisplay/Models/CharCommand.cs)

