# 音视频信息提取

本模块独立于目录扫描和作品归纳。所有路径相对 `/api/v1`，认证与角色权限见[协议约定](protocol.md)，能力标识为 `media.probe`。普通用户可读取节点 media-info；工具状态、任务、受保护字段读取与编辑需要管理员。输入是已经登记的文件节点，不要求节点已有作品归属。任务读取媒体并调用外部 ffprobe；不修改源文件、节点语义属性、作品描述、归属、播放组或归纳输入代次。普通目录扫描不会自动读取媒体正文或启动探测任务。每个节点可有零或一份媒体信息（成功观察或人工补充）；探测未运行、部分失败或全部失败均不阻止扫描、归纳和创建作品，也不排除失败文件。失败仅使该节点缺少本次新媒体信息，错误属于探测任务。

## 工具可用性

`GET /media-probe/status` 返回 available、name（ffprobe）、version、error_code、workers、timeout_seconds。可用性在服务启动及工具配置生效时检查；通过处理设置修改路径后，空闲时自动应用，活动任务继续使用原配置。工具缺失不阻止核心服务启动、扫描或归纳；启动探测返回 503 / media_probe_unavailable。状态错误包括 tool_missing、tool_unusable、tool_invalid、unsupported_platform。

媒体探测支持 Linux 和 Windows x64。Linux 要求 `/proc/self/fd` 可用，Windows 要求 FFprobe 支持 `fd` 输入协议。其他平台的探测返回 unavailable。工具是服务所在机器上的程序，与客户端机器无关；不接受客户端命令、路径或 URL。

## 创建与查询任务

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| POST | /media-probe-jobs | 创建任务，返回 202、任务对象和 Location |
| GET | /media-probe-jobs | 分页任务历史；可按 source_id 过滤 |
| GET | /media-probe-jobs/{job} | 任务及活动进度 |
| POST | /media-probe-jobs/{job}/cancel | 无正文，取消；返回任务对象 |
| GET | /media-probe-jobs/{job}/items | 分页逐节点结果，可按 state 过滤 |
| DELETE | /media-probe-jobs/{job} | 删除终态任务历史，返回 204；保留成功提取结果 |
| GET | /nodes/{node}/media-attributes | 受保护字段与独立修订 ETag，无须成功探测 |
| POST | /nodes/{node}/media-attributes | 逐字段编辑或重置，必须携带 If-Match |
| GET | /nodes/{node}/media-info | 合并人工字段的音视频信息；既无成功观察又无人工记录时返回 404 |

任务创建正文示例：

```json
{"source_id":"source-id","scope_node_id":"","work_id":"","node_ids":[],"force":false}
```

source_id 必填，来源必须启用且未退役。node_ids 为 1..1000 个唯一已登记、present 文件；空数组或省略表示按节点现有 audio/video 媒体类型线索选择文件，不依赖作品成员、作品排除或作品 active 状态。未知节点、其他来源节点、目录或已知 missing 节点不允许作为显式文件，返回 404。重复、空 ID、超出上限或 node_ids、scope_node_id、work_id 同时指定多个范围为 400。

scope_node_id 可选，必须是同来源已登记、present 的目录；只选择目录下的候选文件，空值选择整个来源。创建时固定节点 ID 集合，执行各文件时重新读取当前索引位置；中途新增文件不加入已接受的任务。索引不必全来源完整，但每个实际文件须有可校验的大小和修改时间。

work_id 可选，选择该作品当前 included 成员中、属于 source_id 的 present 音视频文件，不包含子作品独占文件。它与 node_ids、scope_node_id 互斥；未知作品返回 404。作品或目录刷新均使用相同任务接口：指定 work_id 或 scope_node_id，再设置 force=true。目录中的新增文件须先做目录扫描进入索引。

force 默认 false。大小、修改时间、工具版本及已观察写事件代次匹配成功结果时，校验实时文件属性后跳过；true 强制重新提取自动观察，仍保留人工字段。普通重扫不绑定任务身份，不会仅因扫描任务变化而使缓存失效。扫描发现大小/时间变化，或启用监听后收到文件写入/替换事件，旧结果显示 stale，下一次探测重新提取。目录替换事件使后代缓存失效；通知溢出/后端故障会保守使相关来源缓存失效。

启用来源的自动媒体信息策略后，监听变化可触发相应任务；未启用时，客户端仍可按作品、目录或来源发起刷新。监听依赖来源策略 watch=true；周期目录扫描不保证发现属性完全相同且通知漏报的正文改写，此时使用 force=true。若实时大小/修改时间已经与索引不同，先目录扫描再重试；force 不绕过文件变化校验。

全服务只接纳一个活动探测任务，任务内默认两个文件并发；忙碌为 409 / busy。请求连接结束不取消已接受任务，使用取消接口明确取消。任务与扫描、归纳可并行执行；文件处理不阻止其他客户端读取已保存的媒体信息。

## 状态、计数与错误

任务字段：id、source_id、scope_node_id、work_id、force、state、total、processed、succeeded、skipped、failed、request_id、error_code、created_at、started_at、finished_at。身份是字符串，时间为 RFC3339，尚未发生的时间为 null。request_id 是创建请求的 ID；轮询不会覆盖它。

状态为 queued/running/canceling/succeeded/partial/failed/canceled/interrupted。零文件任务可成功；全部处理成功或命中缓存为 succeeded；部分文件失败为 partial，全部文件失败为 failed，error_code 为 item_failures。processed 是已提交成功/跳过/失败的数量，等于 succeeded+skipped+failed；未完成的取消项不计入 processed。结果按文件独立提交，取消或任务故障保留已提交结果。

复用已有结果只累计任务 `skipped` 总数，不保留完成后的逐文件复用明细，也不因复用项输出 progress 文本日志；最终 finished 日志包含完整汇总。pending 目标在复用校验通过后与计数更新同一事务移除，失败回滚两者。最近成功的媒体信息仍保留。

逐节点字段：node_id、state、error_code、updated_at。明细 state 为 pending/running/succeeded/failed/canceled，列出实际提取、失败及未完成项。不保存 skipped 明细，state=skipped 查询返回 invalid_argument。明细 total 是过滤后条目数，可能小于任务 total/processed。检查缓存中的文件在完成前仍显示 running，检查开始时间反映在 updated_at 中；状态筛选、total 和分页采用相同有效状态。工具或解析错误仅返回稳定代码，不透出媒体路径、原始 stderr 或命令。

| error_code | 含义 |
| --- | --- |
| node_unavailable | 文件不可打开、非普通文件或来源已不可用 |
| index_stale | 实时大小/修改时间与索引不同、索引属性未知，或保存时索引/来源已变化；重扫后重试 |
| file_changed | 提取期间文件变化或路径被替换；重扫后重试 |
| probe_timeout | 单文件工具运行超时 |
| probe_failed | ffprobe 未能提取，含损坏、未支持的容器或解码器错误 |
| no_media_stream | 未发现音频或非附图视频流 |
| invalid_output | 工具输出不是可用的结构化媒体信息 |
| output_limit | 工具标准输出或错误输出超过限额 |
| storage_error / internal_error | 任务存储失败或内部故障 |
| server_restarted | 重启前任务未结束；任务标为 interrupted，剩余项标为 canceled |

活动任务查询和取消可在处理期间使用；取消和成功提交竞争时，最终可能仍成功。服务重启不续用旧进程或自动重新启动任务；成功缓存保留，客户端可新建任务继续，正常缓存会跳过已成功文件。任务和逐节点分页遵循 limit=1..500（默认 50）、offset>=0。

## 结果字段与含义

media-info 返回 node_id、byte_size、modified_ns、tool_version、extracted_at、content_revision、revision、fields、inactive_fields、state、information。content_revision 是成功观察绑定的写事件代次；revision 是独立字段编辑修订，均为字符串。byte_size（字节）和 modified_ns（Unix 纳秒）是字符串，避免客户端浮点精度丢失。state 是相对当前索引的 unprobed/current/stale/unavailable；unavailable 表示已知 missing、类型变化、来源停用或退役。查询不实时访问媒体，current 不保证文件正文未发生尚未扫描的变化。后续提取失败不会替换上一次成功结果或人工编辑。unprobed 表示只有人工补充、尚无成功观察，extracted_at 为 null。

information 包含：

- format_name、format_long_name；duration_seconds、start_seconds、bit_rate（bit/s）；容器 tags。
- streams：index、kind、codec、codec_long_name、profile、duration_seconds、start_seconds、bit_rate；sample_rate（Hz）、channels、channel_layout、bits_per_sample、sample_format；width/height（像素）、pixel_format；average_frame_rate、frame_rate、time_base 保留有理数字符串；color_range、color_space、color_transfer、color_primaries；language、default、forced、attached_picture 与流 tags。
- chapters：id、start_seconds、end_seconds、tags；与作品逻辑片段相互独立。
- has_audio、has_video：附图视频流不计入 has_video；truncated、warnings 表示提取限制。

未知数值为 null，不用 0 伪装未知时长/采样率。language 来自流标签，保留工具值，可能是 jpn/eng/und，不根据界面语言推断。default/forced 是容器声明，不是用户播放偏好。tags 保留工具返回的键和值，不自动采纳为作品标题、专辑、声优或文本语言。非 UTF-8 标签在工具解码中可能丢失原字节；出现替换字符返回 text_replacement_character 警告，不猜测修复编码。

单文件工具默认 30 秒；标准输出最多 4 MiB，stderr 最多 32 KiB；最多返回 128 个流、2000 个章节、每组 256 个标签，单标签值最多 16 KiB。截断有 truncated=true 以及 stream_limit/chapter_limit/tag_limit 警告。工具限制单次分配、探测量、协议与容器；不承诺限制总内存。仅获取内嵌字幕、封面和附件的流描述，不导出字幕正文、封面图片或执行转码。


## 字段编辑与重置

`GET /nodes/{node}/media-attributes` 返回 `{node_id,revision,fields}` 和强 ETag。已登记文件即使未探测、工具缺失、资源 missing 或来源停用仍能编辑；未知节点、目录及退役来源返回 404。fields 仅含受保护字段，未出现的字段默认采用自动观察。编辑不会修改源媒体文件、作品资料或归纳代次。

`POST /nodes/{node}/media-attributes` 携带 GET 的 ETag 到 If-Match，正文局部更新，未列出字段保留：

```json
{"fields":{
  "/tags/title":{"origin":"manual","value":"人工标题"},
  "/streams/0/language":{"origin":"ai","value":"jpn"},
  "/duration_seconds":null
}}
```

origin 仅为 manual 或 ai，两者均在重新提取时保留。字段条目为 null 撤销保护，立即采用最近一次自动观察，无须重新读取媒体。条目的 value 为 null 则是显式清空可空数值或移除指定标签，与重置不同。编辑前后值完全相同不增加 revision。一次批量无效则全部拒绝；修订过期 412，缺少 If-Match 为 428，不提供 force 绕过。扫描和自动探测不会使编辑 ETag 过期，并发人工编辑会使其过期。

路径使用 JSON Pointer：`~0` 表示 `~`，`~1` 表示 `/`。支持容器标量（format_name、format_long_name、duration_seconds、start_seconds、bit_rate、has_audio、has_video）、`/tags/<键>`、`/streams/<index>/<标量字段>`、`/streams/<index>/tags/<键>`、`/chapters/<id>/start_seconds`、`/chapters/<id>/end_seconds`、`/chapters/<id>/tags/<键>`。流 index 与章节 id 是观察中的身份，不是数组位置；id/index、流/章节集合、truncated/warnings 不可修改。不存在的流/章节的保护仍保存，media-info 的 inactive_fields 列出未应用路径，不虚构流；身份再次出现时恢复应用。

数值必须使用 JSON 数字，未知数值可为 null；整数技术值必须 >0，时长和章节终点不得为负，起始时间允许负数。布尔/字符串不可为 null，标签字符串可为 null 表示删除。每次最多 2048 项，每节点总保护数不超过 2048；标签键最多 256 字节，字符串最多 16 KiB，保存的字段 JSON 最多 4 MiB，HTTP 正文还受通用 1 MiB 上限约束。

任务日志可通过认证日志接口按任务 ID 或创建请求 ID 检索；探测记录 queued、started、progress、item_failed、cancel_requested、finished。progress 限频记录 total/processed/succeeded/skipped/failed；单文件长时间执行时可轮询任务与 running 条目。日志保留和轮转遵循通用日志规范，任务计数和逐项结果持久保存，历史被删除后不保证日志仍可检索。

## 失败版本退避

真正执行提取失败后，按节点、大小/mtime、写事件代次及工具版本保存失败退避，不覆盖成功观察。同一快照首次等待 30 秒，连续失败翻倍，最多 1 小时；重启保留。退避期间返回逐项 failed/probe_backoff，不启动 FFprobe，也不伪造成功或当成文件缺失。文件属性/观察写代次/工具版本变化自然绕过，显式 force=true 也可绕过。取消、存储错误、索引过期或文件变化不作为同版本解码失败缓存；成功提取清理该节点失败记录。

自动流程仅选择缺少当前成功观察的节点。首次发现文件直接尝试提取，不把发现时间当作写入证据；大小/mtime 改变或监听写事件后，该文件等待 10 秒。未稳定项的 state=deferred、error_code=file_settling，任务汇总 deferred 计数及 partial 状态明确区别暂缓和解码失败。deferred 是本次任务结果，不代表查询时文件仍在写入；自动重试创建独立任务，原任务历史保持。未变更复扫不重置写入时钟。显式 API 请求不使用自动稳定门。等待只作用于该文件，不阻塞同任务其他文件。首次尝试仍校验提取前后的文件快照；同版本工具失败沿用持久退避，避免反复执行工具。

## 记录保留

已结束任务及其重试输入最多保留 48 小时；过期详情返回不存在。刮削／媒体探测任务结束后，明细仅保留未完成项，成功与复用项计入任务汇总；明细分页 total 为实际保留的明细数。性能指标仅在任务执行期间可用。日志同时受 48 小时与总容量限制。作品资料、人工修改、字幕草稿和收听记录不受影响。
