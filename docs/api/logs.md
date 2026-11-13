# 日志查询与请求追踪

`GET /api/v1/logs` 需要管理员会话；网页使用同源 Cookie，原生和外部客户端使用 Bearer。成功的日志查询和健康检查不记录自身访问；失败请求仍可产生诊断日志。

## 参数与响应

| 参数 | 含义 |
| --- | --- |
| level | 可选，精确匹配 debug / info / warn / error；attention 同时匹配 warn 和 error；省略时查询全部保留记录 |
| q | 可选，日志事件及字段的忽略大小写字面子串 |
| request_id | 可选，精确匹配关联请求 ID |
| before | 可选，排除该 ID 及更新日志的字符串游标；必须是正十进制整数文本 |
| limit | 默认 100，范围 1..200；此接口不使用 offset |

```json
{
  "items":[{
    "id":"28","time":"2026-10-29T12:00:00Z","level":"debug","event":"http.request",
    "fields":{"request_id":"opaque-request-id","method":"POST","route":"/api/v1/works","status":201,"duration_ms":1.25,"bytes":420}
  }],
  "next_before":"28",
  "has_more":false
}
```

最新在前，before 为排他上界。第一页不传 before；后续将 next_before 原样传回。空结果 next_before 为 null。新日志到来不会把已有分页游标向前推移；轮转/保留上限仍可能让旧条目不可查。前端将 ID 视为字符串，不转浮点数。

查询范围是最近 5000 条保留记录，不是完整审计历史。磁盘 JSONL 每份最多约 4 MiB，保留当前文件及 3 份备份；重启恢复保留范围内的日志和递增 ID。异常退出造成的不完整最后一行会跳过。磁盘写入失败时可通过进程标准错误及当前内存查询排查，健康检查会报告不可用；不能承诺故障期间的日志重启后仍存在。

## 关联方式

普通请求响应头和错误对象均有 request_id。先保存这个值，再查询：

```http
GET /api/v1/logs?request_id=opaque-request-id&limit=100
Authorization: Bearer <token>
```

扫描任务的 request_id 是启动请求 ID，后续 scan.queued、scan.started、scan.progress、scan.cancel_requested、scan.finished 事件使用同一值，并附 job_id。轮询任务详情的 HTTP 请求有各自独立 ID，不覆盖任务的原始 request_id。

http.request 的稳定字段包括 request_id、method、route、status、duration_ms、bytes。route 是路由模板，不是包含实际路径参数或 query 的原始 URL；鉴权前失败可能为 unmatched。5xx 与 panic 合为一条 error，包含 error_code/error/panic 上下文。明确登录失败、权限拒绝、来源不可用和其他非预期 4xx 为 warn；过期会话、正常取消、缺失资源和普通冲突为 debug。成功读取和普通写访问为 debug；账号/权限及来源配置写入分别记录 account.changed/source.changed 结果摘要（包含 actor_id），不记录请求正文。

event 是机器可识别事件名；fields 允许随事件扩展，客户端不应要求固定字段集合或将英文日志翻译成协议状态。核心事件包括 core.started、core.stopped、http.listening、http.request、account.changed、source.changed、logging.settings_changed、scan.queued/started/finished/progress/cancel_requested/recovered、organize.progress/finished/applied，以及 media_probe.queued/started/progress/item_failed/cancel_requested/finished。进度、排队、启动、逐项失败和图片处理为 debug；人工完成或实际更新保留结果摘要，无变化自动扫描及纯缓存复用默认安静。media_probe.finished 用 failure_codes 汇总失败原因和数量，具体文件保留在任务详情。automation.reload_failed/content_invalidation_failed/watch_degraded 按操作及来源记录首次、最多每五分钟一次累计检查次数和恢复（事件后缀 .recovered）；重启重新计数。旧版 http.error/http.panic 可仍存在于保留记录中，新请求不再另写这两种重复条目。具体可轮询的进度、取消与计数语义见 scanning.md、organization.md 和 media-probe.md。

服务不记录请求正文、认证头或原始查询串。配置的令牌会在持久化前递归脱敏，常见凭据字段也会脱敏，长文本和过大记录会截断。不要让客户端将令牌、用户正文或密码拼入 request_id、q 等诊断信息。

## 详细诊断设置

`GET /api/v1/logs/settings` 与 `PUT /api/v1/logs/settings` 仅限管理员。设置为整个服务器共享的持久配置，首次默认关闭。GET 返回 `{"diagnostics_enabled":false}` 及 ETag；PUT 完整替换该对象，必须传入 GET 的 If-Match，缺失返回 428、过期返回 412，缺失/null/非布尔值和未知字段返回 400。保存失败不改变活动设置；并发保存串行执行持久化与生效。

启用后立即在日志内存、文件和标准错误中记录 DEBUG，所有已派生 logger 同步生效。禁用后停止新增 DEBUG，不清空已有记录；重启恢复开关及仍在保留范围内的记录。设置与其他服务器配置一同保留。仍使用相同脱敏、轮转和健康报告，不提供无限审计历史。

日志页默认请求 level=attention；从任务的关联日志进入时请求全部级别，避免隐藏成功结果。DEBUG 筛选只查已保留记录，不会自动启用诊断。

## 记录保留

已结束任务及其重试输入最多保留 48 小时；过期详情返回不存在。刮削／媒体探测任务结束后，明细仅保留未完成项，成功与复用项计入任务汇总；明细分页 total 为实际保留的明细数。性能指标仅在任务执行期间可用。日志同时受 48 小时与总容量限制。作品资料、人工修改、字幕草稿和收听记录不受影响。
