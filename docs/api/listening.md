# 收听状态与播放协商

这些接口需要当前账户会话；Web 使用同源 Cookie，Android/外部客户端使用 Bearer。身份由认证确定，不接受请求体指定账户。进度、收藏集和播放列表独立。

- `GET /listening/recent?limit=50&offset=0`：当前账户最近进度，按更新时间倒序、节点 ID 排序，返回 `items/total/limit/offset`；条目含原始 name 与当前显示 alias（无别名为空）。
- `GET /listening/progress/{node}`：返回 node_id、name、alias、position_seconds、completed、revision、updated_at；没有记录的已索引普通文件返回位置 0、完成 false、修订从返回值读取，删除后不会重置为 1。单项响应 name/alias 可为空，列表包含索引名称。
- `PUT /listening/progress/{node}`：完整替换 `{ "position_seconds": 123.5, "completed": false }`；必须是当前可用媒体；`If-Match: "progress-<node>:<revision>"`，过期 412。位置是原文件时间，有限数，0..604800 秒。不按 RJ、标题或跨格式推断时间轴。

- `DELETE /listening/progress/{node}`：删除当前账户该资源的续播位置与完成状态，支持普通媒体及本人珍藏片段；要求当前 `If-Match`，修订过期返回 412，成功 204。删除后 GET 返回位置 0、完成 false 和新的修订。
- `DELETE /listening/recent`：原子清空当前账户全部普通媒体与虚拟资源播放记录，无请求体及 If-Match，成功 204；不删除媒体、珍藏片段、收藏集或播放列表，不影响其他账户。

删除使此前获取的进度 ETag 失效，清空也覆盖此前尚未保存进度的资源。客户端必须丢弃删除前排队的写入，并等待已发送写入结束后执行删除。删除正在播放的记录后，本次曲目出现暂停自动同步；重新选择内容播放再建立同步周期。不要自动刷新 ETag 后补写已删除进度。最近收听与首页续听在删除成功后重新读取。

进度写入不修改队列、作品资料和收藏集。删除账户级联清理个人数据；移除来源删除对应节点状态，媒体文件不变。客户端每约 10 秒及播放切换/暂停时尽力同步进度。遇到 412 停止本次曲目出现的自动写入，不自动刷新 ETag 后覆盖；重选曲目建立新同步周期。本机 Android 队列恢复仍独立，进程退出后以服务端最后成功同步的位置为准，未宣称离线或后台持续同步。

`GET /nodes/{node}/playback` 返回具体节点的 profiles：mode、byte_ranges、start_seconds、可选 default_bitrate_kbps；duration_seconds 未知为 null。支持原文件及适用的 mp3、opus、mp4。工具缺失只返回原文件；存在工具不保证该构建包含所有可选编码器。

`GET/HEAD /nodes/{node}/stream` 新增 `mode=opus` 与 `bitrate_kbps=48..320`。原文件不接受码率参数；MP3 默认 192、Opus 默认 96、MP4 音频默认 128 kbps。首次生成仍渐进输出。默认缓存 512 MiB，启动参数 `-transcode-cache-mib` 范围 0..16384，0 关闭。

普通媒体只缓存从 0 开始且完整成功的音频输出；视频、非零 start、超出单项预算的输出继续正常播放但不落盘。每项上限为总容量/处理进程数；正在生成的临时文件预留预算。完整缓存支持 Range、HEAD 和条件请求；其他生成输出用 start 秒偏移。取消、解码失败、写缓存失败不发布，写缓存失败不影响已有流。相同 key 仅一个缓存写者；并发播放仍可能各自转码，受全局进程上限约束。

缓存按节点、当前大小/mtime、写事件代次、实际输出参数、媒体观察和工具文件身份失效，不读取正文指纹。缓存仍经过账户认证和源文件可用性检查；淘汰只删除应用缓存。睡眠定时属于客户端设备控制，Android 定时由原生后台服务执行，不写成服务器任务。

播放条目可以使用虚拟资源 ID。进度属于该资源自己的时间轴，写入时携带解析结果的 resource_revision。资源修订失配返回 412，客户端重新选择资源后再同步。已授权的虚拟资源进度可出现在最近收听中，源文件进度独立保存。
