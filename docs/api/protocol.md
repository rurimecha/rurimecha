# 通用 HTTP 协议

## 地址、认证与能力

示例 Base URL 为 `http://127.0.0.1:8080/api/v1`，实际地址由部署者配置。认证前可访问 `GET /health`、首次设置和登录入口，以及游客开关查询；游客登录仅在管理员开启后可用。其他 API 需要有效会话。原生和外部客户端使用：

```http
Authorization: Bearer <access-token>
```

令牌是 `POST /auth/login` 返回的不透明会话凭据，不是 JWT，有效期 30 天。支持管理员和普通成员；管理操作要求管理员角色，普通成员可浏览播放并维护自己的个人资料，详见[账号权限](accounts.md#权限)。游客权限单独受限。登出或撤销后立即失效；修改密码撤销该账号的全部会话。不要放入 URL、日志或客户端报错内容。首次登录及管理接口见 [账户与会话](accounts.md)。前端通过独立能力判断使用功能：

```json
{
  "api_version": "1",
  "version": "dev",
  "capabilities": ["accounts.multi_user", "accounts.roles", "accounts.sessions", "playback.progress", "playback.profiles", "metadata.scrape", "catalog.crud", "catalog.i18n", "search.fts5", "scan.directory", "scan.jobs", "logs.query", "organize.directory_boundaries"]
}
```

示例只列部分能力；实际以 `/info` 返回的完整列表为准。

`GET /health` 正常返回 `200 {"status":"ok"}`；必要子系统不可用返回 503。`GET /info` 需要认证。

第三方网页跨域调用时，部署者须明确允许该页面 Origin。跨域 API 使用 Bearer，不开放 Cookie credentials；同源网页的全部 API、图片和媒体使用登录设置的 HttpOnly 会话 Cookie，写请求必须校验同源 Origin/Referer（详见 accounts.md）；允许 Authorization、Content-Type、If-Match，请读取服务暴露的 ETag、X-Request-ID、Location。未知 Origin 返回 403；浏览器预检无需令牌。原生客户端无须人为添加 Origin。反向代理需保留认证和来源信息。HTTPS 入口连接 HTTP 上游时，须在网络设置中配置可信代理，并由代理传递外部协议、主机和客户端 IP；配置要求见[反代教程](../USAGE.md#通过-nginx-访问-ruri)。

## JSON 与标识

普通 JSON 请求使用 `Content-Type: application/json`，上限 1 MiB，顶层必须是单个对象；资料包、台本及迁移上传等使用各自文档声明的类型和上限。未知字段、字段类型不符、未知或重复的 query 参数返回 400。PUT 一般为完整替换；`PATCH /works/{work}/metadata-editor` 按[资料编辑契约](metadata-scraping.md)只更新选定部分，不能套用 PUT 的替换规则。未指定可选字段会清空或恢复该字段默认值；PUT 作品的 default_playback_group_id 例外，省略或 null 保留当前值，显式字符串才更新。可选输入的 null 与省略等价，候选的 value 例外：必须提供，null 是明确清空的候选值。媒体字段编辑的条目 null 为重置、条目内 value:null 为明确清空，见 media-probe.md。必填字段不接受缺失或 null。

不需要请求体的 DELETE、任务取消和索引重建不解析请求体，请不要为它们设计额外参数。查询参数的 true/false 只接受这两个小写值。

所有资源 ID 都是不透明字符串。revision、work_revision、日志 id、before 也是字符串，避免 Web 浮点数损失精度；比较和传回时保留原值。数量、排序和分页为 JSON 数字。时间使用 UTC RFC3339，可能含小数秒。空字符串一般表示未知文本或未选择引用，数组响应为空时是 `[]`；具体允许 null 的输出见 OpenAPI。

每个响应有服务生成的 `X-Request-ID`，不会采用客户端自填的同名头。普通 JSON API 默认使用 `Cache-Control: no-store`；媒体、图片与静态页面使用各自的缓存和条件读取约定。ETag 在编辑接口中用于并发控制。

## 并发编辑

读取返回建立快照时已提交的数据；修改尚未提交时，其他请求可以取得先前已提交版本。成功提交后的新读取能看到新版本，作品、归属及搜索不会暴露同一事务中的部分修改。单次多表响应及分页的 total/items 来自同一个快照；不同请求可能跨越提交，不能视为同一快照。写入时仍按当前修订检查 If-Match，旧读取所得标签可能返回 stale_revision。

作品及其所有文本、成员、组和候选共用一个作品修订。读取单个资源、创建或修改资源时返回：

```http
ETag: "work-id:7"
```

对现有作品或其子记录进行 PUT、DELETE，以及在作品下 POST 子记录，须原样传入：

```http
If-Match: "work-id:7"
```

只接受一个带双引号的强标签；不接受 `*`、弱标签、多个标签或其他作品的标签。客户端从作品的 `revision` 或子记录的 `work_revision` 也能构造 `"<work_id>:<revision>"`；优先原样保留响应 ETag。

- 缺少 If-Match：428 / precondition_required。
- 标签格式或所属资源不对：400 / invalid_argument。
- 修订过期：412 / stale_revision；重新读取当前作品和编辑对象，提示用户处理差异，不直接重试覆盖。
- 成功写入：父作品修订加一，失败时不增加。子记录 DELETE 的 204 也返回新的父作品 ETag。
- `force=true` 只表示明确覆盖锁定记录，不绕过修订和关系检查。前端应在用户明确作出此决定时使用。

创建作品不需要 If-Match。来源的修改/删除使用独立来源修订，标签同样为 `"<source_id>:<revision>"`。本地化显示和列表接口不返回可编辑标签；从详情或返回对象取当前修订。扫描不改变作品修订或人工决定。

## 分页与查询

一般列表使用 `limit=1..500`（默认 50）、`offset>=0`（默认 0）：

```json
{"items":[],"total":0,"limit":50,"offset":0}
```

过滤在分页前执行；同一响应的 total 与 items 使用同一数据快照。不同请求间可能发生新增或删除，offset 分页不承诺跨请求快照。日志单独使用游标分页，见 logs.md。

默认顺序：作品和来源按创建时间/ID，文本按规范化语言标识，成员按 play_order/ID，播放组按 sort_order/ID，候选按创建时间/ID，任务按创建时间倒序/ID倒序。节点按最近发布的扫描顺序/ID，问题按 id 升序。

## 状态与错误

创建资源通常返回 201，并通过 Location 给出相对资源 URL；启动任务返回 202；PUT 返回 200；DELETE 和索引重建成功返回 204，无 JSON 正文。普通 GET 返回 200。错误形状始终为：

```json
{"error":{"code":"stale_revision","message":"The resource changed; fetch the current revision before retrying.","request_id":"opaque-request-id"}}
```

客户端根据 code 决定行为并翻译中日英文案；message 用于辅助排查，不解析英文句子判断分支。request_id 与响应头一致。

| HTTP | code | 客户端处理 |
| --- | --- | --- |
| 400 | invalid_argument | 修正字段、JSON、筛选或标签格式 |
| 401 | unauthorized | 检查会话状态，必要时重新登录 |
| 403 | forbidden_origin | 检查服务允许的前端 Origin |
| 404 | not_found | 资源不存在或不属于 URL 中的作品/来源 |
| 405 | method_not_allowed | 使用文档列出的 HTTP 方法，Allow 头列出当前路径支持的方法 |
| 408 | request_canceled | 请求被取消或超时；必要时查询当前资源状态 |
| 409 | conflict | 重复名称、采用决定或仍被引用的关系冲突 |
| 409 | locked | 需要用户明确决定是否 force 覆盖 |
| 409 | source_incomplete | 等待完整扫描成功后再整理，部分结果只作预览 |
| 409 | busy | 相同来源正被处理或已达到任务限制，稍后查询再尝试 |
| 412 | stale_revision | 刷新并处理并发差异 |
| 413 | request_too_large | 缩小 JSON 请求 |
| 415 | unsupported_media_type | 使用 application/json |
| 428 | precondition_required | 提供当前 If-Match |
| 500 | internal_error | 使用 request_id 查询日志 |
| 503 | unavailable | 必要子系统不可用，稍后检查健康状态 |

请求连接中断不表示操作一定未提交；不要盲目重试非幂等 POST。重新查询相应资源或任务。已经接受的扫描任务独立运行，取消须调用专门的取消接口。

规则配置使用 `"rules:<revision>"` 标签，预览/差异使用专用不透明标签；普通归纳执行无需 If-Match，节点编辑使用来源输入代次，详见 [规则整理](organization.md)。两者不能替代作品修订标签。

媒体串流有独立的 HEAD、Range/条件请求和长响应语义；直传的条件/范围错误可为标准纯文本，处理输出开始后的失败会中止连接。允许的 CORS 媒体头及完整说明见 [播放契约](playback.md)。播放列表使用自身 ETag，与作品修订独立。

统一作品入口客户端要求 GET /info 返回 metadata.families 能力。缺少该能力的服务器应明确报告版本不匹配，不回退为单页合并或旧版资料接口。该能力表示支持 works?group_by=family 与 works/{work}/family，详见 catalog.md。
