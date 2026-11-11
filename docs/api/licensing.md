# 产品授权管理

单档授权绑定 Core 实例，实例内所有账户共享。证书表示产品 `rurimecha` 在有限期内已获授权，不携带功能清单、程序版本或数据库结构摘要。当前程序决定哪些功能需要产品授权；增加或调整这些功能不会导致既有证书失效，也不要求重新绑定。普通媒体的 Web 播放与 `/dav/` 依照现有账户规则开放。珍藏片段要求有效产品授权。

## Core 管理接口

均在 `/api/v1` 下。状态可由所有已登录账户读取；激活和反激活仅 admin 可调用。网页使用现有同源 HttpOnly Cookie；写请求仍检查 Origin/Referer；原生使用 Bearer。

| 方法与路径 | 请求及响应 |
| --- | --- |
| `GET /license` | `state`、`configured`、`capabilities`，已有可验证证书时附 `customer`、`license_id`、RFC3339 `expires_at` |
| `POST /license/activate` | `{"license_key":"…","device_name":"我的服务器"}`，密钥 32..256 字节，名称 1..80 个 Unicode 字符；成功返回当前状态 |
| `POST /license/deactivate` | 必须提交空对象 `{}`；返回 `{"ok":true}` |

状态 `active` 表示有效；其余为对应 `license_*` 代码。`capabilities.android_playback` 和 `capabilities.treasured_clips` 是当前程序的能力状态，允许增加其他键；未知键忽略。它用于展示当前可用的功能。

激活及续期需要 Core 连接发行方激活服务。同一许可证、同一实例密钥重复激活复用绑定名额，可在线更新期限和宿主机信息；到期日由云端许可证决定，重新激活不会重置期限。激活密钥只用于本次请求，不在 Core 持久保存或写入日志。超时后可安全重试。

更换其他许可证前必须先反激活，`license_deactivation_required` 为 409；更新同一许可证不需反激活。

反激活先持久保存停用状态，再联网释放名额。网络失败后 `state=license_deactivation_pending`，安卓播放已被禁用，管理员应重试反激活。云端成功而响应丢失也可重试，不重复释放其他绑定。反激活不要求旧证书仍未到期或宿主机仍匹配，不撤销账户登录。云端自助释放不能通知离线副本，旧副本可能使用至既有证书到期。

## 错误

保持公共 JSON 错误结构。`license_required`、`license_expired`、`license_invalid`、`license_clock_error`、`license_host_unavailable`、`license_device_changed`、`license_deactivation_pending` 为 403；反激活待完成时再次激活为 409。`license_key_invalid`、`license_revoked`、`license_activation_limit` 为 403，`license_invalid_input` 为 400，`license_not_configured`、`license_storage_error` 为 503，`license_service_unavailable` 为 502。授权错误不等于账户 401，不得因此退出账户或删除个人资料。

## 云端授权中心

服务地址以发行配置为准。详细路由和 DTO 见 [独立 OpenAPI](license-service.openapi.json)。Core 与中心之间只在激活、更新、反激活时联网。

机器客户端应发送真实应用 User-Agent，例如 `RuriMecha-Core/1`；workers.dev 的浏览器完整性检查可能拒绝通用库默认标识。不能把 User-Agent 当作授权证明，权限始终依赖管理身份、密钥或实例签名。

- `GET /health` 检查发行密钥和结构；`GET /public-key` 返回公开的 product/issuer_url/public_key，用于发行配置，不代替客户端固定的信任根。
- 云端管理接口仅接受独立的 `Authorization: Bearer` 凭据。本机管理台负责管理员登录和云端调用。
- `GET|POST /admin/customers`、`PUT /admin/customers/{customer}` 维护购买用户资料；不表示已完成邮箱身份验证。
- `GET|POST /admin/licenses`、`PUT /admin/licenses/{license}` 管理单档许可证；创建时可选 order_ref 仅作人工登记，重复编号整体回滚，不执行付款确认或发邮件。
- `GET /admin/licenses/{license}/key` 需要管理员身份，返回当前 `{id, license_key}`，包括已停用或到期的许可证。许可证列表省略密钥。
- `POST /admin/licenses/{license}/key` 接受空对象并返回新密钥。原密钥此后不能激活，许可身份、证书和绑定保持不变。
- `GET /admin/activations?license_id=…` 分页读取历史，`POST /admin/activations/{activation}/release` 接受空对象并幂等释放指定绑定。`GET /admin/events` 分页读取事件，可选 license_id。
- 列表默认 limit=50，范围 1..100，offset>=0；用户和许可证支持最多 254 字符的 q 字面检索。许可证列表仅带本页活跃绑定，历史单独查询；items/total/limit/offset 或 licenses/activations/total/limit/offset 来自同一读取事务。
- `POST /v1/activate`、`POST /v1/deactivate` 保持前述签名与绑定语义。激活接受 license_key/device_name/instance_key/host_id，可选 existing_license_id；反激活接受 certificate/proof。
- `POST /v1/self`、`POST /v1/release` 当前始终返回 503 self_service_unavailable。不提供密钥持有人查看全部云端绑定或远程解绑的权限。当前使用 Core 本机管理员反激活或联系发行方管理员处理。

授权中心错误对象含稳定 error.code 与 request_id。常见额外代码：customer_conflict/order_conflict 为 409；self_service_unavailable/license_schema_mismatch/license_not_configured 为 503；request_too_large 为 413；rate_limited 为 429。管理 PUT 为完整替换，按最后提交生效。停用、缩短期限和释放不能即时撤销离线副本，既有证书可能继续使用至原到期日。

授权生效后可创建和使用其他账号。未激活或授权失效时，只有内置管理员可以访问网页和 WebDAV，Android 播放仍要求有效授权。参见 [账号与会话](accounts.md)。

付费通行证在创建后七天内且从未激活时可办理退款，超过七天仍可激活。测试通行证在发放后七天内激活。同一许可证首次激活开始计算31天，重复激活及换机沿用原期限。激活期限已过返回 `license_activation_window_expired`，退款处理中返回 `license_refund_pending`，已退款返回 `license_refunded`，均为403。云端 `rate_limited` 由Core以429传递。
