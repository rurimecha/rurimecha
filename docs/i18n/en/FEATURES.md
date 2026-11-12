# Rurimecha features

Rurimecha (Ruri) organizes and plays your own audio, ASMR, and video collection. Run a Linux x86-64, Docker x86-64, or Windows x64 server, then connect with a browser or the separately installed Android app.

## Interface languages

- Software and website support Simplified Chinese, Traditional Chinese, Japanese, and English, with manual selection or system language matching. Account choices sync after sign-in; anonymous choices stay on the device. Metadata, audio, and subtitle languages remain independent.
- Installers currently use Simplified Chinese. Management and CLI tools primarily use English. Historical release notes are maintained in Chinese and English.

## Organizing your library

- Browse original media folders or organize files into works. Enable scanning, archiving, metadata fetching, and media information extraction independently, and adjust work membership manually.
- Add kikoeru-compatible libraries, search works, browse/filter circles, tags, and voice actors, and save selected works or files to your server as needed.
- Search titles, voice actors, circles, tags, or work numbers. Partial RJ numbers show suggestions; title-only search narrows the scope.
- Fetch RJ work details from DLsite or edit titles, descriptions, and images yourself. Keep Simplified Chinese, Traditional Chinese, Japanese, and English details together, viewing/updating each independently.
- Browse linked originals and language editions together, select a folder for duplicate copies, prefer an original or language edition, and read descriptions in another language while listening.
- Select and copy titles, paths, and work details.

## Playback, resuming, and collecting

- Play from works/folders, choosing available editions and formats such as sound effects, MP3, or WAV.
- Follow subtitles and transcripts, set a sleep timer, resume progress, and organize collections/playlists. Administrators can create, edit, and convert transcripts.
- Browse collections by work cards or lists. Individual audio/video files and clips are grouped in Soundbox. Smart discovery uses the same organization and saves directly as a collection.
- Playlists preserve exact tracks, duplicates, and order. Older custom collections become playlists on upgrade.
- Audio pages, bottom controls, transcript windows, and local playback provide 10-second back/forward buttons; seeking while paused keeps playback paused.
- Resize the transcript on audio pages. On larger web windows, keep the transcript open while browsing elsewhere. Android floating subtitles remain visible over other apps.
- Open Windows from Start and install updates from Ruri → Check for updates. Windows/Linux installers select and validate listen IP/port. Windows uninstall can retain data or explicitly clear it while keeping original media.
- With a server license, preview and save favorite ranges for replay or lists. Clips reference originals and need them to remain available.
- Linux, Windows, and Docker include media tools for loudness normalization, compatible transcoding, and embedded subtitles. See [media tools](USAGE.md#preparing-media-tools).
- Other WebDAV players can browse folders and play originals using Ruri credentials. Access is read-only. Configure certificates, separate HTTP/HTTPS ports, and optional HTTPS redirects.

## Mobile listening and privacy

- Discover LAN servers or enter an address in Android. Use background playback, media keys, full-screen video, and queue restoration.
- With a licensed server, download originals, text, covers, and available text subtitles. Completed downloads play offline in Local library; downloads, queues, favorites, and progress stay on the phone.
- Privacy mode hides covers, videos, and text, with a 3-second reveal. Android asks before speaker playback and pauses when headphones/Bluetooth disconnect.
- Delete individual history records or clear your own history, including resume positions.

## Shared use and moving devices

- The built-in administrator uses the web app/WebDAV. A server license enables extra accounts with shared media but separate collections, playlists, preferences, and progress.
- Configure LAN ranges and per-user remote access. Nginx can provide HTTPS over an HTTP server; same-host Nginx and default Docker bridge deployment need no Trusted proxies entry.
- Inspect active device names, client types, sign-in IPs/times, and revoke unused sessions.
- Export system data, all work details, or one work; restore with new media paths. Missing media can retain details until files return and are scanned again.
- Enable/disable guests for password-free demo browsing/playback. Guests cannot save personal content; disabling ends guest access.

## Scope and limits

- Server packages support Linux/Windows x64 and Docker Linux x86-64; no ARM package is provided.
- Compatible sources play provided originals. Local conversion, embedded subtitles, and normalization depend on formats and available tools.
- WebDAV has no upload, rename, delete, or progress sync. Reverse proxies need a dedicated domain root; subpaths are unsupported.
- Browsers cannot read a computer's real hostname. Session names help identify sign-ins but do not prove device identity.

[Installation and user guide](USAGE.md)
