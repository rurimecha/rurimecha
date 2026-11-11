# 日志查询与请求追踪

`GET /api/v1/logs` 需要管理员会话；网页使用同源 Cookie，原生和外部客户端使用 Bearer。成功的日志查询和健康检查不记录自身访问；失败请求仍可产生诊断日志。

## 参数与响应

| 参数 | 含义 |
| --- | --- |
| level | 可选，精确匹配 info / warn / error |
| q | 可选，日志事件及字段的忽略大小写字面子串 |
| request_id | 可选，精确匹配关联请求 ID |
| before | 可选，排除该 ID 及更新日志的字符串游标；必须是正十进制整数文本 |
| limit | 默认 100，范围 1..200；此接口不使用 offset |

```json
{
  "items":[{
    "id":"28","time":"2026-10-29T12:00:00Z","level":"info","event":"http.request",
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

http.request 的稳定字段包括 request_id、method、route、status、duration_ms、bytes。route 是路由模板，不是包含实际路径参数或 query 的原始 URL；鉴权前失败可能为 unmatched。4xx 通常为 warn，5xx 为 error，其余为 info。

event 是机器可识别事件名；fields 允许随事件扩展，客户端不应要求固定字段集合或将英文日志翻译成协议状态。核心事件包括 core.started、core.stopped、http.listening、http.request、http.error、http.panic、scan.queued、scan.started、scan.finished、scan.progress、scan.cancel_requested、scan.recovered、organize.progress，以及 media_probe.queued/started/progress/item_failed/cancel_requested/finished。扫描和探测的计数日志限频；归纳按 preparing/applying/finished 阶段记录。具体可轮询的进度、取消与计数语义见 scanning.md、organization.md 和 media-probe.md。

服务不记录请求正文、认证头或原始查询串。配置的令牌会在持久化前递归脱敏，常见凭据字段也会脱敏，长文本和过大记录会截断。不要让客户端将令牌、用户正文或密码拼入 request_id、q 等诊断信息。
