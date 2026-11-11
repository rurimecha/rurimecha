# 用户界面配套接口

以下路径省略 `/api/v1`。全部要求已登录账户；管理操作另需管理员角色。同源 Web 使用 Cookie，原生接口保留 Bearer。ID、修订和 ETag 均按字符串处理。Cookie 写入仍验证同源；个人偏好、收藏集和列表按账户隔离，共享媒体库供各账户浏览。

## 外观与浏览偏好

`GET /account/preferences` 返回 ETag 和 `{theme,cover_tint,compact,work_grid,work_sort,filter,directory_browse,resource_browse}`。默认 system、封面取色开启、非紧凑、网格、newest、空筛选。`PUT` 携带该 ETag 完整替换；保存已应用筛选，不保存表单草稿。filter 支持 `library_type=doujin|asmr` 和已应用的 `search_scope=all|title`；省略时网页默认同人音声，API 本身不限制分类。分类与最近入库等排序并行，切换重置分页；ASMR 隐藏作品编号排序和编号搜索提示。可选 smart_path、prefer_effects 控制智能目录与效果音偏好；audio_format_order、video_format_order 分别为音频、视频的不重复小写字母数字格式优先列表；未指定时使用客户端默认排序。偏好独立持久化，不改变媒体资料。过期修订为 412，不自动覆盖。

目录和作品资源使用独立的偏好，不互相改变作品库网格与筛选：

```json
{
  "directory_browse": {
    "grid": false,
    "sort": "name",
    "sort_order": "asc",
    "filter": {},
    "saved_filters": [
      {"id":"preset-id","name":"无损音频","source_id":"source-id","parent_id":"folder-id","filter":{"formats":"wav,flac","recursive":"true"}}
    ]
  },
  "resource_browse": {"view":"media","mode":"directories"}
}
```

目录默认列表、名称自然升序和空条件。directory_browse.filter 和 saved_filters[].filter 保存节点查询的已应用非空字符串条件，允许 q、sort、sort_order，禁止 parent_id、limit、offset；真实范围使用已保存条件外层的 source_id、parent_id。saved_filters 每项含独立 id、名称和原来源/目录范围，重新打开时按当前节点查询校验，不把失效目录替换成全库。resource_browse.view 为 media/all/audio/video/image/text，mode 为 directories/categories；默认 media/directories。目录和资源两个对象在当前偏好格式中均为必需。每个账户最多保存 100 条；id 为 1..64 个 UTF-8 字节且在数组内唯一，name 必须非空白且至多 120 个 Unicode 字符，source_id、parent_id 和字符串映射 filter 均为必需。保存仍使用完整偏好对象和当前 ETag，不保存未应用草稿。

GET 读取到不符合当前格式的已保存偏好时，返回 HTTP 409、error.code=preferences_format_mismatch；不返回缺失字段的旧对象，不填充旧值，不清理、迁移或重写数据与配置。未保存过偏好的账户正常返回当前默认对象。客户端保留错误状态和重试入口，不能把该错误显示成保存成功。

## 资料语言和播放选择

`metadata_locale_order` 是按账户保存的语言顺序，默认简体中文、繁体中文、日语、英语。每项为不重复的具体 BCP 47 代码，最多 32 项。该顺序用于浏览资料，刮削语言由管理设置控制。

`GET /account/works/{work}/metadata-language` 读取个人选择。`PUT` 提交 `{"locale":"ja"}` 保存，空字符串恢复跟随账号。当前作品没有指定语言时返回 404。单作品选择只影响这个账户的展示和统计。后续资料删去该语言时按账号顺序显示其他可用资料。

## 归档与自定义收藏集

收藏集是有序的作品/单曲组合，与设备当前播放队列独立。`GET /collections` 返回 `items`；每项包含 id、revision（字符串）、name、alias、origin、items、truncated、updated_at。`origin=archive` 为归档生成，带 source_id、node_id、source_name、path；其 alias 来自目录节点，仅用于显示。`origin=custom` 为自定义，alias 为空。名称允许相同，身份不由名称决定；客户端按归档/自定义区分并为归档项显示来源目录。

`POST /collections` 接收 `{name,items:[...]}` 创建自定义收藏集。条目为 `{id?,kind:"work",work_id,group_id?,format?,node_ids?}` 或 `{id?,kind:"node",node_id}`。条目 id 是一次出现的独立身份，新条目省略时服务端生成，重排保留；作品条目按 work_id、直接单曲条目按 node_id 各只保留第一次出现。同一文件仍可同时出现在作品条目和直接单曲条目中。创建、完整替换、追加及读取自定义收藏集均采用此规则；归档收藏集不受影响。work 的 node_ids 可保存已设置过的曲目顺序及删减范围；未提供时，播放时使用作品当前默认播放组及默认视角，group_id/format 可覆盖。node_ids 中的节点须属于该作品。每集最多 5000 项，作品快照节点总数也最多 5000。

`GET /collections/{collection}` 返回 ETag，并为 items 增加 name、alias、available 及可选 work_revision、cover_ref；文件条目的 alias 来自节点，作品条目为空。作品不会在详情请求中展开。`PUT` 为完整替换名称和顺序；`POST /collections/{collection}/items` 原子追加条目；`DELETE` 删除自定义收藏集。三种写入都要求当前 If-Match，过期返回 412。归档收藏集写入返回 409/locked，可读取后另存为自定义。新引用须有效，已保存的不可用引用可以保留、排序或移除。

`POST /playback/resolve` 使用 `{collection_id}` 播放完整收藏集，或使用 `{entries:[...]}` 展开/播放指定条目。结果保留 selection_id、selection_kind、selection_work_id、selection_title、selection_group_id、selection_format，客户端为每次加入队列生成独立分组身份；作品作为可展开目录、单曲作为文件展示。最终展开最多 5000 首，超限请求失败，不截断当前队列。收藏集编辑、归档更新不会改写已生成的设备队列快照。

目录 `collection=true` 会在完整归档发布时同步生成对应的归档收藏集：包含后代独立作品，以及未被这些子作品覆盖的音视频文件，按自然顺序排列。平铺音频合集直接生成单曲条目。排除/忽略子树不纳入；不完整归档保留先前收藏集，完整结果撤销不再满足条件的归档收藏集。归档项最多 5000，省略部分以 truncated=true 明示。此投影不改变作品根、编号、元数据或文件归属。移除来源会删除对应归档收藏集，并只清理自定义收藏集中属于该来源的条目。

不再支持智能收藏集、filter/work_ids 收藏集正文或 works 查询的 collection 参数。作品库仍有独立的搜索和筛选。

`GET /works` 与分面支持 sort=newest/title/code/relevance、tags（全部满足）、exclude_tags（任意命中即排除）、kind=audio/video/image/text、has_subtitle、source_id。分面 credit_roles 与 tag_categories 含 value/count 和 role/category，最多 500 组，超限标记 truncated。

## 目录、资源和文件信息

- `GET /filesystem?path=/&q=&limit=50&offset=0`：服务器目录选择器，返回 path/parent/items/total；不列文件或符号链接，不读取文件内容。Linux 默认从 `/` 浏览；Windows 未指定路径或传 `/` 时返回可用盘符，`path` 和 `parent` 均为空字符串。进入盘符根目录后，`parent` 为空字符串，表示返回盘符列表。普通目录仅接受服务器上的绝对路径。
- 节点查询支持 sort=name/modified/size/duration 和 sort_order=asc/desc，默认名称自然升序，修改时间/大小未指定方向时默认降序；目录始终优先，未知资料后置。指定 parent_id 时 q 搜索名称，其他查询搜索路径及有效媒体标题。完整筛选及计数先于分页，见 [目录查询契约](scanning.md)。
- `GET /works/{work}/resources?parent_id=&view=media&limit=50&offset=0`：仅投影当前作品的文件和真实祖先目录；手工组合从真实来源根进入。默认从作品根列子项，无作品根时列来源入口。view 为 media/all/audio/video/image/text，默认保留目录与音视频。条目同时返回原名与节点 alias；返回 items/total/parent_id/root_node_id。
- `GET /nodes/{node}/file-information?force=false`：独立、按需读取文本预览或图片/PDF 属性。文本最多读取 2 MiB、预览 64 KiB；UTF-8/UTF-16，GB18030 回退标注推测。失败保留说明。缓存校验实际文件属性、内容失效代次和当前工具配置，不更改扫描或归属。工具可用性以接口返回为准。

## 图片与播放背景

`GET /works/{work}/artwork` 返回本地图片；`GET /nodes/{node}/artwork` 返回当前音视频的播放图片。响应 items 的 path 是相对 `/api/v1/` 的认证读取地址。专属图片要求同来源、同直接父目录，匹配完整媒体文件名或不含扩展名的前缀，后续必须是明确分隔符；01 不匹配 010。专属图片优先，否则回退作品图片、缓存商品图片或附近图片，最后尝试已有观察中的内嵌封面；不抽取视频帧。

`GET /nodes/{node}/image-roles` 返回 ETag 与 roles；`PUT` 带 ETag 和 `{roles:[cover,track,other],reset:false}`。分类可重叠；reset 恢复自动分类。人工分类独立存储，重扫不删除。封面引用继续使用现有作品 cover_ref 手工修改接口。

`GET/HEAD /nodes/{node}/image` 提供不超过 16 MiB 的 JPEG/PNG/GIF/WebP，`GET/HEAD /nodes/{node}/embedded-artwork` 有界提取已观察附图。根限制、来源状态和会话每次重查；支持 Range/条件请求，缓存 private。内嵌附图按节点文件快照和内容失效代次缓存到独立存储，单图最多 8 MiB；提取共享已有播放处理上限；工具缺失不阻止浏览和原文件播放。

## 人工文件组合

`POST /manual-files` 携带所选来源的 attributes ETag：

```json
{"source_id":"source-id","node_ids":["file-id"],"mode":"combined","title":"作品标题","allow_reassign":false}
```

mode 为 combined/separate/existing；existing 增加 work_id、字符串 work_revision。最多 500 个不重复、可用的索引文件，必须全部同来源。separate 只接受音视频，并收纳同直接目录、完整媒体名追加扩展名的字幕。已有作品选择可用 `/works?source_id=...&q=...` 分页。

操作整批生效。相同目标下已经归属的文件也会保存人工指定；默认归属冲突整体失败；allow_reassign=true 明确允许转移，锁定仍保护。来源输入代次和已有目标修订必须有效；失败/取消不留下部分作品。返回 work_ids/file_count。不支持跨根目录组合，不触发扫描或刮削。完整归档会统一处理人工文件作品的 active 状态，全部文件被忽略时可停用，解除忽略后按原 ID 恢复；不完整输入保留受影响作品。

## 解析设置与耗时

`GET /settings/processing` 返回 ETag、settings/active/pending/active_jobs/validation/revision。设置为 metadata_workers（1..32）、ffmpeg_path、ffprobe_path、exiftool_path。`POST /settings/processing/test` 测试候选、不保存；`PUT` 携带 ETag，验证成功才持久保存。留空查找 PATH，也可填目录；FFprobe 留空优先使用指定 FFmpeg 同目录工具。活动任务保持旧快照，相关消费者空闲后自动应用；保存失败保留旧配置。启动选项只为尚无保存设置时提供初值。

`POST /settings/processing/prepare-tools` 携带当前设置的 If-Match，管理员显式启动固定版本 Linux/Windows x64 LGPL FFmpeg/FFprobe 下载。服务器验证归档及程序哈希、许可证文本、所需编码器和安全文件输入后，在应用数据目录保存程序并替换 ffmpeg_path/ffprobe_path；活动任务结束后按现有空闲规则生效。失败不改当前设置或媒体，重试由管理员再次发起。并发请求返回 409 media_tools_busy；不支持的平台返回 422 media_tools_platform_unavailable；下载或工具检查失败分别返回 503 media_tools_download_failed、422 media_tools_unusable。发行包已随附 FFmpeg/FFprobe；本接口供管理员按需重新准备工具。ExifTool 是独立可选工具，缺失时文件详细信息返回提示，不阻止 FFmpeg/FFprobe 设置。

`GET /task-metrics/{kind}/{job}` 查询采集的并发、工具快照、elapsed_ms、phases_ms、steps 和 finished。kind 为任务接口名称（如 scan-jobs/media-probe-jobs）；未采集的历史返回 available=false，不伪造。步骤包含 calls/failures/total_ms/average_ms/max_ms；并行与包含关系下不得将它们相加作为墙钟耗时。


`DELETE /library-jobs/{job}` 仅删除终态流程历史；活动任务拒绝，不删除索引、媒体或子任务记录。其他任务沿用既有取消、分页、结果、删除及重试接口；重试创建新任务，不改写旧记录。

## 迁移与恢复

管理员可使用[迁移接口](migration.md)导出与恢复系统数据、全部作品或单部作品。

作品资源的分类模式可用 `/works/{work}/members?kind=all|audio|video|image|text`；all 仅列文件，目录模式使用 resources 投影。分类与计数在服务端分页前应用，不通过丢弃当前页条目实现。

`GET /works/{work}/resource-summary` 返回所有当前可用音视频的 files/bytes/duration_seconds，以及按真实来源和直接父目录分组的 items（最多 200 组，另有 truncated）。总计不受分组展示上限影响，字节数为字符串；不同格式分别累计，不标为去重作品时长。人工时长优先，失效自动观察不计入。

媒体探测任务详情的可选 input 字段表示创建时的范围，可用于按原范围重试；未提供该字段时，不推测原请求。

新来源创建会拒绝媒体目录与应用数据目录的父子/相同重叠（media_data_overlap），避免将程序自己的数据库、日志和缓存纳入媒体自动监听。离线的其他路径仍允许登记。

根目录操作现为“移除”：先自动停止相关后台任务，再永久清理该路径的媒体元数据及列表引用；无停用/恢复退役入口。子目录 exclude/ignore 仍仅影响归档，扫描枚举和目录访问沿用原语义。


## 播放组与收录内容

`GET /works/{work}/resources` 与 `GET /works/{work}/resource-summary` 支持可选 `playback_group_id`。指定后仅统计/展示该组的included成员；目录树先过滤成员再投影其真实祖先，分页和总数基于过滤结果。汇总的文件数、字节数、时长及分目录明细使用相同播放组条件。组必须属于URL中的作品，不存在或跨作品的组返回404；本作品空组返回空资源和零汇总。省略/空字符串保留全作品范围，不改变既有目录和成员归属。

分类视图继续使用已有 `/members?playback_group_id=...`。网页“默认播放组”在作品已配置默认组时使用该ID，否则展示全作品；明确选组后目录、分类、汇总及作品级播放/加入队列/添加到播放列表使用同一选择。切组重置目录路径和资源页码，保留视图类型与页面滚动位置，不自动开始播放，也不保存或修改作品默认组。

资源汇总也返回 `preferred_playback`，包含推荐播放组、格式、初始目录和按账号顺序排列的可用格式。显式播放组优先。原汇总仍按请求的播放组统计。

资源汇总的 `playback_formats` 按格式和直接父目录返回可播放文件数，使用作品播放的可用性及默认版本筛选，不受目录汇总 200 项限制。未标注格式时按已支持的文件扩展名回退；仍无法判断时为 `__unclassified`。

`has_subtitle=true` 只统计有效成员中、当前存在且来源可用的 included 字幕关联，或这些媒体已观察到的内嵌字幕流/非空歌词标签；排除关联、缺失资源与无关的歌词作者标签不计入。该筛选反映索引和观察信息，不保证字幕已成功解码。`false` 为相反集合，作品查询与分面共用此条件。


### 封面缩略图

`GET/HEAD /nodes/{node}/image`、`/nodes/{node}/embedded-artwork` 和 `/works/{work}/images/{image}` 接受 `size=thumbnail`，返回最长边不超过 512 像素的 JPEG；省略参数返回原图（内嵌封面返回提取图）。缩略图保持比例，透明区域合成白色。超出 4000 万像素的图片拒绝解码。使用独立 ETag 和私有缓存重新验证。

`GET /settings/thumbnails` 返回 `max_bytes` 和 ETag，默认 20971520 字节；`PUT` 携带 If-Match 完整替换，取值 0..1073741824，0 禁用缓存。配置立即生效，缩容按最近最少使用淘汰；缓存只驻留服务进程内，重启后为空。

## 账户范围

偏好、自定义收藏集及播放列表仅属于当前认证账户；归档收藏集共享只读。只有管理员可使用加密系统迁移包导出和恢复所有账户的个人记录。媒体来源/作品/图片角色等共享资料仅管理员可修改，普通用户只读。

## 商品版本选择

`version_mode=language|original` 控制默认商品版本，默认 language；`edition_language_order` 为独立于资料语言的版本语言顺序，默认空数组（优先已收录原作），最多 32 个不重复的具体语言，不接受 und。两个字段是当前偏好格式的必需项；完整 PUT 仍校验 ETag。网页和 Android 共用当前账号的服务器偏好，不影响设备下载和当前播放队列。

`GET /account/works/{work}/preferred-version` 返回当前作品的 product_code、edition_language、original、playable、version_count、local_work_count、同商品 copy_count，以及显式指定的 work_id（未指定为空）和当前推荐 preferred_work_id。响应 ETag 为 `"version-choice:<revision>"`，修订按账号保存。

`PUT` 同一路径，携带该 ETag，正文 `{"work_id":"目标作品 ID"}`；目标必须为当前同家族的有效服务器作品。空字符串恢复自动选择。不能通过请求体指定其他账号。显式选择可以保留暂不可播放的作品，但自动推荐优先可播放副本；删除作品会撤销其关联选择。过期修订 412、无 If-Match 428、跨家族目标 400、无有效锚点 404。查看其他版本和播放不会隐式改写偏好。
