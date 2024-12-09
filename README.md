# Rainmeter Webhook Monitor

This lightweight background app monitors for incoming webhook messages to a specified port and configured parameter names, and sends a command to Rainmeter with that info.

### Purpose

To update the text of Rainmeter skins with info from a webhook message.

## Features

- Fully headless, but with optional system tray icon
- Uses a json config file to allow multiple sets of possible commands
- Each command set can be triggered by a different parameter name in the webhook
- Optional debug mode which shows a console with debug output

## How to Download

- Go to the [Releases page](https://github.com/ThioJoe/Rainmeter-Webhook-Monitor/releases) (link also found on the right side of the page)
- For the latest release, look under "Assets" and click to download exe file

## Usage

1.  Place the exe in the location of your choice, probably along with your Rainmeter skins
2.  Run it once, it will create the json config file with a couple example commands
3.  Set whatever app to send the webhook to the `[Whatever-IP-Address]/rainmeter` URL on any port you choose. For example: `http://127.0.0.1:9999/rainmeter`
4.  Edit the json config file:
    - Under `WebhookSettings` set the `Port` value to whatever you want, just be sure that it matches where the external service is sending the webhook
    - Under `RainmeterSettings` set the path to your `Rainmeter.exe file`. (There should be two slashes when using a backslash.)
    - Under the `Commands` section, set up one or more groups of commands (see "Command Configuration" instructions below)

## Command Configuration

The `Commands` section of the config file is a list of groups of settings, each group between curly braces `{ }`. Each group represents a separate command that can be triggered by a webhook and its settings. The following properties are available for each command object:

- `WebhookParameterToUseAsValue`: This is the name of the parameter in the webhook message that will be used as the value for the Rainmeter command. For example, if your webhook message contains a parameter called "temperature", you would set this property to "temperature".
- `BangCommand`: This is the Rainmeter bang command that will be executed. The program is intended to be used with `SetOption` and `SetVariable` but theoretically should work with any bang command.
- `MeasureName`: If you are using the "SetOption" bang command, this is the name of the measure that you want to set the option for. (Appears as 1st argument after the bang command)
- `OptionName`: If you are using the "SetOption" bang command, this is the name of the option that you want to set. (Appears as 2nd argument after the bang command)
- `SkinConfigName`: This is the name of the Rainmeter skin config file that contains the measure or variable that you want to update. (Appears as 3rd argument after bang command)

You can have multiple command objects in the `Commands` array. Each object will be triggered by a different parameter in the webhook message, as specified in the `WebhookParameterToUseAsValue` value in the group.

## Real Configuration Example

- I have the following commands configuration in my json file which I use to receive the color temperature value from the F.lux program:

```json
        "Commands": [
            {
                "WebhookParameterToUseAsValue": "ct",
                "BangCommand": "SetOption",
                "MeasureName": "MeasureColorTemp",
                "OptionName": "String",
                "SkinConfigName": "ShowFluxTemp",
                
            }
```

- In the F.lux program the output settings look like this. It's not visible here but the Flux program sends the color temperature value as a parameter called `ct` in its webhook request, which corresponds to the `WebhookParameterToUseAsValue` setting in the json.

<img width="500" alt="image" src="https://github.com/user-attachments/assets/c2536af4-8826-4d83-beea-7c0f7620181e">



- My skin is called `ShowFluxTemp` and which uses `ShowFluxTemp.ini`. This corresponds to the `SkinConfigName` setting in the json. In the Rainmeter skin's `.ini` file there is this measure section. Notice how this corresponds to the `MeasureName` and `OptionName` settings in the json.
```ini
[MeasureColorTemp]
Measure=String
String=0000
```

- Then using that, here's an example of how the updated value is used in a display section:
```ini
[MeterDisplay]
Meter=String
MeasureName=MeasureColorTemp
Text="%1K"
```

( Here's my full .ini config file if you are curious: [ShowFluxTemp.ini](https://github.com/user-attachments/files/18065642/ShowFluxTemp.ini.txt) )


## Command Line Arguments

- /debug: Shows a console window with app activity output logging
- /template: Forces the creation of a new template json file (if one exists, it will rename the new one, not overwrite)

## How to Compile

### Instructions:

1.  Open the "Solution" file (`RainmeterWebhookMonitor.sln`) with Visual Studio 2022
    - It should allow you to install the necessary Nuget packages. There are only a small number of them. It uses .NET 8.0
    - The entire solution/project is included, so after opening it should be ready to compile and run after the packages are downloaded.
3.  Optional: Choose the build "configuration" mode (Either `Release` or `Debug`)
4.  Compile by going to `Build` (top menu) \> `Build Solution`, or if in Debug configuration, `Debug` \> `Start Debugging` (Or just click the toolbar button that says "Start" with the green triangle)
