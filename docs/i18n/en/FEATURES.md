# Rurimecha features

Rurimecha (Ruri) organizes and plays your own audio, ASMR, and video collection. Run a Linux x86-64, Docker x86-64, or Windows x64 server, then connect with a browser or the separately installed Android app.

## Interface languages

- Software and website support Simplified Chinese, Traditional Chinese, Japanese, and English, with manual selection or system language matching. Account choices sync after sign-in; anonymous choices stay on the device. Metadata, audio, and subtitle languages remain independent.
- Installers currently use Simplified Chinese. Management and CLI tools primarily use English. Historical release notes are maintained in Chinese and English.

## Organizing your library

- Use the refresh menu on a folder or work to update details or images, read media information, scan, or organize files separately. You can choose the language and keep manual edits when updating details. Scanning and media inspection are available for local sources. If a work has no folder of its own and draws files from multiple local sources, choose the source to inspect.
- File-type filters stay during continuous browsing and temporary page changes within one source; another source or work starts with default file types. Filters can be cleared or saved explicitly.
- Choose All files in a work to browse every playback group’s files and attachments. Select a filename to read text, zoom in on an image, or page through a PDF.
- Delete individual works while keeping originals and ignoring their own files and ordinary subfolders; independent child works remain. Review and lift deleted-work and folder exclusions in Settings. Cleared information and references are not restored.
- Browse public voice actor profiles and credits, open library copies or search the built-in ASMR.one source. Local voice credits and name management remain available. Circles and illustrators retain library browsing and external discovery.
- With an active server license, administrators can turn on **Smart routing** in **Network settings** to try more connections when accessing DLsite or ASMR Online. Availability depends on the server network and region. Turning it off keeps previously saved information.
- Browse original media folders or organize files into works. Enable scanning, archiving, metadata fetching, and media information extraction independently, and adjust work membership manually.
- Includes a permanent ASMR.one source for browsing and adding works, with editable proxy and download folder. It can coexist with user-added sources at the same address; voice actor browsing uses the built-in source. Browse server folders or enter a path. Saving the setting checks that the server can write there.
- Add kikoeru-compatible libraries, search works, browse/filter circles, tags, and voice actors, and save selected works or files to your server as needed.
- Save compatible-source works to the server by selecting folders, files or file types in a tree. Keep folder structure, skip complete copies, and check progress, stop or retry unfinished files.
- Search titles, voice actors, circles, tags, or work numbers. Partial RJ numbers show suggestions; title-only search narrows the scope.
- Fetch RJ work details from DLsite or edit titles, descriptions, and images yourself. Keep Simplified Chinese, Traditional Chinese, Japanese, and English details together, viewing/updating each independently.
- Browse linked originals and language editions together, select a folder for duplicate copies, prefer an original or language edition, and read descriptions in another language while listening.
- Select and copy titles, paths, and work details.
- For a Kikoeru-compatible source, enter its address and optional proxy, then select Test connection before saving. Existing sources also offer this check. Success means the library can be browsed; it does not measure audio download speed.
- For ASMR.one audio that has not been fully saved to the server, choose Smooth playback first or Quality first in the player. Smooth playback first uses a smaller version when available; Quality first plays the selected original file. Switching keeps your playback position. Completed server downloads always play the local original, and downloads are never replaced with the smaller version. The next track only starts preloading after the current track is fully buffered.

## AI translation and archive suggestions

- With an active server license, administrators can choose a local or remote gateway in **AI configuration**. With a local gateway, add and test provider models. With a remote gateway, connect using its address and access token. Choose a model for each feature you use.
- In the Android local library, open **AI configuration**. Choose the local gateway to add a provider, enter its key, find models, and test the connection. To use a remote gateway, choose it and enter the connection details. Ruri remembers the model for each gateway. After saving, start translating from a work’s details. Cloud models still require internet access on the phone.
- Translate scripts, work details, and folder or file display names, or generate archive suggestions. Translate a whole folder or work by choosing a target language and whether to skip existing names. Switch between original names and translations in the current interface language with one button.

## Playback, resuming, and collecting

- Play from works/folders, choosing available editions and formats such as sound effects, MP3, or WAV.
- Audio and video prefer matching subtitles in their own folder and also offer matches from other folders in the same work and source. Manual subtitle selections and Off choices are saved per file to your account.
- Follow subtitles and transcripts, set a sleep timer, resume progress, and organize collections/playlists. Administrators can create, edit, and convert transcripts.
- Browse collections by work cards or lists. Individual audio/video files and clips are grouped in Soundbox. Smart discovery uses the same organization and saves directly as a collection.
- Playlists preserve exact tracks, duplicates, and order. Start at any item and continue through the full list, play one item or search results, shuffle, or add following items to the queue. Older custom collections become playlists on upgrade.
- Audio pages, bottom controls, transcript windows, and local playback provide 10-second back/forward buttons; seeking while paused keeps playback paused.
- Pause while media loads or buffers. Open work details from a work name on the player page; screen readers announce the current time and total duration on the progress bar.
- Resize the transcript on audio pages. On larger web windows, keep the transcript open while browsing elsewhere. Android floating subtitles stay visible inside Ruri and over other apps, return after unlocking, and reveal a close button when tapped.
- Open Rurimecha from Start or choose to add a desktop shortcut during installation. Windows and Linux installers let you choose and check the listen IP and port. Windows keeps application data by default on uninstall. You can opt to clear it while keeping original media.
- Check versions and updates under **Settings → Version and updates**. Android can also check app updates from the connection screen. Follow the host installation method to update Linux or Docker.
- With an active license on the current server or phone, preview and save favorite ranges for replay or lists. Clips reference originals and need them to remain available.
- Linux, Windows, and Docker include media tools for loudness normalization, compatible transcoding, and embedded subtitles. See [media tools](USAGE.md#preparing-media-tools).
- Other WebDAV players can browse folders and play originals using Ruri credentials. Access is read-only. Configure certificates, separate HTTP/HTTPS ports, and optional HTTPS redirects.

### Video quality and transcoding limits

Open Player settings → Video quality while watching a video. Supported videos offer resolution and bitrate choices. When you switch, playback buffers and resumes at the current position. This applies to local sources and readable kikoeru-compatible videos. Videos that offer only Original format play if your device supports that format. Compatible playback in Auto uses a preset quality. If your connection slows down, choose a lower quality manually.

## Mobile listening and privacy

- On Android, the home page opens **Library on this device** or **Connect to a server** directly. Add a folder on the phone to browse and play files by folder or work. If the server is unreachable, discover another LAN server or enter an address on the server selection screen. Use background playback, media keys, full-screen video, and queue restoration.
- In the Android local library, refresh folders and works, read media information, and view background tasks. Browse ASMR.one or add a Kikoeru-compatible source and choose files to download to your phone.
- Android playback settings can measure and normalize each audio track, create compatible copies using supported device formats, and extract full embedded text tracks as WebVTT.
- Sign in to a server to download originals, text, covers, and available text subtitles. Check progress under **Download tasks**. Play earlier and new downloads from the **Downloaded media** source in **Library on this device**. Use **Download location** to choose a folder for new downloads. Existing files stay where they are.
- Privacy mode hides covers, videos, and text, with a 3-second reveal. Android asks before speaker playback and pauses when headphones/Bluetooth disconnect.
- Delete individual history records or clear your own history, including resume positions.

## Shared use and moving devices

- Initial setup requires a random default administrator password. Windows desktop fills it in automatically; browsers show Windows, Linux, and Docker instructions. Choose your own password to enter the library; the default then expires.
- During testing, enter your own email on the website to receive a free trial automatically, once per address. Activate within 7 days and use it for 31 days after first activation. Retry later or look up an existing license if the email does not arrive.
- With an active server license, administrators can add accounts in **User management** and other accounts can sign in. Users share media and keep separate collections, playlists, preferences, and progress.
- Configure LAN ranges and per-user remote access. Nginx can provide HTTPS over an HTTP server; same-host Nginx and default Docker bridge deployment need no Trusted proxies entry.
- Inspect active device names, client types, sign-in IPs/times, and revoke unused sessions.
- Export system data, all work details, or one work; restore with new media paths. Missing media can retain details until files return and are scanned again.
- With an active server license, enable guests for password-free demo browsing and playback. Guests cannot save personal content; disabling ends guest access.
- Administrators can review problems and operation results, and enable detailed diagnostics in Settings while investigating an issue.
- Finished tasks and runtime logs are kept for up to 48 hours. After completion, details show unfinished items for retry; successes remain in the totals. Once a record expires, start again from its work or source. Library details, favorites, listening progress and subtitle drafts remain available.

## Listening statistics and recommendations

- Open Listening & recommendations from the sidebar or account menu. Listening statistics shows your time, calendar, monthly trends, and leading works, voices, circles and tags. Choose 30 days, 12 months, a year or all time. The phone’s local library keeps its own listening statistics. Connect to a server to view that server’s statistics.
- In work details, select My rating and choose one to five stars. Open it again to change or remove your rating. Your account rates the whole archived work across its formats and playback groups. Cards show your rating separately from the public rating.
- For you offers You might like, Try something different and Listen again. Open a work or play it directly, and use Refresh picks to change the selection. Deleting a resume record keeps your statistics and ratings.

## Scope and limits

- Server packages support Linux/Windows x64 and Docker Linux x86-64; no ARM package is provided.
- Compatible sources can play files provided by the source. Available video quality, transcoding, embedded subtitle and volume normalization options depend on the file and the media tools available to the server.
- WebDAV has no upload, rename, delete, or progress sync. Reverse proxies need a dedicated domain root; subpaths are unsupported.
- Web sessions show the browser and operating system. Use these alongside the sign-in time to recognize your sessions.

[Installation and user guide](USAGE.md)
