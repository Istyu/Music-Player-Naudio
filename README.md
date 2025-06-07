<p align="center">
  <img src="MusicPlayer.jpg?raw=true" />
</p>

## Music Player Naudio

This is a music player with 10 channel equalizer. 
With the EQ can be controlled the Gain and Bandwidth. 
There is an option saving the EQ settings and playlist. 
Only mp3 and wav files can play. 
There is an option open mp3 or wav files via selection window or drag and drop to the music list. The output can be 
monitored on the VU meter. 
There is option controlling the pan.

[Youtube video](https://youtu.be/GKVbbT69IkI).


## Changelog

### Discord Rich Presence

- Shares the currently playing track's title, artist, and duration on your Discord profile.
- Automatically updates when you switch tracks.

**Setup Guide**

1. Prepare the application on the Discord Developer Portal

- Open the [Discord Developer Portal](https://discord.com/developers/applications.)
- Log in with your Discord account.
- Click on **New Application**, then enter the application's name (e.g., Music Player) and icon.
- In the left menu, go to the **Rich Presence** section and fill in the required details (icon, description, etc.).
<p align="center">
  <img src="DiscordRPC1.png?raw=true" />
</p>

2. Obtain the application's Client ID

- After creating the application, go to the **General Information** page and copy the Application (Client) ID. You will need this for configuring the music player application.
- Add the generated Client ID to the Discord.txt file in the following format: `DiscordClientID:[your_discord_client_id]`
E.g.: `DiscordClientID:1234567891011121314`
<p align="center">
  <img src="DiscordRPC2.png?raw=true" />
</p>

### Form Close

Safe application closure with confirmation.
The latest version introduces a confirmation dialog before exiting, preventing accidental closure.

- If the user clicks "No", the application continues running.
- If the application is closed, resources (such as the audio handler and Discord RPC client) are properly released.
- Error handling has been added to manage potential exit-related issues.

This ensures more stable operation and prevents data loss.

### Sample Rate Conversion

Added Sample Rate Conversion.
The new version now supports sample rate conversion for files that do not use the standard 44,100 Hz or 48,000 Hz sample rates.

- Automatic resampling occurs if an MP3 or WAV file has an unsupported sample rate.
- Improves compatibility, especially for audio files with lower or uncommon sample rates.
- Implemented using the NAudio library to ensure optimal audio quality.
- Error handling is included to manage potential file processing errors.

This makes the application more stable and flexible when handling different audio formats.
Variable sample rate tracks are still not supported.

## Installed packages

- DiscordRichPresence 1.3.0.28
- NAudio 2.1.0
- NAudio.Core 2.1.0
- Newtonsoft.Json 13.0.3
- TagLibSharp 2.3.0
