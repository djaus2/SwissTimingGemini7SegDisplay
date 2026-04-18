# Swiss Timing 6 Digit (or 3 + 6 Digit) 7 Segment Display

<table style="border-collapse: collapse; border: none;">
  <tr style="border: none;">
  <td style="border: none; padding: 0;">
  <img width="200" alt="PXL_20260417_052157460" src="https://github.com/user-attachments/assets/1b730f14-3dcb-44e5-972a-ce1c5c4894e1" />
  </td>
  <td style="border: none; padding: 0;">
<h2>Version: 1.0.0</h2>
</td>
</tr>
</table>

> **Status**  
> - 6 Digit Gemini display (in vitro) works in race mode as well clear function
>   - Using Turchel Null Modem cable.
>   - INT31 -> Display In Port
>   - Required mains power.
> - Previous
>   - Null modem cable across  INT131 enables sent time to be displayed in Simulated 7 seg display, with/without colon and dot between digits.
>   - 6 Simulated Digit diplay implemented.  
>   - 3 + 6 digit display option now works in Simulator.
>   - Tuchel Null Modem cable works with INT31 and Simulated Display
> - Next: (In situ)Test with actual display in field

<img width="670" height="466" alt="image" src="https://github.com/user-attachments/assets/7127c5b8-eca1-4074-b8d7-c89a84dcb5c2" />



 <img width="800"  alt="image" src="https://media.giphy.com/media/vkdTVE9bwFJqjR9y7t/giphy.gif" />



## About
This is a WPF app that can generate display timing messages for a Gemini 6 Digit 7 segment display, functionimg for example, as a running clock or elapsed race time for Athletics events. Messages are sent via the selected Serial-Send port.

Times (wallclcok or race running time) are also displayed in a simulated 6 x 7 Segement display in-app. When the Serial-Receive port is not connected, that display displays the transmitted time.

The capability for receiving messages as sent and displaying the received times has also been added but not yet tested. To enable serial reception,the Serial-Receive port is connected and as such the received data is displayed in the 7 segments rather than the transmitted data. In this mode,the app can simulate the physical display.

> Note that it is intended that the app can simultaneously send and receive timing messages

## Latest
- There is now a **Cosmetic** state variable that if true, the app adds appropiate colon/s and dot between digits in the simulated display for the selected time format. 
  - If Receive port not connected then Cosmetic checkbox does not show.
    - 7 Segement display shows the Sent data (Time out) directly.
    - Colon/s and dot show as per the Sent Data format
  - If Receive port is connected then Cosmetic checkbox shows
    - If not selected then no colons/dot show on the receive display. 
      - HHMMSS/MMSSDD checkbox does not display. 
    - If selected then HHMMSS/MMSSDD checkbox shows which determines the displayed time format:
      - HHMMSS/MMSSDD selection determines what separators show on the receive display.
      - Note that selecting Wallclock triggers this to select HHMMSS. 
- Selected ports are now persisted as well as other app settings
- 3 + 6 Digit display now works.
  - 3 left most digits can display Bib Number, Event No or Lap Count etc. 
  - Note that the 3 digit display is not intended for use with the Gemini 6 Digit Gemini display.
- Added [Lap]/[Continue] button that captures race elapsed time whilst clock continues in background. Can continue.
  - Also have lap up and down count options
- If both persisted COM ports exist autoconnect.
- Clear timing button added and resolved.

## Hint
For testing using one INT31:
- Connected INT131 to the PC USB port
- Connected a standard null modem cable between the 2 INT131 RS232 (DB9) ports. 

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

