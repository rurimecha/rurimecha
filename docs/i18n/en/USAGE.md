# Rurimecha user guide

**Want to look around first?** Open the [demo](https://asmr.rurimecha.top/) and choose the guest experience on the sign-in page to browse and play demo works. Guest favorites and listening progress are not saved. Ruri does not provide audio works; add media folders you provide yourself.

**Choose how to get started:** Windows has an MSI installer and needs no command line. Follow the sections below for Linux or Docker. Android connects to an already running Ruri server. The built-in administrator can use the web app and WebDAV without a license; Android online playback, downloads, and saved clips need an active server license. Completed downloads can be played offline on the phone.

**Pricing:** Testing is free. Future pricing is provisionally CNY 10/month, CNY 60/year, or CNY 90 for a lifetime license, with up to 3 server bindings; clients such as Android do not use additional bindings. The final plan will be announced later. Existing free features will remain free. Current free trial licenses require an application and review, first activation within 7 days of issue, and last 31 days after first activation.

## Installation and first sign-in

Download the package for your platform from [GitHub Releases](https://github.com/rurimecha/rurimecha/releases) and verify its SHA-256. Servers support Linux x86-64, Docker Linux x86-64, and Windows x64. Install the Android APK separately and connect it to a running Ruri server. Your own computer can be the server; for a NAS or another computer, use its LAN address.

### Windows installation

1. Open the `.msi`, select Next, and choose the installation folder. Installation is for the current Windows user, requires no administrator privileges, and defaults to `%LOCALAPPDATA%\Programs\Rurimecha`. Each Windows user installs separately. If an older EXE version is installed, uninstall it in Windows Settings before installing the MSI; library data is kept.
2. Choose a listen IP and port. Defaults are `0.0.0.0` and `8080`, accepting connections on all local IPv4 addresses. Use `127.0.0.1` for access only from this computer. A specific IP must belong to this computer. If the availability check fails, free the port or choose another. Reinstallation and upgrades retain existing network settings by default.
3. Finish installation and open **Rurimecha** from Start. If needed, install Microsoft's [WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) and reopen.
4. Choose **Ruri → Open data folder**, open `setup-code` in Notepad, and copy the code to complete first-time setup.

Closing the window hides it in the tray while Ruri keeps running. Double-click the tray icon to reopen; right-click it and choose **Exit Ruri** to stop or update. Data stays in `%LOCALAPPDATA%\Rurimecha\data`, independently of the installation folder. See Connection and network for access from other devices.

### Linux installation

Extract the full x86-64 server package, open a terminal there, and run:

```sh
sudo ./install.sh
```

Enter the listen IP and port, or press Enter for `0.0.0.0:8080`. A failed address/port check leaves existing network settings intact. The installer starts Ruri and enables startup on boot. Open `http://SERVER-LAN-IP:8080/` from another computer; the server needs no graphical desktop. Retrieve the setup code with:

```sh
sudo journalctl -u rurimecha.service -g 'Ruri administrator setup code:' -n 1 --no-pager
```

Service installation requires systemd. Bundled media tools require glibc 2.35 or later, such as Ubuntu 22.04 or Debian 12. Data is in `/var/lib/rurimecha`. The `rurimecha` service user needs read access to media folders and their parents; do not make an entire media disk writable by everyone.

To choose settings explicitly:

```sh
sudo ./install.sh --listen-ip 192.168.1.20 --listen-port 18080
```

Use an IP the server owns. `0.0.0.0` means all IPv4 addresses; `127.0.0.1` is local only. Noninteractive new installations use defaults; existing installations retain settings unless arguments override them. Interactive upgrades can keep existing settings.

Without installing a service, run `./rurimecha-core -data-dir ./data` and leave the terminal open. The code appears there. Keep using the same data folder when upgrading.

### Docker installation

Use Docker and Docker Compose on an x86-64 Linux computer or NAS; this image does not run directly on ARM. A NAS container interface can use the same image, mounts, and ports.

1. Download [compose.yaml](https://raw.githubusercontent.com/rurimecha/rurimecha/main/packaging/docker/compose.yaml) and [reset-admin-password.sh](https://raw.githubusercontent.com/rurimecha/rurimecha/main/packaging/docker/reset-admin-password.sh) into one folder. Create `.env` beside them:

```dotenv
RURI_IMAGE=docker.io/rurimecha/rurimecha:VERSION
RURI_MEDIA_DIR="/srv/media/ASMR"
```

Replace `VERSION` with the release version, or use `latest` for the latest stable release. The media path must already exist on the Docker host. With Docker Desktop Linux containers, a Windows path can be `D:/ASMR`; allow Docker to access that drive. Add `/media/library` inside Ruri, rather than the host path. The sample mount is read-only; transcripts can use Save to Ruri without media write permission.

2. Start and read the setup code after `Ruri administrator setup code:`:

```sh
docker compose up -d
docker compose logs core
```

Open `http://127.0.0.1:8080/` on the Docker host. On a phone, `127.0.0.1` refers to the phone itself.

3. For other devices, first open Network settings. Keep the displayed current LAN ranges and add your home range on another line, for example `192.168.1.0/24` for addresses `192.168.1.x` with mask `255.255.255.0`. Docker's detected ranges may omit your home network. Keep the internal listen address at all IPv4 addresses; do not enter a host IP absent from the container. Then change the Compose port mapping, for example:

```yaml
ports:
  - "192.168.1.20:18080:8080"
```

Run `docker compose up -d` and connect using `http://192.168.1.20:18080/`. The previous localhost mapping no longer applies. Substitute your host's address and permit the port in its firewall. Mapping means **host address : external port : Ruri internal port**.

| Mapping | Address | Use |
| --- | --- | --- |
| `127.0.0.1:8080:8080` | `http://127.0.0.1:8080/` on the host | Local access or Nginx on the host |
| `192.168.1.20:18080:8080` | `http://192.168.1.20:18080/` | Other LAN devices |
| `8080:8080` | Port 8080 on all host addresses | Restrict access yourself |

To change only the external port, change the middle number and keep the final `8080`. Check with `docker compose ps`. See [Docker port publishing](https://docs.docker.com/engine/network/port-publishing/). Phones need the host's LAN IP and external port, not a `172.x.x.x` container address. Discovery may not cross Docker networking; enter the address manually if necessary.

4. Android online playback, extra accounts, and saved clips require server licensing and a host identifier. On Linux, verify `/etc/machine-id` exists and is nonempty, then add its read-only mount under the existing `core` volumes:

```yaml
volumes:
  - ruri-data:/data
  - ${RURI_MEDIA_DIR}:/media/library:ro
  - /etc/machine-id:/run/ruri-host/machine-id:ro
```

Keep the top-level `volumes: ruri-data:` definition. Never copy another machine's identifier. Run `docker compose up -d`, then activate in Server license. On Windows Docker Desktop, run this in PowerShell in the Compose folder:

```powershell
(Get-CimInstance Win32_ComputerSystemProduct).UUID | Set-Content -Encoding ascii ./ruri-host-id
```

Replace the final mount with `./ruri-host-id:/run/ruri-host/machine-id:ro`. Keep this file when recreating containers; reactivate when moving machines. Without an identifier the administrator can still use the website and WebDAV.

Accounts, details, settings, and licensing are in `ruri-data`. **Keep the volume during upgrades; never run `docker compose down -v` to upgrade.** Media stays in the host media folder. Back up and move data and media separately.

### Set the administrator password

Enter the one-time setup code and your new password twice. Then sign in as `admin`. The setup code is not a reusable password. Existing installations keep their accounts and passwords. If forgotten, follow Reset the administrator password; do not delete the data folder or volume.

Change your password in Account & sessions → Change password, entering the current password and the new password twice. Administrators also enter new passwords twice when creating users or resetting passwords.

## Upgrading an existing installation

- Linux service: run `sudo ./install.sh` from the new package. It restarts the service using `/var/lib/rurimecha`. For manual installations, stop Ruri and start the new version with the original `-data-dir`.
- Docker: update `RURI_IMAGE` in the original `.env`, then run `docker compose pull core` and `docker compose up -d core`. Keep the data volume and mounts.
- Windows: choose Ruri → Check for updates, confirm download, finish installation, and reopen.

Supported databases upgrade on startup. Before an upgrade, Ruri creates a `core.db.before-v…` backup folder in the data directory. Sign in with the same account afterward. If startup fails, keep the data folder, check free space and write permissions, and follow the version message before retrying.

## Uninstalling on Windows

Exit from the tray first, then uninstall Rurimecha in Windows Settings → Installed apps. The wizard keeps data by default for reinstallation.

Selecting the option to also clear local application data permanently deletes accounts, library details, collections, progress, transcripts saved to Ruri, settings, and local license information in `%LOCALAPPDATA%\Rurimecha\data`. Original media in source folders is kept. Export a migration package first to retain data, and deactivate before uninstalling if moving the license. Upgrades never perform this cleanup. Cleanup refuses linked or in-use data folders; exit and check the path before retrying, and never delete the media behind a link.

## Adding media folders

1. As administrator, choose Add media path and select a server folder. Windows desktop opens the system picker; a browser can select a drive/folder or enter a full path such as `C:\Music`. In Docker, use `/media/library`.
2. Enable needed operations: automatic scanning finds files, archiving organizes works, metadata fetching gets details for RJ-numbered works, and media information extraction reads duration and tracks.
3. Browse files through media sources, or use the library for organized works. View progress and results in Background tasks.

Change each operation independently in Folder actions → Automatic processing. Manual scanning remains available when automatic scanning is off; other enabled operations can use existing scan results. Archiving needs a complete scan; reading duration only needs discovered files. Removing a source clears its library details, manual organization, and related collection/playlist entries while keeping original files. Media can be on mounted or network drives; keep application data on a local disk.

For a kikoeru-compatible library, select that source type and enter its address. Browse and select works to add; optionally choose a server download folder to save files. Search at the top right finds works (expand search on a phone, then submit). Circle, tag, and voice actor tabs have their own name filters; open an entry to see works and return to the same list.

Compatible-source folders and media information are obtained when adding works. Use Re-archive from the source folder menu to organize again; separate scanning/extraction is unnecessary. Folders marked Ordinary folder stay ordinary until Restore automatic detection is chosen. After assigning a folder as a separate work or ordinary folder, wait for re-archiving and automatic refresh; inspect Background tasks if needed. Returning from another page starts at the source list. Temporary search/filter conditions last only for this visit; save a filter to reuse it.

## Finding works and choosing versions

Search titles, voice actors, circles, or RJ numbers. A partial number such as `RJ12` finds prefixes. After at least two characters, wait for suggestions; source and path distinguish duplicate copies. Enter or Search opens all results. Title-only search covers saved titles in all languages. Temporary library search and filters clear when leaving; work type, sorting, and view are retained.

Cards show up to two lines of tags; All tags shows the rest. Missing circles, voice actors, or tags show unknown placeholders, with the empty tag action disabled. Authors appear as voice actors. Cards at the same width have equal height.

Linked originals and language editions share a library entry. Choose View other product editions to select a server copy by source/path, play it, open its files, or edit details as administrator. Choose product edition and metadata language independently: listen to Japanese while reading English, for example. Metadata-only editions have no playable server files.

In Appearance & browsing preferences choose Original first or language preferences; set an individual work's preferred version from its details, or reset it to global preferences. Exact-number searches, explicit files, and history entries open what you selected. Browsing another edition's details does not switch playback.

## Fetching and editing work details

### Fetching from DLsite

Enable Fetch work details in metadata settings and test the DLsite connection. RJ-numbered audio works can receive titles, covers, and descriptions.

### Viewing, editing, and refreshing languages

New installations fetch Simplified Chinese and Japanese by default. Enable Traditional Chinese or English in metadata settings if available from the source.

- Switch language under View details to change the title, description, people, tags, price, and images together; absent fields show as unavailable.
- Select the language in Edit work details before saving; edits apply only to that language.
- For Unknown details, choose Set language. If the target exists, Merge fills gaps; Overwrite replaces the full target-language record.
- To update one language, choose Refresh individual parts → Force refresh product details and choose languages for this run. Other languages stay intact. Retry failed items from task results.
- Fill missing images, download images again, extract media information, scan, or re-archive from the same refresh menu.

Select and copy titles, paths, details, and messages with Ctrl+C (Command+C on macOS); long-press text on mobile.

## Collections and playlists

Add a whole work to an existing or new collection from its menu. Browse collection cover cards or a list just like the library, then open details and files. References show current work details after updates.

Individual audio/video files and clips added to a collection appear under **Sound drawer**, shown as a work-style cover card. Open it to browse, play, queue, or add items to a playlist. Removing entries keeps originals and their work membership.

Smart discovery uses works and Sound drawer too, and updates with your library. Save as collection makes an editable collection using the currently included scope, while referenced works continue to update. If only part of the content was included, only that part is saved.

Playlists retain exact tracks, clip ranges, duplicates, and order. Save as playlist preserves the queue; adding to a collection gathers works and individual media. Old custom collections become playlists during upgrade, splitting long lists in order. Works without saved tracks expand using their contents at upgrade time. Unrecoverable entries show a note; temporarily missing saved files show as unavailable.

Open a playlist and select **Play all** to start at the beginning. Tap an audio or video item to start there and continue through the full list in order; earlier items remain available with **Previous**. Open **More playback options** to shuffle the full list or add it to the queue. After searching, you can also play only the results. Tapping a search result still plays the full list from that item.

An item's menu lets you play only that item, play it next, add it to the queue, view its file or work details, or choose **Add from here…** to place that item and the following items next or at the end of the queue. A group heading only expands the group; use its menu to play the group. Unavailable items cannot be tapped to play. Editing a playlist leaves the current queue alone.

## Playback and resuming

Play from a work or folder, choosing available editions/formats such as sound effects or WAV. Use Listening history to resume. Add favorites to queues, playlists, or collections.

Audio pages, bottom controls, transcript windows, and local playback have Back 10 seconds and Forward 10 seconds. Seeking while paused stays paused. Wait if duration is unknown or media is loading. Drag the progress bar to see target time and available previews; Playback options select subtitles, tracks, and chapters. Extract media information if tracks/chapters are missing.

Timed transcripts show the current line. In portrait, drag the horizontal handle to resize, or tap to switch between one and multiple lines. In landscape, drag the vertical handle to resize or tap to hide. On larger web windows, audio opens a transcript window at the lower right. Drag its top handle to resize, tap to hide, and reopen using Transcripts & subtitles in bottom controls. It stays open while browsing; adjust timing there or open the full player with cover and transcript.

On Android, choose Floating subtitles for a timed transcript, grant display-over-other-apps permission, return, and tap again. Drag to reposition; × closes the overlay. Full-screen video supports Space to pause/resume, arrow keys to seek, and Esc to leave on the web. On Android, tap for controls and double-tap either side to seek 10 seconds. Set a sleep timer in the player.

Administrators can create, edit, or convert transcripts from the player menu and preview before saving. Save to Ruri for read-only media folders; saving alongside media requires write permission. Check times and styles after conversion.

### Preparing media tools

Linux, Windows, and Docker releases include FFmpeg/FFprobe for compatible transcoding, loudness normalization, previews, audio track selection, and embedded subtitles for supported local files. Availability depends on format and extracted information. Readable compatible-source videos also offer supported transcoding qualities. Keep Linux's `media-tools` beside the program and extract the complete archive.

Check availability under Parsing tools. Custom tools must support `fd` input; enter both executable paths, test, then save. Clear both paths to restore defaults. Automatic download requires internet access and about 500 MB free on the server.

### Connecting another player or file manager

Create a WebDAV connection using `http://SERVER:PORT/dav/`, or `https://CERTIFICATE-SERVER-NAME:PORT/dav/`. Use your current Ruri account/password, not the setup code, and do not append `/dav/` twice. For example `http://192.168.1.20:18080/dav/`, or `https://ruri.example.com/dav/` through Nginx. Without an active license, only the built-in administrator is eligible.

Browse folders and play original files. WebDAV is read-only, with no upload, delete, rename, conversion, or third-party progress sync. Scan newly added files in Ruri before they appear. Playback formats depend on the other player.

In Windows Explorer, use This PC → Add a network location → custom location, enter the complete URL, and supply credentials. Pasting a URL in a browser/address bar does not create a network location.

Windows WebClient normally permits password authentication only over HTTPS. Repeated password prompts, network-name errors, or error 67 over HTTP may be this restriction. Use a client that supports HTTP WebDAV, or configure HTTPS with a trusted certificate and its matching name. Ignoring a browser warning does not establish Windows trust.

Only on a trusted home network, you may allow HTTP credentials. Confirm WebClient exists in `services.msc`, then run as administrator:

```powershell
New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\WebClient\Parameters' -Name BasicAuthLevel -PropertyType DWord -Value 2 -Force
Restart-Service WebClient
```

This affects Windows WebDAV connections system-wide. HTTP passwords are unencrypted, and restarting disconnects current mounts. Use `-Value 1` to restore HTTPS-only. If WebClient is absent or managed by policy, use a dedicated client. See [Microsoft's guide](https://learn.microsoft.com/en-us/iis/publish/using-webdav/using-the-webdav-redirector).

| Symptom | Action |
| --- | --- |
| Website also fails | Check address, Docker external port, firewall, and LAN ranges |
| Wrong password after changing it | Disconnect the location, remove old entries in Credential Manager → Windows Credentials, and reconnect |
| Browser says unsupported method at `/dav/` | Add the address in a WebDAV client; it is not a web directory page |
| Large transfers fail or `0x800700DF` | Windows WebClient limits single-file size even over HTTPS; use a dedicated client or consult `FileSizeLimitInBytes` in Microsoft's guide |
| Very large directories fail | WebClient limits directory property size; use another client without moving media |
| Upload/delete/rename unavailable | Ruri WebDAV is read-only |

## Saved clips

The crown marks licensed features. Open a playable file with a known duration first; contact the administrator if activation/renewal is needed. In Now playing or full-screen video Playback options, select Saved clips. Drag two handles to choose a range; the default is 30 seconds after the current position, or the final 30 seconds near the end. Zoom the selection to fine-tune. Preview stops at the end; save or cancel to return to the previous position.

Open Saved clips in the account menu to replay, queue, add to playlists/collections, or adjust ranges. Deleting a clip removes list references but keeps media. Missing sources or expired licenses keep clips for later use after recovery/renewal.

## Listening on Android

Install the Android client, discover LAN servers or enter an address, then sign in. Check both devices share a LAN and discovery is enabled if nothing appears. Online playback needs a licensed server.

Before playback, open My → Playback settings to change Direct source connections. On uses the phone's own connection; turn it off to use the server's source proxy or if direct playback fails. The same setting is available in playback details.

### Downloading for offline listening

1. Choose edition/format in work details, then Download to device and select files. A file menu can download individual media. Sign-in and a valid server license are required while downloading.
2. Open Local library from the connection page, login page, or account menu to pause, resume, cancel, and view progress. Cancel keeps completed files. Retry missing covers/subtitles.
3. After completion, listen from Local library without network, sign-in, or an active license. Local versions in online details also opens downloads.

Downloads include originals, saved text, covers, and available text subtitles. Your phone must support the original format; download another format if needed. Wi-Fi/wired only is the default; disabling this may use mobile data. Up to 500 files per request and 500 downloaded editions are supported.

Local playback supports background controls, notifications, repeat, shuffle, sleep timer, transcripts, and full-screen video. Local queue, favorites, and progress are separate. Restored queues start paused. Refresh work details updates only text, covers, and subtitles and requires the original server/account and an active server license. Deleting local versions removes only phone copies. Sign-out/server changes keep downloads; uninstalling or clearing app data deletes downloads and records. Download again on a new phone. Saved clips require a server connection.

## Privacy mode and listening history

Use the eye button in the app/player. It hides covers, images, and video, and masks titles, tags, filenames, and subtitles. The small eye reveals an item for 3 seconds. Navigation and progress controls remain usable; the setting stays on this device.

On Android, confirm before speaker playback in privacy mode. Disconnecting headphones/Bluetooth pauses playback; reconnect and resume manually. Open the app if a notification/media-key action needs confirmation. Lock-screen/notification text and covers are masked too.

Progress is still saved. In Listening history, delete one record or clear your history to remove related resume positions and completion status. Collections, playlists, clips, and media remain. Deleting a currently playing record lets playback continue; selecting content again creates a new record.

## Server license and accounts

Without an active license, the built-in administrator can use the web interface and WebDAV. Extra accounts, official Android online playback, and saved clips require activation: enter the key and server name under Server license, then activate.

After activation, administrators create users in User management. Users share the library but keep separate collections, playlists, preferences, progress, and clips. Disabling an account under Access permissions ends its sessions and WebDAV access. Follow sign-in prompts after password, role, or license changes.

Activation, updating, and deactivation need server internet access; an active license works offline until expiry. Renew using the original key. Deactivate on the old server before activating another. If the old server cannot start, contact the provider with purchase information to release the binding. Recreating a container on the same machine requires keeping its data volume and host identifier.

Account & sessions → Login sessions & WebDAV shows current device names, client types, sign-in IPs, and login/expiry times. Revoke unused sessions. IPs reflect sign-in and do not track later network changes. Browsers show browser/OS names, Android device models, and desktop clients computer names. Signed-out/expired history is not retained here.

## Connection and network

### Access from other devices

Linux service defaults to `http://SERVER-LAN-IP:8080/`; allow that port in the firewall. Windows uses installation settings, initially all IPv4 addresses on 8080. Allow `rurimecha-core.exe` in the installation folder through the Windows private-network firewall. If it listens only on `127.0.0.1`, retain local access and add a LAN address in Network settings. Other devices cannot play while the computer sleeps, is off, or Ruri has exited. Docker uses host IP/external port, not the container address.

Blank LAN ranges detect directly attached private networks. Enter one range per line, e.g. `192.168.1.0/24`, retaining current ranges when adding Docker/VPN/routed-home networks. Remote access needs both the server-wide switch and the user's remote access permission. Use HTTPS or a trusted VPN outside home; HTTP login/media are unencrypted. For Nginx follow the next section.

### Enabling HTTPS

Keep HTTP and/or enable HTTPS on a different port (default `8443`). Select a PEM certificate and matching unencrypted private key; reselect both to replace them. Use a name/IP covered by the certificate and trust it on the device. Submit over HTTP only on a trusted LAN. Force HTTPS redirects HTTP, but players/WebDAV should use HTTPS directly. Address, port, and certificate changes disconnect you: note the new address before saving.

Docker also needs a mapping such as `"192.168.1.20:8443:8443"` and `docker compose up -d`. For redirects, keep HTTPS's external and internal port equal. With Nginx, follow below instead.

## Access through Nginx

Use this section if you already have Nginx, a domain, and certificates; LAN-only access can connect directly. Nginx can terminate HTTPS and connect to Ruri over HTTP without uploading that certificate to Ruri. Same-host Nginx needs no Trusted proxies entry, including default Docker bridge deployment. Access rules use the visitor IP. Use a dedicated domain root, e.g. `https://ruri.example.com/`; subpaths such as `/ruri/` are unsupported. HTTP upstream is allowed on trusted networks; HTTPS upstream needs certificate verification.

### Example: HTTPS with Nginx on the same host

1. Point `ruri.example.com` to Nginx and prepare a matching certificate trusted by your devices, for example `/etc/nginx/certs/ruri-fullchain.pem` and `/etc/nginx/certs/ruri-key.pem`.
2. Leave Trusted proxies empty for same-host Nginx. If only Nginx connects, Ruri can listen on `127.0.0.1:8080`. Retain current LAN ranges and add your home range, e.g. `192.168.1.0/24`.
3. Create `/etc/nginx/conf.d/ruri.conf` on Debian/Ubuntu, substituting your domain and paths:

```nginx
server {
    listen 80;
    server_name ruri.example.com;
    return 308 https://ruri.example.com$request_uri;
}

server {
    listen 443 ssl;
    server_name ruri.example.com;
    ssl_certificate     /etc/nginx/certs/ruri-fullchain.pem;
    ssl_certificate_key /etc/nginx/certs/ruri-key.pem;

    client_max_body_size 257m;
    client_body_timeout 600s;
    send_timeout 600s;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $http_host;
        proxy_set_header X-Forwarded-Host $http_host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $remote_addr;
        proxy_set_header Connection "";

        proxy_buffering off;
        proxy_request_buffering off;
        proxy_cache off;
        proxy_read_timeout 600s;
        proxy_send_timeout 600s;
        proxy_redirect off;
        gzip off;
    }
}
```

4. Run `sudo nginx -t` and `sudo systemctl reload nginx`.
5. Sign in at `https://ruri.example.com/`, save a setting, play and seek. Use `https://ruri.example.com/dav/` for WebDAV and the same entry for migration uploads.

This example assumes one Nginx receiving visitors directly. If a CDN/proxy precedes it, restrict trusted ingress and restore visitor addresses there; never trust user-supplied headers. Trust only controlled proxy addresses or dedicated subnets. External access still requires both server and account permissions. For home-only use, keep remote access off and optionally add `allow 192.168.1.0/24; deny all;` to the HTTPS server. Use HTTPS upstream or a trusted tunnel over untrusted networks.

Preserve authentication, cookies, Origin, and Referer. Do not forge origins, rewrite cookies unnecessarily, or add Nginx Basic authentication that occupies Authorization and conflicts with Ruri/WebDAV. Allow PROPFIND for directory listing. Never publicly cache media, images, or login responses.

### Docker and HTTPS upstream

- Only Ruri in Docker: use bridge networking and `127.0.0.1:8080:8080`; host Nginx connects to `http://127.0.0.1:8080`, without Trusted proxies changes after recreating containers. Set home LAN ranges in Ruri.
- Both in Docker: use one dedicated network and `http://core:8080`. Localhost is the Nginx container itself. Trust the fixed Nginx IP or dedicated proxy subnet, not a network shared with unrelated containers. No Ruri host port is needed; users connect to Nginx.
- HTTPS upstream: enable Ruri HTTPS, use `https://127.0.0.1:8443` or `https://core:8443`, and add:

```nginx
proxy_ssl_server_name on;
proxy_ssl_name ruri.example.com;
proxy_ssl_verify on;
proxy_ssl_trusted_certificate /etc/ssl/certs/ca-certificates.crt;
proxy_ssl_verify_depth 3;
```

The name must match Ruri's certificate. For a private CA, use its root certificate instead; keep verification enabled.

### Troubleshooting

| Symptom | Check |
| --- | --- |
| 403 on sign-in/save | Visitor LAN/remote permissions, proxy configuration, trusted addresses for remote/container proxies |
| Redirect loop or unexpected 8443 | Proxy configuration/trust; disable Ruri's forced redirect when only Nginx provides HTTPS |
| 502 | Ruri running, upstream address/port; HTTPS certificate name and root |
| Page loads but media stalls or cannot seek | Preserve Range; disable media cache/buffering; test direct playback |
| Migration upload 413 | `client_max_body_size` and Ruri's own package limit |
| WebDAV cannot list | `/dav/`, credentials, and PROPFIND |

## Moving and restoring

As administrator on the old server, open Migration & restore. Export all work details, system data, or both; system data includes accounts, settings, collections, and progress and requires a password. Keep the package and password safe. Move/copy media separately, install Ruri on the new device, select the package and new media path, and preview restoration. Check missing files before restoring. Missing media can retain details, favorites, and progress; return files to their original relative locations and scan again. After system restoration, restart and sign in with the old account/password.

For one work, export from Migrate this work. With matching relative paths, restore from the target work's same action to replace its details/manual organization. If absent, import the single-work package through Migration & restore. Packages keep details, original sources, edits, and images, and retain update settings. Move media, logs, task records, server licensing, and phone downloads separately.

## Reset the administrator password

- Linux service: `sudo /opt/rurimecha/reset-admin-password.sh --service`; it restarts an active service. For a manual installation, stop Ruri, run `./reset-admin-password.sh` from the extracted folder (append the data path if different), then restart.
- Windows: Exit Ruri from the tray, open the installation folder (default `%LOCALAPPDATA%\Programs\Rurimecha`), run `reset-admin-password.cmd`, then reopen. For another data folder, use `reset-admin-password.cmd "DATA-PATH"` from that folder's command prompt.
- Docker: run `./reset-admin-password.sh` beside the release Compose file. It temporarily stops and restarts an active container.

Sign in with the displayed username and new password. Names and data are kept; devices using that account must sign in again.

## Guest experience

Administrators can enable Guest experience in User management. Guests enter from the login page without a password to browse/play, but cannot edit works or save collections/progress. Turning it off ends access; it can be enabled again. Public demos should use a dedicated library with permission to stream the works.

## Choosing the interface language

Use the language button on the connection/login page or account menu to select 简体中文, 繁體中文, 日本語, English, or Follow system. Signed-in choices are also in Appearance & browsing preferences and saved with your account for other devices. Signed-out choices stay on this device and return after signing out. Offline local playback uses the most recently used language.

Follow system uses a supported device language, choosing Traditional Chinese for Traditional Chinese regions and Simplified Chinese for other Chinese regions, or English if none match. Switching the interface does not rename works, files, or collections, or change metadata, audio, or subtitle languages; choose those independently in their menus. Installers currently use Simplified Chinese. Management/CLI tools primarily use English; historical release notes are Chinese and English.


## Circles, voice actors and illustrators

Search a name in Circles, Voice actors or Illustrators, then open an entry to browse library works. Entry images use associated work covers. Select a work to open its details. Under Works from, choose a DLsite floor to discover more works and open their DLsite pages. Discover works also accepts names absent from your library; configured compatible sources provide another search option. Name searches may miss works or match another person; illustrator searches in compatible sources depend on their indexed credits. Refresh reads from page one again. Failed refreshes retain the current results; Retry tries again. Discovery does not add works or download media automatically.


## Automatic checks without changes

A periodic check that finds no changes does not repeat organization, metadata fetching or media information extraction. Existing pending work and scheduled retries continue.

## Problems and detailed diagnostics

Administrators can open System logs in Settings. Needs attention shows problems first; choose All records for operation results and expand details when reporting an issue. Task details contain progress, failed items, and retry options.

To investigate a problem, open Detailed diagnostics in Settings, enable and save it, reproduce the problem, then disable and save it. The setting applies to the whole server and survives restarts. Disabling stops new diagnostic records; existing records remain within the storage limit. Choose Diagnostics in the log filter to view them. Leave it off for everyday use.


## Listening statistics and recommendations

Open Listening & recommendations from the sidebar or account menu. Listening statistics shows your time, calendar, monthly trends, and leading works, voices, circles and tags. Choose 30 days, 12 months, a year or all time. Playback from the server library is recorded; the phone’s local library is excluded.

In work details, select My rating and choose one to five stars. Open it again to change or remove your rating. Your account rates the whole archived work across its formats and playback groups. Cards show your rating separately from the public rating.

For you offers You might like, Try something different and Listen again. Open a work or play it directly, and use Refresh picks to change the selection. Deleting a resume record keeps your statistics and ratings.


### Video quality and transcoding limits

Open Player settings → Video quality while watching a video. Supported videos offer resolution and bitrate choices; switching resumes at the current position after buffering. Unsupported videos offer only Original format. This applies to local sources and readable kikoeru-compatible videos. Direct playback depends on your device. Compatible Auto uses a default quality budget; it does not continuously adapt to network speed.

Administrators can set maximum transcoded height, video bitrate and worker threads per task in Processing settings. Use 0 for no height/bitrate limit. Threads accept 1–64; 0 uses one worker. Limits apply to new transcoding tasks and do not restrict original-format direct playback. Transcoding preserves source files, does not upscale the picture and retains the source frame rate.

Finished tasks and runtime logs are kept for up to 48 hours. After completion, details show unfinished items for retry; successes remain in the totals. Once a record expires, start again from its work or source. Library metadata, favorites, listening progress and subtitle drafts are retained. This upgrade discards old task history and removes the old database backup after successful migration.
