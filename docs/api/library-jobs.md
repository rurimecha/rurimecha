# 扫描归纳刮削串联任务

`POST /api/v1/library-jobs`（202）创建任务，`GET /library-jobs` 返回最近 100 项，`GET /library-jobs/{job}` 查看，`POST /library-jobs/{job}/cancel` 取消。均要求管理员会话；查询不接受参数。任务独立于创建请求，最多 8 项活动任务，不同来源可并行处理，各阶段复用既有用例。

```json
{"source_ids":["source-id"],"scope_node_id":"","mark_missing":false,"scrape":true,"probe":false,"force_metadata":false,"skip_scan":false,"metadata_only":false}
```

source_ids 为 1..64 个不重复 ID。子树范围必须属于唯一来源且为存在的目录；首次没有完整索引时应全量扫描。其余字段可省略，布尔默认 false。skip_scan 只跳过枚举，继续归纳；metadata_only 跳过扫描和归纳，只处理元数据。force_metadata 传递给刮削和媒体探测，不绕过修订、范围及人工保护。刮削须先启用 scraper；探测依赖外部 ffprobe。

kikoeru 兼容来源已在入库时取得目录和媒体信息。对此类来源运行归纳时必须设置 `skip_scan=true` 且 `probe=false`；可按需设置 `scrape=true`。请求扫描或探测兼容来源返回 400，不创建任务；混合来源请求包含此类无效组合时也整体拒绝。

返回 id、state、input、created_at、finished_at、items；每个来源项包含 source_id、state、observed_nodes、skipped_works 和 stages。阶段含 kind、state、error（稳定代码）、job_ids 和 duration_ms。child job_ids 可在扫描/刮削/探测原接口查询逐项错误。归纳直接在流程阶段执行，不产生单独归纳任务 ID。

状态为 queued/running/canceling/succeeded/partial/failed/canceled/interrupted。不存在或停用的来源只影响自身；扫描失败停止该来源后续步骤，部分扫描可继续；归纳失败停止依赖其结果的刮削阶段，独立媒体观察继续。刮削逐作品隔离，超过 1000 作品分批；取消会通知正在运行的子任务。重启先将未结束历史标记 interrupted。未被用户取消的手动流程在应用完成初始化后以原任务 ID 恢复：保留已完成来源，重算中断来源，复用已成功保存的观察；正常服务关闭也保存 resume_pending=true；显式取消不恢复。自动流程由来源持久策略与修订重新协调。

扫描不完整时保护不确定区域：保留涉及未观察节点、读取失败目录及其所属作品的原归属，阻止跨作品转移。健康独立分支仍可发布。skipped_works 是此次计划中被跳过的提案数，不能当作失败文件总数。不完整结果不推进来源 applied_generation，也不推断删除；后续完整扫描收敛。原有严格归纳接口仍拒绝不完整索引。

# 自动策略

来源 automation policy 包含独立的 auto_scrape、auto_probe 开关（完整替换时省略为 false；创建来源时省略整个 automation 对象则默认开启四项自动操作），与 auto_scan、auto_organize 分别独立控制。已有可信完整索引时，即使关闭自动扫描或归档，也可单独处理已有作品的刮削和已索引文件的媒体信息，状态增加 processing 和 library_job_id。元数据失败可由任务接口重试，正常完成的归纳不会因单纯刮削失败反复触发。监听维持至多 64 个互不覆盖的待扫目录，超限、丢事件或不可信基线回退全量。若多个独立扫描范围在归纳前合并，元数据阶段保守处理整个来源以避免漏掉较早范围；仅单目录完成时保留其元数据范围。


## 独立队列和并发设置

扫描完成后，归档与媒体提取可并行；归档成功后进入刮削。刮削或提取等待不会占用全流程串行槽，也不阻塞其他来源。阶段顺序固定展示 scan、organize、scrape、probe，但不表示执行串行；queued 阶段尚在等待依赖或执行资源。子任务 ID 在创建后立即可见。自动策略关闭某项后，仅停止对应自动阶段；不取消用户单独创建的任务。自动扫描可在同来源的元数据流程尚未结束时继续，发布仍校验当前索引及修订。

`GET /api/v1/settings/queues/{kind}`、`PUT /api/v1/settings/queues/{kind}`，kind 仅支持 scan、organize。要求管理员认证；PUT 必须提供 GET 的 ETag 作为 If-Match，无条件写为 428，过期为 412。返回 `{ "workers": 2, "revision": "1" }`，PUT 请求为 `{ "workers": 2 }`，范围 1–32，持久保存并在重启加载。未知 kind 或非法并发为 400；服务未组装设置模块时为 503。

scan 的默认值来自服务启动扫描并发设置（通常 2）；上限覆盖同时执行的完整扫描任务，单任务多个来源顺序处理，同来源不重复接纳。organize 默认 2，上限覆盖所有入口的来源分析；发布继续使用完整事务以保护唯一归属和人工资料。调低并发不强行终止在途工作，等待其排空；调高可释放更多待办。刮削并发通过 metadata-scraper/settings 的 workers 设置，按作品全局共享；提取并发通过 settings/processing 的 metadata_workers 设置，按文件执行，现阶段探测任务本身仍串行接纳，工作流遇忙会等待。

重启恢复可能重新扫描中断来源，不保证从中断节点继续。成功保存的媒体观察和商品资料可复用；归档完成后才提供更新的作品归属。

## 持续上传与自动处理

扫描去抖保留 30 秒进度上限，不等待整个来源静默。自动归档按来源合并，连续变更时至多每 30 秒启动一批；自动处理批次至少间隔 10 秒，事件不会重置这一进度窗口。首次发现文件直接尝试媒体提取；大小/mtime 或监听写事件已发生变化的文件才等待 10 秒。可处理文件先完成，成功观察的同版本文件不进入自动提取目标。未稳定项计入 deferred / deferred_probes，不计为提取失败。

提取阶段结束后即记录等待项，不等待同一流程的刮削结束；后续使用独立的仅提取任务重试，不重复刮削或归档。每个来源最多一个在途稳定重试，批次至少间隔 10 秒。到期先核对剩余目标：持续写入只推迟期限，不为全部仍在等待的文件创建新任务；已成功、不可用、正在失败退避或同一观察版本已重试失败的文件不触发稳定重试。后续其他目录变更不能丢掉较早等待项。deferred / deferred_probes 保留各次任务的历史结果，不随后续重试完成而回写，也不表示查询时仍在上传。

显式用户任务不使用自动稳定门，force 可绕过缓存/退避。phase=settling 表示有文件等待稳定，扫描/归档仍可继续。静默不能证明上传完成；建议使用临时扩展名或在来源外上传，完成后原子改名到媒体文件名。

来源项增加 organized_generation（字符串），仅记录该归档阶段实际发布的 applied_generation，未归档为 0。自动元数据检查点使用该代次，防止自身归档推进代次后再重复调度同一输入；新扫描或写入仍会重新调度，不消费并发新输入。
