<p align="center">
  <img src="MusicPlayer.jpg?raw=true" />
</p>

## Music Player NAudio (v2.0)

A Windows music player written in C# (WinForms, .NET 6) on top of [NAudio](https://github.com/naudio/NAudio).
It plays **mp3** and **wav** files, has a 10 band equalizer, synced lyrics, album cover animation,
selectable color themes and Discord Rich Presence.

[Youtube video](https://youtu.be/GKVbbT69IkI) (older version).

## Features

### Player
- Smooth fade in / fade out on Play and Pause (no clicks, no "blip" after pausing).
- Click-free seeking: click or drag the progress bar, the position changes once on release.
- Automatically continues with the next song; at the end of the playlist it stops and goes back to the start of the song.
- Volume slider (dB scale, mouse wheel works too, click the speaker icon to mute) and balance (double click = centre).
- Stereo level meter (L / R) with peak hold.
- Taskbar button shows the progress: green while playing, yellow when paused, plus a play / pause overlay icon.
- Files with uncommon sample rates are converted to 44.1 kHz automatically (see *Sample Rate Conversion* below).

### Playlist
- Every song is shown with its album cover (loaded in the background), artist and length; the current song is highlighted.
- Add files with the **+** button or drag & drop them from Explorer onto the list.
- Reorder songs with drag & drop inside the list.
- Search in the playlist (Enter = next match, Esc = clear).
- The playlist is saved automatically after every change.
- Missing songs (moved / deleted, e.g. an unplugged drive) do not crash the player: they are greyed out and you are asked whether to remove them.

### Now playing
- Large album cover with a slow "Ken Burns" zoom and pan (only when the song has its own cover; otherwise a theme coloured default cover is shown).
- Title, artist and bitrate of the current song.

### Lyrics
- Synced lyrics are downloaded automatically from [LRCLIB](https://lrclib.net) (free, no API key). The current line is highlighted and follows the playback exactly, also after seeking and pausing.
- If only plain lyrics exist, they are shown with an estimated position ("not synced").
- Smart search: the artist and title are taken from the tags or the file name and cleaned automatically
  (e.g. `(Official Video)`, `(Lyrics)`, `| Easy Lyrics ...`, `(BassBoosted) 35_39_50hz`, track numbers, uploader channel names in the artist tag).
  Several variants are tried before giving up.
- When several uploads exist, the one whose timing most others agree with is used.
- Manual search: correct the **Artist** / **Title** fields under the lyrics and press **Search lyrics** (or Enter).
- Timing correction: the **◀ / ▶** buttons in the top right corner of the lyrics panel move the lyrics earlier / later
  by 0.5 s (Ctrl: 0.1 s); click the value to reset. The correction is saved for the song.
- Lyrics are cached in the `LyricsCache` folder next to the exe (`.lrc` files can be edited by hand).

### Equalizer
- 10 bands (31 Hz - 16 kHz) on their own page: drag a band up / down for the gain, left / right for the bandwidth.
- **Reset to 0** and **Save EQ**. Changes are kept when the song changes, even before they are saved.

### Appearance
- Fixed size window with a sidebar: **Player**, **Equalizer** and **Appearance** pages.
- 8 color themes (Midnight purple, Ocean blue, Graphite, Bordeaux, Emerald, Sunset, Deep teal, Black); the choice is remembered.
- All message and question boxes use the theme too.

### Closing
- The player asks before closing **only when something is not saved** (EQ settings, or a playlist that could not be saved):
  *Yes* saves and closes, *No* closes without saving, *Cancel* keeps the player open.
- No question on Windows shutdown / log off.

## Building

Requirements: Windows 10/11 and the [.NET SDK](https://dotnet.microsoft.com/download) (6.0 or newer).
Visual Studio is not needed, use `build.cmd` in the `MusicPlayer` folder:

| Command | Result |
|---|---|
| `build.cmd` | Debug build |
| `build.cmd release` | Release build |
| `build.cmd run` | Debug build, then start the player |
| `build.cmd clean` | Delete the build output |
| `build.cmd publish` | **Self-contained release**: one `publish\MusicPlayer.exe` with the whole .NET runtime inside |
| `build.cmd publish run` | The same, then start the published exe |

`publish.cmd` does the same as `build.cmd publish` with a double click.

The published `MusicPlayer.exe` (about 73 MB) can be copied to any 64-bit Windows 10/11 PC and runs **without .NET installed**.
The first publish needs internet access (the .NET packages are downloaded once).
Set `SHOWWARN=1` before calling `build.cmd` to see compiler warnings too.

### Files next to the exe

The player keeps its data next to `MusicPlayer.exe` (they are created when needed):

| File / folder | Content |
|---|---|
| `myPlaylist.txt` | The playlist (one file path per line) |
| `eqsettings.txt` | Saved equalizer settings |
| `theme.txt` | Selected color theme |
| `LyricsCache\` | Downloaded lyrics and timing corrections |
| `Discord.txt` | Discord Client ID (optional, see below) |

To move your settings to another PC, copy these next to the exe there.

## Changelog

### v2.0

- New user interface: sidebar with pages, playlist with album covers and search, "now playing" panel, themed dialogs, 8 color themes, version number.
- Synced lyrics from LRCLIB with smart search, timing correction and cache.
- Smooth Play / Pause fades and click-free seeking.
- Drag & drop reordering of the playlist.
- Taskbar progress and play / pause overlay.
- Ken Burns animation of the album cover.
- Missing songs and a missing `Discord.txt` no longer crash the player.
- Song end is detected from the audio itself, so converted (resampled) files continue with the next song too, and the end of a song is not cut off.
- Fixed: the player could get stuck after the last song of the playlist; unsaved EQ changes were lost on song change; the playlist was not saved on a fresh install; `eqsettings.txt` depended on the Windows number format.
- `build.cmd` / `publish.cmd` for building without Visual Studio, including a self-contained single-exe release.

### Discord Rich Presence

- Shares the currently playing track's title, artist, and duration on your Discord profile.
- Automatically updates when you switch tracks.
- Optional: without a `Discord.txt` (or without a valid ID in it) the player simply runs without Discord status.

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
E.g.: `DiscordClientID:0000000000000000000`
<p align="center">
  <img src="DiscordRPC2.png?raw=true" />
</p>

### Form Close

Safe application closure with confirmation.

- Since v2.0 the question appears only when there are unsaved changes (see *Closing* above).
- If the user clicks "Cancel", the application continues running.
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
