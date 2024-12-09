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
3.  Edit the json config file:
    - Under `WebhookSettings` set the `Port` value to whatever you want, just be sure that it matches where the external service is sending the webhook
    - Under `RainmeterSettings` set the path to your `Rainmeter.exe file`. (There should be two slashes when using a backslash.)
    - Under the `Commands` section, set up one or more groups of commands (see "Command Configuration" instructions below)

## Command Configuration

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
