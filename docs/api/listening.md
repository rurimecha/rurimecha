# 收听状态与播放协商

这些接口需要当前账户会话；Web 使用同源 Cookie，Android/外部客户端使用 Bearer。身份由认证确定，不接受请求体指定账户。进度、收藏集和播放列表独立。

- `GET /listening/recent?limit=50&offset=0`：当前账户最近进度，按更新时间倒序、节点 ID 排序，返回 `items/total/limit/offset`；条目含原始 name 与当前显示 alias（无别名为空）。
- `GET /listening/progress/{node}`：返回 node_id、name、alias、position_seconds、completed、revision、updated_at；没有记录的已索引普通文件返回位置 0、完成 false、修订从返回值读取，删除后不会重置为 1。单项响应 name/alias 可为空，列表包含索引名称。
- `PUT /listening/progress/{node}`：完整替换 `{ "position_seconds": 123.5, "completed": false }`；必须是当前可用媒体；`If-Match: "progress-<node>:<revision>"`，过期 412。位置是原文件时间，有限数，0..604800 秒。不按 RJ、标题或跨格式推断时间轴。

- `DELETE /listening/progress/{node}`：删除当前账户该资源的续播位置与完成状态，支持普通媒体及本人珍藏片段；要求当前 `If-Match`，修订过期返回 412，成功 204。删除后 GET 返回位置 0、完成 false 和新的修订。
- `DELETE /listening/recent`：原子清空当前账户全部普通媒体与虚拟资源播放记录，无请求体及 If-Match，成功 204；不删除媒体、珍藏片段、收藏集或播放列表，不影响其他账户。

删除使此前获取的进度 ETag 失效，清空也覆盖此前尚未保存进度的资源。客户端必须丢弃删除前排队的写入，并等待已发送写入结束后执行删除。删除正在播放的记录后，本次曲目出现暂停自动同步；重新选择内容播放再建立同步周期。不要自动刷新 ETag 后补写已删除进度。最近收听与首页续听在删除成功后重新读取。

进度写入不修改队列、作品资料和收藏集。删除账户级联清理个人数据；移除来源删除对应节点状态，媒体文件不变。客户端每约 10 秒及播放切换/暂停时尽力同步进度。遇到 412 停止本次曲目出现的自动写入，不自动刷新 ETag 后覆盖；重选曲目建立新同步周期。本机 Android 队列恢复仍独立，进程退出后以服务端最后成功同步的位置为准，未宣称离线或后台持续同步。

`GET /nodes/{node}/playback` 返回具体节点的 profiles：mode、byte_ranges、start_seconds、可选 default_bitrate_kbps；duration_seconds 未知为 null。支持原文件及适用的 mp3、opus、mp4；视频额外返回按源信息和管理员限制生成的 video_qualities，详见 playback.md。视频无法探测或缺少所需编解码器时只提供原文件。

`GET/HEAD /nodes/{node}/stream` 新增 `mode=opus` 与 `bitrate_kbps=48..320`。原文件不接受码率参数；MP3 默认 192、Opus 默认 96、MP4 音频默认 128 kbps。首次生成仍渐进输出。默认缓存 512 MiB，启动参数 `-transcode-cache-mib` 范围 0..16384，0 关闭。

普通媒体只缓存从 0 开始且完整成功的音频输出；视频、非零 start、超出单项预算的输出继续正常播放但不落盘。每项上限为总容量/处理进程数；正在生成的临时文件预留预算。完整缓存支持 Range、HEAD 和条件请求；其他生成输出用 start 秒偏移。取消、解码失败、写缓存失败不发布，写缓存失败不影响已有流。相同 key 仅一个缓存写者；并发播放仍可能各自转码，受全局进程上限约束。

缓存按节点、当前大小/mtime、写事件代次、实际输出参数、媒体观察和工具文件身份失效，不读取正文指纹。缓存仍经过账户认证和源文件可用性检查；淘汰只删除应用缓存。睡眠定时属于客户端设备控制，Android 定时由原生后台服务执行，不写成服务器任务。

播放条目可以使用虚拟资源 ID。进度属于该资源自己的时间轴，写入时携带解析结果的 resource_revision。资源修订失配返回 412，客户端重新选择资源后再同步。已授权的虚拟资源进度可出现在最近收听中，源文件进度独立保存。

## 在线收听统计、个人评分与推荐

所有接口属于当前账号，普通用户可用，游客不可用。统计和评分独立于续播位置；删除续播记录不删除统计或评分。没有统计清除接口。客户端只报告服务器媒体库的播放，本机媒体库不报告或补报。

`POST /listening/report` 接收 `{node_id,seconds,utc_offset_minutes}`，seconds 为 1..120 的整数，时区偏移为 -840..840 分钟。节点须是当前可用音视频，珍藏片段按本人原文件解析；作品归属由服务器读取，未归档媒体只增加日总量。首次报告固定该账号的统计时区偏移。客户端约每分钟及暂停、换曲时上报实际播放时间，暂停和缓冲不计入，倍速和跳转不改变累计时间。报告采用累加语义；不自动重试结果不明确的请求，不持久补报。成功 204；输入错误 400、不可用资源 404。统计错误不影响媒体播放。

`GET /listening/statistics?range=30d` 支持 `30d`（默认）、`12m`、`year`、`all`。year 模式可传 `year=2026`，范围 1970..当前统计年份，省略取当前年。响应含 from/to、seconds、active_days、work_count、days，以及 works/voices/circles/tags 排行（各最多十项）。days 项为 `{day,seconds}`，排行项为 `{name,seconds,work_id?}`。work_count 统计范围内累计至少 60 秒的作品。声优、社团、标签来自当前有效作品资料，一次收听可计入多个分类。移除作品清理对应明细，已累计日总量保留；没有逐次播放事件。

`GET /account/works/{work}/rating` 返回 `{work_id,stars,revision}` 和独立 ETag `"rating-<work>:<revision>"`。未评分 stars=0、revision=1。`PUT` 用当前 If-Match 完整提交 `{stars}`；1..5 为整数星级，0 取消评分，保留递增修订。不存在或非有效作品 404；过期修订 412；缺少 If-Match 428。评分绑定整个真实 work ID，不绑定文件、片段、播放组或收藏集，不按同号或商品家族同步。作品列表和详情的 `user_rating` 为当前账号星级，与 product_info 中的公开评分独立。

`GET /listening/recommendations?seed=choice` 返回 `{shelves:[{kind,items}]}`，kind 为 like/explore/again，每组最多八项。每项包含 work（同 WorkDisplay）、reason、feature。reason 为 voice_actor/circle/tag/again/explore/random，feature 为对应显示名称或空字符串；客户端本地化说明。seed 最长 80 字节，相同输入和 seed 保持排序，换批更换 seed。推荐只读取已收录、有效且有可用音频的作品，不联网查询目录或下载文件。同一当前商品家族跨各组只出现一次，采用账号资料语言及优先播放版本。

4/5 星提供正向偏好，3 星中性，1/2 星负向；已评分作品不再叠加收听权重。未评分作品累计至少 120 秒提供有上限的弱信号，跨日重听少量加权。仅有效标签、声优和社团形成特征，空值及占位名称不参与；无特征评分不影响其他作品。推荐计算移除候选自己的证据，不将跳过、收藏或未听完视为负向。低评分作品不进入任何推荐组；换口味必须有有效特征；久未重听可依据该作品本身的评分和收听记录，不要求特征。同一组中同一声优或社团最多出现两次；排序为只读结果，不提供权重配置接口。
