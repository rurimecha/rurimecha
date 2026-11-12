# 音视频串流、队列和字幕

所有路径相对于 `/api/v1`。同源网页使用 HttpOnly/SameSite Cookie，写请求验证 Origin/Referer；原生和外部客户端使用 `Authorization: Bearer <token>`。不接受 URL 令牌、媒体路径或工具参数。

## 开始播放

1. `GET /playback/status` 查询本实例的处理能力。`original=true` 表示提供原文件接口；`transcode` 表示当前平台和配置能启动外部媒体处理，具体编码器仍取决于部署环境。`processes` 是共享处理上限，`active_processes` 是当前占用；`startup_seconds`、`stream_seconds` 是启动和单次串流上限。
2. `POST /playback/resolve` 生成队列，或直接使用已知媒体节点 ID。
3. `GET /nodes/{node}/stream` 默认返回原文件。媒体元素及其后续 Range 请求都需要会话。同源网页由浏览器携带 Cookie；原生和外部客户端应在播放器网络数据源设置 Bearer 请求头。
4. 字幕通过独立接口读取；转码不烧录字幕。`start` 跳转后，字幕仍使用原始媒体时间轴，需要客户端将响应中的起始秒数加到输出时间。

### 串流模式

`GET` 和 `HEAD /nodes/{node}/stream` 使用相同查询参数；不允许未知参数、重复参数和空参数值。

| 参数 | 默认值 | 语义 |
| --- | --- | --- |
| `mode` | `original` | `original`、`mp3`、`opus`、`mp4` |
| `normalize` | `false` | 只能是 `true` 或 `false`；对有音轨的输出做单遍响度标准化 |
| `start` | `0` | 原始媒体时间轴秒数，有限数且在 0..604800 内；已知正时长时必须小于时长 |
| `end` | 不指定 | 原始媒体时间轴结束秒数，必须大于 start；已知时长时不能超出音轨；逐字节原文件直传不接受 |
| `channel` | 不指定 | 仅 `mode=mp3` 可选 `left` 或 `right`，从已探测的至少双声道音轨试听对应声道；必须同时传 end，范围最多30秒，不与 normalize 同用。单声道返回 `audio_channel_unavailable`，缺少当前媒体信息返回 `media_information_required` |
| `audio_stream` | 首个音频流 | 容器的绝对流 index，需要当前媒体信息 |
| `video_stream` | 首个非封面视频流 | 容器的绝对流 index，需要当前媒体信息；MP3 不接受此参数 |

`original&normalize=false` 是逐字节直传，不启动处理进程，不要求媒体信息提取成功。仅接受 `start=0`，不接受显式流选择；使用 Range 跳转。来源必须启用，节点必须是当前索引中的可用音视频文件，实际文件也必须可读。无探测结果时通过支持的音视频扩展名识别；有当前探测结果时以流描述为准。封面图片不算视频流。被替换为目录、特殊文件或根外符号链接的资源不可读取。

kikoeru 兼容来源只提供 original。服务端优先读取已完整保存的文件，否则转发来源媒体；两种情况使用同一节点和播放地址。该来源的媒体信息来自来源 API，即使文件已保存到服务器也不运行 ffprobe 或 FFmpeg。需要本地媒体处理时，应将下载目录另行添加为本地来源。来源断线且文件未完整保存时播放失败；客户端可重试或选择保存到服务器。

该来源的同目录外置字幕按普通目录规则列出；读取正文时优先使用完整保存的字幕文件，否则从来源读取。图片和文本附件也保留原目录路径，可由只读 WebDAV 访问。远端音轨的内嵌字幕提取不提供。

直传支持 `HEAD`、字节 Range（含后缀和多段）、`If-None-Match`、`If-Modified-Since`、`If-Unmodified-Since`、`If-Match` 和 `If-Range`。响应为 200、206、304、412 或 416。`ETag` 是基于当前文件大小/修改时间的弱校验标识，不是内容摘要；不能用弱 ETag 校验 `If-Range`，可使用 `Last-Modified`。多段 Range 返回 `multipart/byteranges`，416 返回 `Content-Range: bytes */<size>`。直传条件/范围错误可返回标准纯文本，不能假设所有错误都有 JSON 正文。

`opus` 默认输出 96 kbps Ogg Opus；`mp3` 默认输出 192 kbps、48 kHz、双声道 MP3，也可从视频提取音频；已知无音轨的视频返回 400。`mp4` 需要画面，输出分片 MP4、H.264 视频及可选 AAC 音频；画面按比例限制为 1280×720 内的偶数尺寸、30 fps，音频为 128 kbps、48 kHz、双声道。纯音频不能请求兼容 MP4，静音视频可正常输出。不会把内嵌封面转成视频。

`original&normalize=true` 有音轨时重新编码音频，保留支持的容器及原音频编码；有画面时复制所选视频流。采样率、声道数尽量保持观察值，Opus 使用 48 kHz；重编码不保证逐字节、无损或保留全部标签、流、章节。支持 MP3、WAV、FLAC、M4A/M4B/ALAC、AAC、Ogg/Opus、MP4/MOV/M4V、MKV、WebM、AVI、WMA/WMV；音频编码限部署环境可用的 MP3/AAC/ALAC/FLAC/Opus/Vorbis/AC3/EAC3、部分 PCM/ADPCM、WMAv2。容器与编码组合也须可封装。AIFF、APE 等不支持的组合返回 422，可改用兼容转码。标准化目标为 -18 LUFS、真峰值 -1.5 dBTP、响度范围 11 LU；单遍处理不保证任意短片段达到精确目标。已知无音轨的视频请求原格式标准化时仍是直传，只允许字节跳转。

所有需要处理的响应为 200，`Accept-Ranges: none`，忽略 Range，且返回 `X-Media-Start`（秒，六位小数）。音视频起点使用 `start`，需要重新发起请求跳转；复制视频的标准化起点还受关键帧影响。输出不提供完整 `Content-Length`。HEAD 验证资源、参数与配置，不启动进程、不占用处理名额，也不保证实际解码一定成功。

示例：

```http
GET /api/v1/nodes/<node>/stream?mode=mp4&start=12.5&normalize=true&audio_stream=2
Authorization: Bearer <token>
```

处理需要 FFmpeg，缺失时其他接口和原文件直传仍可用。外部处理支持 Linux 和 Windows x64。Linux 要求 `/proc/self/fd` 可用，Windows 要求 FFmpeg 支持 `fd` 输入协议。音视频转码与内嵌字幕提取共享名额，默认 4 个，满额返回 503 `playback_busy` 和 `Retry-After: 2`。原文件直传和 HEAD 不占名额。默认等待首字节最多 20 秒，处理过程无输出及网络写入停滞分别限制为 30 秒，单次串流最长 24 小时。断开或取消 HTTP 请求会回收对应子进程；重启后重新请求，不存在持久转码任务；完整零起点音频可写入容量有界缓存，详见 listening.md。

处理成功前才发送媒体正文；此前失败返回 JSON 错误。媒体正文开始后失败会中止连接/HTTP 流，客户端须将截断视为失败，再按原始时间位置重试。关闭响应体即可停止播放请求。挂载文件系统的阻塞读取不承诺立即中断。

## 队列解析

`POST /playback/resolve` 接受以下选择器之一，恰好一个必填：

```json
{"node_ids":["node-a","node-b","node-a"]}
```

```json
{"work_id":"work-id","group_id":"optional-group-id","format":"mp3"}
```

```json
{"directory_node_id":"indexed-directory-node-id"}
```

```json
{"playlist_id":"saved-playlist-id"}
```

`node_ids` 为 1..5000 项，保持输入顺序和重复项，每项必须可用。目录递归展开，不受作品边界影响；按完整相对名称自然排序（2 在 10 前）。作品按成员 `play_order`、自然路径、ID 排序；默认仅使用作品默认组，未设置默认组时包含各组。`group_id`、`format` 只可用于作品选择，组必须属于该作品；格式优先使用归纳或人工成员中的 `media_format`；为空时使用支持的文件扩展名，仍无法判断的可使用 `__unclassified`。已绑定显式视角的成员仅默认视角参与作品展开，直接 `node_ids` 可选择其他视角。未绑定成员正常参与。作品/目录仅包含当前可用音视频，最多展开 5000 个候选文件，空队列返回 `items: []`。过多时须缩小目录或指定组/格式，不静默截断。

`prefer_account_defaults: true` 让作品选择使用账户偏好的播放组和源格式。收藏集或条目请求中，它同样作用于尚未固定文件的作品条目。显式播放组、格式和文件优先。解析结果的作品名与封面按当前账号的资料语言显示。

解析不保存队列，也不修改播放列表。响应 `items` 中每项包含：节点/来源/作品 ID、原始 `name`、显示别名 `alias`（无别名为空）、原始名字节 `name_base64`、`available`、`kind`、`has_audio`、`has_video`、可空时长和尺寸、观察标题/作者、作品原语言标题、`cover_ref`，以及 `information_state`（`absent/unprobed/current/stale/unavailable`）。非法 UTF-8 名称的 `name=null`，客户端可显示 alias 或自行替换不可显示字符。别名不参与格式判断、资源访问、搜索和排序。没有当前探测时的音视频类型是扩展名线索，不能据此保证文件有效或视频有音轨。队列可用性来自索引，实际开始播放会重新核对文件。

## 播放列表

播放列表只属于当前认证账户。每账户最多 100 个，每个最多 5000 项，名称为去除首尾空白后 1..100 个 Unicode 字符。

| 方法与路径 | 行为 |
| --- | --- |
| `GET /playlists?limit=50&offset=0` | 分页预设，按创建时间和 ID 排序；limit=1..500 |
| `POST /playlists` | `{"name":"夜間","node_ids":["node-a"]}`；省略 node_ids 创建空列表；201、Location、ETag |
| `GET /playlists/{playlist}` | 预设及实时索引解析后的 `items`，返回 ETag |
| `PUT /playlists/{playlist}` | 完整替换 `name`、`node_ids` 和 `groups`，省略 node_ids 表示清空 |
| `POST /playlists/{playlist}/items` | 正文为队列选择器，将展开结果追加到末尾 |
| `DELETE /playlists/{playlist}` | 删除预设，204；不修改文件、作品或索引 |

所有已有列表的写入必须携带该列表的 `If-Match: "<playlist-id>:<revision>"`。缺失返回 428，过期返回 412；更新成功 revision 增加并返回新 ETag。操作在同一快照下校验和保存，失败整体回滚，无 `force` 绕过。

保留顺序和重复节点。已有资源失效后仍保留其 ID 和占位条目，`available=false`，不再返回原名/来源/作品信息；可以重排或移除，新增节点须可用。通过已保存列表解析或追加时也保留不可用占位。列表重启持久化，解析与播放不会隐式修改列表或修订。

## 字幕

`GET /nodes/{node}/subtitles` 返回 `tracks` 和 `truncated`；`GET /nodes/{node}/subtitles/{track}` 返回对应正文。track 是服务返回的原样 ID，例如 `external:<node-id>`、`embedded:2`、`lyrics`，不能借此访问未关联节点。

外部字幕自动发现限定同来源、同直接父目录、区分大小写精确主体匹配；支持追加式 `track.mp3.srt`、替换式 `track.srt`，以及简繁日英后缀，如 `track.cht.srt`、`track.mp3.ja.vtt`；后缀及冲突规则见 [字幕关联](media-associations.md)。完整主体优先于剥离语言后缀，追加式对应媒体优先于同主体其他格式；同目录同时存在 `01.wav`、`01.wav.mp3` 时，`01.wav.srt` 不会出现在后者的自动轨道列表。有效人工关系可显式指定仍满足命名约束的配对。追加式轨道先排列。作品中现存、有效且纳入的人工字幕关联可跨目录；撤回/排除关联不继续提供正文。同名目录播放无需先归纳为作品。不按模糊前缀匹配。

支持 SRT、WebVTT、LRC、ASS/SSA 及同名 TXT 纯文本。读取上限 2 MiB、5000 cues、32 轨，截断通过 `truncated=true` 明示。UTF-8、带 BOM 的 UTF-16LE/BE 可确定解码，非 UTF-8 兼容推测 GB18030，响应 `encoding` 明示推测；不自动猜测其他日文编码。字幕语言优先使用有效人工/AI 关联中的 locale（包括显式 und）；自动发现从受支持文件名后缀推导，内嵌轨使用流标签。没有信息为 `und` 或空值，不用界面语言推断。简繁日英的 UTF-8/UTF-16 正文均可读取；不提供 Big5、Shift-JIS 等编码的手动选择；请先将文件转为 UTF-8。

内嵌文本字幕通过外部工具按需提取；图形字幕列为 `unsupported`。歌词使用当前节点媒体标签中的 lyrics/unsyncedlyrics/lyrics-*，既可返回带时间歌词也可返回普通文本。没有当前媒体观察时不提供内嵌轨和歌词；已有可用的外部字幕不依赖媒体探测或 FFmpeg。

列表不读取正文，`state=indexed`、`cue_count=0` 和 `timed=false` 不代表已解析为空；读取单轨后 `state=ready/unavailable`，返回 `cues`、`cue_count`、`timed`、可选 `text`。cues 使用 `start/end` 秒和纯文本 `text`。移除常见字幕标记与 ASS 样式指令，保留重叠和多时间戳 LRC，LRC 正 offset 表示提前、负值表示延后；零时长歌词延续到下一个更晚时间戳，最后一行用已知媒体时长或 5 秒兜底。客户端始终按纯文本渲染，不将字幕内容当作 HTML。ASS 复杂排版、动画和绘图不在此纯文本接口中重现。

台本编辑、格式转换与确认覆盖见 [台本编辑接口](subtitle-editing.md)。

## 错误及跨域

除通用错误外：`503 playback_unavailable` 为外部处理不可用，`503 playback_busy` 为名额占满，`422 media_decode_failed` 为处理未产生有效输出，`422 unsupported_profile` 为不支持的编码/容器/字幕类型，`409 media_information_required` 为选择流或保留编码处理需要当前媒体观察。文件变化后原文件仍可直传；需要技术信息的请求须重新扫描并提取。来源停用、删除、索引缺失或实际文件不可访问返回 404。

允许的浏览器 Origin 沿用全局配置。预检支持 HEAD、Range、If-Range 和条件请求头；暴露 `ETag, X-Request-ID, Location, Accept-Ranges, Content-Range, Content-Length, Last-Modified, X-Media-Start, Retry-After`。媒体响应与其他接口一样不允许共享缓存。播放器需要显式处理代理、浏览器及自身超时，不能把网络重试等同于后台任务恢复。


媒体信息字段编辑会反映到播放解析的标题、作者、时长及流描述；没有成功观察时 information_state=unprobed。写事件失效后，播放只继续使用扩展名线索与人工补充，自动技术观察不再作为显式选流/处理依据；普通原始直传仍可用。编辑与重置见 media-probe.md。

同源网页的播放接口、图片、字幕和媒体流统一使用登录返回的 HttpOnly 会话 Cookie，HTTP/HTTPS 均保留 Range/流式响应；原生与外部 API 客户端继续使用 Bearer。无需 Service Worker 或 URL 令牌。详见 [账户与会话](accounts.md)。

### 有序收藏集与作品块

Selection 另外接受 collection_id 或 entries，与 node_ids/work_id/directory_node_id/playlist_id 互斥。收藏集条目、展开与限制见 presentation.md。Item 的 selection_* 字段携带作品/单曲的出现身份；相同作品多次加入队列不能合并身份，显式加入的单曲不会因其属于某个作品就自动合并到作品块。

## 当前音频配置与缓存

当前新增 Opus、可选码率、完整音频缓存及逐节点配置协商，见 [收听与协商](listening.md)。播放列表仅属于当前账户，解析任意其他账户列表或收藏集被拒绝；归档收藏集共享且只读。缓存失效、容量及生成输出的跳转差异均以该契约为准。


## 加入时分组与平铺媒体（当前格式）

作品、目录和整个收藏集解析后都返回普通媒体项，可选 `queue_group` 描述这次加入的组合：`{id,kind,source_id,title}`。kind为work/directory/collection；id标识本次出现（最多160字节），source_id标识来源（最多160字节），title为加入时名称（非空，最多1000字符）。它不改变媒体的work_id、作品播放版本/格式，也不是媒体项或播放占位。未分组的曲目省略该对象。

同一解析中的组必须连续，重复组ID的其他字段必须完全一致。作品或目录每次解析产生新组；collection_id解析把全部所选曲目放入一个新的外层收藏集组，不创建嵌套组。Playlist解析保留已经保存的分组。客户端把同一结果多次加入当前队列时，必须为每次加入重新分配独立组ID，不能按来源ID合并。

Playlist / PlaylistDetail新增 `groups` 数组，与 `node_ids` 逐项对应，每项为QueueGroup或null；重复node_id仍是独立出现。创建/完整替换可提交groups，省略或空数组表示全部不分组；非空时必须与node_ids等长。完整替换同时替换名称、曲目顺序和分组；412保留草稿，不能丢弃groups后盲目重试。追加作品、目录或收藏集时保留来源分组；追加另一个列表时保留其平铺组合，并为追加的组分配新ID，重复追加不会合并。

组内重排不改变所属组，拖出则清除该项分组并把它移至组外；最后一项移出后组自然消失。组只由成员描述，没有单独的空组记录。来源移除时按同一出现位置同步裁剪node_ids/groups；不修改其他来源的曲目和组。分组描述不作为授权依据，真实媒体仍按当前账户和节点可用性校验。失效媒体不返回其当前文件资料，但账户自己保存的queue_group标题和来源摘要仍保留。

## 按目录筛选结果播放

`POST /playback/resolve` 和 `POST /playlists/{playlist}/items` 的 Selection 增加 `node_query`，与 node_ids、work_id、directory_node_id、playlist_id、collection_id、entries 互斥：

```json
{
  "node_query": {
    "source_id": "source-id",
    "filter": {
      "parent_id": "directory-id",
      "recursive": "true",
      "formats": "wav,flac",
      "sort": "name",
      "sort_order": "asc"
    },
    "start_node_id": "optional-matching-playable-node-id"
  }
}
```

filter 接收[目录节点查询](scanning.md)的全部非分页字段，值均为字符串；不接受 limit/offset 或未知条件。必须提供 parent_id，source_id 指定时必须与目录一致。解析在同一读取/写入快照内使用完整筛选和显示顺序，仅选当前可用、可播放的文件，不依赖客户端当前页。只选目录或非音视频条件时不会扩大成整个文件夹播放。

start_node_id 省略时选择全部；提供时必须是筛选内可播放文件，结果从该文件开始并保留之后的顺序，否则 404。完整匹配可播放文件超过 5000 时整体返回 400，不静默截取；列表追加失败不更改原列表。每次按 node_query 解析标记一个新的目录组合，标题取所选目录；重复追加保留独立出现项。该操作不修改浏览条件、媒体或当前设备队列。

收藏集追加仍使用现有有序条目契约；客户端可以将解析后的完整播放快照转为单曲条目保存，不把 node_query 作为持久目录订阅。

## 视频进度预览

`GET /nodes/{node}/video-preview?second=42` 返回 JPEG，画面按原比例缩小至最多 320×180 像素。请求使用媒体源时间，按 5 秒间隔向下取样。已有缩略图缓存设置也控制预览图缓存，成功响应支持 ETag。缓存命中前仍检查该文件是否可访问。取图需要 FFmpeg 并受播放进程额度限制。遇到忙碌、预览不可用或未安装 FFmpeg 时，客户端仍可用时间和章节定位。


## 虚拟播放条目

node_ids 和自定义收藏集的单曲 node_id 保存不透明的播放资源 ID。解析结果可附 resource_kind、source_node_id 和 resource_revision。原文件省略这些字段。虚拟资源的时长、字幕及跳转均采用该资源的时间轴。可用输出以 playback 能力响应为准，客户端保留虚拟 ID 保存队列和列表。明确单项请求失败会报告错误，混合显式选择和保存列表可返回 unavailable 占位，客户端跳过不可用项。

普通文件的直传和转码协议继续按本文定义。虚拟资源的创建与授权专用契约由产品另行提供。


应用内台本以application:轨道出现在同一字幕列表，source=application。它绑定媒体节点，优先列出，仍计入32轨道上限。列表不读取正文；选中后通过同一GET subtitles/{track}获取文字和时间，和文件台本一样参与播放器高亮与珍藏片段裁剪。

## 收藏集升级后的播放列表

数据库 v5 将旧自定义收藏集迁移为所属账号的持久播放列表。保留成员顺序、已有的作品曲目清单与删减、分组、重复出现和已保存的失效资源 ID。没有曲目清单的作品按升级时的默认播放组、原有格式限制与默认视角展开；已知缺失文件保留为不可用项。无法还原曲目清单的作品记入 `migration_notes`，不伪造媒体。每个列表最多 5000 首，过长结果按顺序拆为带编号的多个列表；迁移允许保留超过日常新建数量上限的已有内容，日常新建限制仍为每账号 100 个。

Playlist/PlaylistDetail 返回只读 `migration_notes: string[]`，普通列表为空数组，编辑时由服务端保留。名称上限为 300 个 Unicode 字符，以保存旧收藏集名称及拆分编号。当前设备队列由用户从已保存列表载入，数据库升级不替换设备队列。
