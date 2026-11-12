# 目录边界与作品归纳

当前采用单作品归属：一个目录或文件最多属于一部作品，也可未归纳。作品根目录及普通后代属于该作品；遇到子作品根，子树转归子作品。新增文件继承当前边界。相同编号在同一作品内重复出现时默认是包装层，不改变外层根；不同位置的同号副本仍是不同作品，人工可以强制拆分同号子目录。

目录扫描和归纳是两步。扫描更新观察索引与自动语义属性；保存人工决定只更新归纳输入；预览/应用或显式归纳任务才原子发布新归属。接口均相对 /api/v1，需有效会话，网页使用同源 Cookie，原生和外部客户端使用 Bearer。

## 人工目录决定与文件覆盖

| 方法 | 路径 | 行为 |
| --- | --- | --- |
| GET | /sources/{source}/decisions | 分页读取人工/AI 决定，返回集合 ETag |
| PUT | /sources/{source}/decisions/{node} | 完整替换一个决定 |
| DELETE | /sources/{source}/decisions/{node} | 删除决定，下次归纳恢复规则和继承 |

GET 支持 limit=1..500（默认 50）、offset（默认 0），响应 items、total、revision。每项为 node_id、decision、work_id、origin。使用集合 ETag（decisions-<source_id> 标签，绑定来源身份及输入代次）进行 PUT/DELETE，不能使用作品 ETag，跨来源标签返回 400；该来源的观察、规则或节点属性变化可能使它过期；普通作品描述编辑和关系发布不会使它过期，412 时刷新集合。不存在的决定 DELETE 返回 404。

PUT 正文示例：

```json
{"decision":"root","work_id":"target-work-id","origin":"manual"}
```

| decision | 节点 | work_id | 作用 |
| --- | --- | --- | --- |
| root | 已登记目录 | 必填 | 固定目录为作品根，后代/新增文件继承，仍可出现子作品 |
| not_work | 已登记目录 | 空/省略 | 当前目录不独立成作品，继承父作品；更深目录仍正常判断 |
| exclude | 已登记目录或文件 | 空/省略 | 目录整个子树或单文件未归纳；未来新增后代同样被排除 |
| assign | 已登记文件 | 必填 | 指定该文件唯一归属，可将其从其他作品转移 |

origin 默认 manual，可 ai。未知节点或目标作品为 404；种类/字段组合错误为 400；祖先已被排除、目标作品属于其他来源、同作品第二个目录根等冲突为 409。目录排除不能覆盖已有后代决定，须先明确删除它们；任何后代决定也不能穿透祖先排除。允许为已知 missing 节点保存决定，不要求实时打开文件。

目标作品先通过 POST /works 创建，也可指定已有作品；已有其他根时，须同批次明确取消旧根边界，或使用专用 root-moves；不能单独隐式移动。没有目录根的草稿可接收显式文件，收到文件后仍无目录继承。

PUT/DELETE 成功返回 node_id、revision、pending=true 和新集合 ETag。pending 表示这次输入变化尚需归纳应用，不是后台任务状态。当前成员查询在应用前仍显示上一套已发布归属；可直接执行归纳，或启用 auto_organize 交由后台收敛。决定保存和归纳应用分别提交；应用失败会保留待处理决定及旧归属，用户可调整决定或处理锁定后重试。

目录操作不展开为只包含当前文件的有限集合；播放组、字幕、视角或描述编辑不会冻结整部作品。

## 规则配置

规则按节点字段分组：boundary、identifier、format、media_kind、category、effect、transparent、collection、ignore。GET/PUT /organize/rules 的 profile 为 fields 对象，完整字段、正则、属性编辑和重置契约见 [节点属性](node-attributes.md)。

目录编号和 boundary=root 是边界线索，媒体及局部结构共同决定自动候选；not_work 只取消当前目录边界。文件编号不自动拆分同目录作品，同号包装与不同号子作品按目录边界处理。

## 预览与应用

POST /sources/{source}/organize：

```json
{"preview":true,"scope_node_id":"","preview_limit":50}
```

scope_node_id 可省略，表示变化触发位置；仍计算完整来源，包括保留的已知 missing 节点。preview_limit 为 1..500，默认 50，只限制展示。响应包含 complete、source_id、scope_node_id、index_revision、config_revision、catalog_revision、input_generation、rules_hash、work_count、works、diagnostics、truncated，以及不透明计划 ETag。

works 每项为 root_node_id、work_id、identifier、title、member_count、groups、trace。work_id 只在显式指定目标时提供，否则为空，不是预分配的新作品身份；root_node_id 对显式文件草稿可为空。groups 包含 key、kind、variant、media_kind、formats、member_count。预览编号/标题来自规则，最终值还会叠加描述覆盖。

同一路径提交 preview=false，直接维护输入、取新快照并应用，不要求 If-Match；即使携带旧预览 ETag，也不会用它绑定执行。预览仅供参考，执行时输入可以不同。取到不完整来源快照为 409 source_incomplete。成功返回字符串 input_generation、desired_generation、applied_generation，以及 pending（提交时仍有后续输入未处理），还返回 source_id、work_ids 及 created_works/updated_works/removed_works/unchanged_works、created_members/updated_members/withdrawn_members。removed 表示退出当前候选，作品描述和历史关系保留为 inactive；不是删除媒体。

应用先撤回全部需要转移的旧有效归属，再建立新归属；节点唯一性、作品范围、关联及搜索在同一事务发布。锁定资源需要转移/排除时返回 409 locked，整批回滚。归纳应用不提供绕过锁定的 force；应先使用成员接口带 force 明确解锁，再重新预览。不能用 force 绕过唯一性、范围或较新已发布代次。

部分读取、取消、离线以及未确认缺失不能推断删除。新取到的不完整索引仅可预览；已经基于完整可信快照开始的计算允许发布该代结果，期间的新观察留待后续处理。需要通过完整扫描或可信基线子树扫描并明确 mark_missing=true 核对缺失；已知 missing 文件保留关系和可用性标记，后续边界变化仍按同一继承规则处理。

`desired_generation` 是当前输入代次，`applied_generation` 是已应用代次，均为字符串。两者不一致表示仍有变化待处理；输入停止变化且成功处理后两者相等。归纳不覆盖受保护的人工或 AI 字段。

## 解除与数据保护

POST /works/{work}/release，正文 {}，使用作品 If-Match：删除指向该作品的 root/assign 决定。它不清空成员、分组、视角或字幕，不立即切换 active，也不会删除不带 work_id 的 exclude/not_work 决定。随后归纳才重算；取消人工根后，默认规则可能仍将同目录识别为作品。要禁止独立成作品使用 not_work，要完全排除使用 exclude。

描述字段、翻译和候选编辑不改变边界。元数据覆盖按字段/语言独立保留；GET /works/{work}/metadata-overrides 查询，POST /works/{work}/metadata-overrides/reset 使用作品 ETag 和 fields 数组撤销指定覆盖（如 `manual:texts/ja/title`，空数组清全部），立即以本地基线、当前 RJ 共享缓存与剩余人工字段重新组合。手工与自动作品均支持，未带 `manual:` 前缀的字段键返回 400。POST /works/{target}/metadata-adoption 使用目标作品 ETag，正文 source_work_id/source_revision，显式复制 inactive 描述，不搬迁归属或候选证据。

旧成员在转移后保留 excluded 历史记录，不构成第二个所有者。人工字幕注解变为不可用，显式视角绑定保留 active=false 历史；资源回原作品且组范围满足时恢复。普通列表默认隐藏 inactive 作品，include_inactive=true 可查看。相同根可复用作品 ID，不按同 RJ 自动搬迁元数据；改名仍视为旧节点缺失和新节点出现。

播放组沿用 main/bonus/video/other、default/se/no_se；MP3/WAV 是组内格式，不要求名单对应。默认组优先选择含 present 媒体的组，显式默认组及成员播放编辑保留；可通过 playback-overrides/reset 撤销覆盖并在下次归纳恢复。实际播放与格式选择见[播放接口](playback.md)。

## 持久归纳任务

POST /organize-jobs：source_id、scope_node_id（可省略）、apply（默认 false）。202 接受后不随请求断开取消；apply=true 明确授权按快照直接计算并应用；若提交时仍有新输入，该任务继续取最新快照直至追平或失败/取消，不另需预览 ETag。任务 result 的变化计数与 analysis 描述最后一轮，work_ids 是最终一轮结果，不累计中间轮次。预览 result 包含字符串 input_generation；应用 result 还包含 desired_generation、applied_generation、pending。GET /organize-jobs 支持分页，GET /organize-jobs/{job} 读详情，POST /organize-jobs/{job}/cancel 无正文取消，DELETE /organize-jobs/{job} 只清理终态历史。

状态 queued/running/canceling/succeeded/failed/canceled/interrupted；phase 为 preparing/applying/finished。`/organize-jobs` 同时接纳一个任务，忙碌为 409；其他来源处理入口可按[队列并发设置](library-jobs.md#独立队列和并发设置)执行；重启标记 interrupted。取消与提交竞争可能成功完成，终态需再查询。追赶任务已成功提交的早先轮次会保留；失败或取消只回滚当轮发布，未处理输入代次仍保留。阶段变化以 organize.progress 记录到认证日志接口，可按 job_id 检索；没有预知的节点总进度，不提供百分比。预览最多 500 部候选、总计 1000 个组，group_count/groups_truncated 提示截断；应用范围不受展示截断限制。

资源关系接口见 [media-associations.md](media-associations.md)，扫描契约见 [scanning.md](scanning.md)。

批量决定、显式根迁移、分页差异和历史清理见 [curation.md](curation.md)。

## 增量计算状态

预览和归纳任务的预览/应用结果包含 analysis：mode 为 full/incremental，computed_branches、reused_branches、computed_nodes 为本次计算统计，fallback_reason 说明 source_boundary、file_routing、invalid_tree 或 cache_capacity 等全量回退原因，正常为空。incremental 表示允许独立分支计算，首次或缓存淘汰时 reused_branches 可以为 0；这些计数不是扫描进度。缓存命中不改变边界、输入代次、人工覆盖或原子发布语义。

目录决定接口是节点 boundary 属性的便捷表达；删除决定只解除这个字段的人工保护，保留其他人工语义属性，

## 记录保留

已结束任务及其重试输入最多保留 48 小时；过期详情返回不存在。刮削／媒体探测任务结束后，明细仅保留未完成项，成功与复用项计入任务汇总；明细分页 total 为实际保留的明细数。性能指标仅在任务执行期间可用。日志同时受 48 小时与总容量限制。作品资料、人工修改、字幕草稿和收听记录不受影响。
