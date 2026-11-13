# 来源、扫描任务与节点

扫描仅发布索引；作品归属通过目录边界和归纳计算。

本地扫描只枚举目录/文件并保存目录元数据，不读取媒体标签、运行 ffprobe、刮削外部站点或在枚举内部判断作品归属。kikoeru 兼容来源按选定作品读取来源 API；其音轨时长来自来源接口；来源未给出大小的文件在入库时读取远端文件头补齐，不运行 ffprobe。节点返回 size（字节数字符串或 null）和 modified_at（RFC3339 或 null）；远端音轨时长可来自来源资料，编码资料不推测。

## 接口

| 方法 | 路径 | 行为 |
| --- | --- | --- |
| GET / POST | /sources | 来源分页/创建 |
| GET / PUT / DELETE | /sources/{source} | 详情/修改名称/移除根目录并删除其媒体元数据 |
| GET | /sources/{source}/nodes | 节点分页及子目录浏览 |
| GET | /nodes/{node} | 单个节点，包括保留的缺失记录 |
| GET | /sources/{source}/remote-works | 浏览来源作品；支持 page、page_size、q、search_field 和 sort |
| GET | /sources/{source}/remote-facets | 读取来源已有的社团、声优或标签；使用 field= circle、voice 或 tag |
| GET | /sources/{source}/remote-works/{code} | 查看 RJ 作品资料和音轨树 |
| GET | /sources/{source}/remote-works/{code}/cover | 查看来源封面 |
| GET | /works/{work}/remote-cover | 读取已入库作品的来源封面 |
| POST | /sources/{source}/remote-works/{code}/import | 入库或刷新选中的 RJ 作品 |
| GET | /nodes/{node}/remote-download | 查看远端文件在服务器上的实际保存状态及最近一次任务 |
| POST | /nodes/{node}/remote-download | 将远端文件保存到来源的下载目录 |
| GET | /remote-download-jobs/{job} | 查看保存进度 |
| POST | /remote-download-jobs/{job}/cancel | 停止保存；再次启动同一文件可续传 |
| GET / POST | /scan-jobs | 任务分页/启动异步扫描 |
| GET / DELETE | /scan-jobs/{job} | 任务详情/删除终态历史 |
| POST | /scan-jobs/{job}/cancel | 请求取消，无请求体 |
| GET | /scan-jobs/{job}/issues | 扫描问题分页 |

## 来源

```json
{"name":"睡眠","path":"/mnt/data/睡眠","enabled":true}
```

本地来源创建时 name 和服务器上的绝对 path 必填；kind 可省略或设为 `local`。根目录不提供停用状态：enabled 省略或 true 均创建正常根目录，false 返回 400。离线目录仍可登记，任务会报告访问问题。重复或父子重叠路径返回 409。

kikoeru 兼容来源使用 `{"name":"远端音声","kind":"kikoeru_compatible","remote_site_url":"https://asmr.one","path":"","download_path":"/media/remote","proxy_url":"http://127.0.0.1:7890"}`。创建时先访问填写的网站，再验证 One 或 Number178 的作品契约；仅已识别的 `asmr.one` 网站可解析到独立的接口域名。验证成功后保存 `remote_site_url`、`remote_api_url` 和 `remote_dialect`（`one` 或 `number178`）。两种契约都不符合返回 422 `remote_source_unsupported`，站点拒绝匿名访问返回 422 `remote_source_access_denied`，网络失败返回 502；不创建来源。`download_path` 可省略；配置后必须是服务器可写绝对目录。`proxy_url` 可省略或留空，留空时使用服务器默认网络；指定后探测、浏览、入库、封面、附件、播放转发和服务器下载都使用此代理，支持不含凭据的 HTTP、HTTPS、SOCKS5 地址。外部媒体通过代理时要求 HTTPS，拒绝私网 IP 字面地址与本地主机名；主机名可由代理解析，TLS 仍验证目标主机身份。与所配置接口地址同主机的媒体不受此限制。Android 直连绕过服务器代理，需代理时选择服务端播放。该来源只手动选择作品入库或刷新，不接受本地扫描、监听、自动探测策略。虚拟根下按 RJ 号平铺作品目录，每个来源单独管理，相同 RJ 号不跨来源合并。入库快照包含来源的作品资料、封面地址和完整目录树；音频、视频、图片、字幕、文本和其他普通文件保持来源中的目录层级。归纳按普通作品边界建立作品，人工描述仍可覆盖来源资料。若来源不可访问，已入库资料保留，远端未保存的文件暂不可播放。

浏览 q 是普通文本，`search_field` 可为 `text`（默认）、`circle`、`voice` 或 `tag`。One 适配器将分类搜索转换为该站语法；Number178 适配器用已有名称找到对应分类，再读取该分类的作品。One 与 Number178 分类列表均可通过 `/remote-facets?field=tag` 等接口读取。sort 支持 `recent`、`downloads`、`release`、`rating`；`page_size` 返回来源实际页大小。浏览不自动入库。封面通过本服务的认证接口读取。入库/刷新需要管理员；作品树的删除以移除来源或现有目录管理规则为准。

配置下载目录后，可对已入库文件启动服务器保存。下载使用来源支持的 Range 续传；未完成文件不进入播放和本地扫描。Number178 来源若只返回播放列表而没有原文件，该作品入库返回 422，不把播放列表当作音频保存。完成的文件优先用于播放和 WebDAV，丢失或大小不符则继续从来源读取。来源移除不删除下载目录中的文件。若另把下载目录加入本地来源，它按独立本地来源扫描，可使用普通本地媒体探测；kikoeru 来源的持久媒体资料仍来自 API；播放时为视频画质协商做临时探测，不写回媒体资料，详见 playback.md。

`GET /nodes/{node}/remote-download` 的 `saved` 只在完整文件当前确实存在且大小匹配时为 true；`download_configured` 表示能否在服务器保存，`job` 为最近一次保存任务或 null。成员可查看状态，管理员可启动或取消保存。作品封面和虚拟目录中的图片、字幕可直接从来源读取，不要求先保存。

响应保留 id/name/path/kind/remote_site_url/remote_api_url/remote_dialect/download_path/proxy_url/revision/root_node_id/created_at/updated_at；本地来源的远端字段为空，enabled 在移除流程之外为 true。PUT 修改 name，须 If-Match；enabled 可省略或 true，false 拒绝。kikoeru 来源还可通过 PUT 修改 `download_path` 和 `proxy_url`；未提交的字段保持原值。`download_path` 留空表示停止新的服务器保存；更换目录不移动旧文件，旧文件仍留在原位置，播放从来源读取直到在新目录重新保存。正在保存时更换目录返回 409。`proxy_url` 留空时恢复使用服务器默认网络，修改只影响新的请求，正在进行的下载使用开始时的代理。来源地址、根路径与类型不可直接修改。

DELETE /sources/{source} 表示永久移除媒体根目录，须该来源 ETag：

- 先阻止这个根目录接收新任务，自动取消并等待相关扫描、归档、探测、刮削任务的最终写入，再执行元数据清理。独立的其他根目录任务继续运行；包含多个根目录的同一个任务是整体取消单位。
- 在同一事务删除该路径的索引、作品（包括人工资料和历史作品）、归属/规则、媒体观察、字幕/播放关联、辅助文件信息、附图及相关任务记录。混合任务历史只清除该根目录部分；播放列表与自定义收藏集移除对应成员，归档收藏集随来源移除。
- 仅该路径使用的商品/图片缓存清理；仍被其他作品引用的共享缓存保留。账号、全局设置、全局词典及服务运行日志保留。
- 不修改或删除媒体原文件。成功 204；来源、旧节点及旧作品随后均为 404。再次添加同一路径会重新索引，不恢复旧人工整理。

取消挂载文件系统上的阻塞调用可能需要等待。取消/超时不等于已删除；取消收尾或事务失败时恢复根目录使用状态，已取消任务不会恢复执行，元数据删除失败整体回滚。客户端只有收到成功才从界面移除该根目录。

## 启动、观察与取消

```http
POST /api/v1/scan-jobs
Authorization: Bearer <token>
Content-Type: application/json

{"source_ids":["source-a","source-b"],"mark_missing":false}
```

source_ids 为 1..64 个不重复且启用的来源 ID。一次任务可以扫描多个根，任务内按顺序处理。不同来源的任务可并行运行，同时执行数量由扫描队列设置决定（默认 2）；最多接纳 64 项活动任务。同一来源已有活动扫描或已达到限制时返回 409 / busy，已接纳但未取得执行名额的任务保持 queued。

返回 202、Location 和任务详情。HTTP 客户端连接结束不取消已经接受的任务。request_id 保存启动请求 ID；可据此查任务日志。建议客户端每 1–2 秒轮询任务详情，并在长时间无变化时降低频率；状态不是百分比，不能据状态推算完成时间。

状态流转：

```text
queued → running → succeeded / partial / failed
       ↘ canceling → canceled
重启时未结束的任务 → interrupted
```

| 状态 | 语义 |
| --- | --- |
| queued | 已接受，准备开始 |
| running | 正在枚举或发布结果 |
| canceling | 已请求取消，等待正在进行的操作返回 |
| succeeded | 所有根完整扫描并发布 |
| partial | 正常结束，但至少一个根有问题或不完整；可用部分仍发布 |
| canceled | 取消完成，本次观察未发布 |
| failed | 任务或保存过程整体失败，本次观察未发布 |
| interrupted | 服务在任务完成前退出，不能自动续跑；可新建任务 |

终态为 succeeded、partial、canceled、failed、interrupted。对终态调用 cancel 幂等返回当前任务；删除仅允许终态且 worker 已释放，删除的是历史与问题，不删除节点。

任务公共字段：id、state、phase、source_ids、mark_missing、request_id、observed_nodes、issue_count、error、created_at、started_at、finished_at。时间尚未发生为 null，error 正常为 null，failed/interrupted 时提供 code 和 message。列表返回摘要；详情及启动/取消响应还提供 roots 数组，每项 source_id、complete、observed_nodes、issue_count。

phase 为 queued/enumerating/publishing/finished。详情查询在枚举批次间更新 observed_nodes/issue_count（含根），无需等待数据库写连接；发布后返回最终精确计数。枚举没有预知总数，不提供虚假百分比；roots 仍在发布结束后提供。活动取消信号同样不等待写连接，取消或失败保留已统计计数，不发布部分索引。进程异常中止可能丢失最后一段内存进度。

日志记录扫描开始、阶段/批次进度、取消请求和结束；可通过认证日志接口按 job_id 或 request_id 检索。普通扫描不启动媒体读取；音视频信息提取是独立可取消任务，参见 media-probe.md。问题接口按分页返回 id、source_id、path、path_base64、operation、message。partial 的单项错误在此查询，不能只看 error 是否为空。

挂载文件系统的阻塞调用可能延迟取消：canceling 不等于 canceled。前端应持续展示当前状态，不把关闭页面、停止轮询或网络超时当作任务已停止。

## 重扫与缺失

默认 mark_missing=false，只更新本次看到的节点，不推断未见节点已删除。人工的作品文本、成员、排除与锁定不会被扫描改写。

mark_missing=true 明确要求核对缺失；只有某个根完整成功时，才把该来源未见节点标记 present=false。发生子项错误、部分目录读取、根离线或取消时，不根据未访问节点推断缺失。部分成功根会发布已观察节点；完整成功根可独立核对缺失。本次所有结果一起发布，保存过程失败整体回滚。

同来源同相对路径重扫保留节点 ID；删除后又出现会重新标记 present=true。用户直接移动或改名目录/文件后统一重扫：新路径视为新节点，不猜测移动，不根据实体证据、内容哈希、作品编号或名称相似度合并，也不自动迁移原路径的人工属性或归属。旧路径只有在完整扫描且 mark_missing=true 时按范围确认缺失；否则沿用保守保留规则。对网盘挂载使用相同本地文件 API，不依赖底层文件系统类型。挂载若静默返回一个完整空目录，服务不能分辨真实删除与挂载内容切换；需由调用方明确决定是否启用 mark_missing。

## 目录浏览与原始路径

`GET /sources/{source}/nodes` 默认列出该来源已知 present 节点，包括根；`GET /nodes` 使用同一契约并允许跨来源。浏览根的直接子项用 `parent_id=<root_node_id>`。parent_id 必须是已知目录，指定来源时必须同属该来源，否则 404。kind 可为 directory、file、symlink、other、unknown。

两种列表都支持以下当前查询字段；未知字段或无效值返回 400。所有条件、准确计数、排序和当前页在同一只读快照中计算，不读取媒体正文、不触发扫描或信息提取。

| 条件 | 取值与语义 |
| --- | --- |
| `recursive` | 默认 false；true 必须指定 parent_id，仅返回该目录后代文件，不包含范围外节点或目录 |
| `media_kinds` | audio、video、image、text、other 的逗号分隔多选；组内任一满足 |
| `formats` | 不含点的小写扩展名逗号分隔，如 wav,flac；按文件格式，不按内部编码 |
| `archive_state` | archived / unarchived；省略不限，按当前有效作品归属 |
| `availability` | present（默认）/ missing / all；是索引可用标记，不推断挂载当前在线状态 |
| `min_size`, `max_size` | 十进制字节字符串，含边界，仅已知文件大小 |
| `modified_from`, `modified_to` | RFC3339 时间，含边界，仅已知文件修改时间 |
| `min_duration`, `max_duration` | 非负有限秒数，含边界；使用有效已有资料，人工时长优先，自动时长必须来自当前有效观察 |
| `include_unknown_duration` | 默认 false；有时长范围时可同时包含未知时长，不将未知视为 0 |
| `has_subtitle` | yes / no / unknown；筛选最近保存的字幕状态。扫描、归档、媒体探测完成或人工编辑后更新；软件外增删文件要等重新扫描/归档。yes 表示已知外置、应用内或内嵌字幕/歌词；资料不完整且没有已知字幕为 unknown，资料完整且没有字幕为 no。查询不重新匹配或验证文件 |
| `language` | 音视频流的 BCP-47 语言，归一化 ISO 语言代码；und 表示未知；不使用字幕、界面或作品资料语言推断 |
| `listening_state` | unstarted / in_progress / completed，仅当前账户的个人进度 |
| `credit`, `role`, `tags`, `exclude_tags`, `released_from`, `released_to` | 按所属作品过滤；署名/角色、全部包含标签、任一排除标签和发行日期沿用作品查询语义；标签为逗号分隔，日期为 YYYY-MM-DD |
| `q` | 忽略大小写的字面子串；有 parent_id 时匹配文件名，无目录范围时匹配相对路径，同时匹配人工标题和当前有效观察中的媒体标题 |
| `sort` | name（默认自然升序）/ modified（默认降序）/ size（默认降序）/ duration（默认升序） |
| `sort_order` | asc / desc；目录始终优先，未知时长/未知文件属性始终后置；等值按自然名称、原名、路径和身份稳定排序 |
| `matching_directories` | 默认 false；非递归时只保留有符合上述文件条件的后代文件夹，matched_file_count 返回真实命中文件数 |

递归范围和目录后代关系在索引发布时维护，查询直接读取，不现场遍历目录树。人工索引操作在成功返回前更新相关范围；扫描任务发布结果时同步更新。任意组合筛选仍在分页前执行，total/file_total/directory_total 和 matched_file_count 精确统计本次读取的数据。

不同条件组同时满足。非递归查询中的文件属性和所属作品条件保留导航目录；kind=file 可隐藏目录，kind=directory 只显示目录。q 默认也匹配当前层目录名；matching_directories=true 时按后代文件命中决定目录是否显示。递归条件不受目录名称影响。unarchived 视图仍保持其仅可用未归档媒体的既有语义。

分页响应在 `{items,total,limit,offset}` 外增加 `file_total`、`directory_total`，分别是本次完整结果的文件/导航目录数；total 还可能包含符号链接等其他节点类型。两项计数不是当前页数量。

节点字段包括：

```json
{
  "id":"opaque-node-id","source_id":"source-a","parent_id":"opaque-root-id",
  "path":"MP3/01.mp3","path_base64":"TVAzLzAxLm1wMw==",
  "name":"01.mp3","alias":"翻译后的曲目名","name_base64":"MDEubXAz",
  "kind":"file","read_complete":false,"present":true,"order":2,
  "seen_job_id":"opaque-job-id","created_at":"2026-10-29T12:00:00Z","updated_at":"2026-10-29T12:00:00Z",
  "media_kind":"audio","format":"mp3","playable":true,"duration_seconds":null,
  "subtitle_state":"unknown","languages":["und"],"listening_state":"unstarted","matched_file_count":0
}
```

path 相对来源根，目录分隔符统一为 `/`。根为 `.`，根 parent_id 为 null。path/name 只在原始字节是合法 UTF-8 时提供字符串，否则为 null；相应 Base64 始终保留服务端文件系统的原始字节和路径分隔符，标准 Base64 带填充。客户端显示异常名字时可自行替换不可显示字符，但不能以替换结果反推身份或修改路径。操作关系应使用 id。

alias 是节点共享的显示名称，仅替换该目录或文件节点的展示文字，不改变上级目录、原始 name/path、搜索、排序、文件访问或归纳。未设置时为空字符串。已扫描节点重扫同一路径保留别名；移动或改名形成新节点身份，别名不自动转移。

`GET /nodes/{node}/alias` 返回 `{node_id,alias,revision}` 和 ETag；未设置别名返回空字符串及修订 1。管理员用 `PUT /nodes/{node}/alias` 携带该 ETag 与 `{ "alias": "显示名称" }` 设置别名，空字符串恢复显示原名。别名为 1–255 个 Unicode 字符、最多 1024 字节，不含首尾空白、控制字符和路径分隔符。节点可为已扫描目录/文件或已登记草稿成员；不存在返回 404，缺少 If-Match 返回 428，过期返回 412。成员读取可见别名但不能通过成员编辑改写；别名编辑不改变作品和扫描修订。

media_kind 为文件类型线索；目录为空字符串。playable 是与 node_query 共用的索引播放资格：文件须 present、来源启用，且当前观察/人工音视频声明或支持的扩展名允许播放；缺失、来源停用、非文件或明确否认音视频时为 false。它不保证现场文件可读、浏览器原生解码或所需编码器已安装；客户端播放动作使用该布尔值，不能仅凭 media_kind 判断。format 为小写文件扩展名，目录或无扩展名时为空。duration_seconds 未知时为 null；人工时长独立有效，失效自动观察不参与时长/语言/字幕否定判断。languages 是去重后的音视频流语言，未知音视频为 ["und"]，不适用时为空数组。listening_state 按当前账户读取；无进度为 unstarted，有进度且未完成为 in_progress，完成为 completed。matched_file_count 只在 matching_directories 查询的目录项提供命中数，其他项为 0。单节点详情同样返回这些汇总字段。

read_complete 只表示该目录名字已读到末尾，不代表其所有后代成功；文件该值为 false。present 表示上次观察或明确核对后的缓存状态，不是一次实时文件存在性检查。order 是返回目录树按子项枚举顺序深度优先展开的序号；文件系统重扫次序可能变化，部分扫描混合保留旧节点时也不保证一个完整新顺序。

符号链接会被记录，不主动遍历其后代；管理员配置的来源根按服务器系统路径规则解析。该 API 不提供任意文件下载或写媒体功能。

节点不再返回 manual_ref_count；通过 GET /works?node_id 查询当前唯一作品归属。作品成员 availability 与 present 由同一索引状态派生；人工关系不因资源缺失自动删除。完整扫描但仍有未核对的旧 present 节点时，后续作品整理拒绝应用，需要按调用方意图启用 mark_missing=true 核对缺失。

## 目录子树扫描

POST /scan-jobs 可增加 scope_node_id，必须是单一来源中已登记且 present 的目录。省略/空值仍全量扫描；子树扫描需要已有已确认完整基线，否则 400。实际只枚举指定目录，并与基线中的未变化区域合成新快照；同来源同相对路径的节点 ID 不变。调用方负责选对变化范围，未扫描区域的外部变化不会被自动发现。

mark_missing=true 只核对被扫描子树的缺失，兄弟节点不改变 present 或实际 seen_job_id/观察时间。任务 observed_nodes 是本次实际枚举数量，完整性表示请求范围的读取结果。子树读取失败使来源快照不完整，不能应用归纳；需完整扫描恢复可信基线。被删除的子树根不能被失败打开推断为删除，应扫描其父目录。旧未核对缺失节点仍阻止整理应用。

来源创建会拒绝与现有根目录相同、父级或子级的规范化路径（409 conflict），包括正在移除但尚未完成的根目录。仅做路径边界检查，不推断符号链接或重复挂载指向同一实体。

显式开启 [来源自动化](automation.md) 后，完整扫描可接续已启用的归纳；HTTP 新来源默认开启，也可独立关闭。来源登记还会尽力拒绝操作系统已确认的同实体或符号链接解析重叠，但这不是所有挂载的全局实体身份保证。按需查询操作系统实体证据使用 [实体比较](entities.md)，不读取媒体正文。

节点响应包含 attributes，为带来源前缀的本地语义字段；并非实时继承结果。扫描更新 auto 字段，manual/ai 字段保留，见 [节点属性](node-attributes.md)。

## 前端媒体与未归档视图

`GET /nodes` 跨来源分页，参数与 `/sources/{source}/nodes` 相同。两者提供 `view=all|media|unarchived`（默认 all）和上述完整筛选。media 保留目录和音视频文件；unarchived 仅返回 present 音视频文件，并排除已移除或正在移除的来源、拥有 active 作品 included 归属的文件、任一祖先或自身 ignored=true / boundary=exclude 的节点。该视图不创建作品、不触发探测，以有效已有观察及节点属性/扩展名作为类型线索，不保证媒体可解码。过滤在分页前执行，total/items 在同一只读快照取得。已归档展示仍使用 `/works`，不以 identification_state 区分归档。

## 新增路径的默认自动处理

`POST /sources` 使用 `automation` 对象分别设置 `auto_scan`、`auto_organize`、`auto_scrape`、`auto_probe`；提供对象时，省略的项目为 false。省略整个对象时，新来源默认开启这四项。来源和策略在同一事务保存，不会先按默认策略运行再更新；已有来源的策略不受影响。指定对象时默认使用 600 秒核对、3 秒合并等待及完整成功后的缺失核对，并在自动扫描或媒体信息提取开启时监听目录变化。后续可通过自动化策略接口分别调整。

例如只自动归档和提取已有文件：

```json
{"name":"媒体目录","path":"/media","automation":{"auto_scan":false,"auto_organize":true,"auto_scrape":false,"auto_probe":true}}
```

关闭自动扫描后，可由手动扫描提供数据；没有可处理内容时不创建后续自动任务。未知字段明确拒绝。

全局刮削开关仍受尊重；新安装默认开启，旧数据库关闭值不被覆盖。缺失核对仍只在完整成功的对应扫描范围确认，离线、不完整或取消不推断删除。来源可先离线登记，状态页显示失败及重试情况。

节点只读投影增加 work_id（当前有效作品归属，无归属为空）和 work_root（是否为活跃作品根）。与节点列表在同一读取快照查询，用于归档目录标识和打开关联作品；不会改变扫描、归属或节点语义输入。

扫描服务可接纳至多 64 项不重叠来源的任务，按 settings/queues/scan 的持久并发预算执行。单任务多个来源顺序枚举；同来源已有活动或等待任务时返回 busy。


## 无变化复扫

扫描任务仍记录本次完整性、问题与结束时间，并刷新观察 checkpoint。`index_revision` 表示可观察目录输入或可信覆盖的变化，不再按每次扫描固定递增；无变化全量或子树复扫保留输入版本和未变节点的 `updated_at`。`seen_job_id` / `snapshot_job_id` 仍指向相应观察，不能把任务 ID 变化当作媒体变化。文件枚举顺序不构成输入变化。

节点新增、实际属性改变、确认缺失/恢复、读取完整性或有效覆盖变化仍推进输入。`mark_missing=false` 时，未确认消失的节点继续保留，但其可信覆盖变化仍会使索引版本前进。发布失败整体回滚，不发送成功完成通知。

## 记录保留

已结束任务及其重试输入最多保留 48 小时；过期详情返回不存在。刮削／媒体探测任务结束后，明细仅保留未完成项，成功与复用项计入任务汇总；明细分页 total 为实际保留的明细数。性能指标仅在任务执行期间可用。日志同时受 48 小时与总容量限制。作品资料、人工修改、字幕草稿和收听记录不受影响。
