# RJ 音声元数据刮削

无需本地作品、可包含本地译文和图片的离线导入，见 [离线元数据包](metadata-packages.md)。

所有接口都要求当前服务管理认证。新安装刮削默认开启，已有关闭设置保留；开启后由客户端提交作品任务，不自动枚举媒体、创建作品或改变归属。现有人工和自动作品均可刮削；普通商品刮削作品须有唯一 `dlsite` 标识符，值为 RJ/BJ/VJ 加 6 位或 8 位数字（输入大小写标准化）。同 RJ 的作品共享一份成功上游缓存，作品身份、归属和人工值仍分别保存。完整的语言资料默认复用已有缓存；部分获取时只补取未完成字段，显式 `force=true` 重新获取本次所选资料。图片缓存是独立阶段，缺图仍可能联网补齐。

## 设置

`GET /api/v1/metadata-scraper/settings` 返回设置及 ETag：

```json
{"enabled": true, "providers": [{"id":"scrape","enabled":false},{"id":"dlsite","enabled":true}], "metadata_locales": ["zh-Hans", "ja"], "scrape_url": "", "scrape_token_configured": false, "workers": 4, "timeout_seconds": 15, "proxy_url": "", "proxy_configured": false, "revision": "1", "download_extras": false, "retry_attempts": 2, "cache_max_age_hours": 0, "failure_cache_seconds": 300, "image_cache_max_bytes": 1073741824, "active": {"enabled": true, "providers": [{"id":"scrape","enabled":false},{"id":"dlsite","enabled":true}], "metadata_locales": ["zh-Hans", "ja"], "scrape_url": "", "scrape_token_configured": false, "workers": 4, "timeout_seconds": 15, "proxy_url": "", "proxy_configured": false, "revision": "1", "download_extras": false, "retry_attempts": 2, "cache_max_age_hours": 0, "failure_cache_seconds": 300, "image_cache_max_bytes": 1073741824}, "pending": false, "active_jobs": 0}
```

新安装默认作品并发 4、超时 15 秒、成功缓存永久复用（0）；已有保存配置不覆盖。

`PUT /api/v1/metadata-scraper/settings` 使用当前 ETag 的 `If-Match`。提交 `enabled`、`workers`（1–32）、`timeout_seconds`（5–180）。`proxy_url` 可省略或 null 以保留，空字符串清空；支持 HTTP、HTTPS、SOCKS5、SOCKS5H，可包含认证信息。返回值删除代理账号和密码，因此普通设置保存应省略 `proxy_url`，不要把脱敏返回值当作带凭据原值写回。

`metadata_locales` 是自动获取语言列表，可独立开启 `zh-Hans`、`zh-Hant`、`ja`、`en`，至少一种；新安装默认 `zh-Hans`、`ja`。自动任务会逐一尝试所有开启的语言，成功资料并存。`POST /metadata-scrape-jobs` 可带 `locale`，仅为本次任务指定一种语言，重试沿用原选择。某种语言未取得时不会把其他语言内容改标为该语言。请求进度按所选语言记录，标题与简介分别按实际文本语言保存，二者可以不同；开启更多语言不改变已有文本的语言标记。获取语言与客户端展示选择独立，来源自动处理开启时会按生效配置补齐所选语言。

`download_extras` 控制默认下载样图和介绍插图，默认 false；PUT 省略/null 保留原值。封面在普通刮削任务中始终参与补图。开关本身不启动任务；download_extras 同其他设置一样可随时保存，活动任务结束后启用。

并发是整个服务共享的**作品**数量；一部作品的 DLsite 商品 JSON/AJAX 发现、按当前商品语言抓取 HTML/必要的本地化 JSON，或者缓存站请求均在同一个槽位执行。同一作品不能同时进入多个任务；同 RJ 的网络请求还会合并，异 RJ 可并发。运行期间允许保存全部配置；顶层字段和 ETag/revision 表示已保存值，`active` 是当前生效的公开快照，`pending` 表示待生效，`active_jobs` 为当前任务数。原任务（包括图片读取与发布）继续使用启动快照；关闭开关也不自动取消原任务，需停止时使用任务取消接口。全部当前任务结束后启用最新保存值；待生效期间新任务返回 `409 scraper_settings_pending`，串联自动流程按忙时策略等待，避免新任务持续延后配置生效。空闲保存立即生效，重启时直接采用保存值。多次保存最后一次生效，过期 ETag 仍为 412，非法输入保留旧配置。关闭保留已有成功元数据和人工值；再次开启不会自动重跑历史任务。

超时是 DLsite 整个阶段的预算，也是每个 HTTP 请求的最大时长；缓存站 HTTP 请求最多为该设置的三倍。代理为空时直连，不继承进程代理环境变量。返回体有大小上限；失败信息不包含代理凭据或上游正文。

## 任务

- `POST /api/v1/metadata-scrape-jobs`：`{"work_ids":["作品ID"],"force":false}`，1–1000 个唯一 ID；返回 202 任务和 Location。未开启返回 `409 scraper_disabled`，同作品已有任务或同时活动任务达到 32 返回 `409 scraper_busy`。
- `GET /api/v1/metadata-scrape-jobs`：`limit=50`、`offset=0`，limit 1–500，返回 `{items,total,limit,offset}`，按创建时间倒序。
- `GET /api/v1/metadata-scrape-jobs/{job}`：状态及 total/succeeded/failed、请求 force 和 cache_hits，时间为 RFC3339。
- `GET /api/v1/metadata-scrape-jobs/{job}/items`：同样分页，每项包括 `work_id,code,state,provider,error,cache_hit,locale_results`。`metadata_locales` 为本项实际处理的语言；`locale_results` 分别报告所选语言 `succeeded`、`partial` 或 `unavailable`；部分语言未取得时，已成功的资料仍保存，任务显示部分完成。
- `POST /api/v1/metadata-scrape-jobs/{job}/cancel`：先发出取消信号，再返回当前任务。终态可能尚未写入，应继续读取任务。已完成任务取消幂等；先前已提交的作品保留。
- `DELETE /api/v1/metadata-scrape-jobs/{job}`：仅清理非活动任务及其结果列表，204；不清理作品元数据。

任务状态为 `queued,running,succeeded,partial,failed,canceled,interrupted`。`partial` 表示部分作品或部分语言/字段未完成；全部作品均未取得任何可发布资料时为 `failed`。项目状态为 `queued,running,succeeded,partial,failed,canceled,interrupted`。服务重启将未完成项标记 `interrupted`，不会暗中重发网络请求。`failed` 数量只统计失败项，不含取消和重启中断项。

商品阶段默认逐请求语言复用该 RJ 的完整持久缓存；跨作品、任务和重启有效，默认没有自动过期。部分资料按失败/未知字段有界补抓，已成功的空字段同样视为完成，不能仅凭标题存在判定整种语言完成。重复的部分获取受失败冷却限制；显式重试或 force 可再次尝试。缓存缺失、配置的有效期届满或 force=true 时，按照 providers 顺序尝试启用的来源。缓存站内部的上游选择不改变 Core 的调用接口。失败保留上次成功值。请求期间手工修改作品（包含 RJ）或作品删除会阻止该作品的旧修订投影，但有效上游结果仍保存为共享事实供其他作品使用；取消和设置失效阻止旧请求发布；条目错误包含 `stale_work,not_found,invalid_metadata,provider_unavailable`，可重新提交该作品。

`cache_hit=true` 表示本项使用共享事实并成功应用到作品，没有为本项重新抓取商品资料；仍可能下载缺失图片。`cache_hits` 是此类成功项数量。结果完全相同不增加作品修订。同批次/重叠 force 请求可复用其开始后产生的新缓存修订，所以一次批量 force 对同 RJ 只刷新一次。刷新失败保留已有成功缓存，下次默认任务仍可使用；无有效标题/身份的失败不缓存，后续默认任务允许重试。

共享缓存刷新不会立即重写所有同号作品。其它作品在下次默认任务或归纳时应用最新事实；默认模式下各作品的人工字段优先；显式全面覆盖按上述范围处理。共享缓存与作品中其他语言的文本及字段分别保留，不把请求语言用作原作正文语言。任务历史或作品删除均不删除共享 RJ 事实。

## 手工刮削与缓存应用模式

作品详情的“更新作品信息”和“应用已有缓存”都可选 `preserve`（默认，保留人工覆盖和候选锁）或 `overwrite`（仅覆盖成功部分）。前者 `force=true` 重新获取；后者 `cache_only=true` 只使用已有缓存，不获取商品资料或下载图片，不能与 `force`、`force_images`、`images_only` 同时使用。没有缓存时任务失败并报告 `not_found`，不会联网。

`overwrite` 必须传 `expected_revisions`，以作品 ID 映射当前十进制修订字符串；任务提交、排队和网络过程中版本或编号变化均拒绝旧结果。重试对每部作品只选择尚未取得或部分完成的语言；若失败发生在发布或图片阶段，则沿用该作品原选择。保留覆盖模式和最后一次已确认的作品修订，不能静默覆盖之后的编辑。`force` 只控制抓取，不表示跳过保护。用户主动 `force=true` 会重新获取所选语言的完整资料，即使先前只有部分资料成功；`retry-failed` 则仅补未完成的语言与字段，保留已成功结果。一次完整刷新内的有限补抓只复用本次新取得的成功字段，不把旧部分缓存当作本次完整刷新。

全面覆盖逐一使用所选语言各自未合并的成功快照，语言资料中的人工字段保护保留，未选语言不更新。单语言任务 sections 使用普通字段名，多语言任务使用 `<request-locale>/<section>`。实际标题与简介的独立状态使用 `texts/<actual-locale>/title`、`texts/<actual-locale>/description`；它们不表示又请求了一种语言。成功部分即使缺失或为空也清空相应商品字段，并解除相应人工覆盖和候选锁；失败或未知部分保留值和保护。来源语言与作品原语言不同的手写译文及其保护、未知用户扩展键、其他作品、目录、播放和收藏保留。明确支持标题、简介、发行日期、语言、署名、标签、封面及来源支持的商品字段；来源未支持或不能确认完整的字段不覆盖。图片引用和图片下载仍为独立阶段。

任务条目 `sections` 按来源和字段报告 `success`、`failed`、`unknown`、`not_requested`、`preserved`，及可选稳定 `reason`；`success` 表示来源成功，`preserved` 表示手写译文有意保留。普通获取和覆盖任务存在所需字段失败/未知时均为 `partial`，可重试；无可确认成功部分时报 `sections_unknown`，作品不变。独立缓存缺少状态时不得推断成功。任务 `partial` 是部分完成条目数，`work_revision` 是其最后确认的作品修订。

## 元数据读取、手工编辑和重置

`GET /api/v1/works/{work}` 返回生效展示语言的作品字段及 `product_info` JSON 对象。管理员显式读取 `GET /works/{work}/metadata` 时可以取得完整多语资料。完整资料中的标题、简介及结构化介绍使用 `texts[].locale`，同一语言的发行日期、署名、标签、封面和其他商品资料使用 `product_info.localized_metadata` 的相同键。DLsite 以当前商品编号对应的官方版本声明选择请求页面语言和整份资料的归属。没有明确声明的原版资料统一归为 `und`，包括缺少简繁区分的中文声明。ASMR.one 未声明资料文本语言时也归为 `und`。请求语言继续用于任务及缓存站观察记录，商品编号和音视频正文语言各自独立。

除现有标题、简介、发售日期、正文语言、编号、社团/声优/脚本/插画/音乐/其他明确署名、题材、封面 URL，`product_info` 尽力保留系列、作品类型、年龄分类、文件格式/大小、价格/货币、折扣、评分/分布、下载/评论数、样图/缩略图、平台、语言版本与原作关联、排行、时长和字幕提示等实际提供的字段。系列名和别名参与作品搜索。扩展字段保留来源单位：DLsite `price` 为日元，asmr.one `duration` 为秒；`file_size` 可能是展示字符串或数值，不自行猜测单位。描述字段保留原始封面/样图 URL；图片任务会另外建立本地缓存，不下载作品音频。

支持 DLsite 作品介绍中的有序章节、段落换行、介绍插图、商品曲目名与标示时长，返回在 `texts[].description_parts`，跟随该份文本的语言。HTML 页面优先，JSON `intro` / `intro_s` 和 asmr.one 简介作为回退；不提供可执行 HTML。HTML 图片章节不会被简短 JSON 摘要替换。JSON 标题同时识别 `work_name` / `product_name`；署名采用稳定角色顺序。asmr.one 另外接收实际提供的插画、脚本与作者字段。

以下规范字段可以直接用于详情页，不必由客户端判断不同上游的原始形状。原始扩展字段仍保留，不把商品关联变成本地作品身份：

| 字段 | 形状和含义 |
| --- | --- |
| `product_info.sample_images` | 有序图片数组，元素为 `{url,thumbnail_url?,width?,height?,caption?}`；URL 为 HTTP(S)，尺寸省略/0 表示未知。HTML 轮播优先，JSON 样图回退，去重并排除轮播封面 |
| `product_info.age_rating` | `all_ages` / `r15` / `r18`；没有明确证据则缺失。`nsfw=false` 不足以推断全年龄 |
| `product_info.series_name` / `series_id` | 兼容 DLsite 与 asmr.one 系列对象；系列编号只作商品事实，不据此合并作品 |
| `product_info.language_versions` | `{code,language?,title?}` 数组；code 为规范 RJ，language 为已知 BCP 47；同号去重。其它版本不写进本作品正文语言，也不创建本地作品 |
| `product_info.thumbnail_cover_url` | asmr.one 缩略图 URL；使用规范小写键，原始字段仍能在 raw 中查看 |

`sample_images`、`language_versions` 各最多 256 项，有固定形状校验；`age_rating` 校验枚举。三者仍可分别手工编辑、保护、清空和重置。未知普通扩展键继续按下述 JSON 契约接受。图片字段保存来源引用，本地下载与读取见下节；商品曲目表不绑定本地文件、字幕或播放组。

上游缺少介绍区域/字段时保留旧的成功结构化介绍；明确存在但为空的介绍区域清空该语言的自动结构化介绍及纯文本简介。人工保护不受影响。结构化介绍超限时保留可容纳的段落，并在 `scrape_warnings` 中报告 `description_parts_truncated`，不会增加网络重试。已经存在的成功缓存不会因软件升级自动重新抓取；需要补全这些内容时，显式提交 `force=true`。

`PUT /works/{work}` 原有完整替换契约继续生效，`product_info` 可手工编辑。每个键分别保护，文本标题/简介/结构化介绍按语言分别保护；通过 `manual:` 前缀字段路径标记人工值，例如 `manual:texts/ja/title`、`manual:product_info/series_name`，前缀不加入实际标题或 JSON 值。默认刮削和归纳不覆盖；人工建立时的非空字段和首次刮削前编辑立即保存为人工标记。显式空值或删除文本也是保护，不能等同于解除标记。未知商品键允许保存，键须满足 `^[a-z][a-z0-9_]{0,63}$`，最多 100 个，每个 JSON 值最多 256 KiB（`localized_metadata` 合计最多 4 MiB，内层仍受原字段限制），仍受请求整体 1 MiB 限制。现有锁定候选仅保护它对应字段/语言，不冻结其它元数据。

`GET /works/{work}/metadata-overrides` 查看人工保护。`POST /works/{work}/metadata-overrides/reset` 带作品 ETag，使用 `{"fields":["manual:texts/ja/title","manual:product_info/series_name"]}` 取消指定保护；空列表取消全部。所有手工或自动作品都支持；字段名必须使用 GET 返回的完整 `manual:` 前缀键，未带前缀返回 400。重置在本地自动/初始人工基线之上应用当前有效 RJ 的共享成功缓存，再叠加剩余人工保护。未刮削的手工作品使用创建时本地基线；有同号缓存时直接应用，不发请求。人工改 RJ 同样立即重新计算，未保护的旧编号商品字段不会残留；显式字段锁定仍保留。

`GET /api/v1/works/{work}/scraped-metadata` 按作品当前 RJ 返回共享事实的 `work_id,provider,code,source_url,raw,fetched_at,cache_revision`，不存在时 404。`cache_revision` 是共享事实修订，不是作品编辑修订；缓存可能比最终作品数据更新，或该作品此前因并发编辑未能投影。`raw` 是供检查的上游证据，不含本作品人工值，可能与最终数据不同，也可能含不可信 HTML/URL，应作为纯文本/数据展示，不能直接执行或注入页面。同 RJ 的部分刷新保留前次未提供的规范字段；署名按角色替换本次提供的完整名单，未返回的角色保留；不同 RJ 不继承旧商品字段。原始证据只代表本次成功响应，不声称所有保留字段均来自本次请求。


## 图片下载与本地读取

图片阶段按作品**当前最终元数据**选择 URL，包括人工修改后的封面/样图/介绍图；不会把原始上游封面覆盖到人工选择之上。普通任务先提交商品元数据，再下载封面与封面缩略图；`download_extras=false` 默认不下载样图和介绍插图。图片失败、取消不会撤回已经提交的作品描述，也不会改变成员、播放组或扫描归纳代次。

原 `POST /metadata-scrape-jobs` 增加：

| 参数 | 默认和作用 |
| --- | --- |
| `images_only` | false；true 时只按当前作品 URL 补图，不抓取/应用商品资料，不要求 RJ。不能同时传 `force=true` |
| `force_images` | false；true 强制重下载选中的图片，与商品资料的 force 独立 |
| `include_extras` | 省略/null 采用 settings.download_extras；true/false 仅覆盖本次任务是否包含样图和介绍插图 |

例如已有作品只补齐全部图片：

```json
{"work_ids":["作品ID"],"images_only":true,"include_extras":true}
```

如果 URL 不变但上游图片内容变了，增加 `"force_images":true`。图片缓存不绑定任务；相同完整 URL 跨作品/任务/重启复用，文件丢失时重下载；同批次或重叠强制任务可复用启动后成功更新的内容。同一 URL 只下载一次可并发复用的成功结果，失败不作为成功缓存。未变化的已有图片不做 HEAD/条件网络复查，没有自动 TTL。

任务和条目增加 `images_total,images_downloaded,images_cached,images_failed`。计数以“作品所引用的图片目标”为单位，同一 URL 在另一个作品中可以计为缓存命中。条目 `phase` 为 metadata/images/done，另有独立 `image_error`（`unsupported_image_url,image_unavailable,image_limit`）。商品成功但图片失败时条目可为 succeeded，任务为 partial，工作项 failed 仍为 0；客户端应分别展示作品和图片计数。图片执行期间每个结果都会保存进度并记录关联 job_id/work_id 的日志。取消和重启 interrupted 沿用原任务接口，已成功图片仍保留。

- `GET /works/{work}/images`：返回 `{work_id,work_revision,items}`。每项有 `id,url,kinds,cached,mime,size,fetched_at,error`。kinds 为 `cover,cover_thumbnail,sample,sample_thumbnail,description,description_thumbnail` 中的一个或多个；同 URL 去重，保留第一次出现的顺序。`cached=true` 才能读取本地图片；失败刷新可能同时有 cached=true 和 error，表示继续提供旧成功内容。
- `GET /works/{work}/images/{image}`、`HEAD`：读取本地字节/响应头。只接受该作品当前仍引用的图片 ID；来源 URL 被移除或替换后，旧 ID 经该作品读取为 404。支持 Range、ETag/304 和 Last-Modified；需要管理认证，缓存为 private 且须重新验证。GET/HEAD 永不隐式下载，未缓存/文件缺失返回 404；关闭刮削设置仍可读取已缓存且仍被引用的图片。

客户端通过上述图片接口展示本地缓存，作品详情的来源 URL 不会被改写为本地路径。最多处理每作品 1024 个去重图片地址；超出时报告 image_limit，作品元数据仍保留。单图最多 16 MiB，支持 JPEG/PNG/GIF/WebP，保留原始字节、透明度和动画，不做缩放/转码；HTML/SVG 等不缓存。下载共用作品任务并发，每作品串行逐图，使用当前代理/超时设置。

自动下载仅接受 HTTPS（默认端口/443）且无 URL 用户凭据的 DLsite、asmr.one、asmr-200.com、asmr-300.com 及其子域，重定向也受同一限制。其它 HTTP(S) 引用可以作为元数据保存，但图片任务报告 unsupported_image_url；不会访问任意私人地址。源媒体目录不写入图片，缓存由服务管理。删除任务/作品不自动删除跨作品共享缓存；成功替换会清理旧字节版本，重启清理未被缓存记录引用的本服务临时/旧版本文件。可通过 image-cache/prune 调整已下载图片占用；不提供按作品清空接口。


## 多来源与失败恢复

当前启用 `dlsite` 编号 RJ/BJ/VJ（6 或 8 位数字）；缓存 code 规范化为大写。BJ 使用 DLsite books，VJ 使用 pro，RJ 使用 maniax；缓存站可在内部使用 ASMR.one 回退，Core 不直接调用该提供者。FANZA 暂停：不创建 FANZA 商品刮削条目、不请求其上游；内置 fanza-code 规则停用，既有编号、资料和缓存保留可读。

商品刮削在持久化任务前过滤无有效编号、格式无效或只有暂停的 FANZA 编号的作品，不为它们创建任务条目，也不计入总数或失败数。混合选择仅处理其余作品；全部被过滤时返回 HTTP 400 `no_scrapable_works`，不创建空任务。自动扫描归档流程遇到该结果正常继续，没有商品刮削子任务。一个作品存在多个不同商品编号时仍记录 `unsupported_identifier` 冲突项，不存在作品记录 `not_found`，不会拒绝整批；空/重复选择等请求格式错误仍拒绝。显式 `images_only=true` 仅补齐现有图片引用，继续不要求商品编号。不同作品即使编号相同也保持独立，只共享上游事实缓存。翻译商品无封面且提供 original_workno/translation_info 原作编号时，最多多取一次原作资料，仅补封面并记录 cover_source_code，不改标题或作品编号。

设置新增可选字段：`retry_attempts`（0..3，默认 2 次重试，250/500/1000ms 退避）、`cache_max_age_hours`（0..87600，默认 0（永久复用））、`failure_cache_seconds`（0..3600，默认 300；0 关闭）、`image_cache_max_bytes`（0..1TiB，默认 1GiB；0 不自动限制）。PUT 省略这些新增字段保留原值。缓存过期或 force 才联网；近期失败不重复访问上游，项错误为 provider_failure_cached。取消不写失败缓存，失败刷新不删除最后成功资料。force 绕过失败缓存；HTTP/超时重试有界，字段格式错误不重试。

`POST /metadata-scrape-jobs/{job}/retry-failed` 为终止任务中失败或图片失败的作品创建新任务（202），成功的其他作品不重复。无失败项返回 400；原任务未终止返回 409。

## 图片容量与清理

`GET /metadata-scraper/image-cache` 返回 files、bytes、max_bytes。缓存容量不足时按 fetched_at 从旧到新淘汰；单张图片大于容量返回 image_cache_capacity。读取图片不会自动联网，淘汰后可用仅补图任务重新获取。

`POST /metadata-scraper/image-cache/prune` 正文 `{"max_bytes":0}` 清空已下载图片；非零保留至指定容量。只删除 Core 自己管理的缓存文件，不删除源媒体、封面引用、商品事实或人工元数据。文件删除在数据库事务之后进行；失败/重启可能暂留无引用缓存文件，由启动清理回收。已有打开的文件流按操作系统语义继续，尚未打开的图片可能返回未缓存。

## 当前商品资料与语言版本

默认任务逐一尝试 `metadata_locales` 中开启的资料语言；作品任务的 `locale` 仅重新获取指定的一种语言。translation_info.lang 和同编号 language_editions 只作为版本语言事实，不选择或推断资料页面语言。初始 JSON 请求有独立的较短预算，避免其超时耗尽整个阶段而无法尝试 HTML。

显式请求非日文资料时读取所选 locale 的 HTML 和 JSON；日文发现响应可以补充商品事实，但不能作为中文/英文简介或结构段落。DLsite 按实际文本证据分别确定标题和简介语言，不把请求 locale、音声版本语言或标题与默认响应相同当作日语证明；缺乏足够证据时保存为 und。日语请求返回中文原文时，中文原文保留其实际语言；目标语言缺失时不复制同一内容到多种语言。asmr.one 提供的一份文本按 `zh-Hans` 保存，不根据商品正文/字幕语言或标题字形猜测文本语言。

普通 product_info 字符串事实包括 metadata_request_locale（DLsite 请求 locale，如 zh_TW）、metadata_text_locale（本次主文本实际语言）、metadata_original_locale（当前编号有可靠原文文本证据时记录的语言）、edition_language（当前商品版本语言）。标题/简介实际语言以 texts 中各自的 locale 为准。上述事实与 languages、音轨和字幕语言独立；不表示本地拥有相关媒体。原文合并采用当前编号的可靠原文证据，不赋予日语特殊优先级，关联商品的语言不能改写当前编号的原文语言。language_editions 接受上游当前存在的数组或数字键对象形态，多值 lang 分别解析；关联商品的语言不赋给当前作品正文。

language_versions 和 original_workno/translation_info 的原作编号是关联商品信息；不同编号不合并成当前作品的文本语言或媒体成员。客户端可显示关联商品，按编号搜索本地作品后精确匹配 identifiers；同号的多部本地作品都应保留选择。查询和打开关联商品不修改当前作品。人工/AI 本地译文继续独立保存，未采用候选不算已有译文。既有成功缓存不会因此自动重抓，显式 force=true 才刷新。

## 资料来源与连接测试

GET／PUT `/metadata-scraper/settings` 使用 providers，固定包含 scrape（缓存站）和 dlsite 各一次，数组顺序为优先级，enabled 可分别开关。默认缓存站关闭、DLsite开启；允许全部关闭，已有本地成功资料仍可复用。开启缓存站需有效格式地址与非空凭据。

scrape_url 为根地址，scrape_token 写入专用，省略保留、空串清除；GET/active 只返回 scrape_token_configured。缓存站凭据应为具有查询权限的密钥，不是初始登录密码。来源失败或未取得请求内容时，尝试下一个已启用来源；不混用两来源的正文。

POST `/metadata-scraper/test-connection` 接受 source=dlsite/scrape 及可选草稿 scrape_url/scrape_token/proxy_url/timeout_seconds，省略值取已保存设置。仅管理员可用，不保存配置、不读取作品或写入抓取失败缓存；独立测试某来源，即使该来源目前关闭也可测试。最多30秒，返回 source/ok/error_code/http_status/duration_ms。缓存站验证查询/刷新权限；DLsite验证访问及明显访问验证页面。错误不返回上游正文、代理凭据或令牌。

Core 缓存站客户端使用统一 POST `/api/v1/lookup`，按需请求 metadata 或 image_references 字段组，传入语言优先级；图片字段仅为地址，图片字节仍由 Core 图片任务下载。缓存站无可用图片地址时继续尝试已开启的 DLsite，关闭的来源不请求。商品资料与补图分别使用各自范围，不因取得文字就停止寻找图片地址。

Core 本地成功缓存仍优先，force 刷新按当前来源顺序执行。缓存站404可回退下一来源，最终结果决定任务状态。失败缓存绑定配置修订；强制刷新绕过。缓存站保留统一作品接口，不提供按上游区分的公开路径。

GET `/works/{work}/scraped-metadata` 的 via/cache_url 记录当时实际获取途径，不从当前设置推断。DLsite、缓存站和导入分别标注，地址不含密钥。

## 统一元数据编辑

`GET /works/{work}/metadata-editor` 返回作品 ETag 和 `{revision,values,automatic_values,scraped_values,locked_fields,can_assign_unknown_language}`。三个值映射都以无 `manual:` 前缀的字段路径为键，例如 `texts/ja/title`、`localized/zh-Hans/credits`、`localized/zh-Hans/product_info/series_name`；商品编号和原始资料语言仍使用共用路径 `identifiers`、`original_locale`。values 是当前最终资料；automatic_values 是当前本地基线叠加有效商品缓存、不叠加人工或候选锁定的预览资料；scraped_values 只包含有效商品号已有成功缓存中的标准化资料，无缓存为空对象；文本中未取得的空标题、空简介或空详细介绍不提供恢复选项。仅返回保存内容，不联网，不按显示语言回退。locked_fields 包含人工覆盖与候选锁定对应字段；署名作为一组，全部语言资料字段按语言分别保护；`localized/<locale>/` 后可使用 `release_date`、`release_date_precision`、`languages`、`credits`、`tags`、`cover_ref` 或 `product_info/<key>`。source/sections/via/cache_url 是只读获取记录，不作为人工字段返回。

`PATCH /works/{work}/metadata-editor` 使用作品 If-Match，正文 `{"values":{"texts/ja/title":"手动标题"},"unlock":["product_info/series_name"]}`。总计 1..512 个字段操作；同字段不能同时写入和解除保护，unlock 不得重复。values 明确保存并保护每个指定值，即使与当前值相同。unlock 清除对应人工覆盖及匹配候选锁定，然后使用现有基线/商品缓存重新计算资料；空 unlock 不清除全部。简介候选锁同时保护详细介绍，解除二者之一会解除这份候选锁；发行日期与精度同理，其余人工字段保护保留。失败整批回滚，成功返回更新的 Work 和 ETag。过期 412、缺条件 428、非法字段/值/重复 400；不修改目录边界、文件归属或默认播放组。

界面在当前语言有已下载资料时提供“恢复为刮削版本”，明确选择字段后使用 unlock；没有对应语言缓存时不伪造刮削值。确认翻译使用 values 写入所选目标语言字段（以及存在标记），结果立即成为人工保护。纯文本简介替换目标语言的详细介绍时同时明确清空并保护 description_parts，其他语言不受影响。

`POST /works/{work}/metadata-language` 携带该作品的 `If-Match`，请求例如 `{"locale":"zh-Hant","mode":"merge"}`。目标允许 `zh-Hans`、`zh-Hant`、`ja`、`en`。仅适用于可归类的未知来源资料，`can_assign_unknown_language` 用于显示操作。标题、整份介绍及商品资料采用同一归属。 `merge` 只填目标缺少的字段，已有列表也视为一个字段。 `overwrite` 以未知资料替换目标语言的整份资料。操作对这部作品及其当前商品编号持续生效，采用刮削资料的保存与恢复方式。来源有保护字段、或覆盖会替换受保护的目标字段时返回 `409 locked`。成功返回作品与新 ETag，过期返回 `412`。

## 提供者字段与补全规则

product.json、编号 AJAX 和精确身份 HTML 分别验证，不因一个端点失败而整体丢弃其它端点确认的署名、标签、日期或系列。字段缺失/失败保留已有资料；署名按实际观察的角色保留未确认的其它角色。HTML 标题允许 OG/title 回退，但必须具有匹配当前编号的 DLsite canonical；错号、推荐卡片和伪造域名拒绝。

新增扩展事实：summaries 为按 BCP 47 locale 保存的短摘要，与完整 description/description_parts 独立；credit_references 保存提供者、角色、署名 ID、名称和链接；original_product 保存关联原作的编号、标题、社团与系列事实，不替换当前版文本；advertised_size/advertised_duration 保留卖方容量/时长，与本地媒体探测分开。field_sources 记录由次来源补齐的字段路径。原作可在当前版无封面时提供封面回退。不同语言摘要和正文均保留，抓取不自动翻译。

## 评分与价格的共同展示字段

DLsite RJ 的动态商品请求优先使用 maniax-touch 的编号 AJAX，失败、空对象或错号时尝试 maniax 桌面 AJAX；BJ/VJ 使用各自 books/pro 分区，不枚举不确定的站点。product.json 提供基准资料，当前动态值逐字段覆盖。不会因统计字段缺失额外调用第三方统计接口。

DLsite 与 ASMR.one 均通过 product_info 提供：rate_average_2dp（0–5）、rate_count（评分人数）、rate_count_detail（分布）、dl_count（销量）、review_count（评论数）、wishlist_count（上游明确提供时的收藏数）、price（现价）、official_price（原价）、price_without_tax（税前价）、currency（JPY）、discount_rate（0–100 百分比）和 is_discount/is_sale/is_free（明确布尔标志）。只保存上游实际提供且类型、范围有效的字段，不推测缺失的评分人数、折扣或原价。评分 rate_average 可作为上游另一种评分表示，统一写入 rate_average_2dp。

数值字符串规范化为 JSON 数字；零价格、零人数及 false 保留。null、缺失及非法动态字段不会覆盖此前有效事实或人工字段。价格为当前观察，不承诺实时；本地优先 GET 不因访问评分/价格而刷新，仍需显式刷新。现价零仅表示当前免费，不认定永久免费。Flutter 详情页统一显示两来源的评分、人数、价格、原价、优惠与销量，并可展开评分分布。

## 分项刷新

商品资料任务可传 skip_images=true，只更新资料，不下载封面或样图；与 images_only/force_images 不可同时启用。默认 false 保留显式联合下载能力。任务响应包含 skip_images，重试保留这一选择。

Flutter 作品详情的“分项刷新”分别提交：缓存优先商品资料、强制商品资料、补齐图片、重新下载图片、补齐媒体信息、强制媒体探测、作品根扫描、基于已有索引重新归档。前两者均 skip_images=true；补图 images_only=true/force_images=false，不重新抓取资料；媒体探测分别 force=false/true。扫描和归档仅提交原有对应接口，缺少本地作品根时禁用。每次只创建所选任务，来源自动化策略仍可独立运行。强制商品刷新保留既有人工保护/全面覆盖选择，全面覆盖仍绑定作品修订。

## 自动获取关联商品资料

普通作品刮削成功保存当前商品后，复用同一来源配置、代理、缓存、失败缓存和取消机制，附带获取上游明确声明的原作／语言版本资料。每个作品任务最多新获取／刷新 8 个额外商品、展开两层；已有有效缓存不占该次抓取额度；不按标题猜测关系，不创建远端版本对应的本地作品，不把关联商品字段应用到当前作品，也不修改文件归属。仅补图和仅应用缓存操作不额外联网抓取关联商品。

关联结果在任务 item.sections 的 `related/<商品编号>` 中返回，state 为 success/failed/unknown/not_requested；原因 related_limit 表示达到本次关联范围上限，related_partial 表示资料未完整取得。关联失败使当前任务项为 partial，但当前商品已成功取得的资料保留。失败项重试可重新尝试关联获取；仅达到范围上限标记 not_requested，不计为获取失败，也不自动无限重试；再次获取时可复用已完成缓存，继续补充剩余商品。普通关联获取复用未过期成功缓存，显式失败重试绕过近期失败限制。当前商品的图片处理仍独立，关联商品的图片不自动批量下载。

关联关系随成功缓存事实更新，读取现有资料及家族接口不触发抓取。原作声明冲突、循环或超大关联分量不静默合并，家族查询按独立商品报告冲突。

## 记录保留

已结束任务及其重试输入最多保留 48 小时；过期详情返回不存在。刮削／媒体探测任务结束后，明细仅保留未完成项，成功与复用项计入任务汇总；明细分页 total 为实际保留的明细数。性能指标仅在任务执行期间可用。日志同时受 48 小时与总容量限制。作品资料、人工修改、字幕草稿和收听记录不受影响。
