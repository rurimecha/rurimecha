# 批量纠正、差异预览和维护

路径相对 /api/v1，均需有效会话，网页使用同源 Cookie，原生和外部客户端使用 Bearer。来源级操作要求来源存在；历史清理不删除当前有效的缺失关系。写入规则与目录决定相同：保存输入后，直接执行归纳或 已开启的自动归档才发布新归属；预览不发布关系。所有失败整批回滚，不改变磁盘媒体。身份和修订用字符串。

## 批量目录决定

POST /sources/{source}/decisions/batch，If-Match 使用 GET decisions 的集合 ETag：

```json
{"edits":[{"node_id":"parent","decision":"exclude"},{"node_id":"child","delete":true}]}
```

edits 为 1..1000 项，同节点只能一次。delete 默认 false；true 时必须已有决定，且不能再带 decision/work_id/origin。其他字段与单项决定相同。按最终图校验，例子允许先排除父目录、同时删除后代决定；请求顺序不影响结果。可以在同批次重新指定或交换作品根，前提是旧根在最终状态不再为原作品生成边界。

不存在为 404，参数/重复节点为 400，归属/排除/根范围冲突为 409，过期为 412。成功返回 revision、pending=true、edit_count 和新集合 ETag。完全相同的决定不改修订；edit_count 是请求项数。单项 PUT/DELETE 复用相同校验，删除祖先决定时不能留下不一致的待处理根。

## 显式迁移作品根

POST /sources/{source}/root-moves，使用集合 ETag：

```json
{"work_id":"existing-work","node_id":"new-directory"}
```

必须已有不同的当前根，两个目录必须同来源。一次保存旧根 not_work、新根 root；旧根不独立成作品，但其后代仍正常判断。没有根的草稿直接用目录 root 决定即可。成功返回 revision、pending=true 和新集合 ETag。此操作只调整归纳边界，不移动/重命名文件。

应用后保留作品 ID 和既有描述元数据；相关资源按新边界重算、人工注解按原规则保护。旧目录文件若有显式 assign 覆盖，仍归原指定作品；替换根不会抢走覆盖。原根被其他作品接管时，旧作品如还拥有显式文件可成为无根资料作品。锁定导致归纳应用冲突时，输入保留、旧发布结果整体保留。

## 归属与关联差异预览

POST /sources/{source}/organize-diff?limit=50&offset=0，正文 {}，也可带 scope_node_id。limit 为 1..500，offset 非负。第一页可不带 If-Match，后续 offset>0 必须使用第一页返回的计划 ETag；任意快照变化返回 412。完整来源仍是计算范围，分页仅限制输出。

响应 source_id、scope_node_id、complete、summary、items、total。summary 是未分页的全部计数：added、removed、moved、locked_nodes、subtitle_links、rendition_bindings。items 每项都有以下字段，未适用的字符串为空：

| 字段 | 含义 |
| --- | --- |
| kind | added / removed / moved / subtitle / rendition |
| node_id | 受影响节点 |
| before_work_id | 原作品 |
| after_work_id | 目标作品；新自动候选尚无分配 ID 时为空 |
| after_root_node_id | 目标作品根；新自动候选以此识别，无根资料作品可为空 |
| resource_id | 字幕关联 ID，或视角版本 ID |
| member_id | 原成员；视角与该成员共同标识绑定 |
| locked | 节点归属变化是否被资源锁阻止 |

node 条目只报告所有者变化，不包含同作品内普通播放组调整。subtitle/rendition 条目表示当前有效关系将撤回；不把本来已不可用的字幕再次计为失效。字幕默认自动重配和人工历史保护仍正常执行，差异不是“字幕文件删除列表”。total 是所有节点/关联条目总数，不等于变化节点数。按 kind/node/resource/member 稳定排序。

差异查询不保存决定或计划，不预分配作品 ID。新作品只能用 root 标识目标，不能把空 work_id 当成未归纳。此 ETag 只绑定差异分页；普通 /organize 应用重新读取当前输入，不绑定预览 ETag。complete=false 时只作参考，不能应用；locked_nodes>0 表示需要先明确解锁或调整决定。

## 恢复自动播放分类

POST /works/{work}/playback-overrides/reset，使用作品 ETag：

```json
{"member_ids":["member-id"],"reset_default":true}
```

member_ids 最多 1000 个且不能重复；至少选择成员或 reset_default。只处理同作品 included 成员；须已有归纳推导记录，资料草稿无推导记录时 409。恢复所选成员的自动分类与顺序，reset_default 撤销默认组覆盖；显式解锁在重置时生效；下一次归纳才发布规则分类、顺序和默认组。节点别名独立保留。返回 work、pending=true 和作品 ETag。

文件 assign/exclude 决定、描述元数据不删除，人工自定义组也不删除。已绑定视角（含保留历史）须先显式解绑，否则 409。锁定成员默认 409 locked；?force=true 明确解除所选成员锁并重置。默认组单独重置不解锁其他成员。

## 历史清理与保留策略

POST /sources/{source}/history-cleanup：

```json
{"before":"2026-10-01T00:00:00Z","limit":100,"include_manual":false,"force":false,"preview":true}
```

before 必填 RFC3339，不能晚于服务端当前时间，按记录 updated_at 保留较新数据。limit 默认 100，1..500；同时限制选中的作品和单独成员数量。先选 inactive 作品，再选 excluded 历史成员，稳定按时间/ID 排序。归纳任务历史另用原有任务接口清理。

默认保护人工元数据覆盖、候选、人工字幕、视角、人工组、人工默认组及成员注解。include_manual=true 明确允许删除这些旧数据；锁定仍须 force=true。仍被候选引用的单项成员不清理；整作品删除时会级联处理其候选。含 included 成员或仍被人工根/assign 指向的作品不清理。任何 included/missing 当前归属、扫描节点、目录决定和媒体都不删除。

预览返回 input（规范化参数）、work_ids、member_ids、member_deletions、limit_reached 和专用 cleanup ETag。member_deletions 包括删除作品级联的成员数，可能超过 limit；limit_reached 仅说明选项配额已满，不证明还有更多记录。大历史作品可产生较大的级联事务，调用方应按预览控制批次。

应用提交相同参数且 preview=false，If-Match 必须为清理预览 ETag。缺失为 428；参数、选择或任意来源/catalog 修订改变为 412。成功返回 works、members 实际删除数，members 包括级联。整批失败回滚。清理历史不等于永久忽略目录，未来规则条件恢复时自动作品仍可能重建。

当前不自动定时清理，不以“缺失很久”为理由删除有效人工关系。调用方用 before 与 limit 决定保留期和批次。


### 平铺组合快照

CollectionEntry及CollectionEntryView可带 `queue_group`（结构同playback契约的QueueGroup）。自定义收藏集保存当前队列时，作品组合生成带当前曲目清单的work条目，单独曲目生成node条目；相同作品后续出现的不同曲目并入首次work条目，直接node_id只保留首次出现，其余条目保持相对顺序与来源名称。一般创建、替换和追加请求中的重复work_id仍保留首次条目。组ID在条目顺序中必须连续，描述一致；移除最后一个成员不留下空组。普通作品条目仍可保留work_id/group_id/format/node_ids选择语义，这些字段与队列显示分组独立。

直接解析entries时保留显式queue_group，未显式分组的work条目产生独立作品组。以collection_id加入队列/播放列表时，整次加入使用以收藏集名称命名的新组，保留实际媒体顺序，不展开成嵌套的作品组。来源和标题只是加入时摘要，不赋予额外资源访问权限。

追加带分组的自定义条目时，为新增组重新分配出现ID，避免重复追加与已有组混合；条目顺序及成员快照不变。
